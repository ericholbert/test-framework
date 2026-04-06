using System.CommandLine;
using TestFramework.Core.Common;
using TestFramework.Core.Configuration;
using TestFramework.Core.Logger;
using TestFramework.Core.Orchestration;

namespace TestFramework.Core
{
    internal class Program
    {
        private const string MANIFEST_NAME = "manifest.json";
        private const string SETTINGS_NAME = "configuration.json";

        private static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand("Test Framework");

            var paths = new Argument<string[]>("paths") { Arity = ArgumentArity.OneOrMore };
            var recursive = new Option<bool?>("--recursive", "-r");
            var parallel = new Option<int?>("--parallel", "-p");
            var retention = new Option<ObjectRetention?>("--retention");
            var logLevel = new Option<LogLevel?>("--log-level", "-l");
            var testOutput = new Option<TestOutput?>("--test-output", "-t");

            rootCommand.Arguments.Add(paths);
            rootCommand.Options.Add(recursive);
            rootCommand.Options.Add(parallel);
            rootCommand.Options.Add(retention);
            rootCommand.Options.Add(logLevel);
            rootCommand.Options.Add(testOutput);

            rootCommand.SetAction(
                async (parseResult, ct) =>
                {
                    if (parseResult.Errors.Count > 0)
                    {
                        foreach (var parseError in parseResult.Errors)
                        {
                            Console.Error.WriteLine(parseError.Message);
                        }

                        return 1;
                    }

                    var filePaths = parseResult.GetValue(paths);
                    var (projectPath, manifestPath, settingsPath) = GetConfigurationPaths(filePaths![0]);
                    var manifest = await GetManifestAsync(manifestPath);
                    var fileSettings = await GetFileSettingsAsync(settingsPath);
                        
                    var settings = new Settings()
                    {
                        Recursive = parseResult.GetValue(recursive) ?? fileSettings.Recursive ?? false,
                        MaxParallelism = parseResult.GetValue(parallel) ?? fileSettings.MaxParallelism ?? 1,
                        ObjectRetention = parseResult.GetValue(retention) ?? fileSettings.ObjectRetention ?? ObjectRetention.Test,
                        LogLevel = parseResult.GetValue(logLevel) ?? fileSettings.LogLevel ?? LogLevel.Info,
                        TestOutput = parseResult.GetValue(testOutput) ?? fileSettings.TestOutput ?? TestOutput.Step,
                    };

                    await new RunnerOrchestrator(
                        settings,
                        new ConsoleLogger(settings.LogLevel),
                        new TestLogger(settings.TestOutput))
                        .RunAsync(manifest.Runner, projectPath, filePaths);

                    return 0;
                });

            var parseResult = rootCommand.Parse(args);
            return await parseResult.InvokeAsync();
        }

        private static (string projectPath, string manifestPath, string settingsPath) GetConfigurationPaths(string path)
        {
            var directory = new DirectoryInfo(path);

            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, MANIFEST_NAME);

                if (File.Exists(candidate))
                {
                    return (directory.FullName, candidate, Path.Combine(directory.FullName, SETTINGS_NAME));
                }

                directory = directory.Parent;
            }

            throw new FileNotFoundException("Could not find manifest");
        }

        private static async Task<Manifest> GetManifestAsync(string path)
        {
            return Json.Deserialize<Manifest>(await File.ReadAllTextAsync(path));
        }

        private static async Task<FileSettings> GetFileSettingsAsync(string path)
        {
            FileSettings? settings = null;
            if (File.Exists(path))
            {
                settings = Json.Deserialize<FileSettings>(await File.ReadAllTextAsync(path));
            }
            return settings ?? new FileSettings();
        }
    }
}