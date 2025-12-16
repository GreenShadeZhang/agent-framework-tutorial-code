import { useEffect, useState } from 'react';
import { api } from '../api/client';
import { useToastStore } from '../store/toastStore';

interface Workflow {
  id: string;
  name: string;
  description: string;
  version: string;
  isPublished: boolean;
  createdAt: string;
  updatedAt: string;
}

interface WorkflowListProps {
  onLoadWorkflow: (workflowId: string) => void;
}

export default function WorkflowList({ onLoadWorkflow }: WorkflowListProps) {
  const [workflows, setWorkflows] = useState<Workflow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const { showToast } = useToastStore();

  useEffect(() => {
    loadWorkflows();
  }, []);

  const loadWorkflows = async () => {
    try {
      setLoading(true);
      const data = await api.getWorkflows();
      setWorkflows(data);
      setError(null);
    } catch (err: any) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async (id: string, name: string) => {
    if (!confirm(`确定要删除工作流"${name}"吗？`)) return;

    try {
      await api.deleteWorkflow(id);
      setWorkflows(workflows.filter(w => w.id !== id));
      showToast('工作流删除成功', 'success');
    } catch (err: any) {
      setError(err.message);
      showToast('删除失败: ' + err.message, 'error');
      setError(err.message);
    }
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
          onClick={loadWorkflows}
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
        <h2 className="text-xl font-bold">工作流列表</h2>
      </div>

      {workflows.length === 0 ? (
        <div className="text-center py-12 bg-gray-50 rounded-lg">
          <p className="text-gray-500">暂无工作流，开始设计新的工作流吧</p>
        </div>
      ) : (
        <div className="space-y-3">
          {workflows.map((workflow) => (
            <div
              key={workflow.id}
              className="bg-white border border-gray-200 rounded-lg p-4 hover:shadow-md transition-shadow"
            >
              <div className="flex justify-between items-start">
                <div className="flex-1">
                  <div className="flex items-center gap-2 mb-1">
                    <h3 className="font-semibold text-lg">{workflow.name}</h3>
                    {workflow.isPublished && (
                      <span className="text-xs bg-green-100 text-green-800 px-2 py-1 rounded">
                        已发布
                      </span>
                    )}
                    <span className="text-xs bg-gray-100 px-2 py-1 rounded">
                      v{workflow.version}
                    </span>
                  </div>
                  <p className="text-sm text-gray-600 mb-2">{workflow.description}</p>
                  <div className="text-xs text-gray-500">
                    创建于: {new Date(workflow.createdAt).toLocaleString()}
                  </div>
                </div>
                <div className="flex gap-2 ml-4">
                  <button
                    onClick={() => onLoadWorkflow(workflow.id)}
                    className="text-sm text-blue-600 hover:text-blue-700 px-3 py-1 border border-blue-600 rounded hover:bg-blue-50"
                  >
                    加载
                  </button>
                  <button
                    onClick={() => handleDelete(workflow.id, workflow.name)}
                    className="text-sm text-red-600 hover:text-red-700 px-3 py-1 border border-red-600 rounded hover:bg-red-50"
                  >
                    删除
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
