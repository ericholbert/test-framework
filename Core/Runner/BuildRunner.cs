using System.Diagnostics;
using TestFramework.Core.Configuration;
using TestFramework.Core.Logger;

namespace TestFramework.Core.Runner
{
    public abstract class BuildRunner : RunnerBase
    {
        protected Lazy<Task<BuildInfo>> build;
        private BuildInfo? buildInfo;

        protected BuildRunner(string? name, string? srcPath, RunnerType type, ILogger logger) : base(name, srcPath, type, logger)
        {
            build = new Lazy<Task<BuildInfo>>(GetBuildInfoAsync, isThreadSafe: true);
        }

        public abstract Task BuildAsync();

        public abstract Task<BuildInfo> GetBuildInfoAsync();

        public override async Task RunAsync()
        {
            buildInfo = await build.Value;
            await base.RunAsync();
        }

        protected override Process GetRunProcess()
        {
            return GetProcess(arguments: buildInfo?.Arguments, workingDirectory: buildInfo?.WorkingDirectory);
        }

        public record BuildInfo(string Arguments, string WorkingDirectory);
    }
}