using System.Text.Json;

namespace DeferredTransactions.Internal;

internal static class JsonDefaults
{
    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };
}
