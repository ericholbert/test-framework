namespace TestFramework.Core.Protocol
{
    public class DefinitionRequest() : Message(RunnerMethod.GetDefinitions)
    {
        public required string AttributeName { get; set; }
    }
}
