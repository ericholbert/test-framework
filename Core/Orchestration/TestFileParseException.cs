namespace TestFramework.Core.Orchestration
{
    internal class TestFileParseException : Exception
    {
        public TestFileParseException(string path, int line, int column, string message)
            : base(Format(path, line, column, message))
        {
        }

        private static string Format(string path, int line, int column, string message)
        {
            return $"[{path}:{line + 1},{column + 1}] {message}";
        }
    }
}
