# Getting Started with Agent Group Chat

This quick start guide will help you get the Agent Group Chat application running in 5 minutes.

## Prerequisites Checklist

Before you begin, ensure you have:

- [ ] **.NET 9.0 SDK** or later installed
  ```bash
  dotnet --version  # Should show 9.0.x or higher
  ```
  If not installed: https://dot.net

- [ ] **Azure OpenAI Service** access
  - Azure subscription
  - Azure OpenAI resource created
  - At least one model deployed (e.g., gpt-4o-mini)

- [ ] **Azure CLI** (for authentication)
  ```bash
  az --version
  ```
  If not installed: https://docs.microsoft.com/cli/azure/install-azure-cli

## Quick Start (5 Minutes)

### Step 1: Clone the Repository (30 seconds)

```bash
git clone https://github.com/GreenShadeZhang/agent-framework-tutorial-code-.git
cd agent-framework-tutorial-code-/src/AgentGroupChat
```

### Step 2: Configure Azure OpenAI (1 minute)

**Option A: Edit appsettings.json** (Recommended for development)

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource-name.openai.azure.com/",
    "DeploymentName": "gpt-4o-mini"
  }
}
```

Replace:
- `your-resource-name`: Your Azure OpenAI resource name
- `gpt-4o-mini`: Your deployment name (check Azure portal)

**Option B: Use Environment Variables**

```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource-name.openai.azure.com/"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o-mini"
```

### Step 3: Authenticate with Azure (1 minute)

```bash
az login
```

This opens a browser for authentication. Sign in with your Azure account.

### Step 4: Run the Application (30 seconds)

```bash
dotnet run
```

Wait for the output:
```
Now listening on: https://localhost:5001
```

### Step 5: Open in Browser (30 seconds)

Navigate to: **https://localhost:5001**

You should see the Agent Group Chat interface! 🎉

## First Conversation

Try these sample interactions:

1. **Start a simple chat**:
   ```
   Hello! Can someone help me?
   ```
   The triage agent will greet you and introduce the available agents.

2. **Mention Sunny**:
   ```
   @Sunny How's your day going?
   ```
   Sunny will respond with an optimistic message.

3. **Ask Techie a question**:
   ```
   @Techie What's Blazor?
   ```
   Techie will explain with technical details.

4. **Request from Artsy**:
   ```
   @Artsy Show me something beautiful
   ```
   Artsy will share an artistic perspective and possibly an image.

5. **Ask Foodie for recommendations**:
   ```
   @Foodie What should I cook for dinner?
   ```
   Foodie will suggest delicious recipes.

## Understanding the Interface

### Left Sidebar
- **New Chat**: Creates a new conversation
- **Session List**: Your saved conversations (auto-saved)

### Main Chat Area
- **Available Agents**: Shows all 4 agents you can interact with
- **Message History**: Your conversation with agents
- **Input Box**: Type messages here

### @ Mentions
- Use `@AgentName` to talk to a specific agent
- Example: `@Sunny`, `@Techie`, `@Artsy`, `@Foodie`

## What's Happening Behind the Scenes

1. **Your message** is sent to the triage agent
2. **Triage agent** determines which specialist agent should respond
3. **Handoff** happens to the appropriate agent
4. **Specialist agent** generates a response
5. **Response streams** back to your browser in real-time
6. **Session is saved** automatically to LiteDB

## Next Steps

### Customize Agent Personalities

Edit `Services/AgentChatService.cs` to change agent personalities:

```csharp
new AgentProfile
{
    Id = "sunny",
    Name = "Sunny",
    Avatar = "☀️",
    Personality = "Your custom personality",
    SystemPrompt = "Your custom instructions..."
}
```

### Add More Agents

Simply add new `AgentProfile` entries in the `_agentProfiles` list. They'll automatically join the handoff workflow!

### Integrate Real Image Generation

Replace the placeholder in `Services/ImageGenerationTool.cs`:

```csharp
public async Task<string> GenerateImage(string prompt)
{
    // Add DALL-E integration here
    var response = await dalleClient.GenerateImageAsync(prompt);
    return response.ImageUrl;
}
```

## Common Issues

### "Azure OpenAI endpoint not configured"
- Check your `appsettings.json` or environment variables
- Ensure endpoint URL is correct

### "Authentication failed"
- Run `az login` again
- Check if your account has access to the Azure OpenAI resource

### "Deployment not found"
- Verify deployment name in Azure portal
- Ensure the deployment is active

### Agents not responding
- Check Azure OpenAI quota
- View logs: `dotnet run --verbosity detailed`

For more help, see [TROUBLESHOOTING.md](TROUBLESHOOTING.md)

## Development Tips

### Run in Development Mode

```bash
dotnet run --environment Development
```

Enables detailed error pages and logging.

### Watch for Changes

```bash
dotnet watch run
```

Automatically restarts when you modify code.

### Build for Production

```bash
dotnet publish -c Release -o ./publish
```

Creates optimized binaries in `./publish` folder.

### Enable Detailed Logging

In `appsettings.Development.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.Agents.AI": "Debug"
    }
  }
}
```

## Learning Resources

### Microsoft Agent Framework
- Official Repo: https://github.com/microsoft/agent-framework
- Workflow Patterns: https://github.com/microsoft/agent-framework/tree/main/dotnet/samples

### Azure OpenAI
- Documentation: https://learn.microsoft.com/azure/ai-services/openai/
- Quickstart: https://learn.microsoft.com/azure/ai-services/openai/quickstart

### Blazor
- Tutorial: https://learn.microsoft.com/aspnet/core/blazor/tutorials/build-a-blazor-app
- Best Practices: https://learn.microsoft.com/aspnet/core/blazor/

## Architecture Overview

```
User Input
    ↓
