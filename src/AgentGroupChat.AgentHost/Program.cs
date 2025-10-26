using AgentGroupChat.AgentHost;
using AgentGroupChat.AgentHost.Services;
using AgentGroupChat.Models;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.Services.AddOpenApi();

// Add services to the container.
builder.Services.AddProblemDetails();

// Add HttpClient factory for MCP service
builder.Services.AddHttpClient();

// Configure database provider (SQLite or PostgreSQL)
var databaseProvider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (databaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
{
    // Use PostgreSQL
    if (string.IsNullOrEmpty(connectionString))
    {
        connectionString = builder.Configuration["ConnectionStrings:PostgreSQL"] 
            ?? Environment.GetEnvironmentVariable("POSTGRESQL_CONNECTION_STRING")
            ?? "Host=localhost;Database=agentchat;Username=postgres;Password=postgres";
    }
    
    builder.Services.AddDbContext<AgentDbContext>(options =>
        options.UseNpgsql(connectionString));
}
else
{
    // Use SQLite (default)
    if (string.IsNullOrEmpty(connectionString))
    {
        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        Directory.CreateDirectory(dbPath);
        connectionString = $"Data Source={Path.Combine(dbPath, "agentchat.db")}";
    }
    
    builder.Services.AddDbContext<AgentDbContext>(options =>
        options.UseSqlite(connectionString));
}

// Register custom services with interface-based dependency injection
builder.Services.AddScoped<ISessionService, EfCoreSessionService>();
builder.Services.AddSingleton<McpToolService>();
builder.Services.AddScoped<AgentChatService>();

// Enable CORS for Web frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWeb", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Initialize MCP service at startup
var app = builder.Build();

// Initialize MCP connections
var mcpService = app.Services.GetRequiredService<McpToolService>();
await mcpService.InitializeAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseCors("AllowWeb");

// ===== API Endpoints =====

// Get all agent profiles
app.MapGet("/api/agents", (AgentChatService agentService) =>
{
    return Results.Ok(agentService.GetAgentProfiles());
})
.WithName("GetAgents")
.WithOpenApi();

// Get all sessions
app.MapGet("/api/sessions", (ISessionService sessionService) =>
{
    var sessions = sessionService.GetAllSessions();
    
    // 映射到前端模型（不包含消息详情，只有元数据）
    var result = sessions.Select(s => new
    {
        s.Id,
        s.Name,
        s.CreatedAt,
        s.LastUpdated,
        s.MessageCount,
        s.IsActive,
        Messages = new List<object>() // 空消息列表，前端会通过 /messages 端点加载
    }).ToList();
    
    return Results.Ok(result);
})
.WithName("GetSessions")
.WithOpenApi();

// Create new session
app.MapPost("/api/sessions", (ISessionService sessionService) =>
{
    var session = sessionService.CreateSession();
    
    // 映射到前端模型
    var result = new
    {
        session.Id,
        session.Name,
        session.CreatedAt,
        session.LastUpdated,
        session.MessageCount,
        session.IsActive,
        Messages = new List<object>() // 新会话没有消息
    };
    
    return Results.Ok(result);
})
.WithName("CreateSession")
.WithOpenApi();

// Get specific session
app.MapGet("/api/sessions/{id}", (string id, ISessionService sessionService) =>
{
    var session = sessionService.GetSession(id);
    if (session == null)
        return Results.NotFound();
    
    // 映射到前端模型（不包含消息详情，前端会通过 /messages 端点加载）
    var result = new
    {
        session.Id,
        session.Name,
        session.CreatedAt,
        session.LastUpdated,
        session.MessageCount,
        session.IsActive,
        Messages = new List<object>() // 空消息列表
    };
    
    return Results.Ok(result);
})
.WithName("GetSession")
.WithOpenApi();

// Send message and get streaming response
app.MapPost("/api/chat", async (ChatRequest request, AgentChatService agentService, ISessionService sessionService) =>
{
    if (string.IsNullOrWhiteSpace(request.Message) || string.IsNullOrWhiteSpace(request.SessionId))
        return Results.BadRequest("Message and SessionId are required");

    var session = sessionService.GetSession(request.SessionId);
    if (session == null)
        return Results.NotFound("Session not found");

    // 发送消息并自动持久化（新架构：消息通过 ChatMessageStore 自动保存）
    var responses = await agentService.SendMessageAsync(
        request.Message, 
        request.SessionId);

    return Results.Ok(responses);
})
.WithName("SendChatMessage")
.WithOpenApi();

// Delete session
app.MapDelete("/api/sessions/{id}", (string id, ISessionService sessionService) =>
{
    sessionService.DeleteSession(id);
    return Results.Ok();
})
.WithName("DeleteSession")
.WithOpenApi();

// Clear conversation (keep session, clear messages)
app.MapPost("/api/sessions/{id}/clear", (string id, AgentChatService agentService, ISessionService sessionService) =>
{
    var session = sessionService.GetSession(id);
    if (session == null)
        return Results.NotFound("Session not found");
    
    agentService.ClearConversation(id);
    return Results.Ok();
})
.WithName("ClearConversation")
.WithOpenApi();

// Get conversation history
app.MapGet("/api/sessions/{id}/messages", (string id, AgentChatService agentService, ISessionService sessionService) =>
{
    var session = sessionService.GetSession(id);
    if (session == null)
        return Results.NotFound("Session not found");
    
    var history = agentService.GetConversationHistory(id);
    return Results.Ok(history);
})
.WithName("GetConversationHistory")
.WithOpenApi();

// Get statistics
app.MapGet("/api/stats", (ISessionService sessionService) =>
{
    var stats = sessionService.GetStatistics();
    return Results.Ok(stats);
})
.WithName("GetStatistics")
.WithOpenApi();

// Get MCP server information
app.MapGet("/api/mcp/servers", (McpToolService mcpService) =>
{
    var servers = mcpService.GetServerInfo();
    return Results.Ok(servers);
})
.WithName("GetMcpServers")
.WithOpenApi();

// ===== 调试端点 =====

// Debug: Get raw messages from database
app.MapGet("/api/debug/messages/{sessionId}", (string sessionId, ISessionService sessionService) =>
{
    var sessionServiceImpl = sessionService as EfCoreSessionService;
    if (sessionServiceImpl == null)
    {
        return Results.Problem("Session service is not EfCoreSessionService");
    }
    
    var collection = sessionServiceImpl.GetMessagesCollection();
    var messages = collection.Find(sessionId).ToList();
    
    return Results.Ok(new
    {
        SessionId = sessionId,
        TotalMessages = messages.Count,
        Messages = messages.Select(m => new
        {
            m.Id,
            m.SessionId,
            m.MessageId,
            m.Timestamp,
            MessageText = m.MessageText?.Length > 100 
                ? m.MessageText.Substring(0, 100) + "..." 
                : m.MessageText,
            m.AgentId,
            m.AgentName,
            m.AgentAvatar,
            m.IsUser,
            m.Role,
            m.ImageUrl
        }).ToList()
    });
})
.WithName("DebugGetMessages")
.WithOpenApi();

// Debug: Get all sessions with message counts
app.MapGet("/api/debug/sessions", (ISessionService sessionService) =>
{
    var sessionServiceImpl = sessionService as EfCoreSessionService;
    if (sessionServiceImpl == null)
    {
        return Results.Problem("Session service is not EfCoreSessionService");
    }
    
    var collection = sessionServiceImpl.GetMessagesCollection();
    var sessions = sessionService.GetAllSessions();
    
    var result = sessions.Select(s => new
    {
        s.Id,
        s.Name,
        s.CreatedAt,
        s.LastUpdated,
        s.MessageCount,
        ActualMessageCount = collection.Count(s.Id),
        s.IsActive
    }).ToList();
    
    return Results.Ok(result);
})
.WithName("DebugGetSessions")
.WithOpenApi();

app.MapDefaultEndpoints();

app.Run();

// Request model for chat endpoint
public record ChatRequest(string SessionId, string Message);
