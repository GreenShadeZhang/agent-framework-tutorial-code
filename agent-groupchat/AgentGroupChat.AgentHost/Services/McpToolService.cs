using AgentGroupChat.Models;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using System.Net.Http.Headers;

namespace AgentGroupChat.AgentHost.Services;

/// <summary>
/// Service for managing MCP (Model Context Protocol) server connections and tools.
/// This service initializes MCP clients, handles authentication, and provides tools to agents.
/// Based on the official Agent Framework MCP samples.
/// </summary>
public class McpToolService : IAsyncDisposable
{
    private readonly ILogger<McpToolService> _logger;
    private readonly IConfiguration _configuration;
    private readonly List<McpClientWrapper> _mcpClients = new();
    private readonly HttpClient _httpClient;
    private bool _initialized = false;

    public McpToolService(
        ILogger<McpToolService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient("McpClient");
    }

    /// <summary>
    /// Initialize all configured MCP server connections
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            _logger.LogWarning("MCP service already initialized");
            return;
        }

        var config = _configuration.GetSection("McpServers").Get<McpServersConfig>();
        if (config == null || config.Servers.Count == 0)
        {
            _logger.LogInformation("No MCP servers configured");
            _initialized = true;
            return;
        }

        foreach (var serverConfig in config.Servers.Where(s => s.Enabled))
        {
            try
            {
                _logger.LogInformation("Initializing MCP server: {ServerName} ({Endpoint})", 
                    serverConfig.Name, serverConfig.Endpoint);

                var client = await CreateMcpClientAsync(serverConfig, cancellationToken);
                if (client != null)
                {
                    var tools = await client.Client.ListToolsAsync();
                    
                    _mcpClients.Add(new McpClientWrapper
                    {
                        Config = serverConfig,
                        Client = client.Client,
                        Tools = tools.Cast<AITool>().ToList()
                    });

                    _logger.LogInformation("Successfully initialized MCP server '{ServerName}' with {ToolCount} tools",
                        serverConfig.Name, tools.Count());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize MCP server: {ServerName}", serverConfig.Name);
            }
        }

        _initialized = true;
        _logger.LogInformation("MCP service initialized with {ClientCount} active servers", _mcpClients.Count);
    }

    /// <summary>
    /// Create an MCP client for a specific server configuration
    /// </summary>
    private async Task<McpClientWrapper?> CreateMcpClientAsync(
        McpServerConfig config, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Create logger factory for MCP client
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Create transport based on authentication type
            IClientTransport transport = config.AuthType.ToLowerInvariant() switch
            {
                "bearer" => CreateBearerTokenTransport(config),
                "oauth" => CreateOAuthTransport(config),
                _ => CreateNoAuthTransport(config)
            };

            // Create MCP client
            var mcpClient = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken, loggerFactory: loggerFactory);

            _logger.LogInformation("Created MCP client for server: {ServerName}", config.Name);

            return new McpClientWrapper
            {
                Config = config,
                Client = mcpClient,
                Tools = new List<AITool>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create MCP client for server: {ServerName}", config.Name);
            return null;
        }
    }

    /// <summary>
    /// Create HTTP transport with Bearer token authentication
    /// </summary>
    private IClientTransport CreateBearerTokenTransport(McpServerConfig config)
    {
        if (string.IsNullOrEmpty(config.BearerToken))
        {
            throw new InvalidOperationException($"Bearer token is required for server: {config.Name}");
        }

        // Configure HttpClient with Bearer token
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", config.BearerToken);

        // Convert config transport mode to SDK transport mode
        var transportMode = ConvertToSdkTransportMode(config.TransportMode);

        var transportOptions = new HttpClientTransportOptions
        {
            Endpoint = new Uri(config.Endpoint),
            Name = config.Name
        };

        // Only set TransportMode if not AutoDetect (let SDK auto-detect by default)
        if (config.TransportMode != McpTransportMode.AutoDetect)
        {
            transportOptions.TransportMode = transportMode;
        }

        var transport = new HttpClientTransport(transportOptions, httpClient);

        _logger.LogDebug("Created Bearer token transport for {ServerName} with mode: {TransportMode}", 
            config.Name, config.TransportMode);
        return transport;
    }

    /// <summary>
    /// Create HTTP transport with OAuth authentication
    /// </summary>
    private IClientTransport CreateOAuthTransport(McpServerConfig config)
    {
        if (config.OAuth == null)
        {
            throw new InvalidOperationException($"OAuth configuration is required for server: {config.Name}");
        }

        var transportOptions = new HttpClientTransportOptions
        {
            Endpoint = new Uri(config.Endpoint),
            Name = config.Name,
            OAuth = new()
            {
                ClientId = config.OAuth.ClientId,
                ClientSecret = config.OAuth.ClientSecret,
                RedirectUri = new Uri(config.OAuth.RedirectUri),
                // Note: For production OAuth flow, you would need to implement
                // AuthorizationRedirectDelegate similar to the Agent_MCP_Server_Auth sample
            }
        };

        // Only set TransportMode if not AutoDetect
        if (config.TransportMode != McpTransportMode.AutoDetect)
        {
            transportOptions.TransportMode = ConvertToSdkTransportMode(config.TransportMode);
        }

        var transport = new HttpClientTransport(transportOptions, _httpClient);

        _logger.LogDebug("Created OAuth transport for {ServerName} with mode: {TransportMode}", 
            config.Name, config.TransportMode);
        return transport;
    }

    /// <summary>
    /// Create HTTP transport without authentication
    /// </summary>
    private IClientTransport CreateNoAuthTransport(McpServerConfig config)
    {
        var transportOptions = new HttpClientTransportOptions
        {
            Endpoint = new Uri(config.Endpoint),
            Name = config.Name
        };

        // Only set TransportMode if not AutoDetect
        if (config.TransportMode != McpTransportMode.AutoDetect)
        {
            transportOptions.TransportMode = ConvertToSdkTransportMode(config.TransportMode);
        }

        var transport = new HttpClientTransport(transportOptions, _httpClient);

        _logger.LogDebug("Created no-auth transport for {ServerName} with mode: {TransportMode}", 
            config.Name, config.TransportMode);
        return transport;
    }

    /// <summary>
    /// Convert configuration TransportMode to SDK HttpTransportMode
    /// </summary>
    private static HttpTransportMode ConvertToSdkTransportMode(McpTransportMode mode)
    {
        return mode switch
        {
            McpTransportMode.Sse => HttpTransportMode.Sse,
            McpTransportMode.StreamableHttp => HttpTransportMode.StreamableHttp,
            McpTransportMode.AutoDetect => HttpTransportMode.AutoDetect,
            _ => HttpTransportMode.AutoDetect
        };
    }

    /// <summary>
    /// Get all available MCP tools from all connected servers
    /// </summary>
    public IEnumerable<AITool> GetAllTools()
    {
        if (!_initialized)
        {
            _logger.LogWarning("MCP service not initialized. Call InitializeAsync() first.");
            return Enumerable.Empty<AITool>();
        }

        return _mcpClients.SelectMany(c => c.Tools);
    }

    /// <summary>
    /// Get MCP tools from a specific server
    /// </summary>
    public IEnumerable<AITool> GetToolsByServerId(string serverId)
    {
        var client = _mcpClients.FirstOrDefault(c => 
            c.Config.Id.Equals(serverId, StringComparison.OrdinalIgnoreCase));

        return client?.Tools ?? Enumerable.Empty<AITool>();
    }

    /// <summary>
    /// Get information about all connected MCP servers
    /// </summary>
    public IEnumerable<McpServerInfo> GetServerInfo()
    {
        return _mcpClients.Select(c => new McpServerInfo
        {
            Id = c.Config.Id,
            Name = c.Config.Name,
            Endpoint = c.Config.Endpoint,
            Description = c.Config.Description,
            ToolCount = c.Tools.Count,
            IsConnected = true
        });
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _mcpClients)
        {
            try
            {
                await client.Client.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing MCP client for server: {ServerName}", client.Config.Name);
            }
        }

        _mcpClients.Clear();
        _httpClient?.Dispose();
        
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Wrapper class for MCP client with its configuration and tools
    /// </summary>
    private class McpClientWrapper
    {
        public required McpServerConfig Config { get; set; }
        public required McpClient Client { get; set; }
        public required List<AITool> Tools { get; set; }
    }
}

/// <summary>
/// Information about an MCP server
/// </summary>
public class McpServerInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ToolCount { get; set; }
    public bool IsConnected { get; set; }
}
