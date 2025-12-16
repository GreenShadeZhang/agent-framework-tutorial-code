/**
 * 执行器配置模态框
 * 提供各类执行器的详细配置界面
 */

import React, { useState, useCallback, useMemo, useEffect } from 'react';
import type { 
  ExecutorDefinition,
  AgentExecutorConfig,
  ConditionGroupConfig,
  ForeachConfig,
  SendActivityConfig,
  QuestionConfig,
  ModelProvider,
} from '../../../types/workflow';
import {
  getExecutorIcon,
  getExecutorLabel,
  isAgentExecutor,
} from '../../../types/workflow';
import { useWorkflowStore } from '../../../store/workflowStore';

// ==================== 类型定义 ====================

interface ExecutorConfigModalProps {
  isOpen: boolean;
  executorId: string | null;
  onClose: () => void;
}

// ==================== 配置表单组件 ====================

/**
 * 智能体配置表单
 */
interface AgentConfigFormProps {
  config: AgentExecutorConfig;
  onChange: (config: AgentExecutorConfig) => void;
}

const AgentConfigForm: React.FC<AgentConfigFormProps> = ({ config, onChange }) => {
  const modelProviders: ModelProvider[] = ['OpenAI', 'AzureOpenAI', 'Anthropic', 'GoogleAI', 'Ollama', 'Custom'];
  
  const handleChange = <K extends keyof AgentExecutorConfig>(
    key: K,
    value: AgentExecutorConfig[K]
  ) => {
    onChange({ ...config, [key]: value });
  };

  const handleModelChange = <K extends keyof AgentExecutorConfig['modelConfig']>(
    key: K,
    value: AgentExecutorConfig['modelConfig'][K]
  ) => {
    onChange({
      ...config,
      modelConfig: { ...config.modelConfig, [key]: value },
    });
  };

  return (
    <div className="space-y-6">
      {/* 基本信息 */}
      <section>
        <h3 className="text-lg font-semibold text-gray-800 mb-4 flex items-center gap-2">
          <span>📝</span> 基本信息
        </h3>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">名称</label>
            <input
              type="text"
              value={config.name}
              onChange={(e) => handleChange('name', e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="智能体名称"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">描述</label>
            <input
              type="text"
              value={config.description || ''}
              onChange={(e) => handleChange('description', e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="可选描述"
            />
          </div>
        </div>
      </section>

      {/* 指令模板 */}
      <section>
        <h3 className="text-lg font-semibold text-gray-800 mb-4 flex items-center gap-2">
          <span>📜</span> 指令模板
        </h3>
        <textarea
          value={config.instructionsTemplate}
          onChange={(e) => handleChange('instructionsTemplate', e.target.value)}
          rows={6}
          className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 font-mono text-sm"
          placeholder="你是一个有帮助的AI助手...&#10;&#10;可以使用 {{ variable }} 语法引用变量"
        />
        <p className="text-xs text-gray-500 mt-1">
          支持 Scriban 模板语法，使用 {'{{ variable }}'} 引用变量
        </p>
      </section>

      {/* 模型配置 */}
      <section>
        <h3 className="text-lg font-semibold text-gray-800 mb-4 flex items-center gap-2">
          <span>🧠</span> 模型配置
        </h3>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">提供商</label>
            <select
              value={config.modelConfig.provider}
              onChange={(e) => handleModelChange('provider', e.target.value as ModelProvider)}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 bg-white"
            >
              {modelProviders.map((p) => (
                <option key={p} value={p}>{p}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">模型</label>
            <input
              type="text"
              value={config.modelConfig.model}
              onChange={(e) => handleModelChange('model', e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="gpt-4o"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              温度: {config.modelConfig.temperature}
            </label>
            <input
              type="range"
              min={0}
              max={2}
              step={0.1}
              value={config.modelConfig.temperature}
              onChange={(e) => handleModelChange('temperature', parseFloat(e.target.value))}
              className="w-full"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">最大Token</label>
            <input
              type="number"
              value={config.modelConfig.maxTokens || ''}
              onChange={(e) => handleModelChange('maxTokens', parseInt(e.target.value) || undefined)}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="可选"
            />
          </div>
        </div>

        {config.modelConfig.provider === 'AzureOpenAI' && (
          <div className="grid grid-cols-2 gap-4 mt-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">端点</label>
              <input
                type="url"
                value={config.modelConfig.endpoint || ''}
                onChange={(e) => handleModelChange('endpoint', e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                placeholder="https://xxx.openai.azure.com/"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">部署名称</label>
              <input
                type="text"
                value={config.modelConfig.deploymentName || ''}
                onChange={(e) => handleModelChange('deploymentName', e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                placeholder="gpt-4o-deployment"
              />
            </div>
          </div>
        )}
      </section>

      {/* 选项 */}
      <section>
        <h3 className="text-lg font-semibold text-gray-800 mb-4 flex items-center gap-2">
          <span>⚙️</span> 选项
        </h3>
        <div className="space-y-3">
          <label className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={config.enableStreaming}
              onChange={(e) => handleChange('enableStreaming', e.target.checked)}
              className="w-4 h-4 rounded border-gray-300 text-blue-500"
            />
            <span className="text-sm text-gray-700">启用流式响应</span>
          </label>
          <label className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={config.reflectOnToolUse}
              onChange={(e) => handleChange('reflectOnToolUse', e.target.checked)}
              className="w-4 h-4 rounded border-gray-300 text-blue-500"
            />
            <span className="text-sm text-gray-700">工具调用后反思</span>
          </label>
        </div>
      </section>
    </div>
  );
};

/**
 * 条件组配置表单
 */
interface ConditionConfigFormProps {
  config: ConditionGroupConfig;
  onChange: (config: ConditionGroupConfig) => void;
  executors: ExecutorDefinition[];
}

const ConditionConfigForm: React.FC<ConditionConfigFormProps> = ({ config, onChange, executors }) => {
  const conditions = config.conditions || [];
  
  const addCondition = () => {
    onChange({
      ...config,
      conditions: [...conditions, { expression: '', targetExecutorId: '' }],
    });
  };
  
  const updateCondition = (index: number, field: 'expression' | 'targetExecutorId', value: string) => {
    const newConditions = [...conditions];
    newConditions[index] = { ...newConditions[index], [field]: value };
    onChange({ ...config, conditions: newConditions });
  };
  
  const removeCondition = (index: number) => {
    onChange({
      ...config,
      conditions: conditions.filter((_, i) => i !== index),
    });
  };
  
  return (
    <div className="space-y-6">
      <section>
        <h3 className="text-lg font-semibold text-gray-800 mb-4 flex items-center gap-2">
          <span>🔀</span> 条件分支
        </h3>
        <p className="text-sm text-gray-500 mb-4">
          按顺序检查条件，匹配第一个为真的分支
        </p>
        
        {conditions.map((cond, index) => (
          <div key={index} className="flex gap-2 mb-3 p-3 bg-gray-50 rounded-lg">
            <div className="flex-1">
              <label className="block text-xs font-medium text-gray-600 mb-1">条件表达式</label>
              <input
                type="text"
                value={cond.expression}
                onChange={(e) => updateCondition(index, 'expression', e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm font-mono"
                placeholder="=Local.Intent == 'booking'"
              />
            </div>
            <div className="w-40">
              <label className="block text-xs font-medium text-gray-600 mb-1">跳转目标</label>
              <select
                value={cond.targetExecutorId}
                onChange={(e) => updateCondition(index, 'targetExecutorId', e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm"
              >
                <option value="">选择...</option>
                {executors.map((e) => (
                  <option key={e.id} value={e.id}>{e.name}</option>
                ))}
              </select>
            </div>
            <button
              onClick={() => removeCondition(index)}
              className="self-end p-2 text-red-500 hover:bg-red-50 rounded-lg"
            >
              ✕
            </button>
          </div>
        ))}
        
        <button
          onClick={addCondition}
          className="w-full py-2 border-2 border-dashed border-gray-300 text-gray-500 rounded-lg hover:border-blue-400 hover:text-blue-500"
        >
          + 添加条件
        </button>
      </section>

      <section>
        <h3 className="text-lg font-semibold text-gray-800 mb-4 flex items-center gap-2">
          <span>↪️</span> 默认分支
        </h3>
        <select
          value={config.defaultTarget || ''}
          onChange={(e) => onChange({ ...config, defaultTarget: e.target.value || undefined })}
          className="w-full px-3 py-2 border border-gray-300 rounded-lg"
        >
          <option value="">无默认分支（继续顺序执行）</option>
          {executors.map((e) => (
            <option key={e.id} value={e.id}>{e.name}</option>
          ))}
        </select>
        <p className="text-xs text-gray-500 mt-1">
          当所有条件都不满足时执行的分支
        </p>
      </section>
    </div>
  );
};

/**
 * 循环配置表单
 */
interface ForeachConfigFormProps {
  config: ForeachConfig;
  onChange: (config: ForeachConfig) => void;
}

const ForeachConfigForm: React.FC<ForeachConfigFormProps> = ({ config, onChange }) => {
  return (
    <div className="space-y-6">
      <section>
        <h3 className="text-lg font-semibold text-gray-800 mb-4 flex items-center gap-2">
          <span>🔄</span> 循环配置
        </h3>
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">集合表达式</label>
            <input
              type="text"
              value={config.itemsExpression}
              onChange={(e) => onChange({ ...config, itemsExpression: e.target.value })}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 font-mono"
              placeholder="conversation.messages"
            />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">项变量名</label>
              <input
                type="text"
                value={config.itemVariableName}
                onChange={(e) => onChange({ ...config, itemVariableName: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 font-mono"
                placeholder="item"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">索引变量名</label>
              <input
                type="text"
                value={config.indexVariableName}
                onChange={(e) => onChange({ ...config, indexVariableName: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 font-mono"
                placeholder="index"
              />
            </div>
          </div>
        </div>
      </section>
    </div>
  );
};

/**
 * 发送消息配置表单
 */
interface SendActivityConfigFormProps {
  config: SendActivityConfig;
  onChange: (config: SendActivityConfig) => void;
}

const SendActivityConfigForm: React.FC<SendActivityConfigFormProps> = ({ config, onChange }) => {
  return (
    <div className="space-y-6">
      <section>
        <h3 className="text-lg font-semibold text-gray-800 mb-4 flex items-center gap-2">
          <span>💬</span> 消息配置
        </h3>
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">消息类型</label>
            <select
              value={config.messageType || 'text'}
              onChange={(e) => onChange({ ...config, messageType: e.target.value as 'text' | 'markdown' | 'card' })}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 bg-white"
            >
              <option value="text">纯文本</option>
              <option value="markdown">Markdown</option>
              <option value="card">卡片</option>
            </select>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">消息内容</label>
            <textarea
              value={config.message}
              onChange={(e) => onChange({ ...config, message: e.target.value })}
              rows={6}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="你好！我是你的AI助手..."
            />
          </div>
        </div>
      </section>
    </div>
  );
};

/**
 * 问题配置表单
 */
interface QuestionConfigFormProps {
  config: QuestionConfig;
  onChange: (config: QuestionConfig) => void;
}

const QuestionConfigForm: React.FC<QuestionConfigFormProps> = ({ config, onChange }) => {
  return (
    <div className="space-y-6">
      <section>
        <h3 className="text-lg font-semibold text-gray-800 mb-4 flex items-center gap-2">
          <span>❔</span> 问题配置
        </h3>
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">提示语</label>
            <textarea
              value={config.prompt}
              onChange={(e) => onChange({ ...config, prompt: e.target.value })}
              rows={3}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="请输入您的问题..."
            />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">结果变量</label>
              <input
                type="text"
                value={config.resultVariable}
                onChange={(e) => onChange({ ...config, resultVariable: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 font-mono"
                placeholder="user_response"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">超时(秒)</label>
              <input
                type="number"
                value={config.timeout || ''}
                onChange={(e) => onChange({ ...config, timeout: parseInt(e.target.value) || undefined })}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                placeholder="可选"
              />
            </div>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">验证表达式</label>
            <input
              type="text"
              value={config.validationExpression || ''}
              onChange={(e) => onChange({ ...config, validationExpression: e.target.value || undefined })}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 font-mono"
              placeholder="可选，如: length(response) > 0"
            />
          </div>
        </div>
      </section>
    </div>
  );
};

// ==================== 主组件 ====================

export const ExecutorConfigModal: React.FC<ExecutorConfigModalProps> = ({
  isOpen,
  executorId,
  onClose,
}) => {
  const workflow = useWorkflowStore((state) => state.workflow);
  const updateExecutor = useWorkflowStore((state) => state.updateExecutor);
  
  const executor = useMemo(
    () => workflow?.executors.find((e) => e.id === executorId),
    [workflow, executorId]
  );

  const [localConfig, setLocalConfig] = useState<Record<string, unknown>>({});
  const [localName, setLocalName] = useState('');
  const [localDescription, setLocalDescription] = useState('');

  // 初始化本地状态
  useEffect(() => {
    if (executor) {
      setLocalConfig(executor.config as Record<string, unknown>);
      setLocalName(executor.name);
      setLocalDescription(executor.description || '');
    }
  }, [executor]);

  const handleSave = useCallback(() => {
    if (!executorId) return;
    
    updateExecutor(executorId, {
      name: localName,
      description: localDescription,
      config: localConfig,
    });
    
    onClose();
  }, [executorId, localName, localDescription, localConfig, updateExecutor, onClose]);

  const handleCancel = useCallback(() => {
    onClose();
  }, [onClose]);

  if (!isOpen || !executor) return null;

  const icon = getExecutorIcon(executor.type);
  const label = getExecutorLabel(executor.type);

  // 根据执行器类型渲染配置表单
  const renderConfigForm = () => {
    const handleConfigChange = (config: Record<string, unknown>) => {
      setLocalConfig(config);
    };

    if (isAgentExecutor(executor.type)) {
      return (
        <AgentConfigForm
          config={localConfig as unknown as AgentExecutorConfig}
          onChange={(config) => handleConfigChange(config as unknown as Record<string, unknown>)}
        />
      );
    }

    switch (executor.type) {
      case 'ConditionGroup':
        return (
          <ConditionConfigForm
            config={localConfig as unknown as ConditionGroupConfig}
            onChange={(config) => handleConfigChange(config as unknown as Record<string, unknown>)}
            executors={workflow?.executors || []}
          />
        );
      
      case 'Foreach':
        return (
          <ForeachConfigForm
            config={localConfig as unknown as ForeachConfig}
            onChange={(config) => handleConfigChange(config as unknown as Record<string, unknown>)}
          />
        );
      
      case 'SendActivity':
        return (
          <SendActivityConfigForm
            config={localConfig as unknown as SendActivityConfig}
            onChange={(config) => handleConfigChange(config as unknown as Record<string, unknown>)}
          />
        );
      
      case 'Question':
        return (
          <QuestionConfigForm
            config={localConfig as unknown as QuestionConfig}
            onChange={(config) => handleConfigChange(config as unknown as Record<string, unknown>)}
          />
        );
      
      default:
        // 通用 JSON 编辑器
        return (
          <div className="space-y-4">
            <h3 className="text-lg font-semibold text-gray-800">配置 (JSON)</h3>
            <textarea
              value={JSON.stringify(localConfig, null, 2)}
              onChange={(e) => {
                try {
                  setLocalConfig(JSON.parse(e.target.value));
                } catch {
                  // 忽略解析错误
                }
              }}
              rows={12}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 font-mono text-sm"
            />
          </div>
        );
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* 背景遮罩 */}
      <div 
        className="absolute inset-0 bg-black/50 backdrop-blur-sm"
        onClick={handleCancel}
      />
      
      {/* 模态框内容 */}
      <div className="relative bg-white rounded-xl shadow-2xl w-full max-w-2xl max-h-[90vh] flex flex-col">
        {/* 头部 */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-200">
          <div className="flex items-center gap-3">
            <span className="text-2xl">{icon}</span>
            <div>
              <h2 className="text-xl font-bold text-gray-800">配置执行器</h2>
              <p className="text-sm text-gray-500">{label}</p>
            </div>
          </div>
          <button
            onClick={handleCancel}
            className="p-2 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-lg transition-colors"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        
        {/* 内容区域 */}
        <div className="flex-1 overflow-y-auto px-6 py-4">
          {/* 通用配置 */}
          <div className="grid grid-cols-2 gap-4 mb-6 pb-6 border-b border-gray-200">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">节点名称</label>
              <input
                type="text"
                value={localName}
                onChange={(e) => setLocalName(e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                placeholder="执行器名称"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">描述</label>
              <input
                type="text"
                value={localDescription}
                onChange={(e) => setLocalDescription(e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
                placeholder="可选描述"
              />
            </div>
          </div>
          
          {/* 类型特定配置 */}
          {renderConfigForm()}
        </div>
        
        {/* 底部操作栏 */}
        <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-gray-200 bg-gray-50">
          <button
            onClick={handleCancel}
            className="px-4 py-2 text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition-colors"
          >
            取消
          </button>
          <button
            onClick={handleSave}
            className="px-4 py-2 text-white bg-blue-600 rounded-lg hover:bg-blue-700 transition-colors"
          >
            保存
          </button>
        </div>
      </div>
    </div>
  );
};

export default ExecutorConfigModal;
