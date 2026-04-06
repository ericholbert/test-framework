namespace TestFramework.Core.Orchestration
{
    internal class RunnerException : Exception
    {
        private readonly string? stackTrace;

        public RunnerException(string message) : base(message)
        {
        }

        public RunnerException(string message, string? stackTrace) : base(message)
        {
            this.stackTrace = stackTrace;
        }

        public override string? StackTrace => stackTrace;

        public string FullMessage => $"{Message}{(StackTrace != null ? $"{Environment.NewLine}{StackTrace}" : "")}";
    }
}
