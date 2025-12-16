using WorkflowDesigner.Api.Models;

namespace WorkflowDesigner.Api.Services;

/// <summary>
/// 工作流服务接口
/// </summary>
public interface IWorkflowService
{
    /// <summary>
    /// 获取所有工作流
    /// </summary>
    Task<IEnumerable<WorkflowDefinition>> GetAllWorkflowsAsync();

    /// <summary>
    /// 根据ID获取工作流
    /// </summary>
    Task<WorkflowDefinition?> GetWorkflowByIdAsync(string id);

    /// <summary>
    /// 创建工作流
    /// </summary>
    Task<WorkflowDefinition> CreateWorkflowAsync(WorkflowDefinition workflow);

    /// <summary>
    /// 更新工作流
    /// </summary>
    Task<WorkflowDefinition?> UpdateWorkflowAsync(string id, WorkflowDefinition workflow);

    /// <summary>
    /// 删除工作流
    /// </summary>
    Task<bool> DeleteWorkflowAsync(string id);

    /// <summary>
    /// 执行工作流
    /// </summary>
    Task<ExecutionLog> ExecuteWorkflowAsync(string id, Dictionary<string, object> parameters);

    /// <summary>
    /// 执行工作流并流式返回事件 (SSE)
    /// </summary>
    IAsyncEnumerable<ExecutionEvent> ExecuteWorkflowStreamAsync(
        string id,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用 Agent Framework 执行工作流并流式返回事件
    /// </summary>
    IAsyncEnumerable<ExecutionEvent> ExecuteWorkflowWithFrameworkAsync(
        string workflowId,
        string userInput,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 渲染 Prompt 模板
    /// </summary>
    Task<string> RenderPromptTemplateAsync(
        WorkflowDefinition workflow,
        Dictionary<string, object> parameters);

    /// <summary>
    /// 导出为 YAML
    /// </summary>
    Task<string> ExportToYamlAsync(WorkflowDefinition workflow);

    /// <summary>
    /// 从 YAML 导入
    /// </summary>
    Task<WorkflowDefinition> ImportFromYamlAsync(string yaml, string name);

    /// <summary>
    /// 转换为 Agent Framework YAML 格式
    /// </summary>
    Task<string> ConvertToAgentFrameworkYamlAsync(string workflowId);
}
