using TestFramework.Bindings.Dotnet;
using TestFramework.Core.Common;
using TestFramework.Core.Configuration;
using TestFramework.Core.Logger;
using TestFramework.Core.Protocol;
using TestFramework.Core.Runner;

namespace TestFramework.Core.Orchestration
{
    internal class RunnerSession : IDisposable
    {
        private readonly IRunner runner;
        private readonly ILogger logger;

        public RunnerSession(RunnerType type, ILogger logger)
        {
            this.logger = logger;
            runner = GetRunner(type);
        }

        public string Id => runner.Id;

        public RunnerType Type => runner.Type;

        public async Task RunAsync() => await runner.RunAsync();

        public void Dispose() => runner.Dispose();

        public async Task InitializeAsync(string path)
        {
            await logger.LogAsync(LogLevel.Debug, $"Initializing", Id);
            await HandshakeAsync();
            await InitAsync(path);
            await logger.LogAsync(LogLevel.Info, $"Initialized successfully", Id);
        }

        public async Task CheckDefinitionsAsync()
        {
            async Task<IReadOnlyList<MethodDefinition>?> GetDefinitionsAsync(string attributeName)
            {
                return await WriteAndReadAsync<DefinitionRequest, IReadOnlyList<MethodDefinition>>(
                    new DefinitionRequest() { AttributeName = attributeName });
            }

            async Task CheckSingleAttribute(string attributeName)
            {
                var result = await GetDefinitionsAsync(attributeName);
                if (result?.Count > 1)
                {
                    throw new InvalidOperationException($"Only one {attributeName} attribute is allowed");
                }
            }

            async Task CheckMandatoryAttribute(string attributeName)
            {
                var result = await GetDefinitionsAsync(attributeName);
                if (result == null || result?.Count == 0)
                {
                    throw new InvalidOperationException($"At least one {attributeName} attribute must be present");
                }
            }
            
            await logger.LogAsync(LogLevel.Debug, $"Starting definition checks", Id);

            await CheckSingleAttribute(Attributes.GET_FILE_PATHS);
            await CheckSingleAttribute(Attributes.PARSE_FILES);
            await CheckSingleAttribute(Attributes.PARSE_STEPS);

            await CheckMandatoryAttribute(Attributes.STEP);

            await logger.LogAsync(LogLevel.Info, $"Definition checks ended", Id);
        }

        public async Task<IReadOnlyList<string>> GetFilePathsAsync(IEnumerable<string> paths, bool recursive)
        {
            await logger.LogAsync(LogLevel.Debug, $"Starting file path retrieval", Id);

            var result = (await WriteAndReadAsync<InvokeRequest, IReadOnlyList<string>>(
                new InvokeRequest()
                {
                    AttributeName = Attributes.GET_FILE_PATHS,
                    Arguments = [paths, recursive],
                })) ?? DefaultImplementations.GetFilePaths(paths, recursive);

            if (result.Count == 0)
            {
                throw new InvalidOperationException("No test file was found");
            }

            await logger.LogAsync(LogLevel.Info, $"Retrieved {result.Count} file paths", Id);

            return result;
        }

        public async Task<IReadOnlyList<TestFile>> ParseFilesAsync(IEnumerable<string> paths)
        {
            await logger.LogAsync(LogLevel.Debug, $"Starting test file parsing", Id);

            var result = (await WriteAndReadAsync<InvokeRequest, IReadOnlyList<TestFile>>(
                new InvokeRequest()
                {
                    AttributeName = Attributes.PARSE_FILES,
                    Arguments = [paths],
                })) ?? await DefaultImplementations.ParseFilesAsync(paths);

            if (result.Count == 0)
            {
                throw new InvalidOperationException("No test file was parsed");
            }
            if (result.Any(x => x.Tests.Count == 0 || x.Tests.Any(y => y.RawSteps.Count == 0)))
            {
                throw new InvalidOperationException("Received test file with no test or step");
            }

            await logger.LogAsync(LogLevel.Info, $"Parsed {result.Count} test files", Id);

            return result;
        }

