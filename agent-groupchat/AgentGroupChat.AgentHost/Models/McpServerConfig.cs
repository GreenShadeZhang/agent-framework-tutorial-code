namespace AgentGroupChat.Models;

/// <summary>
/// MCP transport mode enumeration
/// Defines how the client communicates with the MCP server
/// </summary>
public enum McpTransportMode
{
    /// <summary>
    /// Automatically detect the transport mode based on server capabilities
    /// </summary>
    AutoDetect = 0,

    /// <summary>
    /// Use Server-Sent Events (SSE) for streaming communication
    /// Recommended for DashScope and other SSE-based services
    /// </summary>
    Sse = 1,

    /// <summary>
    /// Use streamable HTTP for communication
    /// </summary>
    StreamableHttp = 2
}

/// <summary>
/// Configuration for MCP (Model Context Protocol) servers
/// Supports multiple MCP server endpoints with different authentication methods
/// </summary>
public class McpServersConfig
{
    /// <summary>
    /// List of MCP server configurations
    /// </summary>
    public List<McpServerConfig> Servers { get; set; } = new();
}

/// <summary>
/// Configuration for a single MCP server endpoint
/// </summary>
public class McpServerConfig
{
    /// <summary>
    /// Unique identifier for this MCP server
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name for this MCP server
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// MCP server endpoint URL (e.g., https://dashscope.aliyuncs.com/api/v1/mcps/TextToImage/sse)
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Authentication type: "Bearer", "OAuth", "None"
    /// </summary>
    public string AuthType { get; set; } = "None";

    /// <summary>
    /// Transport mode for MCP communication: AutoDetect, Sse, StreamableHttp
    /// Default is AutoDetect which lets the client automatically determine the best mode
    /// For DashScope and SSE-based services, use Sse explicitly for better compatibility
    /// </summary>
    public McpTransportMode TransportMode { get; set; } = McpTransportMode.AutoDetect;

    /// <summary>
    /// Bearer token for authentication (when AuthType is "Bearer")
    /// </summary>
    public string? BearerToken { get; set; }

    /// <summary>
    /// OAuth configuration (when AuthType is "OAuth")
    /// </summary>
    public McpOAuthConfig? OAuth { get; set; }

    /// <summary>
    /// Whether this server is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Optional description of the server's capabilities
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// OAuth configuration for MCP server
/// </summary>
public class McpOAuthConfig
{
    /// <summary>
    /// OAuth client ID
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// OAuth client secret
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// OAuth redirect URI
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// OAuth authorization server URL
    /// </summary>
    public string AuthorizationUrl { get; set; } = string.Empty;

    /// <summary>
    /// OAuth token endpoint URL
    /// </summary>
    public string TokenUrl { get; set; } = string.Empty;
}
