# Agent Group Chat - Architecture

## System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Blazor Server UI                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │ Session List │  │ Chat Messages│  │ Input Box    │      │
│  │ Sidebar      │  │ Display      │  │ + @ Mention  │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                    Service Layer                             │
│  ┌────────────────────────────────────────────────────────┐ │
│  │         AgentChatService (Core Logic)                  │ │
│  │  ┌──────────────────────────────────────────────────┐ │ │
│  │  │        Handoff Workflow (AgentWorkflowBuilder)   │ │ │
│  │  │                                                  │ │ │
│  │  │  ┌─────────┐      ┌──────────┐                 │ │ │
│  │  │  │ Triage  │◄────►│ Sunny    │                 │ │ │
│  │  │  │ Agent   │      │ Agent    │                 │ │ │
│  │  │  └─────────┘      └──────────┘                 │ │ │
│  │  │       ▲                                         │ │ │
│  │  │       │           ┌──────────┐                 │ │ │
│  │  │       ├──────────►│ Techie   │                 │ │ │
│  │  │       │           │ Agent    │                 │ │ │
│  │  │       │           └──────────┘                 │ │ │
│  │  │       │                                         │ │ │
│  │  │       │           ┌──────────┐                 │ │ │
│  │  │       ├──────────►│ Artsy    │                 │ │ │
│  │  │       │           │ Agent    │                 │ │ │
│  │  │       │           └──────────┘                 │ │ │
│  │  │       │                                         │ │ │
│  │  │       │           ┌──────────┐                 │ │ │
│  │  │       └──────────►│ Foodie   │                 │ │ │
│  │  │                   │ Agent    │                 │ │ │
│  │  │                   └──────────┘                 │ │ │
│  │  └──────────────────────────────────────────────────┘ │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                               │
│  ┌────────────────────┐       ┌────────────────────┐        │
│  │ SessionService     │       │ ImageGenerationTool│        │
│  │ (LiteDB)          │       │ (Placeholder API)  │        │
│  └────────────────────┘       └────────────────────┘        │
└─────────────────────────────────────────────────────────────┘
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                    External Services                         │
│  ┌──────────────────┐            ┌──────────────────┐       │
│  │  Azure OpenAI    │            │   LiteDB Store   │       │
│  │  (GPT-4o-mini)   │            │   (sessions.db)  │       │
│  └──────────────────┘            └──────────────────┘       │
└─────────────────────────────────────────────────────────────┘
```

## Key Components

### 1. UI Layer (Blazor Server)

**Components/Pages/Home.razor**
- Main chat interface
- Session management sidebar
- Message display with agent avatars
- Input box with @ mention support
- Real-time updates using SignalR

### 2. Service Layer

#### AgentChatService
The core service that orchestrates multi-agent interactions:

- **Handoff Workflow**: Uses `AgentWorkflowBuilder` to create a handoff pattern
- **Triage Agent**: Routes messages to appropriate specialist agents
- **Specialist Agents**: Four personality-based agents (Sunny, Techie, Artsy, Foodie)
- **Message Processing**: Handles streaming responses from agents
- **Image Generation**: Triggers image generation based on context

#### SessionService
Manages chat session persistence:

- **LiteDB Integration**: Lightweight document database
- **CRUD Operations**: Create, read, update, delete sessions
- **Auto-save**: Automatically persists messages

#### ImageGenerationTool
Provides image generation capabilities:

- Currently uses placeholder images
- Can be extended to integrate with:
  - DALL-E (OpenAI)
  - Stable Diffusion
  - Azure Computer Vision

### 3. Data Models

#### AgentProfile
```csharp
- Id: Unique identifier
- Name: Display name
- Avatar: Emoji or image
- Personality: Description
- SystemPrompt: AI instructions
- Description: Agent role
```

#### ChatMessage
```csharp
- Id: Message identifier
- AgentId: Sender agent ID
- AgentName: Sender name
- AgentAvatar: Sender avatar
- Content: Message text
- ImageUrl: Optional image
- Timestamp: Message time
- IsUser: User vs Agent flag
```

#### ChatSession
```csharp
- Id: Session identifier
- Name: Session name
- Messages: List of messages
- CreatedAt: Creation time
- LastUpdated: Update time
```

## Message Flow

### User Sends Message

```
1. User types message with @ mention (e.g., "@Sunny Hello!")
   ▼
2. Home.razor captures input
   ▼
3. SendMessage() called
   ▼
4. AgentChatService.SendMessageAsync()
   ▼
