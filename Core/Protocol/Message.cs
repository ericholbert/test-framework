namespace TestFramework.Core.Protocol
{
    public class Message(RunnerMethod method)
    {
        public RunnerMethod Method { get; } = method;
    }
}