        public async Task<IReadOnlyList<MethodDefinition>> GetStepDefinitionsAsync()
        {
            await logger.LogAsync(LogLevel.Debug, $"Starting step definition retrieval", Id);

            var result = await WriteAndReadAsync<DefinitionRequest, IReadOnlyList<MethodDefinition>>(
                new DefinitionRequest { AttributeName = Attributes.STEP });

            if (result == null || result.Any(x => x.AttributeValue == null))
            {
                throw new InvalidOperationException("No step definition can be empty");
            }

            await logger.LogAsync(LogLevel.Info, $"Found {result.Count} step definitions", Id);

            return result;
        }

        public async Task ParseStepsAsync(TestFile file, IEnumerable<MethodDefinition> definitions)
        {
            await logger.LogAsync(LogLevel.Debug, $"Starting step parsing", Id);

            var rawSteps = file.Tests
                .SelectMany(x => x.RawSteps)
                .ToArray();

            var steps = (await WriteAndReadAsync<InvokeRequest, IReadOnlyList<StepDefinition>>(
                new InvokeRequest
                {
                    AttributeName = Attributes.PARSE_STEPS,
                    Arguments = [rawSteps],
                    DefinitionCacheId = Attributes.STEP,
                })) ?? DefaultImplementations.ParseSteps(rawSteps, definitions);

            if (rawSteps.Length != steps.Count)
            {
                throw new InvalidOperationException("Number of raw and parsed steps must be the same");
            }

            foreach (var test in file.Tests)
            {
                for (var i = 0; i < test.RawSteps.Count; i++)
                {
                    test.Steps.Add(steps[i]);
                }
            }

            await logger.LogAsync(LogLevel.Info, $"Parsed {steps.Count} steps", Id);
        }

        public async Task InvokeAsync(InvokeRequest request)
        {
            try
            {
                await WriteAsync(request);
            }
            catch (InvocationFailedException e)
            {
                throw RemapException(request, e);
            }
        }

        public async Task<T?> InvokeAsync<T>(InvokeRequest request)
        {
            try
            {
                return await WriteAndReadAsync<InvokeRequest, T>(request);
            }
            catch (InvocationFailedException e)
            {
                throw RemapException(request, e);
            }
        }

        private IRunner GetRunner(RunnerType type)
        {
            return type switch
            {
                RunnerType.Dotnet => new DotnetRunner(logger),
                RunnerType.Python => throw new NotImplementedException(),
                RunnerType.Java => throw new NotImplementedException(),
                _ => throw new NotImplementedException(),
            };
        }

        private async Task<TResponse?> WriteAndReadAsync<TMessage, TResponse>(TMessage message) where TMessage : Message
        {
            var id = await runner.WriteAsync(message);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            try
            {
                var response = await runner.ReadAsync<TResponse>(id, cts.Token);

                if (response.Error != null)
                {
                    await logger.LogAsync(LogLevel.Debug, $"Received runner error", Id, id);
                    if (response.Error.InvocationFail)
                    {
                        throw new InvocationFailedException(response.Error.Message, response.Error.StackTrace);
                    }
                    throw new RunnerException(response.Error.Message, response.Error.StackTrace);
                }

                return response.Content;
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("Timeout for reading has been reached");
            }
        }

        private async Task WriteAsync<TMessage>(TMessage message) where TMessage : Message
        {
            await WriteAndReadAsync<TMessage, object>(message);
        }

        private async Task HandshakeAsync()
        {
            if ((await WriteAndReadAsync<ValueRequest<string>, string>(
                new ValueRequest<string>(RunnerMethod.Handshake) { Value = "Hello from core" })) != "Hello from runner")
            {
                throw new InvalidOperationException("Could not handshake with runner");
            }
        }

        private async Task InitAsync(string path)
        {
            await WriteAsync(new ValueRequest<string>(RunnerMethod.Init) { Value = path });
        }

        private InvocationFailedException RemapException(InvokeRequest request, InvocationFailedException e)
        {
            if (request.AttributeName == Attributes.STEP)
            {
                logger.LogAsync(LogLevel.Debug, $"{nameof(InvocationFailedException)} remapped to {nameof(StepFailedException)}", Id);
                return new StepFailedException(e.Message, e.StackTrace);
            }
            return e;
        }
    }
}
