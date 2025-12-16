/**
 * 声明式工作流类型定义
 * 对齐 Agent Framework 官方支持的 Action 类型
 * @see https://github.com/microsoft/agent-framework/tree/main/dotnet/src/Microsoft.Agents.AI.Workflows.Declarative
 */

// ==================== 基础类型 ====================

export interface Position {
  x: number;
  y: number;
}

// ==================== 执行器类型 ====================

/**
 * 执行器类型枚举 - 仅包含 Agent Framework 官方支持的类型
 */
export type ExecutorType =
  // 智能体调用 (核心)
  | 'InvokeAzureAgent'
  // 流程控制
  | 'ConditionGroup'
  | 'GotoAction'
  | 'Foreach'
  | 'BreakLoop'
  | 'ContinueLoop'
  | 'EndWorkflow'
  | 'EndConversation'
  // 状态管理
  | 'SetVariable'
  | 'SetTextVariable'
  | 'SetMultipleVariables'
  | 'ParseValue'
  | 'ResetVariable'
  | 'ClearAllVariables'
  // 消息
  | 'SendActivity'
  | 'AddConversationMessage'
  | 'RetrieveConversationMessages'
  // 会话管理
  | 'CreateConversation'
  | 'DeleteConversation'
  | 'CopyConversationMessages'
  // 人工输入
  | 'Question';

/**
 * 执行器类型分组 - Agent Framework 官方支持
 */
export const ExecutorTypeGroups = {
  agents: [
    { type: 'InvokeAzureAgent', label: 'Azure智能体', icon: '🤖', description: '调用 Azure AI Foundry 智能体' },
  ],
  controlFlow: [
    { type: 'ConditionGroup', label: '条件分支', icon: '🔀', description: '多条件分支选择' },
    { type: 'GotoAction', label: '跳转', icon: '↪️', description: '跳转到指定节点' },
    { type: 'Foreach', label: '循环', icon: '🔄', description: '遍历集合执行' },
    { type: 'BreakLoop', label: '中断循环', icon: '⏹️', description: '跳出当前循环' },
    { type: 'ContinueLoop', label: '继续循环', icon: '⏭️', description: '跳过当前迭代' },
    { type: 'EndWorkflow', label: '结束工作流', icon: '🏁', description: '结束当前工作流' },
    { type: 'EndConversation', label: '结束会话', icon: '👋', description: '结束整个会话' },
  ],
  stateManagement: [
    { type: 'SetVariable', label: '设置变量', icon: '📝', description: '设置单个变量值' },
    { type: 'SetTextVariable', label: '设置文本变量', icon: '📄', description: '设置文本变量' },
    { type: 'SetMultipleVariables', label: '批量设置变量', icon: '📋', description: '同时设置多个变量' },
    { type: 'ParseValue', label: '解析值', icon: '🔍', description: '解析和转换数据' },
    { type: 'ResetVariable', label: '重置变量', icon: '🔄', description: '重置变量到默认值' },
    { type: 'ClearAllVariables', label: '清除变量', icon: '🗑️', description: '清除所有变量' },
  ],
  messages: [
    { type: 'SendActivity', label: '发送消息', icon: '💬', description: '发送消息给用户' },
    { type: 'AddConversationMessage', label: '添加对话消息', icon: '➕', description: '向对话添加消息' },
    { type: 'RetrieveConversationMessages', label: '获取对话消息', icon: '📥', description: '获取对话历史' },
  ],
  conversation: [
    { type: 'CreateConversation', label: '创建会话', icon: '🆕', description: '创建新的对话会话' },
    { type: 'DeleteConversation', label: '删除会话', icon: '❌', description: '删除对话会话' },
    { type: 'CopyConversationMessages', label: '复制对话', icon: '📋', description: '复制对话消息' },
  ],
  humanInput: [
    { type: 'Question', label: '问题询问', icon: '❔', description: '向用户提问并等待回复' },
  ],
} as const;

// ==================== 执行器配置 ====================

/**
 * 执行器定义
 */
export interface ExecutorDefinition {
  id: string;
  type: ExecutorType;
  name: string;
  description?: string;
  position: Position;
  config: ExecutorConfig;
}

/**
 * 执行器配置联合类型
 */
export type ExecutorConfig =
  | AgentExecutorConfig
  | ConditionConfig
  | ConditionGroupConfig
  | ForeachConfig
  | SetVariableConfig
  | SendActivityConfig
  | QuestionConfig
  | SubWorkflowConfig
  | ParallelConfig
  | GotoConfig
  | Record<string, unknown>;

/**
 * 智能体执行器配置
 */
export interface AgentExecutorConfig {
  agentDefinitionId?: string;
  name: string;
  description?: string;
  instructionsTemplate: string;
  modelConfig: ModelConfiguration;
  tools: ToolReference[];
  workbenches: WorkbenchConfig[];
  handoffs: HandoffConfig[];
  inputMappings: VariableMapping[];
  outputMappings: VariableMapping[];
  reflectOnToolUse: boolean;
  enableStreaming: boolean;
}

