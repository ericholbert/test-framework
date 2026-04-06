using TestFramework.Core.Logger;

namespace TestFramework.Core.Configuration
{
    internal class FileSettings()
    {
        public bool? Recursive { get; init; }
        public int? MaxParallelism { get; init; }
        public ObjectRetention? ObjectRetention { get; init; }
        public LogLevel? LogLevel { get; init; }
        public TestOutput? TestOutput { get; init; }
    }
}
