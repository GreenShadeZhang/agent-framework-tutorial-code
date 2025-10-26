# Dynamic Agent Loading - Implementation Summary

## Overview
This implementation refactors the Agent Group Chat project to support dynamic agent loading and management, inspired by [BotSharp](https://github.com/SciSharp/BotSharp)'s SaaS architecture - an enterprise-grade AI multi-agent framework in .NET that features modular design, dynamic agent loading, and multi-tenant capabilities.

## Key Features Implemented

### 1. Database-Driven Agent Management
- **PersistedAgentProfile**: Model for storing agent configurations in LiteDB
- **AgentRepository**: Service for CRUD operations on agents
- Dynamic loading of agent configurations from database

### 2. Agent Group Orchestration
- **AgentGroup**: Model for defining groups of collaborating agents
- **AgentGroupRepository**: Service for managing agent groups
- Each group can have custom Triage agent instructions

### 3. Dynamic Workflow Management
- **WorkflowManager**: Creates and caches workflows per agent group
- Each group gets its own Triage agent and workflow
- Workflows are cached for performance
- Support for multiple concurrent group-based workflows

### 4. Refactored AgentChatService
- Removed hardcoded agent profiles
- Agents now loaded dynamically from database via repositories
- Support for group-based chat routing
- Backward compatible with default group

### 5. REST API Endpoints

#### Agent Management
- `GET /api/admin/agents` - Get all agents
- `GET /api/admin/agents/{id}` - Get agent by ID
- `POST /api/admin/agents` - Create/update agent
- `DELETE /api/admin/agents/{id}` - Delete agent

#### Group Management
- `GET /api/admin/groups` - Get all groups
- `GET /api/admin/groups/{id}` - Get group by ID
- `POST /api/admin/groups` - Create/update group
- `DELETE /api/admin/groups/{id}` - Delete group

#### Initialization
- `POST /api/admin/initialize` - Initialize default agents and groups

### 6. Web Administration UI
- Admin page at `/admin` with two tabs:
  - **Agents Tab**: View, create, edit, delete agents
  - **Groups Tab**: View, create, edit, delete agent groups
- "Initialize Default Data" button to seed database
- Navigation link added to main layout

## Architecture Highlights

### Database Layer (LiteDB)
```
Collections:
- agents: PersistedAgentProfile[]
- agent_groups: AgentGroup[]
- sessions: PersistedChatSession[]
- messages: PersistedChatMessage[]
```

### Service Layer
```
AgentRepository
  ├── GetAllEnabled()
  ├── GetById(id)
  ├── Upsert(agent)
  ├── Delete(id)
  └── InitializeDefaultAgents()

AgentGroupRepository
  ├── GetAllEnabled()
  ├── GetById(id)
  ├── Upsert(group)
  ├── Delete(id)
  └── InitializeDefaultGroup()

WorkflowManager
  ├── GetOrCreateWorkflow(groupId)
  ├── ClearWorkflowCache(groupId)
  └── ClearAllWorkflowCache()
```

### Workflow Creation Flow
1. Client sends message with optional `groupId`
2. `AgentChatService` calls `WorkflowManager.GetOrCreateWorkflow(groupId)`
3. `WorkflowManager` checks cache, returns existing or creates new workflow:
   - Loads group configuration from `AgentGroupRepository`
   - Loads agent profiles from `AgentRepository`
   - Creates Triage agent with custom or default instructions
   - Creates Specialist agents from profiles
   - Builds handoff workflow using `AgentWorkflowBuilder`
   - Caches workflow for reuse
4. Workflow executes with triage routing to specialists
5. Results returned to client

## Comparison with BotSharp

### Similarities
- Dynamic agent loading from database
- Agent grouping and orchestration
- Plugin/modular architecture
- SaaS-ready with multi-tenant potential
- RESTful API for management

### Differences
- Simpler architecture focused on handoff workflows
- Uses Microsoft Agent Framework instead of custom framework
- LiteDB instead of complex database setup
- Minimal implementation for core functionality

## Usage Examples

### Initialize Default Data (via Admin UI)
1. Navigate to `/admin`
2. Click "Initialize Default Data"
3. Creates 4 default agents (Sunny, Techie, Artsy, Foodie) and default group

### Create Custom Agent (via API)
```json
POST /api/admin/agents
{
  "id": "doctor",
  "name": "Dr. Health",
  "avatar": "🏥",
  "personality": "Professional and caring medical expert",
  "systemPrompt": "You are Dr. Health, a knowledgeable medical professional...",
  "description": "Medical health advisor",
  "enabled": true
}
```

### Create Custom Group (via API)
```json
POST /api/admin/groups
{
  "id": "medical-team",
  "name": "Medical Team",
  "description": "Specialized medical consultation team",
  "agentIds": ["doctor", "nurse", "pharmacist"],
  "enabled": true,
  "triageSystemPrompt": "Route medical questions to appropriate specialist..."
}
```

### Send Message to Specific Group
```json
POST /api/chat
{
  "sessionId": "session-123",
  "message": "I need medical advice",
  "groupId": "medical-team"
}
```

## Benefits

1. **Flexibility**: Agents can be modified without code changes
2. **Scalability**: Support for unlimited agent configurations and groups
3. **Multi-tenancy Ready**: Each group can represent different use cases
4. **Maintainability**: Separation of agent data from application logic
5. **Extensibility**: Easy to add new agents and groups via UI or API

## Migration from Previous Version

The refactored version is backward compatible:
- Existing chat sessions continue to work
- Default group "default" is created automatically
- If no group specified, uses default group
- All existing API endpoints remain functional

## Future Enhancements

Potential improvements:
1. Dialog forms for creating/editing agents and groups in Web UI
2. Agent template library
3. Import/export agent configurations
4. Agent performance analytics
5. Multi-language support
6. Tool/function management per agent
7. Permission/role-based access control
8. Audit logging for configuration changes
