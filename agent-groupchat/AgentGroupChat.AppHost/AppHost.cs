var builder = DistributedApplication.CreateBuilder(args);


var agentHost = builder.AddProject<Projects.AgentGroupChat_AgentHost>("agenthost");

builder.AddProject<Projects.AgentGroupChat_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(agentHost)
    .WaitFor(agentHost);

builder.Build().Run();
