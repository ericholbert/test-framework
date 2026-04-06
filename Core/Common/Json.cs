using System.Text.Json;
using System.Text.Json.Serialization;

namespace TestFramework.Core.Common
{
    public static class Json
    {
        private static readonly JsonSerializerOptions options = new()
        {
            Converters = { new JsonStringEnumConverter() },
            IncludeFields = true,
        };

        public static string Serialize<T>(T value)
            => JsonSerializer.Serialize(value, options);

        public static T? TryDeserialize<T>(string? json) where T : class
            => TryDeserialize(json, (json) => JsonSerializer.Deserialize<T>(json, options));

        public static T? TryDeserialize<T>(JsonElement json) where T : class
            => TryDeserialize(json, (json) => JsonSerializer.Deserialize<T>(json, options));

        public static T Deserialize<T>(string? json) where T : class
            => Deserialize(() => TryDeserialize<T>(json));

        public static T Deserialize<T>(JsonElement json) where T : class
            => Deserialize(() => TryDeserialize<T>(json));

        private static TOut? TryDeserialize<TIn, TOut>(TIn? json, Func<TIn, TOut?> f) where TOut : class
        {
            if (json != null)
            {
                try
                {
                    return f(json);
                }
                catch (JsonException)
                {
                }
                catch (NotSupportedException)
                {
                }
            }
            return null;
        }

        private static T Deserialize<T>(Func<T?> f) where T : class
            => f() ?? throw new JsonException($"Could not deserialize {typeof(T).Name}");
    }
}