/**
 * 模型配置
 */
export interface ModelConfiguration {
  provider: ModelProvider;
  model: string;
  temperature: number;
  maxTokens?: number;
  endpoint?: string;
  deploymentName?: string;
}

export type ModelProvider = 'OpenAI' | 'AzureOpenAI' | 'Anthropic' | 'GoogleAI' | 'Ollama' | 'Custom';

/**
 * 工具引用
 */
export interface ToolReference {
  type: ToolType;
  name: string;
  config: Record<string, unknown>;
}

export type ToolType = 'Function' | 'Mcp' | 'OpenApi' | 'CodeInterpreter' | 'FileSearch' | 'WebSearch' | 'Custom';

/**
 * 工作台配置
 */
export interface WorkbenchConfig {
  type: 'Static' | 'Mcp';
  tools: ToolReference[];
  mcpServerParams?: McpServerParams;
}

/**
 * MCP服务器参数
 */
export interface McpServerParams {
  type: 'Stdio' | 'Sse' | 'StreamableHttp';
  command?: string;
  args?: string[];
  url?: string;
  envVars?: Record<string, string>;
}

/**
 * 交接配置
 */
export interface HandoffConfig {
  targetAgentId: string;
  condition?: string;
  messageTemplate?: string;
}

/**
 * 变量映射
 */
export interface VariableMapping {
  source: string;
  target: string;
}

/**
 * 条件配置
 */
export interface ConditionConfig {
  expression: string;
  trueBranchTarget?: string;
  falseBranchTarget?: string;
}

/**
 * 条件组配置
 */
export interface ConditionGroupConfig {
  conditions: ConditionItem[];
  defaultTarget?: string;
}

export interface ConditionItem {
  expression: string;
  targetExecutorId: string;
}

/**
 * 循环配置
 */
export interface ForeachConfig {
  itemsExpression: string;
  itemVariableName: string;
  indexVariableName: string;
  bodyStartExecutorId?: string;
}

/**
 * 设置变量配置
 */
export interface SetVariableConfig {
  variableName: string;
  value: string;
  valueType?: 'string' | 'number' | 'boolean' | 'object' | 'array';
}

/**
 * 发送消息配置
 */
export interface SendActivityConfig {
  message: string;
  messageType?: 'text' | 'markdown' | 'card';
}

/**
 * 问题配置
 */
export interface QuestionConfig {
  prompt: string;
  resultVariable: string;
  validationExpression?: string;
  timeout?: number;
}

/**
 * 子工作流配置
 */
export interface SubWorkflowConfig {
  workflowId: string;
  inputMappings: VariableMapping[];
  outputMappings: VariableMapping[];
}

/**
 * 并行配置
 */
export interface ParallelConfig {
  targets: string[];
  waitForAll: boolean;
}

/**
 * 跳转配置
 */
export interface GotoConfig {
  targetExecutorId: string;
}

// ==================== 边定义 ====================

/**
 * 边组类型
 */
export type EdgeGroupType = 'Single' | 'FanOut' | 'FanIn' | 'SwitchCase';

/**
 * 边组定义
 */
export interface EdgeGroupDefinition {
  id: string;
  type: EdgeGroupType;
  sourceExecutorId: string;
  edges: EdgeDefinition[];
}

/**
 * 边定义
 */
export interface EdgeDefinition {
  id: string;
  targetExecutorId: string;
  condition?: string;
  label?: string;
}

// ==================== 输入输出规范 ====================

/**
 * 输入规范
 */
export interface InputSpecification {
  typeName: string;
  schema: JsonSchemaDefinition;
}

/**
 * 输出规范
 */
export interface OutputSpecification {
  typeName: string;
  schema: JsonSchemaDefinition;
}

/**
 * JSON Schema 定义
 */
export interface JsonSchemaDefinition {
  type: 'string' | 'number' | 'integer' | 'boolean' | 'array' | 'object';
  description?: string;
  properties?: Record<string, PropertySchema>;
  required?: string[];
}

/**
 * 属性 Schema
 */
export interface PropertySchema {
  type: 'string' | 'number' | 'integer' | 'boolean' | 'array' | 'object';
  description?: string;
  default?: unknown;
  enum?: string[];
  format?: string;
  items?: PropertySchema;
  nestedProperties?: Record<string, PropertySchema>;
}

// ==================== 变量定义 ====================

/**
 * 变量定义
 */
export interface VariableDefinition {
  name: string;
  type: 'string' | 'number' | 'boolean' | 'object' | 'array';
  description?: string;
  defaultValue?: unknown;
  scope: VariableScope;
}

export type VariableScope = 'Workflow' | 'Conversation' | 'Global';

// ==================== 工作流定义 ====================

/**
 * 声明式工作流定义
 */
