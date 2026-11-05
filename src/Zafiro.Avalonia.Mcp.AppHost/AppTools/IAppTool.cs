using System.Text.Json;
using Zafiro.Avalonia.Mcp.Protocol.Models;

namespace Zafiro.Avalonia.Mcp.AppHost.AppTools;

/// <summary>
/// A tool the app offers an agent beyond its UI — reading its own state, acting on its own model —
/// registered with <see cref="McpDiagnosticsOptions.AddTool(IAppTool)"/> and reached through the MCP
/// tools <c>list_app_tools</c> and <c>call_app_tool</c>.
/// </summary>
/// <remarks>
/// Invoked off the UI thread, as the request arrives; a tool that touches controls marshals to
/// <c>Dispatcher.UIThread</c> itself.
/// </remarks>
public interface IAppTool
{
    /// <summary>The name <c>call_app_tool</c> calls it by; unique within the app.</summary>
    string Name { get; }

    /// <summary>What it does and returns, for the agent.</summary>
    string Description { get; }

    /// <summary>The JSON Schema of its arguments object; null when it takes none.</summary>
    JsonElement? InputSchema { get; }

    /// <param name="arguments">The arguments object the agent sent: an empty object when it sent none.</param>
    Task<AppToolResult> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken);
}

/// <summary>An <see cref="IAppTool"/> from a delegate.</summary>
public sealed class AppTool : IAppTool
{
    private readonly Func<JsonElement, CancellationToken, Task<AppToolResult>> _invoke;

    /// <param name="inputSchema">The arguments' JSON Schema as JSON text; null when it takes none.</param>
    public AppTool(string name, string description, string? inputSchema, Func<JsonElement, CancellationToken, Task<AppToolResult>> invoke)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(invoke);
        Name = name;
        Description = description;
        InputSchema = inputSchema is null ? null : JsonDocument.Parse(inputSchema).RootElement.Clone();
        _invoke = invoke;
    }

    public string Name { get; }

    public string Description { get; }

    public JsonElement? InputSchema { get; }

    public Task<AppToolResult> InvokeAsync(JsonElement arguments, CancellationToken cancellationToken) =>
        _invoke(arguments, cancellationToken);
}
