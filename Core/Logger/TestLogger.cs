namespace TestFramework.Core.Logger
{
    internal class TestLogger : LoggerBase<TestOutput>, ITestLogger
    {
        public TestLogger(TestOutput minimumLevel) : base(minimumLevel)
        {
        }

        protected override Task WriteAsync(string message)
        {
            Console.WriteLine(message);
            return Task.CompletedTask;
        }

        protected override string Compose(TestOutput level, string message, params string[] statuses)
        {
            return $"{(statuses.Length > 0 ? string.Join(" ", statuses.Select(x => $"[{x}]")) + " " : "")}{message}";
        }
    }
}
