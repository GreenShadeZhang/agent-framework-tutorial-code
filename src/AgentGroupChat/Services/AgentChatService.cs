using AgentGroupChat.Models;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace AgentGroupChat.Services;

/// <summary>
/// Service for managing multi-agent chat with handoff pattern.
/// </summary>
public class AgentChatService
{
    private readonly IChatClient _chatClient;
    private readonly List<AgentProfile> _agentProfiles;
    private readonly Dictionary<string, ChatClientAgent> _agents;
    private readonly Workflow _workflow;
    private readonly ImageGenerationTool _imageTool;

    public AgentChatService(IConfiguration configuration)
    {
        // Initialize OpenAI client
        var endpoint = configuration["AzureOpenAI:Endpoint"] ?? 
                      Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? 
                      throw new InvalidOperationException("Azure OpenAI endpoint not configured");
        var deploymentName = configuration["AzureOpenAI:DeploymentName"] ?? 
                            Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? 
                            "gpt-4o-mini";

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            .GetChatClient(deploymentName);
        _chatClient = azureClient as IChatClient ?? throw new InvalidOperationException("Failed to get chat client");

        _imageTool = new ImageGenerationTool();

        // Define agent profiles with different personalities
        _agentProfiles = new List<AgentProfile>
        {
            new AgentProfile
            {
                Id = "sunny",
                Name = "Sunny",
                Avatar = "☀️",
                Personality = "Cheerful and optimistic",
                SystemPrompt = "You are Sunny, a cheerful and optimistic AI assistant who loves to share positive thoughts and daily life photos. " +
                              "You often talk about sunshine, nature, and happy moments. When sharing photos, describe them enthusiastically. " +
                              "Always respond in a warm and friendly tone.",
                Description = "The optimistic one who loves sunshine"
            },
            new AgentProfile
            {
                Id = "techie",
                Name = "Techie",
                Avatar = "🤖",
                Personality = "Tech-savvy and analytical",
                SystemPrompt = "You are Techie, a tech-savvy and analytical AI assistant who loves gadgets, coding, and technology. " +
                              "You enjoy sharing photos of your latest tech discoveries and explaining how things work. " +
                              "You use technical terms but explain them clearly.",
                Description = "The tech enthusiast who codes and tinkers"
            },
            new AgentProfile
            {
                Id = "artsy",
                Name = "Artsy",
                Avatar = "🎨",
                Personality = "Creative and artistic",
                SystemPrompt = "You are Artsy, a creative and artistic AI assistant who sees beauty in everything. " +
                              "You love to share photos of art, design, and beautiful scenes. " +
                              "You often describe things with vivid, colorful language and appreciate aesthetics.",
                Description = "The artist who finds beauty everywhere"
            },
            new AgentProfile
            {
                Id = "foodie",
                Name = "Foodie",
                Avatar = "🍜",
                Personality = "Food-loving and enthusiastic",
                SystemPrompt = "You are Foodie, a food-loving AI assistant who adores trying new dishes and sharing food photos. " +
                              "You love to describe flavors, textures, and cooking experiences. " +
                              "You're always excited about meals and culinary adventures.",
                Description = "The food enthusiast who loves to eat and cook"
            }
        };

        // Create agents with image generation tool
        _agents = new Dictionary<string, ChatClientAgent>();
        foreach (var profile in _agentProfiles)
        {
            var agent = new ChatClientAgent(_chatClient, profile.SystemPrompt, profile.Id, profile.Description);
            // Note: Tool registration would require proper Microsoft.Agents.AI API
            // For now, we'll handle image generation separately
            _agents[profile.Id] = agent;
        }

        // Create triage agent for routing
        var triageAgent = new ChatClientAgent(_chatClient,
            "You are a triage agent that routes messages to the appropriate agent based on mentions. " +
            "When a user mentions an agent with @AgentName, handoff to that agent. " +
            "Available agents: @Sunny (cheerful), @Techie (tech-savvy), @Artsy (artistic), @Foodie (food-loving). " +
            "If no specific agent is mentioned, respond with a friendly greeting and list available agents.",
            "triage",
            "Routes messages to the appropriate agent");

        // Build handoff workflow
        var builder = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent);
        
