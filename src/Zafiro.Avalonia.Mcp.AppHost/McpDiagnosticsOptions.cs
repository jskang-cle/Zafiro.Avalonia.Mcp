using Zafiro.Avalonia.Mcp.AppHost.AppTools;

namespace Zafiro.Avalonia.Mcp.AppHost;

public enum TransportKind
{
    /// <summary>NamedPipe on desktop (Windows/Linux/macOS), TCP loopback on Android.</summary>
    Auto,
    /// <summary>Force NamedPipes (will fail in Android sandboxes).</summary>
    NamedPipe,
    /// <summary>Force TCP loopback. Useful for cross-process debugging on desktop, mandatory on Android.</summary>
    Tcp
}

public sealed class McpDiagnosticsOptions
{
    private readonly List<IAppTool> _tools = [];

    public TransportKind Transport { get; set; } = TransportKind.Auto;

    /// <summary>The app's own tools, reached through <c>list_app_tools</c> and <c>call_app_tool</c>.</summary>
    public IReadOnlyList<IAppTool> Tools => _tools;

    /// <summary>Offers one of the app's own tools to an agent. Names are unique.</summary>
    public McpDiagnosticsOptions AddTool(IAppTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (_tools.Any(t => t.Name == tool.Name))
            throw new ArgumentException($"An app tool named '{tool.Name}' is already registered.", nameof(tool));
        _tools.Add(tool);
        return this;
    }
}
