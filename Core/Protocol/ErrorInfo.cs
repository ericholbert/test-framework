namespace TestFramework.Core.Protocol
{
    public class ErrorInfo
    {
        public required string Message { get; init; }
        public required string? StackTrace { get; init; }
        public required bool InvocationFail { get; init; }
    }
}
