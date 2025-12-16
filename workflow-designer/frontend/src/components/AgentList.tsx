import { useEffect, useState } from 'react';
import { useAppStore } from '../store/appStore';
import { api } from '../api/client';
import AgentForm from './AgentForm';
import { useToastStore } from '../store/toastStore';

export default function AgentList() {
  const { agents, setAgents, deleteAgent } = useAppStore();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [editingAgentId, setEditingAgentId] = useState<string | undefined>();
  const { showToast } = useToastStore();

  useEffect(() => {
    loadAgents();
  }, []);

  const loadAgents = async () => {
    try {
      setLoading(true);
      const data = await api.getAgents();
      setAgents(data);
      setError(null);
    } catch (err: any) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm('确定要删除这个智能体吗?')) return;
    
    try {
      await api.deleteAgent(id);
      deleteAgent(id);
      showToast('智能体删除成功', 'success');
    } catch (err: any) {
      setError(err.message);
      showToast('删除失败: ' + err.message, 'error');
    }
  };

  const handleFormSuccess = () => {
    setShowForm(false);
    setEditingAgentId(undefined);
    loadAgents();
  };

  const handleEdit = (agentId: string) => {
    setEditingAgentId(agentId);
    setShowForm(true);
  };

  const handleCancelForm = () => {
    setShowForm(false);
    setEditingAgentId(undefined);
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-gray-500">加载中...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="bg-red-50 border border-red-200 rounded-lg p-4">
        <div className="text-red-800">错误: {error}</div>
        <button
          onClick={loadAgents}
          className="mt-2 text-sm text-red-600 hover:text-red-700"
        >
          重试
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex justify-between items-center">
        <h2 className="text-xl font-bold">智能体列表</h2>
        <button 
          onClick={() => setShowForm(true)}
          className="bg-blue-500 hover:bg-blue-600 text-white px-4 py-2 rounded"
        >
          新建智能体
        </button>
      </div>

      {agents.length === 0 ? (
        <div className="text-center py-12 bg-gray-50 rounded-lg">
          <p className="text-gray-500">暂无智能体，点击上方按钮创建</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {agents.map((agent) => (
            <div
              key={agent.id}
              className="bg-white border border-gray-200 rounded-lg p-4 hover:shadow-md transition-shadow"
            >
              <div className="flex justify-between items-start mb-2">
                <h3 className="font-semibold text-lg">{agent.name}</h3>
                <span className="text-xs bg-gray-100 px-2 py-1 rounded">
                  {agent.type}
                </span>
              </div>
              <p className="text-sm text-gray-600 mb-4">{agent.description}</p>
              <div className="flex justify-end space-x-2">
                <button 
                  onClick={() => handleEdit(agent.id)}
                  className="text-sm text-blue-600 hover:text-blue-700"
                >
                  编辑
                </button>
                <button
                  onClick={() => handleDelete(agent.id)}
                  className="text-sm text-red-600 hover:text-red-700"
                >
                  删除
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
      
      {showForm && (
        <AgentForm
          agentId={editingAgentId}
          onSuccess={handleFormSuccess}
          onCancel={handleCancelForm}
        />
      )}
    </div>
  );
}
