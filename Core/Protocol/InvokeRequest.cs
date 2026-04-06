namespace TestFramework.Core.Protocol
{
    public class InvokeRequest() : Message(RunnerMethod.Invoke)
    {
        public required string AttributeName { get; set; }
        public string? AttributeId { get; set; }
        public object[]? Arguments { get; set; }
        public bool ClearInstance { get; set; } = true;
        public string? DefinitionCacheId { get; set; }
    }
}
