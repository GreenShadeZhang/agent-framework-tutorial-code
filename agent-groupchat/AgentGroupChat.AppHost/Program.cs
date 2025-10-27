var builder = DistributedApplication.CreateBuilder(args);


// Add AgentHost backend service
var agentHost = builder.AddProject<Projects.AgentGroupChat_AgentHost>("agenthost");

//// Add Web frontend service
builder.AddProject<Projects.AgentGroupChat_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(agentHost)
    .WaitFor(agentHost);

builder.Build().Run();
