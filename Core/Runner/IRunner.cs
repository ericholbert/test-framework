using TestFramework.Core.Configuration;
using TestFramework.Core.Protocol;

namespace TestFramework.Core.Runner
{
    public interface IRunner
    {
        RunnerType Type { get; }
        string Id { get; }

        Task RunAsync();
        void Dispose();
        Task<EnvelopeResponse<T>> ReadAsync<T>(string id, CancellationToken ct);
        Task<string> WriteAsync<T>(T message) where T : Message;
    }
}