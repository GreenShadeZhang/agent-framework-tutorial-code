namespace WorkflowDesigner.Api.Models;

/// <summary>
/// 智能体定义
/// </summary>
public class AgentDefinition
{
    /// <summary>
    /// 智能体ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 智能体名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 提示词模板 (支持Scriban语法)
    /// </summary>
    public string InstructionsTemplate { get; set; } = string.Empty;

    /// <summary>
    /// 智能体类型
    /// </summary>
    public AgentType Type { get; set; } = AgentType.Assistant;

    /// <summary>
    /// 模型配置
    /// </summary>
    public ModelConfig ModelConfig { get; set; } = new();

    /// <summary>
    /// 工具配置列表
    /// </summary>
    public List<ToolConfig> Tools { get; set; } = new();

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 智能体类型
/// </summary>
public enum AgentType
{
    /// <summary>
    /// 助手型智能体
    /// </summary>
    Assistant,

    /// <summary>
    /// 网页浏览智能体
    /// </summary>
    WebSurfer,

    /// <summary>
    /// 代码生成智能体
    /// </summary>
    Coder,

    /// <summary>
    /// 自定义智能体
    /// </summary>
    Custom
}

/// <summary>
/// 模型配置
/// </summary>
public class ModelConfig
{
    /// <summary>
    /// 模型名称
    /// </summary>
    public string Model { get; set; } = "gpt-4";

    /// <summary>
    /// 温度参数
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// 最大输出Token数
    /// </summary>
    public int MaxTokens { get; set; } = 2000;

    /// <summary>
    /// Top P 采样参数
    /// </summary>
    public double TopP { get; set; } = 1.0;
}

/// <summary>
/// 工具配置
/// </summary>
public class ToolConfig
{
    /// <summary>
    /// 工具名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 工具类型
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 工具参数
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
}
