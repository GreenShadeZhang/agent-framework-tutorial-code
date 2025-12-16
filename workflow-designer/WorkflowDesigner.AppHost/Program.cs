var builder = DistributedApplication.CreateBuilder(args);

// Add the backend API
var api = builder.AddProject("workflowdesigner-api", "../WorkflowDesigner.Api/WorkflowDesigner.Api.csproj")
    .WithHttpHealthCheck("/health");

// Add the frontend Vite app
var frontend = builder.AddViteApp("workflowdesigner-frontend", "../frontend")
    .WithNpm()
    .WithEnvironment("BROWSER", "none")
    .WithReference(api)
    .WithHttpEndpoint(env: "PORT", name: "frontend-http")
    .WithExternalHttpEndpoints();

builder.Build().Run();
