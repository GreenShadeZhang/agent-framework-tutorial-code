/**
 * 执行器节点组件 - 工作流设计器的核心节点
 * 参考 AutoGen Studio 和 Agent Framework DevUI 设计模式
 */

import React, { memo, useCallback, useMemo } from 'react';
import { Handle, Position } from 'reactflow';
import type { NodeProps } from 'reactflow';
import type { 
  WorkflowNodeData,
  ExecutorType,
  AgentExecutorConfig,
  ConditionGroupConfig,
  ForeachConfig,
} from '../../../types/workflow';
import { 
  ExecutorTypeGroups,
  getExecutorIcon,
  getExecutorLabel,
  isAgentExecutor,
} from '../../../types/workflow';
import { useWorkflowStore } from '../../../store/workflowStore';

// ==================== 节点样式配置 ====================

interface NodeStyle {
  bgColor: string;
  borderColor: string;
  textColor: string;
  accentColor: string;
}

const getNodeStyle = (type: ExecutorType, isSelected: boolean, isRunning: boolean, hasError: boolean): NodeStyle => {
  // 基础样式映射
  const baseStyles: Record<string, NodeStyle> = {
    agent: {
      bgColor: 'bg-gradient-to-br from-blue-50 to-blue-100',
      borderColor: 'border-blue-300',
      textColor: 'text-blue-800',
      accentColor: 'bg-blue-500',
    },
    controlFlow: {
      bgColor: 'bg-gradient-to-br from-purple-50 to-purple-100',
      borderColor: 'border-purple-300',
      textColor: 'text-purple-800',
      accentColor: 'bg-purple-500',
    },
    stateManagement: {
      bgColor: 'bg-gradient-to-br from-green-50 to-green-100',
      borderColor: 'border-green-300',
      textColor: 'text-green-800',
      accentColor: 'bg-green-500',
    },
    messages: {
      bgColor: 'bg-gradient-to-br from-yellow-50 to-yellow-100',
      borderColor: 'border-yellow-300',
      textColor: 'text-yellow-800',
      accentColor: 'bg-yellow-500',
    },
    conversation: {
      bgColor: 'bg-gradient-to-br from-orange-50 to-orange-100',
      borderColor: 'border-orange-300',
      textColor: 'text-orange-800',
      accentColor: 'bg-orange-500',
    },
    humanInput: {
      bgColor: 'bg-gradient-to-br from-pink-50 to-pink-100',
      borderColor: 'border-pink-300',
      textColor: 'text-pink-800',
      accentColor: 'bg-pink-500',
    },
    tools: {
      bgColor: 'bg-gradient-to-br from-cyan-50 to-cyan-100',
      borderColor: 'border-cyan-300',
      textColor: 'text-cyan-800',
      accentColor: 'bg-cyan-500',
    },
    workflow: {
      bgColor: 'bg-gradient-to-br from-indigo-50 to-indigo-100',
      borderColor: 'border-indigo-300',
      textColor: 'text-indigo-800',
      accentColor: 'bg-indigo-500',
    },
  };

  // 确定类型分组
  let category = 'controlFlow';
  if (ExecutorTypeGroups.agents.some(g => g.type === type)) category = 'agent';
  else if (ExecutorTypeGroups.controlFlow.some(g => g.type === type)) category = 'controlFlow';
  else if (ExecutorTypeGroups.stateManagement.some(g => g.type === type)) category = 'stateManagement';
  else if (ExecutorTypeGroups.messages.some(g => g.type === type)) category = 'messages';
  else if (ExecutorTypeGroups.conversation.some(g => g.type === type)) category = 'conversation';
  else if (ExecutorTypeGroups.humanInput.some(g => g.type === type)) category = 'humanInput';

  let style = baseStyles[category];

  // 覆盖特殊状态
  if (hasError) {
    style = {
      bgColor: 'bg-gradient-to-br from-red-50 to-red-100',
      borderColor: 'border-red-500',
      textColor: 'text-red-800',
      accentColor: 'bg-red-500',
    };
  } else if (isRunning) {
    style = {
      ...style,
      borderColor: 'border-green-500',
    };
  } else if (isSelected) {
    style = {
      ...style,
      borderColor: 'border-blue-500',
    };
  }

  return style;
};

// ==================== 子组件 ====================

interface HandleBadgeProps {
  count: number;
  position: 'top' | 'bottom';
}

