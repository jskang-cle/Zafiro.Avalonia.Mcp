using System.Text.Json;
using Zafiro.Avalonia.Mcp.AppHost.AppTools;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using Zafiro.Avalonia.Mcp.Protocol.Models;

namespace Zafiro.Avalonia.Mcp.AppHost.Handlers;

/// <summary>The app's own tools: their names, descriptions and argument schemas.</summary>
public sealed class ListAppToolsHandler(IReadOnlyDictionary<string, IAppTool> tools) : IRequestHandler
{
    public string Method => ProtocolMethods.ListAppTools;

    public Task<object> Handle(DiagnosticRequest request) =>
        Task.FromResult<object>(new
        {
            tools = tools.Values.Select(t => new AppToolInfo(t.Name, t.Description, t.InputSchema)).ToArray(),
        });
}

/// <summary>Calls one of the app's own tools by name with an arguments object.</summary>
public sealed class CallAppToolHandler(IReadOnlyDictionary<string, IAppTool> tools) : IRequestHandler
{
    public string Method => ProtocolMethods.CallAppTool;

    public async Task<object> Handle(DiagnosticRequest request)
    {
        if (request.Params is not { ValueKind: JsonValueKind.Object } p
            || !p.TryGetProperty("name", out var nameElement)
            || nameElement.GetString() is not { Length: > 0 } name)
        {
            return HandlerResult.InvalidParam("name", "call_app_tool needs the name of one of the app's tools.");
        }

        if (!tools.TryGetValue(name, out var tool))
        {
            return HandlerResult.Error(
                DiagnosticErrorCodes.InvalidParam,
                $"The app has no tool '{name}'.",
                tools.Count == 0
                    ? "This app registers no tools of its own."
                    : $"Call list_app_tools; the app has: {string.Join(", ", tools.Keys.Order(StringComparer.Ordinal))}.",
                new { name });
        }

        var arguments = p.TryGetProperty("arguments", out var a) && a.ValueKind == JsonValueKind.Object
            ? a
            : EmptyObject;
        return await tool.InvokeAsync(arguments, CancellationToken.None);
    }

    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();
}
