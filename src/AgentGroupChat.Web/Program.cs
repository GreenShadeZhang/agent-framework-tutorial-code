using AgentGroupChat.Web.Components;
using AgentGroupChat.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

// This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
// Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
// NOTE: When running with Aspire, use: new("https+http://agenthost")
// For standalone testing without Aspire, use: new("https://localhost:7390") or new("http://localhost:5390")
Uri baseAddress = new("https+http://agenthost");

// Add HttpClient for AgentHost API communication
builder.Services.AddHttpClient<AgentHostClient>(client => 
{
    client.BaseAddress = baseAddress;
    client.Timeout = TimeSpan.FromMinutes(5); // Allow long-running agent responses
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