Blazor UI (Home.razor)
    ↓
AgentChatService
    ↓
Workflow (Handoff Pattern)
    ↓
Triage Agent → Routes to → Specialist Agents
    ↓
Response Stream
    ↓
Update UI + Save to LiteDB
```

For detailed architecture, see [ARCHITECTURE.md](ARCHITECTURE.md)

## Project Structure

```
src/AgentGroupChat/
├── Components/
│   └── Pages/
│       └── Home.razor          # Main UI
├── Models/
│   ├── AgentProfile.cs         # Agent configuration
│   ├── ChatMessage.cs          # Message model
│   └── ChatSession.cs          # Session model
├── Services/
│   ├── AgentChatService.cs     # Core logic (HANDOFF!)
│   ├── SessionService.cs       # LiteDB persistence
│   └── ImageGenerationTool.cs  # Image generation
└── Program.cs                  # App startup
```

## Key Files to Explore

1. **Services/AgentChatService.cs** - The heart of the application
   - Handoff workflow implementation
   - Agent definitions
   - Message processing

2. **Components/Pages/Home.razor** - The UI
   - Chat interface
   - Session management
   - Real-time updates

3. **Models/AgentProfile.cs** - Agent configuration
   - Personality definitions
   - System prompts

## Tips for Success

✅ **DO:**
- Start with simple questions to test agents
- Use @ mentions to direct questions
- Create new sessions for different topics
- Experiment with agent personalities

❌ **DON'T:**
- Send very long messages (may hit token limits)
- Expect instant responses (AI takes time)
- Share sensitive information (this is a demo app)

## Feedback and Contributions

Found a bug? Have a suggestion?
- Open an issue: https://github.com/GreenShadeZhang/agent-framework-tutorial-code-/issues
- Submit a PR: https://github.com/GreenShadeZhang/agent-framework-tutorial-code-/pulls

## License

MIT License - Feel free to use and modify!

---

**Happy chatting with your AI agents! 🤖✨**

If you have any questions, check the [TROUBLESHOOTING.md](TROUBLESHOOTING.md) or reach out!
