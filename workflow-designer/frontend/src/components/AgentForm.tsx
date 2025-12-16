import { useState, useEffect } from 'react';
import { api } from '../api/client';
import { useToastStore } from '../store/toastStore';

interface AgentFormProps {
  agentId?: string;
  onSuccess: () => void;
  onCancel: () => void;
}

export default function AgentForm({ agentId, onSuccess, onCancel }: AgentFormProps) {
  const [formData, setFormData] = useState({
    name: '',
    type: 'Assistant',
    description: '',
    instructionsTemplate: '',
    model: 'gpt-4',
    temperature: 0.7,
    maxTokens: 2000,
  });
  const [saving, setSaving] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { showToast } = useToastStore();

  useEffect(() => {
    if (agentId) {
      loadAgent();
    }
  }, [agentId]);

  const loadAgent = async () => {
    try {
      setLoading(true);
      const agent = await api.getAgent(agentId!);
      setFormData({
        name: agent.name,
        type: agent.type,
        description: agent.description,
        instructionsTemplate: agent.instructionsTemplate,
        model: agent.modelConfig.model,
        temperature: agent.modelConfig.temperature,
        maxTokens: agent.modelConfig.maxTokens,
      });
    } catch (err: any) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    try {
      setSaving(true);
      setError(null);
      
      const payload = {
        name: formData.name,
        type: formData.type,
        description: formData.description,
        instructionsTemplate: formData.instructionsTemplate,
        modelConfig: {
          model: formData.model,
          temperature: parseFloat(formData.temperature.toString()),
          maxTokens: parseInt(formData.maxTokens.toString()),
        },
        tools: [],
        metadata: {},
      };

      if (agentId) {
        await api.updateAgent(agentId, payload);
        showToast('智能体更新成功', 'success');
      } else {
        await api.createAgent(payload);
        showToast('智能体创建成功', 'success');
      }
      
      onSuccess();
    } catch (err: any) {
      setError(err.message);
      showToast(agentId ? '更新失败: ' + err.message : '创建失败: ' + err.message, 'error');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg shadow-xl max-w-2xl w-full max-h-[90vh] overflow-y-auto">
        <div className="p-6">
          <h2 className="text-2xl font-bold mb-4">{agentId ? '编辑智能体' : '新建智能体'}</h2>
          
          {loading && (
            <div className="text-center py-8">
              <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-500 mx-auto"></div>
              <p className="text-gray-600 mt-2">加载中...</p>
            </div>
          )}
          
          {!loading && (
            <>
              {error && (
                <div className="bg-red-50 border border-red-200 rounded-lg p-3 mb-4">
                  <p className="text-red-800 text-sm">{error}</p>
                </div>
              )}
              
              <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                名称 *
              </label>
              <input
                type="text"
                required
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="例如：客服助手"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                类型 *
              </label>
              <select
                value={formData.type}
                onChange={(e) => setFormData({ ...formData, type: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                <option value="Assistant">助手型</option>
                <option value="WebSurfer">网页浏览</option>
                <option value="Coder">代码生成</option>
                <option value="Custom">自定义</option>
              </select>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                描述
              </label>
              <textarea
                value={formData.description}
                onChange={(e) => setFormData({ ...formData, description: e.target.value })}
                rows={3}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="简要描述智能体的功能..."
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                指令模板 *
              </label>
              <textarea
                required
                value={formData.instructionsTemplate}
                onChange={(e) => setFormData({ ...formData, instructionsTemplate: e.target.value })}
                rows={5}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="定义智能体的角色和行为，支持 Scriban 模板语法..."
              />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  模型
                </label>
                <select
                  value={formData.model}
                  onChange={(e) => setFormData({ ...formData, model: e.target.value })}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                >
                  <option value="gpt-4">GPT-4</option>
                  <option value="gpt-4-turbo">GPT-4 Turbo</option>
                  <option value="gpt-3.5-turbo">GPT-3.5 Turbo</option>
                </select>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  温度 (0-1)
                </label>
                <input
                  type="number"
                  min="0"
                  max="1"
                  step="0.1"
                  value={formData.temperature}
                  onChange={(e) => setFormData({ ...formData, temperature: parseFloat(e.target.value) })}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                最大令牌数
              </label>
              <input
                type="number"
                min="100"
                max="8000"
                step="100"
                value={formData.maxTokens}
                onChange={(e) => setFormData({ ...formData, maxTokens: parseInt(e.target.value) })}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>

            <div className="flex justify-end space-x-3 pt-4 border-t">
              <button
                type="button"
                onClick={onCancel}
                disabled={saving}
                className="px-4 py-2 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 disabled:opacity-50"
              >
                取消
              </button>
              <button
                type="submit"
                disabled={saving}
                className="px-4 py-2 bg-blue-500 text-white rounded-lg hover:bg-blue-600 disabled:opacity-50"
              >
                {saving ? (agentId ? '保存中...' : '创建中...') : (agentId ? '保存' : '创建')}
              </button>
            </div>
          </form>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
