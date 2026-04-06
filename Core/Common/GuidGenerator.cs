namespace TestFramework.Core.Common
{
    public static class GuidGenerator
    {
        public static string Get(string prefix)
        {
            return $"{prefix}:{Convert.ToBase64String(Guid.NewGuid().ToByteArray())}";
        }
    }
}
