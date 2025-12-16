using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.Text.Json.Serialization;
using WorkflowDesigner.Api.Models;
using WorkflowDesigner.Api.Repository;
using WorkflowDesigner.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (OpenTelemetry, Health checks, Service discovery)
builder.AddServiceDefaults();

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // 支持枚举字符串转换
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // 使用 camelCase 命名策略
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        // 忽略 null 值
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Workflow Designer API", Version = "v1" });
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure LiteDB
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "Data", "workflow-designer.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
builder.Services.AddSingleton(new LiteDbContext($"Filename={dbPath};Connection=shared"));

// Register repositories
builder.Services.AddScoped<IRepository<AgentDefinition>>(sp =>
    new LiteDbRepository<AgentDefinition>(sp.GetRequiredService<LiteDbContext>(), "agents"));
builder.Services.AddScoped<IRepository<WorkflowDefinition>>(sp =>
    new LiteDbRepository<WorkflowDefinition>(sp.GetRequiredService<LiteDbContext>(), "workflows"));
builder.Services.AddScoped<IRepository<ExecutionLog>>(sp =>
    new LiteDbRepository<ExecutionLog>(sp.GetRequiredService<LiteDbContext>(), "execution_logs"));

// Configure AI Chat Client (使用 OpenAI 或 Azure OpenAI)
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var openAiModel = builder.Configuration["OpenAI:Model"] ?? "gpt-4o-mini";
var openAiBaseUrl = builder.Configuration["OpenAI:BaseUrl"];

if (!string.IsNullOrEmpty(openAiApiKey))
{
    builder.Services.AddSingleton<IChatClient>(sp =>
    {
        try
        {
            // 使用 OpenAI Chat Client
            var client = new OpenAIClient(new ApiKeyCredential(openAiApiKey), new OpenAIClientOptions() { Endpoint = new Uri(openAiBaseUrl ?? "") });
            var chatClient = client.GetChatClient(openAiModel);
            return chatClient as IChatClient ?? new EmptyChatClient();
        }
        catch (Exception ex)
        {
            sp.GetRequiredService<ILogger<Program>>().LogWarning(ex,
                "Failed to configure OpenAI client, using EmptyChatClient");
            return new EmptyChatClient();
        }
    });
}
else
{
    builder.Services.AddSingleton<IChatClient>(sp =>
    {
        sp.GetRequiredService<ILogger<Program>>().LogWarning(
            "OpenAI API key not configured, using EmptyChatClient. Set OpenAI:ApiKey in appsettings.json or OPENAI_API_KEY environment variable.");
        return new EmptyChatClient();
    });
}

// Register Agent Framework components
builder.Services.AddScoped<WorkflowExecutor>();

// Register services
builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddSingleton<ITemplateService, ScribanTemplateService>();

var app = builder.Build();

// Map default endpoints (health checks, etc.)
app.MapDefaultEndpoints();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();
