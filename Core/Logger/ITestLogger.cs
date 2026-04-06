
namespace TestFramework.Core.Logger
{
    internal interface ITestLogger
    {
        Task LogAsync(TestOutput level, string message, params string[] statuses);
    }
}
