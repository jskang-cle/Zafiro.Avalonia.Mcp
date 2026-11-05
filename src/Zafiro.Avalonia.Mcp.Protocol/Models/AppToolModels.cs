using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zafiro.Avalonia.Mcp.Protocol.Models;

/// <summary>A tool the app itself offers, as <c>list_app_tools</c> reports it.</summary>
/// <param name="Name">What <c>call_app_tool</c> calls it by.</param>
/// <param name="Description">What it does and returns, for the agent.</param>
/// <param name="InputSchema">The JSON Schema of its arguments object; null when it takes none.</param>
public sealed record AppToolInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inputSchema")] JsonElement? InputSchema);

/// <summary>One part of an app tool's result: text, or an image.</summary>
public sealed record AppToolContent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string? Text = null,
    [property: JsonPropertyName("data")] string? Data = null,
    [property: JsonPropertyName("mimeType")] string? MimeType = null)
{
    public static AppToolContent FromText(string text) => new("text", Text: text);

    /// <summary>An image, carried base64-encoded.</summary>
    public static AppToolContent FromImage(byte[] bytes, string mimeType = "image/png") =>
        new("image", Data: Convert.ToBase64String(bytes), MimeType: mimeType);
}

/// <summary>
/// What an app tool returned: its parts in order, and whether it failed. A failure the tool reports
/// itself — "no such signal" — is a result with <see cref="IsError"/> set, not an exception.
/// </summary>
public sealed record AppToolResult(
    [property: JsonPropertyName("content")] IReadOnlyList<AppToolContent> Content,
    [property: JsonPropertyName("isError")] bool IsError = false)
{
    public static AppToolResult Text(string text) => new([AppToolContent.FromText(text)]);

    /// <summary>A value serialized as indented JSON text.</summary>
    public static AppToolResult Json<T>(T value) =>
        Text(JsonSerializer.Serialize(value, IndentedOptions));

    public static AppToolResult Error(string message) => new([AppToolContent.FromText(message)], IsError: true);

    private static readonly JsonSerializerOptions IndentedOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };
}
