using System.Text.Json;
using Zafiro.Avalonia.Mcp.AppHost;
using Zafiro.Avalonia.Mcp.AppHost.AppTools;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.Protocol;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using Zafiro.Avalonia.Mcp.Protocol.Models;
using Xunit;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

public class AppToolHandlersTests
{
    private static readonly IAppTool Echo = new AppTool(
        "echo", "Echoes its text argument.",
        """{"type":"object","properties":{"text":{"type":"string"}},"required":["text"]}""",
        (args, _) => Task.FromResult(AppToolResult.Text(args.GetProperty("text").GetString()!)));

    private static readonly IAppTool Picture = new AppTool(
        "picture", "Returns a caption and an image.", null,
        (_, _) => Task.FromResult(new AppToolResult([AppToolContent.FromText("a picture"), AppToolContent.FromImage([1, 2, 3])])));

    private static async Task<DiagnosticResponse> Dispatch(IEnumerable<IAppTool> tools, string method, object? @params = null)
    {
        var dispatcher = new RequestDispatcher(tools);
        var json = await dispatcher.Dispatch(ProtocolSerializer.Serialize(new DiagnosticRequest
        {
            Id = "test",
            Method = method,
            Params = @params is null ? null : ProtocolSerializer.ToElement(@params),
        }));
        return ProtocolSerializer.Deserialize<DiagnosticResponse>(json)!;
    }

    [Fact]
    public async Task ListAppTools_ReportsNameDescriptionAndSchema()
    {
        var response = await Dispatch([Echo, Picture], ProtocolMethods.ListAppTools);

        var tools = response.Result!.Value.GetProperty("tools").EnumerateArray().ToList();
        Assert.Equal(["echo", "picture"], tools.Select(t => t.GetProperty("name").GetString()));
        Assert.Equal("object", tools[0].GetProperty("inputSchema").GetProperty("type").GetString());
        Assert.False(tools[1].TryGetProperty("inputSchema", out _));   // omitted, as the protocol omits nulls
    }

    [Fact]
    public async Task ListAppTools_IsEmpty_WhenTheAppRegistersNone()
    {
        var response = await Dispatch([], ProtocolMethods.ListAppTools);

        Assert.Empty(response.Result!.Value.GetProperty("tools").EnumerateArray());
    }

    [Fact]
    public async Task CallAppTool_PassesTheArgumentsAndReturnsTheContent()
    {
        var response = await Dispatch([Echo], ProtocolMethods.CallAppTool, new { name = "echo", arguments = new { text = "hello" } });

        var content = response.Result!.Value.GetProperty("content")[0];
        Assert.Equal("text", content.GetProperty("type").GetString());
        Assert.Equal("hello", content.GetProperty("text").GetString());
        Assert.False(response.Result!.Value.GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task CallAppTool_CarriesAnImageAsBase64()
    {
        var response = await Dispatch([Picture], ProtocolMethods.CallAppTool, new { name = "picture" });

        var image = response.Result!.Value.GetProperty("content")[1];
        Assert.Equal("image", image.GetProperty("type").GetString());
        Assert.Equal("image/png", image.GetProperty("mimeType").GetString());
        Assert.Equal([1, 2, 3], Convert.FromBase64String(image.GetProperty("data").GetString()!));
    }

    [Fact]
    public async Task CallAppTool_GivesAToolWithoutArgumentsAnEmptyObject()
    {
        JsonValueKind? seen = null;
        var probe = new AppTool("probe", "Records what it was given.", null, (args, _) =>
        {
            seen = args.ValueKind;
            return Task.FromResult(AppToolResult.Text("ok"));
        });

        await Dispatch([probe], ProtocolMethods.CallAppTool, new { name = "probe" });

        Assert.Equal(JsonValueKind.Object, seen);
    }

    [Fact]
    public async Task CallAppTool_RefusesAnUnknownNameAndListsTheKnownOnes()
    {
        var response = await Dispatch([Echo, Picture], ProtocolMethods.CallAppTool, new { name = "nope" });

        Assert.Equal(DiagnosticErrorCodes.InvalidParam, response.ErrorInfo!.Code);
        Assert.Contains("echo, picture", response.ErrorInfo.Suggested);
    }

    [Fact]
    public async Task CallAppTool_RefusesAMissingName()
    {
        var response = await Dispatch([Echo], ProtocolMethods.CallAppTool, new { });

        Assert.Equal(DiagnosticErrorCodes.InvalidParam, response.ErrorInfo!.Code);
    }

    [Fact]
    public async Task CallAppTool_ReportsAToolsOwnFailureAsAnErrorResult()
    {
        var failing = new AppTool("failing", "Always fails.", null, (_, _) => Task.FromResult(AppToolResult.Error("no such signal")));

        var response = await Dispatch([failing], ProtocolMethods.CallAppTool, new { name = "failing" });

        Assert.True(response.Result!.Value.GetProperty("isError").GetBoolean());
        Assert.Equal("no such signal", response.Result!.Value.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task CallAppTool_ReportsAThrowingToolAsInternal()
    {
        var throwing = new AppTool("throwing", "Throws.", null, (_, _) => throw new InvalidOperationException("boom"));

        var response = await Dispatch([throwing], ProtocolMethods.CallAppTool, new { name = "throwing" });

        Assert.Equal(DiagnosticErrorCodes.Internal, response.ErrorInfo!.Code);
        Assert.Equal("boom", response.ErrorInfo.Message);
    }

    [Fact]
    public void AddTool_RefusesADuplicateName()
    {
        var options = new McpDiagnosticsOptions().AddTool(Echo);

        Assert.Throws<ArgumentException>(() => options.AddTool(Echo));
    }
}
