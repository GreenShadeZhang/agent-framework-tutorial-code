using AgentGroupChat.Web;
using AgentGroupChat.Web.Components;
using AgentGroupChat.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Add root components
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Add MudBlazor services with configuration to reduce JS interop issues
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = false;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 3000;
    config.SnackbarConfiguration.HideTransitionDuration = 500;
    config.SnackbarConfiguration.ShowTransitionDuration = 500;
});

// Get the base address from configuration or use a default
// For Aspire, this will be set via appsettings
var agentHostUrl = builder.Configuration["AgentHostUrl"] ?? "https://localhost:7390";

// Add HttpClient for AgentHost API communication
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(agentHostUrl) });

builder.Services.AddScoped<AgentHostClient>();

await builder.Build().RunAsync();
