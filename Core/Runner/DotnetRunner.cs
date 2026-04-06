using TestFramework.Core.Configuration;
using TestFramework.Core.Logger;

namespace TestFramework.Core.Runner
{
    public class DotnetRunner : BuildRunner
    {
        public DotnetRunner(ILogger logger, string? name = null, string? srcPath = null) : base(name, srcPath, RunnerType.Dotnet, logger)
        {
        }

        public override async Task BuildAsync()
        {
            var buildProcess = GetProcess(arguments: "build", redirect: false);
            buildProcess.Start();
            await buildProcess.WaitForExitAsync();
        }

        public override async Task<BuildInfo> GetBuildInfoAsync()
        {
            var resultProcess = GetProcess(arguments: $"msbuild -getProperty:TargetPath");
            resultProcess.Start();
            var path = (await resultProcess.StandardOutput.ReadToEndAsync()).TrimEnd();
            if (path == string.Empty)
            {
                throw new InvalidOperationException("Could not retrieve TargetPath");
            }
            return new BuildInfo(Path.GetFileName(path), Path.GetDirectoryName(path)!);
        }
    }
}