const HandleBadge: React.FC<HandleBadgeProps> = ({ count, position }) => {
  if (count === 0) return null;
  
  const positionClass = position === 'top' ? '-top-1 -right-1' : '-bottom-1 -right-1';
  
  return (
    <span className={`absolute ${positionClass} bg-gray-500 text-white text-[10px] rounded-full w-4 h-4 flex items-center justify-center`}>
      {count}
    </span>
  );
};

interface StatusIndicatorProps {
  isRunning: boolean;
  hasError: boolean;
}

const StatusIndicator: React.FC<StatusIndicatorProps> = ({ isRunning, hasError }) => {
  if (!isRunning && !hasError) return null;
  
  if (hasError) {
    return (
      <div className="absolute -top-2 -right-2 w-5 h-5 bg-red-500 rounded-full flex items-center justify-center">
        <span className="text-white text-xs">!</span>
      </div>
    );
  }
  
  return (
    <div className="absolute -top-2 -right-2 w-5 h-5">
      <div className="w-full h-full bg-green-500 rounded-full animate-ping opacity-75"></div>
      <div className="absolute top-0 left-0 w-full h-full bg-green-500 rounded-full"></div>
    </div>
  );
};

// ==================== 节点内容组件 ====================

interface AgentNodeContentProps {
  config: AgentExecutorConfig;
}

const AgentNodeContent: React.FC<AgentNodeContentProps> = ({ config }) => {
  return (
    <div className="space-y-1 text-xs">
      {config.name && (
        <div className="flex items-center gap-1 text-gray-600">
          <span>🤖</span>
          <span className="truncate">{config.name}</span>
        </div>
      )}
      {config.modelConfig?.model && (
        <div className="flex items-center gap-1 text-gray-600">
          <span>🧠</span>
          <span>{config.modelConfig.model}</span>
        </div>
      )}
      {(config.tools?.length ?? 0) > 0 && (
        <div className="flex items-center gap-1 text-gray-600">
          <span>🔧</span>
          <span>{config.tools?.length} 工具</span>
        </div>
      )}
      {(config.handoffs?.length ?? 0) > 0 && (
        <div className="flex items-center gap-1 text-gray-600">
          <span>🔀</span>
          <span>{config.handoffs?.length} 交接</span>
        </div>
      )}
    </div>
  );
};

interface ConditionNodeContentProps {
  config: ConditionGroupConfig;
  type: 'ConditionGroup';
}

const ConditionNodeContent: React.FC<ConditionNodeContentProps> = ({ config }) => {
  return (
    <div className="text-xs text-gray-600">
      {(config.conditions?.length || 0)} 个条件分支
    </div>
  );
};

interface ForeachNodeContentProps {
  config: ForeachConfig;
}

const ForeachNodeContent: React.FC<ForeachNodeContentProps> = ({ config }) => {
  return (
    <div className="space-y-1 text-xs text-gray-600">
      <div className="font-mono truncate max-w-[180px]">
        {config.itemsExpression || '[]'}
      </div>
      <div>
        变量: {config.itemVariableName}
      </div>
    </div>
  );
};

// ==================== 主节点组件 ====================

