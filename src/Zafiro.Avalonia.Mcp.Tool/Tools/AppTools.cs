using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Tool.Connection;

namespace Zafiro.Avalonia.Mcp.Tool.Tools;

/// <summary>
/// The connected app's own tools — what it registered with <c>AddTool</c> for an agent to reach
/// beyond its UI. Two fixed tools rather than one MCP tool each, so they work with any client
/// whether or not it follows a tool list that changes after <c>connect_to_app</c>.
/// </summary>
[McpServerToolType]
public sealed class AppTools
{
    [McpServerTool(Name = "list_app_tools"), Description("""
        List the tools the connected app offers itself, beyond its UI (its own state and actions, registered by the app). Call before call_app_tool.
        Returns: {tools:[{name, description, inputSchema}]}; an empty list when the app registers none.
        Example: {"tools":[{"name":"read_signals","description":"The station's signals and their values.","inputSchema":{"type":"object","properties":{"device":{"type":"string"}}}}]}
        """)]
    public static async Task<string> ListAppTools(ConnectionPool pool)
    {
        var conn = pool.GetActive();
        return await conn.InvokeAsync(ProtocolMethods.ListAppTools, new { });
    }

    [McpServerTool(Name = "call_app_tool"), Description("""
        Call one of the connected app's own tools by name, with an arguments object matching its inputSchema from list_app_tools.
        Returns: the tool's result as text and/or images; a failure the tool reports itself is returned as an error result.
        Example: call_app_tool name="read_signals" arguments={"device":"plc_main"}
        """)]
    public static async Task<IReadOnlyList<ContentBlock>> CallAppTool(
        ConnectionPool pool,
        [Description("The tool's name, as list_app_tools reports it")] string name,
        [Description("The tool's arguments object, matching its inputSchema; omit when it takes none")] JsonElement? arguments = null,
        [Description("How long to wait for the tool, in seconds (1-600, default 30)")] int timeoutSeconds = 30)
    {
        var conn = pool.GetActive();
        var parms = new Dictionary<string, object?> { ["name"] = name };
        if (arguments is { ValueKind: JsonValueKind.Object } a) parms["arguments"] = a;
        var timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 600));
        return await conn.InvokeRichAsync(ProtocolMethods.CallAppTool, parms, ToContent, timeout);
    }

    private static IReadOnlyList<ContentBlock> ToContent(JsonElement result)
    {
        if (result.TryGetProperty("isError", out var isError) && isError.ValueKind == JsonValueKind.True)
            ToolFailure.Mark();

        var blocks = new List<ContentBlock>();
        if (result.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in content.EnumerateArray())
            {
                var type = part.TryGetProperty("type", out var t) ? t.GetString() : null;
                if (type == "image" && part.TryGetProperty("data", out var data) && data.GetString() is { } base64)
                {
                    var mime = part.TryGetProperty("mimeType", out var m) ? m.GetString() ?? "image/png" : "image/png";
                    blocks.Add(ImageContentBlock.FromBytes(Convert.FromBase64String(base64), mime));
                }
                else if (part.TryGetProperty("text", out var text) && text.GetString() is { } s)
                {
                    blocks.Add(new TextContentBlock { Text = s });
                }
            }
        }
        return blocks.Count > 0 ? blocks : [new TextContentBlock { Text = "The tool returned nothing" }];
    }
}
