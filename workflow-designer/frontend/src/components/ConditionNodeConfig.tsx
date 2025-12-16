import { useState } from 'react';

interface ConditionNodeConfigProps {
  nodeId: string;
  initialData: {
    condition: string;
    label?: string;
  };
  onSave: (data: { condition: string; label: string }) => void;
  onCancel: () => void;
}

export default function ConditionNodeConfig({ 
  initialData, 
  onSave, 
  onCancel 
}: ConditionNodeConfigProps) {
  const [label, setLabel] = useState(initialData.label || '');
  const [condition, setCondition] = useState(initialData.condition || '');

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSave({ label, condition });
  };

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg shadow-xl max-w-2xl w-full">
        <div className="p-6">
          <h2 className="text-2xl font-bold mb-4">配置条件节点</h2>
          
          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                节点标签 *
              </label>
              <input
                type="text"
                required
                value={label}
                onChange={(e) => setLabel(e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="例如：检查用户权限"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                条件表达式 *
              </label>
              <textarea
                required
                value={condition}
                onChange={(e) => setCondition(e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 h-32"
                placeholder="例如：user.role === 'admin'"
              />
              <p className="text-xs text-gray-500 mt-1">
                使用 JavaScript 表达式，可以访问工作流参数和上下文变量
              </p>
            </div>

            <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
              <h3 className="font-semibold text-sm mb-2">条件说明：</h3>
              <ul className="text-xs text-gray-700 space-y-1">
                <li>• 条件为 true 时走 True 分支（左侧连接点）</li>
                <li>• 条件为 false 时走 False 分支（右侧连接点）</li>
                <li>• 可以使用工作流参数，如：parameters.userId</li>
                <li>• 可以使用上一步结果，如：context.previousResult</li>
              </ul>
            </div>

            <div className="flex justify-end space-x-3 pt-4 border-t">
              <button
                type="button"
                onClick={onCancel}
                className="px-4 py-2 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50"
              >
                取消
              </button>
              <button
                type="submit"
                className="px-4 py-2 bg-blue-500 text-white rounded-lg hover:bg-blue-600"
              >
                保存
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
}