        // Add handoffs from triage to all agents
        builder.WithHandoffs(triageAgent, _agents.Values.ToArray());
        
        // Add handoffs from all agents back to triage
        builder.WithHandoffs(_agents.Values.ToArray(), triageAgent);

        _workflow = builder.Build();
    }

    public List<AgentProfile> GetAgentProfiles() => _agentProfiles;

    public AgentProfile? GetAgentProfile(string agentId) => 
        _agentProfiles.FirstOrDefault(a => a.Id.Equals(agentId, StringComparison.OrdinalIgnoreCase));

    public async Task<List<Models.ChatMessage>> SendMessageAsync(string message, List<Models.ChatMessage> history)
    {
        var messages = new List<Models.ChatMessage>();
        
        try
        {
            // Convert history to ChatMessage format for workflow
            var chatMessages = new List<AIChatMessage>
            {
                new(ChatRole.User, message)
            };

            // Run workflow
            await using StreamingRun run = await InProcessExecution.StreamAsync(_workflow, chatMessages);
            await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

            string? currentAgentId = null;
            var responseText = new System.Text.StringBuilder();

            await foreach (WorkflowEvent evt in run.WatchStreamAsync())
            {
                if (evt is AgentRunUpdateEvent updateEvent)
                {
                    if (updateEvent.ExecutorId != currentAgentId)
                    {
                        // Save previous agent's message if any
                        if (currentAgentId != null && responseText.Length > 0)
                        {
                            var profile = GetAgentProfile(currentAgentId);
                            if (profile != null)
                            {
                                messages.Add(new Models.ChatMessage
                                {
                                    AgentId = profile.Id,
                                    AgentName = profile.Name,
                                    AgentAvatar = profile.Avatar,
                                    Content = responseText.ToString(),
                                    IsUser = false
                                });
                            }
                            responseText.Clear();
                        }
                        currentAgentId = updateEvent.ExecutorId;
                    }

                    responseText.Append(updateEvent.Update.Text);
                }
                else if (evt is WorkflowOutputEvent)
                {
                    // Save final message
                    if (currentAgentId != null && responseText.Length > 0)
                    {
                        var profile = GetAgentProfile(currentAgentId);
                        if (profile != null)
                        {
                            var content = responseText.ToString();
                            messages.Add(new Models.ChatMessage
                            {
                                AgentId = profile.Id,
                                AgentName = profile.Name,
                                AgentAvatar = profile.Avatar,
                                Content = content,
                                IsUser = false
                            });

                            // Check if we should generate an image
                            if (ShouldGenerateImage(content))
                            {
                                var imageUrl = await _imageTool.GenerateImage($"{profile.Personality} scene");
                                messages.Add(new Models.ChatMessage
                                {
                                    AgentId = profile.Id,
                                    AgentName = profile.Name,
                                    AgentAvatar = profile.Avatar,
                                    Content = $"Here's a photo I'd like to share! 📸",
                                    ImageUrl = imageUrl,
                                    IsUser = false
                                });
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            messages.Add(new Models.ChatMessage
            {
                AgentId = "system",
                AgentName = "System",
                AgentAvatar = "⚠️",
                Content = $"Error: {ex.Message}",
                IsUser = false
            });
        }

        return messages;
    }

    private bool ShouldGenerateImage(string content)
    {
        // Simple heuristic to determine if agent should share an image
        var imageKeywords = new[] { "photo", "picture", "image", "show", "look", "see", "here" };
        return imageKeywords.Any(keyword => content.Contains(keyword, StringComparison.OrdinalIgnoreCase)) 
               && new Random().Next(0, 2) == 0; // 50% chance
    }
}