export const ExecutorNode: React.FC<NodeProps<WorkflowNodeData>> = memo(({ data, id, selected }) => {
  const { executor, isRunning = false, hasError = false, onEdit, onDelete } = data;
  const { type, name, description, config } = executor;
  
  const openConfigModal = useWorkflowStore(state => state.openConfigModal);
  const deleteExecutor = useWorkflowStore(state => state.deleteExecutor);
  const getIncomingEdges = useWorkflowStore(state => state.getIncomingEdges);
  const getOutgoingEdges = useWorkflowStore(state => state.getOutgoingEdges);
  
  const style = useMemo(
    () => getNodeStyle(type, selected || false, isRunning, hasError),
    [type, selected, isRunning, hasError]
  );
  
  const icon = getExecutorIcon(type);
  const label = getExecutorLabel(type);
  
  const incomingCount = getIncomingEdges(id).length;
  const outgoingCount = getOutgoingEdges(id).length;
  
  const handleEdit = useCallback(() => {
    if (onEdit) {
      onEdit();
    } else {
      openConfigModal(id);
    }
  }, [id, onEdit, openConfigModal]);
  
  const handleDelete = useCallback(() => {
    if (onDelete) {
      onDelete();
    } else {
      deleteExecutor(id);
    }
  }, [id, onDelete, deleteExecutor]);
  
  // 渲染节点特定内容
  const renderContent = () => {
    if (isAgentExecutor(type)) {
      return <AgentNodeContent config={config as AgentExecutorConfig} />;
    }
    
    if (type === 'ConditionGroup') {
      return (
        <ConditionNodeContent 
          config={config as ConditionGroupConfig} 
          type={type} 
        />
      );
    }
    
    if (type === 'Foreach') {
      return <ForeachNodeContent config={config as ForeachConfig} />;
    }
    
    if (description) {
      return <div className="text-xs text-gray-500 truncate">{description}</div>;
    }
    
    return null;
  };
  
  // 确定 Handle 配置
  const showTopHandle = type !== 'EndWorkflow' && type !== 'EndConversation';
  const showBottomHandle = type !== 'EndWorkflow' && type !== 'EndConversation';
  const showMultipleOutputs = type === 'ConditionGroup';
  
  return (
    <div
      className={`
        relative min-w-[200px] max-w-[280px] rounded-lg border-2 shadow-lg
        ${style.bgColor} ${style.borderColor}
        transition-all duration-200 hover:shadow-xl
        ${selected ? 'ring-2 ring-blue-400 ring-offset-2' : ''}
        ${isRunning ? 'animate-pulse' : ''}
      `}
    >
      {/* 状态指示器 */}
      <StatusIndicator isRunning={isRunning} hasError={hasError} />
      
      {/* 输入 Handle */}
      {showTopHandle && (
        <div className="relative">
          <Handle
            type="target"
            position={Position.Top}
            className={`!w-3 !h-3 !bg-gray-400 hover:!bg-gray-600 !border-2 !border-white`}
          />
          <HandleBadge count={incomingCount} position="top" />
        </div>
      )}
      
      {/* 节点头部 */}
      <div className={`px-3 py-2 border-b ${style.borderColor} flex items-center justify-between`}>
        <div className="flex items-center gap-2">
          <span className="text-lg">{icon}</span>
          <div>
            <div className={`font-semibold ${style.textColor} text-sm`}>{name}</div>
            <div className="text-xs text-gray-500">{label}</div>
          </div>
        </div>
        
        {/* 操作按钮 */}
        <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
          <button
            onClick={handleEdit}
            className="p-1 rounded hover:bg-white/50 text-gray-600 hover:text-gray-800"
            title="编辑"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
            </svg>
          </button>
          <button
            onClick={handleDelete}
            className="p-1 rounded hover:bg-white/50 text-gray-600 hover:text-red-600"
            title="删除"
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
            </svg>
          </button>
        </div>
      </div>
      
      {/* 节点内容 */}
      <div className="px-3 py-2 min-h-[40px]">
        {renderContent()}
      </div>
      
      {/* 输出 Handle */}
      {showBottomHandle && (
        <>
          {showMultipleOutputs ? (
            <div className="flex justify-around px-4 -mb-1.5">
              <Handle
                type="source"
                position={Position.Bottom}
                id="true"
                className={`!w-3 !h-3 !bg-green-500 hover:!bg-green-600 !border-2 !border-white`}
                style={{ left: '30%' }}
              />
              <Handle
                type="source"
                position={Position.Bottom}
                id="false"
                className={`!w-3 !h-3 !bg-red-500 hover:!bg-red-600 !border-2 !border-white`}
                style={{ left: '70%' }}
              />
            </div>
          ) : (
            <div className="relative">
              <Handle
                type="source"
                position={Position.Bottom}
                className={`!w-3 !h-3 !bg-gray-400 hover:!bg-gray-600 !border-2 !border-white`}
              />
              <HandleBadge count={outgoingCount} position="bottom" />
            </div>
          )}
        </>
      )}
    </div>
  );
});

ExecutorNode.displayName = 'ExecutorNode';

// ==================== 特殊节点类型 ====================

/**
 * 开始节点
 */
export const StartNode: React.FC<NodeProps<WorkflowNodeData>> = memo(({ selected }) => {
  return (
    <div
      className={`
        w-24 h-24 rounded-full bg-gradient-to-br from-green-400 to-green-600
        flex items-center justify-center shadow-lg border-4 border-white
        ${selected ? 'ring-4 ring-green-300' : ''}
      `}
    >
      <div className="text-center text-white">
        <div className="text-2xl">▶️</div>
        <div className="text-xs font-semibold mt-1">开始</div>
      </div>
      
      <Handle
        type="source"
        position={Position.Bottom}
        className="!w-4 !h-4 !bg-white !border-2 !border-green-500"
      />
    </div>
  );
});

