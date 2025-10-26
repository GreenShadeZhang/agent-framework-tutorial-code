using AgentGroupChat.AgentHost.Services;
using AgentGroupChat.Models;
using Scalar.AspNetCore;
using LiteDB;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.Services.AddOpenApi();

// Add services to the container.
builder.Services.AddProblemDetails();

// Add HttpClient factory for MCP service
builder.Services.AddHttpClient();

// Register LiteDB database as singleton with shared mode for better concurrency
builder.Services.AddSingleton<LiteDatabase>(sp =>
{
    var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
    Directory.CreateDirectory(dbPath);
    var dbFilePath = Path.Combine(dbPath, "sessions.db");
    
    // 使用连接字符串配置 LiteDB
    // Mode=Shared: 允许多个进程/线程读取，但写入时会锁定
    // Connection=shared: 共享连接模式，提高并发性能
    var connectionString = $"Filename={dbFilePath};Mode=Shared;Connection=shared";
    
    return new LiteDatabase(connectionString);
});

// Register repositories
builder.Services.AddSingleton<AgentRepository>();
builder.Services.AddSingleton<AgentGroupRepository>();

// Register IChatClient for WorkflowManager
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var defaultModelProvider = configuration["DefaultModelProvider"] ?? "AzureOpenAI";

    if (defaultModelProvider == "AzureOpenAI")
    {
        var endpoint = configuration["AzureOpenAI:Endpoint"] ??
                      Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ??
                      throw new InvalidOperationException("Azure OpenAI endpoint not configured");
        var deploymentName = configuration["AzureOpenAI:DeploymentName"] ??
                            Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ??
                            "gpt-4o-mini";
        var apiKey = configuration["AzureOpenAI:ApiKey"] ??
                     Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ??
                     throw new InvalidOperationException("Azure OpenAI API key not configured");

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey))
            .GetChatClient(deploymentName);
        return azureClient.AsIChatClient() ?? throw new InvalidOperationException("Failed to get chat client");
    }
    else if (defaultModelProvider == "OpenAI")
    {
        var baseUrl = configuration["OpenAI:BaseUrl"] ??
                      Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ??
                      string.Empty;
        var modelName = configuration["OpenAI:ModelName"] ??
                        Environment.GetEnvironmentVariable("OPENAI_MODEL_NAME") ??
                        "gpt-4o-mini";
        var apiKey = configuration["OpenAI:ApiKey"] ??
                        Environment.GetEnvironmentVariable("OPENAI_API_KEY") ??
                        throw new InvalidOperationException("OpenAI API key not configured");

        var options = !string.IsNullOrEmpty(baseUrl) ?
              new OpenAIClientOptions { Endpoint = new Uri(baseUrl) } : null;
        var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), options);

        return openAiClient.GetChatClient(modelName).AsIChatClient()
            ?? throw new InvalidOperationException("Failed to get chat client");
    }
    else
    {
        throw new InvalidOperationException($"Unsupported DefaultModelProvider: {defaultModelProvider}");
    }
});

