namespace TestFramework.Core.Protocol
{
    public class EnvelopeResponse<T> : Envelope<T>
    {
        public ErrorInfo? Error { get; set; }
    }
}
