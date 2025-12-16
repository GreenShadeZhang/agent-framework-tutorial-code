using WorkflowDesigner.Api.Models;

namespace WorkflowDesigner.Api.Services;

/// <summary>
/// 智能体服务接口
/// </summary>
public interface IAgentService
{
    /// <summary>
    /// 获取所有智能体
    /// </summary>
    Task<IEnumerable<AgentDefinition>> GetAllAgentsAsync();

    /// <summary>
    /// 根据ID获取智能体
    /// </summary>
    Task<AgentDefinition?> GetAgentByIdAsync(string id);

    /// <summary>
    /// 创建智能体
    /// </summary>
    Task<AgentDefinition> CreateAgentAsync(AgentDefinition agent);

    /// <summary>
    /// 更新智能体
    /// </summary>
    Task<AgentDefinition?> UpdateAgentAsync(string id, AgentDefinition agent);

    /// <summary>
    /// 删除智能体
    /// </summary>
    Task<bool> DeleteAgentAsync(string id);
}
