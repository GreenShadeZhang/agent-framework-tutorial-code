using WorkflowDesigner.Api.Models;
using WorkflowDesigner.Api.Repository;

namespace WorkflowDesigner.Api.Services;

/// <summary>
/// 智能体服务实现
/// </summary>
public class AgentService : IAgentService
{
    private readonly IRepository<AgentDefinition> _agentRepository;

    public AgentService(IRepository<AgentDefinition> agentRepository)
    {
        _agentRepository = agentRepository;
    }

    public async Task<IEnumerable<AgentDefinition>> GetAllAgentsAsync()
    {
        return await _agentRepository.GetAllAsync();
    }

    public async Task<AgentDefinition?> GetAgentByIdAsync(string id)
    {
        return await _agentRepository.GetByIdAsync(id);
    }

    public async Task<AgentDefinition> CreateAgentAsync(AgentDefinition agent)
    {
        agent.CreatedAt = DateTime.UtcNow;
        agent.UpdatedAt = DateTime.UtcNow;
        return await _agentRepository.AddAsync(agent);
    }

    public async Task<AgentDefinition?> UpdateAgentAsync(string id, AgentDefinition agent)
    {
        var existing = await _agentRepository.GetByIdAsync(id);
        if (existing == null)
        {
            return null;
        }

        agent.Id = id;
        agent.CreatedAt = existing.CreatedAt;
        agent.UpdatedAt = DateTime.UtcNow;
        return await _agentRepository.UpdateAsync(agent);
    }

    public async Task<bool> DeleteAgentAsync(string id)
    {
        return await _agentRepository.DeleteAsync(id);
    }
}
