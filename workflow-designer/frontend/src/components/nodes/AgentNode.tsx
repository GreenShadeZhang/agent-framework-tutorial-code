import { memo } from 'react';
import { Handle, Position, type NodeProps } from 'reactflow';

export interface AgentNodeData {
  agentId?: string;
  agentName?: string;
  agentType?: string;
  description?: string;
  instructionsTemplate?: string;
  inputVariables?: string[];
  outputVariables?: string[];
  onEdit?: () => void;
  // 兼容旧的字段名
  id?: string;
  name?: string;
  type?: string;
}

function AgentNode({ data, selected }: NodeProps<AgentNodeData>) {
  // 兼容新旧字段名
  const agentName = data.agentName || data.name || '未配置智能体';
  const agentType = data.agentType || data.type || 'Assistant';
  
  return (
    <div
      className={`px-4 py-3 shadow-md rounded-lg border-2 bg-white min-w-[200px] cursor-pointer ${
        selected ? 'border-blue-500' : 'border-gray-300'
      }`}
      onDoubleClick={data.onEdit}
    >
      <Handle type="target" position={Position.Top} className="w-3 h-3" />
      
      <div className="flex items-center gap-2 mb-2">
        <div className="w-8 h-8 rounded-full bg-blue-100 flex items-center justify-center">
          <span className="text-blue-600 text-sm font-semibold">
            {agentType === 'Coder' ? '💻' : agentType === 'WebSurfer' ? '🌐' : '🤖'}
          </span>
        </div>
        <div className="flex-1">
          <div className="font-semibold text-sm">{agentName}</div>
          <div className="text-xs text-gray-500">{agentType}</div>
        </div>
      </div>
      
      {data.description && (
        <div className="text-xs text-gray-600 mt-2 line-clamp-2">
          {data.description}
        </div>
      )}

      {/* 显示输入输出变量 */}
      {(data.inputVariables?.length || data.outputVariables?.length) && (
        <div className="mt-2 pt-2 border-t border-gray-200 space-y-1">
          {data.inputVariables && data.inputVariables.length > 0 && (
            <div className="text-xs">
              <span className="text-gray-500">输入:</span>
              <span className="text-blue-600 ml-1">{data.inputVariables.join(', ')}</span>
            </div>
          )}
          {data.outputVariables && data.outputVariables.length > 0 && (
            <div className="text-xs">
              <span className="text-gray-500">输出:</span>
              <span className="text-green-600 ml-1">{data.outputVariables.join(', ')}</span>
            </div>
          )}
        </div>
      )}

      {/* 自定义指令提示 */}
      {data.instructionsTemplate && (
        <div className="mt-2 pt-2 border-t border-gray-200">
          <div className="text-xs text-purple-600 flex items-center gap-1">
            <span>✨</span>
            <span>使用自定义指令</span>
          </div>
        </div>
      )}
      
      <Handle type="source" position={Position.Bottom} className="w-3 h-3" />
    </div>
  );
}

export default memo(AgentNode);
