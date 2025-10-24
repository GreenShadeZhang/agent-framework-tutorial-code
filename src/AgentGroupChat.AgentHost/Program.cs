using AgentGroupChat.AgentHost.Services;
using AgentGroupChat.Models;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.Services.AddOpenApi();

// Add services to the container.
builder.Services.AddProblemDetails();

// Configure Azure OpenAI chat client
var azureOpenAIEndpoint = builder.Configuration["AzureOpenAI:Endpoint"];
var azureOpenAIKey = builder.Configuration["AzureOpenAI:ApiKey"];
var azureOpenAIDeploymentName = builder.Configuration["AzureOpenAI:DeploymentName"];

if (string.IsNullOrEmpty(azureOpenAIEndpoint) || string.IsNullOrEmpty(azureOpenAIKey))
{
    throw new InvalidOperationException(
        "Azure OpenAI configuration is missing. Please set AzureOpenAI:Endpoint and AzureOpenAI:ApiKey in appsettings.json or environment variables.");
}

// Register custom services
builder.Services.AddSingleton<SessionService>();
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

var app = builder.Build();

app.MapOpenApi();

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
app.MapGet("/api/sessions", (SessionService sessionService) =>
{
    return Results.Ok(sessionService.GetAllSessions());
})
.WithName("GetSessions")
.WithOpenApi();

// Create new session
app.MapPost("/api/sessions", (SessionService sessionService) =>
{
    var session = sessionService.CreateSession();
    return Results.Ok(session);
})
.WithName("CreateSession")
.WithOpenApi();

// Get specific session
app.MapGet("/api/sessions/{id}", (string id, SessionService sessionService) =>
{
    var session = sessionService.GetSession(id);
    if (session == null)
        return Results.NotFound();
    return Results.Ok(session);
})
.WithName("GetSession")
.WithOpenApi();

// Send message and get streaming response
app.MapPost("/api/chat", async (ChatRequest request, AgentChatService agentService, SessionService sessionService) =>
{
    if (string.IsNullOrWhiteSpace(request.Message) || string.IsNullOrWhiteSpace(request.SessionId))
        return Results.BadRequest("Message and SessionId are required");

    var session = sessionService.GetSession(request.SessionId);
    if (session == null)
        return Results.NotFound("Session not found");

    // Add user message to session
    var userMsg = new AgentGroupChat.Models.ChatMessage
    {
        Content = request.Message,
        IsUser = true
    };
    session.Messages.Add(userMsg);
    sessionService.UpdateSession(session);

    // Get agent responses
    var responses = await agentService.SendMessageAsync(request.Message, session.Messages);
    
    // Add responses to session
    foreach (var response in responses)
    {
        session.Messages.Add(response);
    }
    sessionService.UpdateSession(session);

    return Results.Ok(responses);
})
.WithName("SendChatMessage")
.WithOpenApi();

app.MapDefaultEndpoints();

app.Run();

// Request model for chat endpoint
public record ChatRequest(string SessionId, string Message);
