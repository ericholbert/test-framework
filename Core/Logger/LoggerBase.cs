namespace TestFramework.Core.Logger
{
    public abstract class LoggerBase<T> where T : struct, Enum
    {
        private readonly T minimumLevel;

        public LoggerBase(T minimumLevel)
        {
            this.minimumLevel = minimumLevel;
        }

        public async Task LogAsync(T level, string message, params string[] statuses)
        {
            await WriteIfEnabledAsync(level, () => Compose(level, message, statuses));
        }

        protected abstract Task WriteAsync(string message);

        protected virtual string Compose(T level, string message, params string[] statuses)
        {
            return $"{DateTime.Now:yyyy-MM-dd HH:mm:ss:fff} " +
                $"{string.Join(" ", new[] { level.ToString().ToUpper() }.Concat(statuses).Select(x => $"[{x}]"))} " +
                $"{message}";
        }

        private async Task WriteIfEnabledAsync(T level, Func<string> getMessage)
        {
            if (IsEnabled(level))
            {
                await WriteAsync(getMessage());
            }
        }

        private bool IsEnabled(T current)
        {
            return current.CompareTo(minimumLevel) >= 0;
        }
    }
}
