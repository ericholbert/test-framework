using System.Threading.Channels;
using TestFramework.Bindings.Dotnet;
using TestFramework.Core.Common;
using TestFramework.Core.Configuration;
using TestFramework.Core.Logger;
using TestFramework.Core.Protocol;

namespace TestFramework.Core.Orchestration
{
    internal class RunnerOrchestrator
    {
        private readonly Settings settings;
        private readonly ILogger logger;
        private readonly ITestLogger testLogger;

        public RunnerOrchestrator(Settings settings, ILogger logger, ITestLogger testLogger)
        {
            this.settings = settings;
            this.logger = logger;
            this.testLogger = testLogger;
        }

        public async Task RunAsync(RunnerType type, string path, IEnumerable<string> paths)
        {
            await logger.LogAsync(LogLevel.Info, "Starting orchestration");

            IReadOnlyList<TestFile> files;

            {
                using var runner = new RunnerSession(type, logger);

                await runner.RunAsync();
                await runner.InitializeAsync(path);
                await runner.CheckDefinitionsAsync();

                var filePaths = await runner.GetFilePathsAsync(paths, settings.Recursive);
                files = await runner.ParseFilesAsync(filePaths);
            }

            await logger.LogAsync(LogLevel.Info, "Discovery phase ended");

            await DistributeFilesAsync(type, path, files);
        }

        private async Task DistributeFilesAsync(RunnerType type, string path, IReadOnlyList<TestFile> files)
        {
            var channel = Channel.CreateUnbounded<TestFile>(new UnboundedChannelOptions
            {
                SingleWriter = true,
                SingleReader = false,
            });

            var producer = Task.Run(
                async () =>
                {
                    Exception? error = null;

                    try
                    {
                        foreach (var file in files)
                        {
                            await channel.Writer.WriteAsync(file);
                        }
                    }
                    catch (Exception e)
                    {
                        error = e;
                        throw;
                    }
                    finally
                    {
                        channel.Writer.Complete(error);
                    }
                });

            var workers = Enumerable
                .Range(0, Math.Min(settings.MaxParallelism, files.Count))
                .Select(_ => Task.Run(
                    async () =>
                    {
                        using var runner = new RunnerSession(type, logger);

                        await runner.RunAsync();
                        await runner.InitializeAsync(path);

                        var definitions = await runner.GetStepDefinitionsAsync();

                        try
                        {
                            await RunHookAsync(runner, Attributes.BEFORE_RUN);

                            await foreach (var file in channel.Reader.ReadAllAsync())
                            {
                                await runner.ParseStepsAsync(file, definitions);

                                try
                                {
                                    await RunFileAsync(runner, file);
                                }
                                catch (InvocationFailedException)
                                {
                                    await logger.LogAsync(LogLevel.Info, "File skipped", file.Name);
                                }
                            }
                        }
                        finally
                        {
                            try
                            {
                                await RunHookAsync(runner, Attributes.AFTER_RUN);
                            }
                            catch (RunnerException e)
                            {
                                await logger.LogAsync(LogLevel.Error, e.FullMessage);
                            }
                        }
                    }))
                .ToArray();

            await Task.WhenAll(workers.Append(producer));
        }

        private async Task RunFileAsync(RunnerSession runner, TestFile file)
        {
            try
            {
                await RunHookAsync(runner, Attributes.BEFORE_FILE, file);

                await testLogger.LogAsync(TestOutput.File, "Running file", runner.Id, file.Name);
                while (file.TestPosition < file.Tests.Count)
                {
                    await RunTestAsync(runner, file);
                    file.TestPosition++;
                }
                await testLogger.LogAsync(TestOutput.File, "File ended", runner.Id, file.Name);
            }
            finally
            {
                try
                {
                    await RunHookAsync(runner, Attributes.AFTER_FILE, file);
                }
                catch (RunnerException e)
                {
                    await logger.LogAsync(LogLevel.Error, e.FullMessage);
                }
            }
        }

        private async Task RunTestAsync(RunnerSession runner, TestFile file)
        {
            var test = file.Tests[file.TestPosition];

            try
            {
                await RunHookAsync(runner, Attributes.BEFORE_TEST, file);

                await testLogger.LogAsync(TestOutput.Test, "Running test", runner.Id, test.Name);
                while (test.StepPosition < test.Steps.Count)
                {
                    await RunStepAsync(runner, file);
                    test.StepPosition++;
                }
                await testLogger.LogAsync(TestOutput.Test, "Test ended", runner.Id, test.Name);
            }
            finally
            {
                try
                {
                    await RunHookAsync(runner, Attributes.AFTER_TEST, file);
                }
                catch (RunnerException e)
                {
                    await logger.LogAsync(LogLevel.Error, e.FullMessage);
                }
            }
        }

        private async Task RunStepAsync(RunnerSession runner, TestFile file)
        {
            static bool ShouldClear(TestFile file, ObjectRetention retention)
            {
                var lastTest = file.Tests.Last();
                var isLastStep = lastTest.StepPosition == lastTest.Steps.Count - 1;
                var isLastTest = file.TestPosition == file.Tests.Count - 1;
                return retention switch
                {
                    ObjectRetention.Step => true,
                    ObjectRetention.Test => isLastStep,
                    ObjectRetention.File => isLastTest && isLastStep,
                    _ => throw new NotImplementedException(),
                };
            }

            var test = file.Tests[file.TestPosition];
            var step = test.Steps[test.StepPosition];

            try
            {
                await RunHookAsync(runner, Attributes.BEFORE_STEP, file);

                await testLogger.LogAsync(TestOutput.Step, $"Running step", runner.Id, step.Name);
                await runner.InvokeAsync(
                    new InvokeRequest()
                    {
                        AttributeName = Attributes.STEP,
                        AttributeId = step.Id,
                        Arguments = step.Arguments,
                        ClearInstance = ShouldClear(file, settings.ObjectRetention),
                    });
                await testLogger.LogAsync(TestOutput.Step, "SUCCESS", runner.Id, step.Name);
            }
            catch (RunnerException e)
            {
                await logger.LogAsync(LogLevel.Debug, $"Failing due to {e.GetType().Name}", runner.Id, step.Name);
                await testLogger.LogAsync(TestOutput.Step, $"FAIL{Environment.NewLine}{e.FullMessage}", runner.Id, step.Name);
                file.IsFailing = true;
                throw;
            }
            finally
            {
                try
                {
                    await RunHookAsync(runner, Attributes.AFTER_STEP, file);
                }
                catch (RunnerException e)
                {
                    await logger.LogAsync(LogLevel.Error, e.FullMessage);
                }
            }
        }

        private async Task RunHookAsync(RunnerSession runner, string attributeName, TestFile? file = null)
        {
            await testLogger.LogAsync(TestOutput.Hook, $"Running hook", runner.Id, attributeName);
            var request = new InvokeRequest() { AttributeName = attributeName };
            if (file != null)
            {
                request.Arguments = [file];
            }
            await runner.InvokeAsync(request);
        }
    }
}
