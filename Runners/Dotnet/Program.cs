using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using TestFramework.Bindings.Dotnet;
using TestFramework.Core.Common;
using TestFramework.Core.Logger;
using TestFramework.Core.Protocol;
using TestFramework.Core.Runner;

namespace TestFramework.Runners.Dotnet
{
    internal class Program
    {
        private static ImmutableArray<Type> types = [];
        private static readonly ConcurrentDictionary<string, (MethodDefinition, MethodInfo)[]> definitionCache = [];
        private static readonly ConcurrentDictionary<Type, object> instanceCache = [];

        private static async Task Main(string[] args)
        {
            string? line;

            while ((line = Console.In.ReadLine()) != null)
            {
                var envelope = Json.TryDeserialize<Envelope<JsonElement>>(line);

                if (envelope == null)
                {
                    continue;
                }

                var response = new EnvelopeResponse<object>()
                {
                    Id = envelope.Id,
                    Method = envelope.Method,
                };

                try
                {
                    if (envelope.Method == RunnerMethod.Handshake)
                    {
                        response.Content = Handshake(envelope.Content);
                    }

                    if (envelope.Method == RunnerMethod.Init)
                    {
                        await InitAsync(envelope.Content);
                    }

                    if (envelope.Method == RunnerMethod.GetDefinitions)
                    {
                        response.Content = GetDefinitions(envelope.Content);
                    }

                    if (envelope.Method == RunnerMethod.Invoke)
                    {
                        response.Content = Invoke(envelope.Content, response);
                    }
                }
                catch (Exception e)
                {
                    response.Error = new ErrorInfo()
                    {
                        Message = e.Message,
                        StackTrace = e.StackTrace,
                        InvocationFail = false,
                    };
                }
                finally
                {
                    Console.WriteLine(Json.Serialize(response));
                }
            }
        }

        private static string? Handshake(JsonElement content)
        {
            var request = Json.Deserialize<ValueRequest<string>>(content);
            return request.Value == "Hello from core" ? "Hello from runner" : null;
        }

        private static async Task InitAsync(JsonElement content)
        {
            var request = Json.Deserialize<ValueRequest<string>>(content);
            var runner = new DotnetRunner(new ConsoleLogger(LogLevel.None), srcPath: request.Value);
            await runner.BuildAsync();
            var buildInfo = await runner.GetBuildInfoAsync();
            var assembly = Assembly.LoadFrom(Path.Join(buildInfo.WorkingDirectory, buildInfo.Arguments));
            types = assembly.GetTypes().ToImmutableArray();
        }

        private static MethodDefinition[] GetDefinitions(JsonElement content)
        {
            var request = Json.Deserialize<DefinitionRequest>(content);

            MethodInfo[] infos = types
                .SelectMany(x => x.GetMethods()
                    .Where(y => y.CustomAttributes
                        .Any(z => z.AttributeType.Name == request.AttributeName)))
                .ToArray();

            MethodDefinition[] result = infos.Select(
                x => new MethodDefinition()
                {
                    Id = GuidGenerator.Get("INVOKE"),
                    AttributeValue = x.CustomAttributes
                        .FirstOrDefault(y => y.AttributeType.Name == request.AttributeName)?.ConstructorArguments
                            .FirstOrDefault().Value?.ToString()
                })
                    .ToArray();

            definitionCache[request.AttributeName] = result
                .Zip(infos, (definition, method) => (definition, method))
                .ToArray();

            return result;
        }

        private static object? Invoke(JsonElement content, EnvelopeResponse<object> response)
        {
            var request = Json.Deserialize<InvokeRequest>(content);
            MethodInfo[] infos;

            if (request.AttributeId == null)
            {
                infos = types
                    .SelectMany(x => x.GetMethods()
                        .Where(y => y.CustomAttributes
                            .Any(z => z.AttributeType.Name == request.AttributeName)))
                    .ToArray();
            }
            else
            {
                infos = definitionCache[request.AttributeName]
                    .Where(x => x.Item1.Id == request.AttributeId)
                    .Select(y => y.Item2)
                    .ToArray();
            }

            object? result = null;

            foreach (var info in infos)
            {
                if (info.DeclaringType == null)
                {
                    continue;
                }

                object? instance = !info.IsStatic
                    ? instanceCache.GetOrAdd(info.DeclaringType, x => Activator.CreateInstance(info.DeclaringType)!)
                    : null;

                var arguments = ConvertArgument(
                    info,
                    request.DefinitionCacheId != null
                        ? (request.Arguments ?? [])
                            .Append(definitionCache[request.DefinitionCacheId]
                                .Select(x => x.Item1)
                                .ToArray())
                            .ToArray()
                        : request.Arguments);

                try
                {
                    result = info.Invoke(instance, arguments);
                }
                catch (Exception e)
                {
                    response.Error = new ErrorInfo()
                    {
                        Message = e.InnerException?.Message ?? e.Message,
                        StackTrace = e.InnerException?.StackTrace ?? e.StackTrace,
                        InvocationFail = true,
                    };
                }
            }

            if (request.ClearInstance == true)
            {
                instanceCache.Clear();
            }

            return result;
        }

        private static object?[] ConvertArgument(MethodInfo method, object[]? arguments)
        {
            if (arguments == null)
            {
                return [];
            }

            var result = new object?[arguments.Length];
            var parameters = method.GetParameters();

            for (int i = 0; i < arguments.Length; i++)
            {
                var targetType = parameters[i].ParameterType;

                if (arguments[i] is JsonElement element)
                {
                    var effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;

                    if (element.ValueKind == JsonValueKind.String && effectiveType != typeof(string))
                    {
                        result[i] = Convert.ChangeType(element.GetString(), effectiveType, CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        result[i] = JsonSerializer.Deserialize(element.GetRawText(), targetType);
                    }
                }
                else
                {
                    result[i] = arguments[i];
                }
            }

            return result;
        }
    }
}
