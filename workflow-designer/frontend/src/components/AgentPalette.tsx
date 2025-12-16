import { useEffect, useState } from 'react';
import { useAppStore } from '../store/appStore';
import { api } from '../api/client';

interface AgentPaletteProps {
  onDragStart: (event: React.DragEvent, agentId: string, agentData: any) => void;
}

export default function AgentPalette({ onDragStart }: AgentPaletteProps) {
  const { agents, setAgents } = useAppStore();
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    loadAgents();
  }, []);

  const loadAgents = async () => {
    try {
      setLoading(true);
      const data = await api.getAgents();
      setAgents(data);
    } catch (error) {
      console.error('Failed to load agents:', error);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="w-64 bg-white border-r border-gray-200 p-4 overflow-y-auto">
      <h3 className="font-bold text-lg mb-4">组件库</h3>
      
      <div className="space-y-4">
        <div>
          <h4 className="text-sm font-semibold text-gray-700 mb-2">智能体</h4>
          {loading ? (
            <div className="text-sm text-gray-500">加载中...</div>
          ) : agents.length === 0 ? (
            <div className="text-sm text-gray-500">暂无智能体</div>
          ) : (
            <div className="space-y-2">
              {agents.map((agent) => (
                <div
                  key={agent.id}
                  draggable
                  onDragStart={(e) => onDragStart(e, agent.id, {
                    agentId: agent.id,
                    agentName: agent.name,
                    agentType: agent.type,
                    description: agent.description
                  })}
                  className="p-3 bg-blue-50 border border-blue-200 rounded cursor-move hover:bg-blue-100 transition-colors"
                >
                  <div className="flex items-center gap-2">
                    <span className="text-lg">
                      {agent.type === 'Coder' ? '💻' : agent.type === 'WebSurfer' ? '🌐' : '🤖'}
                    </span>
                    <div className="flex-1 min-w-0">
                      <div className="font-medium text-sm truncate">{agent.name}</div>
                      <div className="text-xs text-gray-600">{agent.type}</div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        <div>
          <h4 className="text-sm font-semibold text-gray-700 mb-2">控制节点</h4>
          <div className="space-y-2">
            <div
              draggable
              onDragStart={(e) => {
                e.dataTransfer.setData('application/reactflow', 'condition');
                e.dataTransfer.effectAllowed = 'move';
              }}
              className="p-3 bg-yellow-50 border border-yellow-200 rounded cursor-move hover:bg-yellow-100 transition-colors"
            >
              <div className="flex items-center gap-2">
                <span className="text-lg">🔀</span>
                <div className="font-medium text-sm">条件判断</div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
