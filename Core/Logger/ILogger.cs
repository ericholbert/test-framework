namespace TestFramework.Core.Logger
{
    public interface ILogger
    {
        Task LogAsync(LogLevel level, string message, params string[] statuses);
    }
}
