namespace TestFramework.Core.Orchestration
{
    internal class StepFailedException : InvocationFailedException
    {
        public StepFailedException(string message, string? stackTrace) : base(message, stackTrace)
        {
        }
    }
}
