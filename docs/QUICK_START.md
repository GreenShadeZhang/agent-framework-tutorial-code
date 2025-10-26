# Quick Start Guide - Dynamic Agent Loading

## Prerequisites
- .NET 9.0 SDK
- Azure OpenAI or OpenAI API key

## Setup

### 1. Configure API Keys

Edit `src/AgentGroupChat.AgentHost/appsettings.json`:

```json
{
  "DefaultModelProvider": "OpenAI",
  "OpenAI": {
    "BaseUrl": "",
    "ModelName": "gpt-4o-mini",
    "ApiKey": "your-api-key-here"
  }
}
```

Or set environment variables:
```bash
export OPENAI_API_KEY="your-api-key"
export OPENAI_MODEL_NAME="gpt-4o-mini"
```

### 2. Run the Application

Using .NET Aspire (recommended):
```bash
cd src
dotnet run --project AgentGroupChat.AppHost
```

Or run services individually:

Terminal 1 - Backend:
```bash
cd src/AgentGroupChat.AgentHost
dotnet run
```

Terminal 2 - Frontend:
```bash
cd src/AgentGroupChat.Web
dotnet run
```

### 3. Initialize Data

1. Open browser to `http://localhost:5000` (or the URL shown in terminal)
2. Navigate to **Admin** page
3. Click **"Initialize Default Data"** button
4. This will create:
   - 4 default agents (Sunny ☀️, Techie 🤖, Artsy 🎨, Foodie 🍜)
   - 1 default agent group with all 4 agents

### 4. Start Chatting

1. Navigate to **Chat** page (home)
2. Create a new chat session
3. Send a message - the triage agent will route to the appropriate specialist
4. Try different types of messages:
   - "I'm feeling happy today" → Routes to Sunny
   - "How do I code a React component?" → Routes to Techie
   - "Tell me about impressionism" → Routes to Artsy
   - "What's a good recipe for pasta?" → Routes to Foodie

## Managing Agents

### View Agents
- Go to **Admin** → **Agents** tab
- See all configured agents with their properties

### Create/Edit Agent
Currently via API. Example:

```bash
curl -X POST http://localhost:5000/api/admin/agents \
  -H "Content-Type: application/json" \
  -d '{
    "id": "helper",
    "name": "Helper",
    "avatar": "🤝",
    "personality": "Helpful and supportive",
    "systemPrompt": "You are a helpful assistant...",
    "description": "General helper",
    "enabled": true
  }'
```

### Delete Agent
- Click the delete icon (🗑️) next to an agent
- Confirm deletion

## Managing Agent Groups

### View Groups
- Go to **Admin** → **Groups** tab
- See all configured groups

### Create Custom Group
Example via API:

```bash
curl -X POST http://localhost:5000/api/admin/groups \
  -H "Content-Type: application/json" \
  -d '{
    "id": "tech-support",
    "name": "Tech Support Team",
    "description": "Technical support specialists",
    "agentIds": ["techie", "helper"],
    "enabled": true,
    "triageSystemPrompt": "Route technical questions to appropriate expert..."
  }'
```

## API Endpoints

### Agent Management
- `GET /api/admin/agents` - List all agents
- `GET /api/admin/agents/{id}` - Get specific agent
- `POST /api/admin/agents` - Create/update agent
- `DELETE /api/admin/agents/{id}` - Delete agent

### Group Management
- `GET /api/admin/groups` - List all groups
- `GET /api/admin/groups/{id}` - Get specific group
- `POST /api/admin/groups` - Create/update group
- `DELETE /api/admin/groups/{id}` - Delete group

### Initialization
- `POST /api/admin/initialize` - Initialize default data

### Chat
- `GET /api/sessions` - List sessions
- `POST /api/sessions` - Create session
- `POST /api/chat` - Send message (supports optional `groupId` parameter)

## Architecture

```
┌─────────────┐
│  Web UI     │
│  (Blazor)   │
└──────┬──────┘
       │ HTTP
       ▼
┌─────────────────────┐
│   AgentHost API     │
│  ┌──────────────┐   │
│  │WorkflowMgr   │   │
│  └──────┬───────┘   │
│         │           │
│  ┌──────▼───────┐   │
│  │ Repositories │   │
│  └──────┬───────┘   │
│         │           │
│  ┌──────▼───────┐   │
│  │   LiteDB     │   │
│  │  - agents    │   │
│  │  - groups    │   │
│  │  - sessions  │   │
│  │  - messages  │   │
│  └──────────────┘   │
└─────────────────────┘
```

## Workflow Execution

1. **User sends message** → AgentChatService
2. **Load workflow** → WorkflowManager gets/creates workflow for group
3. **Triage** → Triage agent analyzes message
4. **Handoff** → Routes to appropriate specialist agent
5. **Response** → Specialist agent responds
6. **Save** → Message and response saved to database
7. **Return** → Response sent back to user

## Troubleshooting

### "Agent not found" error
- Ensure you've initialized default data
- Check that agents are enabled in Admin panel

### "Group not found" error
- Ensure default group exists (initialize data)
- Check group configuration in Admin panel

### API connection errors
- Verify AgentHost service is running
- Check CORS settings if accessing from different origin
- Ensure correct base URL in Web app config

### Empty responses
- Check OpenAI API key is configured
- Verify API key has sufficient credits
- Check AgentHost logs for detailed errors

## Database Location

LiteDB database is stored at:
```
src/AgentGroupChat.AgentHost/Data/sessions.db
```

To reset all data, stop the application and delete this file.

## Next Steps

1. ✅ Initialize default data
2. ✅ Test basic chat functionality
3. Create custom agents for your use case
4. Create specialized agent groups
5. Customize triage agent prompts
6. Explore advanced features in documentation

For detailed implementation information, see [DYNAMIC_AGENT_LOADING.md](../docs/DYNAMIC_AGENT_LOADING.md)
