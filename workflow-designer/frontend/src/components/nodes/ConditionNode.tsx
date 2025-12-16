import { memo } from 'react';
import { Handle, Position, type NodeProps } from 'reactflow';

export interface ConditionNodeData {
  condition: string;
  label?: string;
  onEdit?: () => void;
}

function ConditionNode({ data, selected }: NodeProps<ConditionNodeData>) {
  return (
    <div
      className={`px-4 py-3 shadow-md rounded-lg border-2 bg-yellow-50 min-w-[150px] cursor-pointer ${
        selected ? 'border-yellow-500' : 'border-yellow-300'
      }`}
      onDoubleClick={data.onEdit}
    >
      <Handle type="target" position={Position.Top} className="w-3 h-3" />
      
      <div className="flex items-center gap-2">
        <span className="text-2xl">🔀</span>
        <div>
          <div className="font-semibold text-sm">条件判断</div>
          <div className="text-xs text-gray-600 mt-1">
            {data.label || '分支条件'}
          </div>
          {data.condition && (
            <div className="text-xs text-gray-500 mt-1 font-mono">
              {data.condition.length > 30 
                ? data.condition.substring(0, 30) + '...' 
                : data.condition}
            </div>
          )}
        </div>
      </div>
      
      <Handle
        type="source"
        position={Position.Bottom}
        id="true"
        className="w-3 h-3"
        style={{ left: '30%' }}
      />
      <Handle
        type="source"
        position={Position.Bottom}
        id="false"
        className="w-3 h-3"
        style={{ left: '70%' }}
      />
    </div>
  );
}

export default memo(ConditionNode);
