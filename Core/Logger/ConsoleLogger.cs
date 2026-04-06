namespace TestFramework.Core.Logger
{
    public class ConsoleLogger : LoggerBase<LogLevel>, ILogger
    {
        public ConsoleLogger(LogLevel minimumLevel) : base(minimumLevel)
        {
        }

        protected override async Task WriteAsync(string message)
        {
            await Console.Error.WriteLineAsync(message);
        }
    }
}
