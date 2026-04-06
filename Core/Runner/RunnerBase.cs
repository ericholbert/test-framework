using System.Diagnostics;
using TestFramework.Core.Common;
using TestFramework.Core.Configuration;
using TestFramework.Core.Logger;
using TestFramework.Core.Protocol;

namespace TestFramework.Core.Runner
{
    public abstract class RunnerBase : IRunner
    {
        private static readonly string runnerHome = Path.Join(AppContext.BaseDirectory, "../../../../Runners");

        protected ILogger logger;
        private readonly string name;
        private readonly string srcPath;
        private readonly RunnerType type;
        private readonly string id;
        private Process? process;
        private bool disposed = false;

        protected RunnerBase(string? name, string? srcPath, RunnerType type, ILogger logger)
        {
            this.name = name ?? type.ToString().ToLower();
            this.srcPath = srcPath ?? Path.Join(runnerHome, type.ToString());
            this.type = type;
            this.logger = logger;
            id = GuidGenerator.Get("RUNNER");
            logger.LogAsync(LogLevel.Info, $"Created {this.type} runner", Id);
        }

        public RunnerType Type => type;

        public string Id => id;

        private Process Process
        {
            get => process ?? throw new InvalidOperationException($"{Type} runner has not been started");
            set => process = value;
        }

        public virtual Task RunAsync()
        {
            Process = GetRunProcess();
            if (!Process.Start())
            {
                throw new InvalidOperationException($"{Type} runner could not be started");
            }
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            Process.StandardInput.Close();
            Process.Dispose();
            disposed = true;
        }

        public async Task<EnvelopeResponse<T>> ReadAsync<T>(string id, CancellationToken ct)
        {
            await logger.LogAsync(LogLevel.Debug, "Reading", Id, id);
            while (true)
            {
                var line = await Process.StandardOutput.ReadLineAsync(ct)
                    ?? throw new InvalidOperationException("Runner output stream ended unexpectedly");
                var result = Json.TryDeserialize<EnvelopeResponse<T>>(line);

                if (result == null)
                {
                    Console.WriteLine(line);
                    continue;
                }

                if (result.Id == id)
                {
                    await logger.LogAsync(LogLevel.Debug, $"Received message{(result.Content == null ? " without content" : "")}", Id, id);
                    return result;
                }

                await logger.LogAsync(LogLevel.Warning, $"Received unexpected message {result.Id}", Id, id);
            }
        }

        public async Task<string> WriteAsync<T>(T message) where T : Message
        {
            var envelope = new Envelope<T>()
            {
                Id = GuidGenerator.Get("MESSAGE"),
                Method = message.Method,
                Content = message,
            };
            await logger.LogAsync(LogLevel.Debug, "Writing", Id, envelope.Id);
            await Process.StandardInput.WriteLineAsync(Json.Serialize(envelope));
            await Process.StandardInput.FlushAsync();
            return envelope.Id;
        }

        protected Process GetProcess(
            string? fileName = null,
            string? arguments = null,
            string? workingDirectory = null,
            bool createNoWindow = true,
            bool redirect = true)
            => new()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = fileName ?? name,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory ?? srcPath,
                    UseShellExecute = false,
                    RedirectStandardInput = redirect,
                    RedirectStandardOutput = redirect,
                    RedirectStandardError = redirect,
                    CreateNoWindow = createNoWindow
                }
            };

        protected virtual Process GetRunProcess()
        {
            return GetProcess();
        }
    }
}