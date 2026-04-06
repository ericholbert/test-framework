namespace TestFramework.Core.Protocol
{
    public class Envelope<T>()
    {
        public string Owner { get; } = nameof(TestFramework);
        public required string Id { get; init; }
        public required RunnerMethod Method { get; init; }
        public T? Content { get; set; }
    }
}