export interface DeclarativeWorkflowDefinition {
  id: string;
  name: string;
  description?: string;
  version: string;
  startExecutorId: string;
  maxIterations: number;
  executors: ExecutorDefinition[];
  edgeGroups: EdgeGroupDefinition[];
  inputSpec: InputSpecification;
  outputSpec: OutputSpecification;
  variables: VariableDefinition[];
  metadata: Record<string, unknown>;
  createdAt: string;
  updatedAt: string;
}

// ==================== React Flow 节点数据 ====================

/**
 * 工作流节点数据 (用于React Flow)
 */
export interface WorkflowNodeData {
  executor: ExecutorDefinition;
  isSelected?: boolean;
  isRunning?: boolean;
  hasError?: boolean;
  output?: unknown;
  onEdit?: () => void;
  onDelete?: () => void;
}

/**
 * 工作流边数据 (用于React Flow)
 */
export interface WorkflowEdgeData {
  edgeDefinition: EdgeDefinition;
  edgeGroup: EdgeGroupDefinition;
  isAnimated?: boolean;
}

// ==================== 执行状态 ====================

/**
 * 执行器状态
 */
export type ExecutorState = 'pending' | 'running' | 'completed' | 'failed' | 'skipped';

/**
 * 执行事件
 */
export interface ExecutionEvent {
  type: 'start' | 'node_start' | 'node_complete' | 'node_error' | 'output' | 'end' | 'error';
  executorId?: string;
  message?: string;
  data?: unknown;
  timestamp: string;
}

// ==================== 工具函数 ====================

/**
 * 获取执行器类型的图标
 */
export function getExecutorIcon(type: ExecutorType): string {
  for (const group of Object.values(ExecutorTypeGroups)) {
    const found = group.find(item => item.type === type);
    if (found) return found.icon;
  }
  return '📦';
}

/**
 * 获取执行器类型的标签
 */
export function getExecutorLabel(type: ExecutorType): string {
  for (const group of Object.values(ExecutorTypeGroups)) {
    const found = group.find(item => item.type === type);
    if (found) return found.label;
  }
  return type;
}

/**
 * 获取执行器类型的描述
 */
export function getExecutorDescription(type: ExecutorType): string {
  for (const group of Object.values(ExecutorTypeGroups)) {
    const found = group.find(item => item.type === type);
    if (found) return found.description;
  }
  return '';
}

/**
 * 判断执行器是否为智能体类型
 */
export function isAgentExecutor(type: ExecutorType): boolean {
  return type === 'InvokeAzureAgent';
}

/**
 * 判断执行器是否为控制流类型
 */
export function isControlFlowExecutor(type: ExecutorType): boolean {
  return ['ConditionGroup', 'GotoAction', 'Foreach', 'BreakLoop', 'ContinueLoop', 'EndWorkflow', 'EndConversation'].includes(type);
}

/**
 * 创建默认执行器配置
 */
export function createDefaultExecutorConfig(type: ExecutorType): ExecutorConfig {
  switch (type) {
    case 'InvokeAzureAgent':
      return {
        name: '',
        description: '',
        instructionsTemplate: '',
        modelConfig: {
          provider: 'AzureOpenAI',
          model: 'gpt-4o',
          temperature: 0.7,
        },
        tools: [],
        workbenches: [],
        handoffs: [],
        inputMappings: [],
        outputMappings: [],
        reflectOnToolUse: false,
        enableStreaming: true,
      } as AgentExecutorConfig;

    case 'ConditionGroup':
      return {
        conditions: [],
        defaultTarget: undefined,
      } as ConditionGroupConfig;

    case 'Foreach':
      return {
        itemsExpression: '[]',
        itemVariableName: 'item',
        indexVariableName: 'index',
      } as ForeachConfig;

    case 'SetVariable':
    case 'SetTextVariable':
    case 'SetMultipleVariables':
      return {
        variableName: '',
        value: '',
        valueType: 'string',
      } as SetVariableConfig;

    case 'SendActivity':
      return {
        message: '',
        messageType: 'text',
      } as SendActivityConfig;

    case 'Question':
      return {
        prompt: '',
        resultVariable: 'user_response',
      } as QuestionConfig;

    case 'GotoAction':
      return {
        targetExecutorId: '',
      } as GotoConfig;

    case 'CreateConversation':
      return {
        conversationId: '',
      };

    case 'EndWorkflow':
    case 'EndConversation':
    case 'BreakLoop':
    case 'ContinueLoop':
    case 'DeleteConversation':
    case 'ClearAllVariables':
    case 'ResetVariable':
    case 'ParseValue':
    case 'AddConversationMessage':
    case 'RetrieveConversationMessages':
    case 'CopyConversationMessages':
      return {};

    default:
      return {};
  }
}

/**
 * 创建新的执行器定义
 */
export function createExecutorDefinition(
  type: ExecutorType,
  position: Position,
  name?: string
): ExecutorDefinition {
  return {
    id: crypto.randomUUID(),
    type,
    name: name || getExecutorLabel(type),
    description: getExecutorDescription(type),
    position,
    config: createDefaultExecutorConfig(type),
  };
}
