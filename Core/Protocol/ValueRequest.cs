namespace TestFramework.Core.Protocol
{
    public class ValueRequest<T>(RunnerMethod method) : Message(method)
    {
        public required T Value { get; set; }
    }
}