StartNode.displayName = 'StartNode';

/**
 * 结束节点
 */
export const EndNode: React.FC<NodeProps<WorkflowNodeData>> = memo(({ selected }) => {
  return (
    <div
      className={`
        w-24 h-24 rounded-full bg-gradient-to-br from-red-400 to-red-600
        flex items-center justify-center shadow-lg border-4 border-white
        ${selected ? 'ring-4 ring-red-300' : ''}
      `}
    >
      <Handle
        type="target"
        position={Position.Top}
        className="!w-4 !h-4 !bg-white !border-2 !border-red-500"
      />
      
      <div className="text-center text-white">
        <div className="text-2xl">⏹️</div>
        <div className="text-xs font-semibold mt-1">结束</div>
      </div>
    </div>
  );
});

EndNode.displayName = 'EndNode';

/**
 * 并行执行节点（扇出）
 */
export const FanOutNode: React.FC<NodeProps<WorkflowNodeData>> = memo(({ data, selected }) => {
  const { executor } = data;
  
  return (
    <div
      className={`
        min-w-[160px] rounded-lg bg-gradient-to-br from-indigo-100 to-indigo-200
        border-2 border-indigo-400 shadow-lg
        ${selected ? 'ring-2 ring-indigo-400' : ''}
      `}
    >
      <Handle
        type="target"
        position={Position.Top}
        className="!w-3 !h-3 !bg-indigo-500 !border-2 !border-white"
      />
      
      <div className="px-4 py-3 text-center">
        <div className="text-2xl">📤</div>
        <div className="font-semibold text-indigo-800 text-sm">{executor.name}</div>
        <div className="text-xs text-indigo-600">并行分发</div>
      </div>
      
      <div className="flex justify-around px-2 pb-1">
        <Handle
          type="source"
          position={Position.Bottom}
          id="out-1"
          className="!w-2 !h-2 !bg-indigo-500"
          style={{ left: '25%' }}
        />
        <Handle
          type="source"
          position={Position.Bottom}
          id="out-2"
          className="!w-2 !h-2 !bg-indigo-500"
          style={{ left: '50%' }}
        />
        <Handle
          type="source"
          position={Position.Bottom}
          id="out-3"
          className="!w-2 !h-2 !bg-indigo-500"
          style={{ left: '75%' }}
        />
      </div>
    </div>
  );
});

FanOutNode.displayName = 'FanOutNode';

/**
 * 并行合并节点（扇入）
 */
export const FanInNode: React.FC<NodeProps<WorkflowNodeData>> = memo(({ data, selected }) => {
  const { executor } = data;
  
  return (
    <div
      className={`
        min-w-[160px] rounded-lg bg-gradient-to-br from-teal-100 to-teal-200
        border-2 border-teal-400 shadow-lg
        ${selected ? 'ring-2 ring-teal-400' : ''}
      `}
    >
      <div className="flex justify-around px-2 pt-1">
        <Handle
          type="target"
          position={Position.Top}
          id="in-1"
          className="!w-2 !h-2 !bg-teal-500"
          style={{ left: '25%' }}
        />
        <Handle
          type="target"
          position={Position.Top}
          id="in-2"
          className="!w-2 !h-2 !bg-teal-500"
          style={{ left: '50%' }}
        />
        <Handle
          type="target"
          position={Position.Top}
          id="in-3"
          className="!w-2 !h-2 !bg-teal-500"
          style={{ left: '75%' }}
        />
      </div>
      
      <div className="px-4 py-3 text-center">
        <div className="text-2xl">📥</div>
        <div className="font-semibold text-teal-800 text-sm">{executor.name}</div>
        <div className="text-xs text-teal-600">并行合并</div>
      </div>
      
      <Handle
        type="source"
        position={Position.Bottom}
        className="!w-3 !h-3 !bg-teal-500 !border-2 !border-white"
      />
    </div>
  );
});

FanInNode.displayName = 'FanInNode';

// ==================== 节点类型映射 ====================

export const nodeTypes = {
  executor: ExecutorNode,
  start: StartNode,
  end: EndNode,
  fanOut: FanOutNode,
  fanIn: FanInNode,
};

export default ExecutorNode;
