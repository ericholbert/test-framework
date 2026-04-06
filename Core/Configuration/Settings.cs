using TestFramework.Core.Logger;

namespace TestFramework.Core.Configuration
{
    internal class Settings()
    {
        public required bool Recursive { get; init; }
        public required int MaxParallelism { get; init; }
        public required ObjectRetention ObjectRetention { get; init; }
        public required LogLevel LogLevel { get; init; }
        public required TestOutput TestOutput { get; init; }
    }
}