5. Workflow processes message:
   - Triage agent identifies target agent
   - Handoff to specialist agent
   - Agent generates response
   ▼
6. Response streamed back:
   - Text content
   - Optional image generation
   ▼
7. Messages added to session
   ▼
8. SessionService persists changes
   ▼
9. UI updates with new messages
```

## Handoff Pattern Implementation

The handoff pattern allows seamless agent-to-agent routing:

```csharp
// 1. Create agents
var triageAgent = new ChatClientAgent(client, triagePrompt, "triage", "Router");
var sunnyAgent = new ChatClientAgent(client, sunnyPrompt, "sunny", "Cheerful");
// ... more agents

// 2. Build workflow
var builder = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent);

// 3. Configure handoffs
builder.WithHandoffs(triageAgent, [sunnyAgent, techieAgent, artsyAgent, foodieAgent]);
builder.WithHandoffs([sunnyAgent, techieAgent, artsyAgent, foodieAgent], triageAgent);

// 4. Create workflow
var workflow = builder.Build();

// 5. Execute
await InProcessExecution.StreamAsync(workflow, messages);
```

## Agent Personalities

### ☀️ Sunny (Optimistic)
- **Role**: Spreads positivity and encouragement
- **Style**: Warm, friendly, uplifting
- **Topics**: Daily life, motivation, happiness

### 🤖 Techie (Tech-savvy)
- **Role**: Technical expert and explainer
- **Style**: Analytical, precise, informative
- **Topics**: Technology, coding, gadgets

### 🎨 Artsy (Creative)
- **Role**: Artist and aesthetic appreciator
- **Style**: Expressive, colorful, imaginative
- **Topics**: Art, design, beauty

### 🍜 Foodie (Food enthusiast)
- **Role**: Culinary expert and food lover
- **Style**: Enthusiastic, descriptive, appetizing
- **Topics**: Food, cooking, recipes

## Configuration

### Required Settings

**appsettings.json**:
```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "DeploymentName": "gpt-4o-mini"
  }
}
```

**Environment Variables**:
```bash
AZURE_OPENAI_ENDPOINT
AZURE_OPENAI_DEPLOYMENT_NAME
```

### Authentication

Uses `DefaultAzureCredential` chain:
1. Environment variables
2. Managed Identity
3. Visual Studio
4. Azure CLI
5. Azure PowerShell

## Storage

### LiteDB
- **Location**: `Data/sessions.db`
- **Collections**:
  - `sessions`: Chat sessions
- **Features**:
  - Embedded database
  - No server required
  - ACID transactions
  - LINQ queries

## Extensibility Points

### Adding New Agents

1. Define `AgentProfile` in `AgentChatService`
2. Agent automatically registered in workflow
3. No additional code needed

### Custom Image Generation

Replace `ImageGenerationTool.GenerateImage()`:
```csharp
public async Task<string> GenerateImage(string prompt)
{
    // Integrate with DALL-E, Stable Diffusion, etc.
    var response = await imageService.GenerateAsync(prompt);
    return response.Url;
}
```

### Alternative Storage

Implement `ISessionService`:
```csharp
public interface ISessionService
{
    List<ChatSession> GetAllSessions();
    ChatSession? GetSession(string id);
    ChatSession CreateSession(string? name = null);
    void UpdateSession(ChatSession session);
    void DeleteSession(string id);
}
```

## Performance Considerations

1. **Streaming Responses**: Uses `StreamingRun` for real-time updates
2. **SignalR**: Blazor Server's built-in real-time communication
3. **LiteDB**: Fast embedded database for local storage
4. **Agent Caching**: IChatClient instances reused across requests

## Security Considerations

1. **Credential Management**: Uses Azure Identity, no hardcoded secrets
2. **Input Validation**: Sanitize user input before processing
3. **Rate Limiting**: Consider implementing for production use
4. **CORS**: Configure appropriately for deployment
5. **HTTPS**: Always use in production

## Deployment Options

1. **Azure App Service**: Blazor Server on Azure
2. **Docker**: Containerized deployment
3. **IIS**: Traditional Windows hosting
4. **Linux**: Cross-platform with Kestrel

## Future Enhancements

- [ ] Real DALL-E integration
- [ ] Voice input/output
- [ ] Multi-modal agents (vision + text)
- [ ] Agent memory across sessions
- [ ] Custom agent creation UI
- [ ] Export/import conversations
- [ ] Agent collaboration (multiple agents responding together)
- [ ] Emoji reactions
- [ ] File uploads/downloads