// Register custom services
builder.Services.AddSingleton<PersistedSessionService>();
builder.Services.AddSingleton<McpToolService>();
builder.Services.AddSingleton<WorkflowManager>();
builder.Services.AddSingleton<AgentChatService>();

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
app.MapGet("/api/sessions", (PersistedSessionService sessionService) =>
{
    var sessions = sessionService.GetAllSessions();
    
    // 映射到前端模型（不包含消息详情，只有元数据）
    var result = sessions.Select(s => new
    {
        s.Id,
        s.Name,
        s.GroupId,
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
app.MapPost("/api/sessions", (PersistedSessionService sessionService, CreateSessionRequest? request) =>
{
    var session = sessionService.CreateSession(request?.Name, request?.GroupId);
    
    // 映射到前端模型
    var result = new
    {
        session.Id,
        session.Name,
        session.GroupId,
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
app.MapGet("/api/sessions/{id}", (string id, PersistedSessionService sessionService) =>
{
    var session = sessionService.GetSession(id);
    if (session == null)
        return Results.NotFound();
    
    // 映射到前端模型（不包含消息详情，前端会通过 /messages 端点加载）
    var result = new
    {
        session.Id,
        session.Name,
        session.GroupId,
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
app.MapPost("/api/chat", async (ChatRequest request, AgentChatService agentService, PersistedSessionService sessionService) =>
{
    if (string.IsNullOrWhiteSpace(request.Message) || string.IsNullOrWhiteSpace(request.SessionId))
        return Results.BadRequest("Message and SessionId are required");

    var session = sessionService.GetSession(request.SessionId);
    if (session == null)
        return Results.NotFound("Session not found");

    // 使用会话关联的 GroupId 发送消息
    var responses = await agentService.SendMessageAsync(
        request.Message, 
        request.SessionId,
        session.GroupId); // 使用会话的 GroupId

    return Results.Ok(responses);
})
.WithName("SendChatMessage")
.WithOpenApi();

// Delete session
app.MapDelete("/api/sessions/{id}", (string id, PersistedSessionService sessionService) =>
{
    sessionService.DeleteSession(id);
    return Results.Ok();
})
.WithName("DeleteSession")
.WithOpenApi();

// Clear conversation (keep session, clear messages)
app.MapPost("/api/sessions/{id}/clear", (string id, AgentChatService agentService, PersistedSessionService sessionService) =>
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
app.MapGet("/api/sessions/{id}/messages", (string id, AgentChatService agentService, PersistedSessionService sessionService) =>
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
app.MapGet("/api/stats", (PersistedSessionService sessionService) =>
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

// ===== Agent Management Endpoints =====

// Get all agents from database
app.MapGet("/api/admin/agents", (AgentRepository agentRepo) =>
{
    var agents = agentRepo.GetAll();
    return Results.Ok(agents);
})
.WithName("GetAllAgentsFromDb")
.WithOpenApi();

// Get agent by ID
app.MapGet("/api/admin/agents/{id}", (string id, AgentRepository agentRepo) =>
{
    var agent = agentRepo.GetById(id);
    if (agent == null)
        return Results.NotFound($"Agent {id} not found");
    
    return Results.Ok(agent);
})
.WithName("GetAgentById")
.WithOpenApi();

// Create or update agent
app.MapPost("/api/admin/agents", (PersistedAgentProfile agent, AgentRepository agentRepo, WorkflowManager workflowManager) =>
{
    if (string.IsNullOrWhiteSpace(agent.Id))
        return Results.BadRequest("Agent ID is required");
    
    agentRepo.Upsert(agent);
    
    // Clear workflow cache to force recreation with new agent config
    workflowManager.ClearAllWorkflowCache();
    
    return Results.Ok(agent);
})
.WithName("UpsertAgent")
.WithOpenApi();

// Delete agent
app.MapDelete("/api/admin/agents/{id}", (string id, AgentRepository agentRepo, WorkflowManager workflowManager) =>
{
    var deleted = agentRepo.Delete(id);
    if (!deleted)
        return Results.NotFound($"Agent {id} not found");
    
    // Clear workflow cache
    workflowManager.ClearAllWorkflowCache();
    
    return Results.Ok();
})
.WithName("DeleteAgent")
.WithOpenApi();

// ===== Agent Group Management Endpoints =====

// Get all agent groups
app.MapGet("/api/admin/groups", (AgentGroupRepository groupRepo) =>
{
    var groups = groupRepo.GetAll();
    return Results.Ok(groups);
})
.WithName("GetAllGroups")
.WithOpenApi();

// Get agent group by ID
app.MapGet("/api/admin/groups/{id}", (string id, AgentGroupRepository groupRepo) =>
{
    var group = groupRepo.GetById(id);
    if (group == null)
        return Results.NotFound($"Group {id} not found");
    
    return Results.Ok(group);
})
.WithName("GetGroupById")
.WithOpenApi();

// Create or update agent group
app.MapPost("/api/admin/groups", (AgentGroup group, AgentGroupRepository groupRepo, WorkflowManager workflowManager) =>
{
    if (string.IsNullOrWhiteSpace(group.Id))
        return Results.BadRequest("Group ID is required");
    
    groupRepo.Upsert(group);
    
    // Clear workflow cache for this group
    workflowManager.ClearWorkflowCache(group.Id);
    
    return Results.Ok(group);
})
.WithName("UpsertGroup")
.WithOpenApi();

// Delete agent group
app.MapDelete("/api/admin/groups/{id}", (string id, AgentGroupRepository groupRepo, WorkflowManager workflowManager) =>
{
    var deleted = groupRepo.Delete(id);
    if (!deleted)
        return Results.NotFound($"Group {id} not found");
    
    // Clear workflow cache
    workflowManager.ClearWorkflowCache(id);
    
    return Results.Ok();
})
.WithName("DeleteGroup")
.WithOpenApi();

// ===== Initialization Endpoint =====

// Initialize default agents and groups
app.MapPost("/api/admin/initialize", (AgentRepository agentRepo, AgentGroupRepository groupRepo) =>
{
    try
    {
        agentRepo.InitializeDefaultAgents();
        groupRepo.InitializeDefaultGroup();
        
        return Results.Ok(new
        {
            Message = "Default agents and groups initialized successfully",
            AgentCount = agentRepo.GetAll().Count,
            GroupCount = groupRepo.GetAll().Count
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error initializing data: {ex.Message}");
    }
})
.WithName("InitializeDefaultData")
.WithOpenApi();

// ===== 调试端点 =====

// Debug: Get raw messages from database
app.MapGet("/api/debug/messages/{sessionId}", (string sessionId, PersistedSessionService sessionService) =>
{
    var collection = sessionService.GetMessagesCollection();
    var messages = collection.Find(m => m.SessionId == sessionId).ToList();
    
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
app.MapGet("/api/debug/sessions", (PersistedSessionService sessionService) =>
{
    var collection = sessionService.GetMessagesCollection();
    var sessions = sessionService.GetAllSessions();
    
    var result = sessions.Select(s => new
    {
        s.Id,
        s.Name,
        s.CreatedAt,
        s.LastUpdated,
        s.MessageCount,
        ActualMessageCount = collection.Count(m => m.SessionId == s.Id),
        s.IsActive
    }).ToList();
    
    return Results.Ok(result);
})
.WithName("DebugGetSessions")
.WithOpenApi();

app.MapDefaultEndpoints();

app.Run();

// Request models
public record ChatRequest(string SessionId, string Message);
public record CreateSessionRequest(string? Name = null, string? GroupId = null);
