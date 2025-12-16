using WorkflowDesigner.Api.Models;

namespace WorkflowDesigner.Api.Services;

/// <summary>
/// 声明式工作流服务接口 - 对齐 Agent Framework 官方格式
/// </summary>
public interface IDeclarativeWorkflowService
{
    /// <summary>
    /// 获取所有声明式工作流
    /// </summary>
    Task<IEnumerable<DeclarativeWorkflowDefinition>> GetAllAsync();

    /// <summary>
    /// 根据ID获取声明式工作流
    /// </summary>
    Task<DeclarativeWorkflowDefinition?> GetByIdAsync(string id);

    /// <summary>
    /// 创建声明式工作流
    /// </summary>
    Task<DeclarativeWorkflowDefinition> CreateAsync(DeclarativeWorkflowDefinition workflow);

    /// <summary>
    /// 更新声明式工作流
    /// </summary>
    Task<DeclarativeWorkflowDefinition?> UpdateAsync(string id, DeclarativeWorkflowDefinition workflow);

    /// <summary>
    /// 删除声明式工作流
    /// </summary>
    Task<bool> DeleteAsync(string id);

    /// <summary>
    /// 从 YAML 导入工作流
    /// </summary>
    Task<DeclarativeWorkflowDefinition> ImportFromYamlAsync(string yaml);

    /// <summary>
    /// 导出为 Agent Framework YAML 格式
    /// </summary>
    Task<string> ExportToYamlAsync(string id);

    /// <summary>
    /// 执行声明式工作流
    /// </summary>
    Task<DeclarativeExecutionResult> ExecuteAsync(string id, string userInput);

    /// <summary>
    /// 执行声明式工作流（流式）
    /// </summary>
    IAsyncEnumerable<ExecutionEvent> ExecuteStreamAsync(
        string id,
        string userInput,
        CancellationToken cancellationToken = default);
}
