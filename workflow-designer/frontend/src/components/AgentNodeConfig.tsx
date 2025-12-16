import { useState, useEffect } from 'react';
import { api } from '../api/client';
import { useToastStore } from '../store/toastStore';

interface AgentNodeConfigProps {
  nodeId: string;
  initialData: {
    id?: string;
    name?: string;
    type?: string;
    instructionsTemplate?: string;
    inputVariables?: string[];
    outputVariables?: string[];
  };
  onSave: (data: any) => void;
  onCancel: () => void;
}

export default function AgentNodeConfig({
  initialData,
  onSave,
  onCancel,
}: AgentNodeConfigProps) {
  const [agents, setAgents] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedAgentId, setSelectedAgentId] = useState(initialData.id || '');
  const [customInstructions, setCustomInstructions] = useState(initialData.instructionsTemplate || '');
  const [useCustomInstructions, setUseCustomInstructions] = useState(!!initialData.instructionsTemplate);
  const [inputVariables, setInputVariables] = useState(initialData.inputVariables?.join(', ') || '');
  const [outputVariables, setOutputVariables] = useState(initialData.outputVariables?.join(', ') || '');
  const { showToast } = useToastStore();

  useEffect(() => {
    loadAgents();
  }, []);

  const loadAgents = async () => {
    try {
      const data = await api.getAgents();
      setAgents(data);
      if (!selectedAgentId && data.length > 0) {
        setSelectedAgentId(data[0].id);
      }
    } catch (err: any) {
      showToast('加载智能体失败: ' + err.message, 'error');
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    if (!selectedAgentId) {
      showToast('请选择一个智能体', 'error');
      return;
    }

    const selectedAgent = agents.find((a) => a.id === selectedAgentId);
    if (!selectedAgent) {
      showToast('选中的智能体不存在', 'error');
      return;
    }

    const data = {
      id: selectedAgentId,
      name: selectedAgent.name,
      type: selectedAgent.type,
      instructionsTemplate: useCustomInstructions ? customInstructions : selectedAgent.instructionsTemplate,
      inputVariables: inputVariables
        ? inputVariables.split(',').map((v) => v.trim()).filter((v) => v)
        : [],
      outputVariables: outputVariables
        ? outputVariables.split(',').map((v) => v.trim()).filter((v) => v)
        : [],
    };

    onSave(data);
  };

  const selectedAgent = agents.find((a) => a.id === selectedAgentId);

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg shadow-xl max-w-3xl w-full max-h-[90vh] overflow-y-auto">
        <div className="p-6">
          <h2 className="text-2xl font-bold mb-4">配置智能体节点</h2>

          {loading ? (
            <div className="text-center py-8">
              <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-500 mx-auto"></div>
              <p className="text-gray-600 mt-2">加载中...</p>
            </div>
          ) : (
            <form onSubmit={handleSubmit} className="space-y-4">
              {/* 智能体选择 */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  选择智能体 *
                </label>
                <select
                  value={selectedAgentId}
                  onChange={(e) => setSelectedAgentId(e.target.value)}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                  required
                >
                  {agents.map((agent) => (
                    <option key={agent.id} value={agent.id}>
                      {agent.name} ({agent.type})
                    </option>
                  ))}
                </select>
                {selectedAgent && (
                  <p className="text-xs text-gray-500 mt-1">
                    {selectedAgent.description}
                  </p>
                )}
              </div>

              {/* 输入变量 */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  输入变量
                </label>
                <input
                  type="text"
                  value={inputVariables}
                  onChange={(e) => setInputVariables(e.target.value)}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                  placeholder="例如：userId, query, context"
                />
                <p className="text-xs text-gray-500 mt-1">
                  用逗号分隔多个变量，这些变量将从上游节点或工作流参数中获取
                </p>
              </div>

              {/* 输出变量 */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  输出变量
                </label>
                <input
                  type="text"
                  value={outputVariables}
                  onChange={(e) => setOutputVariables(e.target.value)}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                  placeholder="例如：result, summary, recommendations"
                />
                <p className="text-xs text-gray-500 mt-1">
                  智能体执行结果将存储到这些变量中，供下游节点使用
                </p>
              </div>

              {/* 自定义指令 */}
              <div>
                <div className="flex items-center mb-2">
                  <input
                    type="checkbox"
                    id="useCustomInstructions"
                    checked={useCustomInstructions}
                    onChange={(e) => setUseCustomInstructions(e.target.checked)}
                    className="mr-2"
                  />
                  <label htmlFor="useCustomInstructions" className="text-sm font-medium text-gray-700">
                    使用自定义指令（覆盖智能体默认指令）
                  </label>
                </div>
                {useCustomInstructions && (
                  <>
                    <textarea
                      value={customInstructions}
                      onChange={(e) => setCustomInstructions(e.target.value)}
                      className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 h-32"
                      placeholder="输入自定义指令模板..."
                    />
                    <p className="text-xs text-gray-500 mt-1">
                      支持变量替换，如：{'{{userId}}'}, {'{{query}}'}
                    </p>
                  </>
                )}
                {!useCustomInstructions && selectedAgent && (
                  <div className="bg-gray-50 p-3 rounded-lg">
                    <p className="text-xs font-semibold text-gray-700 mb-1">当前智能体默认指令：</p>
                    <p className="text-xs text-gray-600 whitespace-pre-wrap">
                      {selectedAgent.instructionsTemplate.length > 200
                        ? selectedAgent.instructionsTemplate.substring(0, 200) + '...'
                        : selectedAgent.instructionsTemplate}
                    </p>
                  </div>
                )}
              </div>

              {/* 说明信息 */}
              <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
                <h3 className="font-semibold text-sm mb-2">配置说明：</h3>
                <ul className="text-xs text-gray-700 space-y-1">
                  <li>• <strong>输入变量</strong>：节点执行时需要的数据，可以从工作流参数或上游节点输出获取</li>
                  <li>• <strong>输出变量</strong>：节点执行结果的变量名，可被下游节点引用</li>
                  <li>• <strong>自定义指令</strong>：可以为特定场景定制指令，不影响智能体的基础配置</li>
                  <li>• <strong>变量引用</strong>：在自定义指令中使用 {'{{variableName}}'} 格式引用变量</li>
                </ul>
              </div>

              {/* 按钮 */}
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
          )}
        </div>
      </div>
    </div>
  );
}
