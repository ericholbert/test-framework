namespace TestFramework.Core.Orchestration
{
    internal class InvocationFailedException : RunnerException
    {
        public InvocationFailedException(string message, string? stackTrace) : base(message, stackTrace)
        {
        }
    }
}
