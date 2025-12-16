import { useCallback, useRef, useState } from 'react';
import ReactFlow, {
  MiniMap,
  Controls,
  Background,
  useNodesState,
  useEdgesState,
  addEdge,
  BackgroundVariant,
  type Connection,
  type Edge,
  type Node,
  type NodeTypes,
} from 'reactflow';
import 'reactflow/dist/style.css';
import AgentNode from './nodes/AgentNode';
import ConditionNode from './nodes/ConditionNode';
import AgentPalette from './AgentPalette';
import ExecutionPanel from './ExecutionPanel';
import WorkflowList from './WorkflowList';
import ConditionNodeConfig from './ConditionNodeConfig';
import AgentNodeConfig from './AgentNodeConfig';
import { api } from '../api/client';
import { useToastStore } from '../store/toastStore';

const nodeTypes: NodeTypes = {
  agent: AgentNode,
  condition: ConditionNode,
};

const initialNodes: Node[] = [
  {
    id: 'start-1',
    type: 'input',
    data: { label: '开始' },
    position: { x: 250, y: 25 },
  },
];

const initialEdges: Edge[] = [];

let nodeId = 1;

export default function WorkflowCanvas() {
  const reactFlowWrapper = useRef<HTMLDivElement>(null);
  const [nodes, setNodes, onNodesChange] = useNodesState(initialNodes);
  const [edges, setEdges, onEdgesChange] = useEdgesState(initialEdges);
  const [reactFlowInstance, setReactFlowInstance] = useState<any>(null);
  const [saving, setSaving] = useState(false);
  const [currentWorkflowId, setCurrentWorkflowId] = useState<string | null>(null);
  const [currentWorkflowName, setCurrentWorkflowName] = useState<string>('未命名工作流');
  const [showExecutionPanel, setShowExecutionPanel] = useState(false);
  const [showWorkflowList, setShowWorkflowList] = useState(false);
  const [editingName, setEditingName] = useState(false);
  const [tempName, setTempName] = useState('');
  const [editingConditionNode, setEditingConditionNode] = useState<string | null>(null);
  const [editingAgentNode, setEditingAgentNode] = useState<string | null>(null);
  const [showYamlPreview, setShowYamlPreview] = useState(false);
  const [yamlContent, setYamlContent] = useState('');
  const { showToast } = useToastStore();

  const onConnect = useCallback(
    (params: Connection | Edge) => setEdges((eds) => addEdge(params, eds)),
    [setEdges]
  );

  const onDragOver = useCallback((event: React.DragEvent) => {
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
  }, []);

  const onDrop = useCallback(
    (event: React.DragEvent) => {
      event.preventDefault();

      if (!reactFlowWrapper.current || !reactFlowInstance) return;

      const reactFlowBounds = reactFlowWrapper.current.getBoundingClientRect();
      const type = event.dataTransfer.getData('application/reactflow');

      // 如果是从 AgentPalette 拖拽的
      const agentData = event.dataTransfer.getData('agent-data');
      let nodeData: any = {};
      let nodeType = 'agent';

      if (agentData) {
        nodeData = JSON.parse(agentData);
        nodeType = 'agent';
      } else if (type === 'condition') {
        nodeType = 'condition';
        nodeData = { 
          condition: '', 
          label: '条件判断',
          onEdit: () => {} // Will be set after node creation
        };
      } else {
        return;
      }

      const position = reactFlowInstance.project({
        x: event.clientX - reactFlowBounds.left,
        y: event.clientY - reactFlowBounds.top,
      });

      const newNodeId = `${nodeType}-${nodeId++}`;
      const newNode: Node = {
        id: newNodeId,
        type: nodeType,
        position,
        data: nodeType === 'condition' 
          ? { ...nodeData, onEdit: () => setEditingConditionNode(newNodeId) }
          : nodeType === 'agent'
          ? { ...nodeData, onEdit: () => setEditingAgentNode(newNodeId) }
          : nodeData,
      };

      setNodes((nds) => nds.concat(newNode));
    },
    [reactFlowInstance, setNodes]
  );

  const handleDragStart = (
    event: React.DragEvent,
    _agentId: string,
    agentData: any
  ) => {
    event.dataTransfer.setData('application/reactflow', 'agent');
    event.dataTransfer.setData('agent-data', JSON.stringify(agentData));
    event.dataTransfer.effectAllowed = 'move';
  };

  const onSave = async () => {
    try {
      setSaving(true);

      // 映射 React Flow 节点类型到后端枚举
      const mapNodeType = (type: string | undefined): string => {
        switch (type) {
          case 'input':
            return 'Start';
          case 'agent':
            return 'Agent';
          case 'condition':
            return 'Condition';
          case 'output':
            return 'End';
          default:
            return 'Agent';
        }
      };

      const workflowData = {
        name: currentWorkflowName === '未命名工作流' 
          ? '工作流-' + new Date().toISOString() 
          : currentWorkflowName,
        description: '通过可视化设计器创建的工作流',
        version: '1.0.0',
        nodes: nodes.map((node) => ({
          id: node.id,
          type: mapNodeType(node.type),
          position: node.position,
          data: node.data,
        })),
        edges: edges.map((edge) => ({
          id: edge.id,
          source: edge.source,
          target: edge.target,
          type: 'Direct',
          condition: null,
        })),
        parameters: [],
        yamlContent: '',
        workflowDump: JSON.stringify({ nodes, edges }),
        metadata: {},
        isPublished: false,
      };

      if (currentWorkflowId) {
        // 更新现有工作流
        const response = await api.updateWorkflow(currentWorkflowId, workflowData);
        setCurrentWorkflowName(response.name);
      } else {
        // 创建新工作流
        const response = await api.createWorkflow(workflowData);
        setCurrentWorkflowId(response.id);
        setCurrentWorkflowName(response.name);
      }
      showToast('工作流保存成功！', 'success');
    } catch (error: any) {
      console.error('Failed to save workflow:', error);
      showToast('保存失败: ' + error.message, 'error');
    } finally {
      setSaving(false);
    }
  };

  const loadWorkflow = async (workflowId: string) => {
    try {
      const workflow = await api.getWorkflow(workflowId);
      
      // 解析 workflowDump 恢复画布状态
      if (workflow.workflowDump) {
        const dump = JSON.parse(workflow.workflowDump);
        // 为节点添加编辑处理器
        const nodesWithHandlers = (dump.nodes || []).map((node: Node) => {
          if (node.type === 'condition') {
            return {
              ...node,
              data: {
                ...node.data,
                onEdit: () => setEditingConditionNode(node.id),
              },
            };
          } else if (node.type === 'agent') {
            return {
              ...node,
              data: {
                ...node.data,
                onEdit: () => setEditingAgentNode(node.id),
              },
            };
          }
          return node;
        });
        setNodes(nodesWithHandlers);
        setEdges(dump.edges || []);
      }
      
      setCurrentWorkflowId(workflow.id);
      setCurrentWorkflowName(workflow.name);
      setShowWorkflowList(false);
      showToast('工作流加载成功！', 'success');
    } catch (error: any) {
      console.error('Failed to load workflow:', error);
      showToast('加载失败: ' + error.message, 'error');
    }
  };

  const createNewWorkflow = () => {
    setNodes(initialNodes);
    setEdges(initialEdges);
    setCurrentWorkflowId(null);
    setCurrentWorkflowName('未命名工作流');
  };

  const handleStartEdit = () => {
    setTempName(currentWorkflowName);
    setEditingName(true);
  };

  const handleSaveName = () => {
    if (tempName.trim()) {
      setCurrentWorkflowName(tempName.trim());
    }
    setEditingName(false);
  };

  const handleCancelEdit = () => {
    setTempName('');
    setEditingName(false);
  };

  const handleSaveCondition = (data: { condition: string; label: string }) => {
    if (!editingConditionNode) return;
    
    setNodes((nds) =>
      nds.map((node) => {
        if (node.id === editingConditionNode) {
          return {
            ...node,
            data: {
              ...node.data,
              condition: data.condition,
              label: data.label,
              onEdit: () => setEditingConditionNode(node.id),
            },
          };
        }
        return node;
      })
    );
    
    setEditingConditionNode(null);
    showToast('条件配置已保存', 'success');
  };

  const handleSaveAgent = (data: any) => {
    if (!editingAgentNode) return;
    
    setNodes((nds) =>
      nds.map((node) => {
        if (node.id === editingAgentNode) {
          return {
            ...node,
            data: {
              ...data,
              onEdit: () => setEditingAgentNode(node.id),
              // 兼容旧字段名
              agentId: data.id,
              agentName: data.name,
              agentType: data.type,
            },
          };
        }
        return node;
      })
    );
    
    setEditingAgentNode(null);
    showToast('智能体节点配置已保存', 'success');
  };

  const onExecute = async () => {
    if (!currentWorkflowId) {
      // 如果工作流未保存，先保存
      await onSave();
      if (!currentWorkflowId) return; // 保存失败
    }
    
    setShowExecutionPanel(true);
  };

  const handleExportYaml = async () => {
    if (!currentWorkflowId) {
      showToast('请先保存工作流', 'error');
      return;
    }

    try {
      const response = await api.exportWorkflowYaml(currentWorkflowId);
      setYamlContent(response.yaml);
      setShowYamlPreview(true);
    } catch (error: any) {
      showToast('导出失败: ' + error.message, 'error');
    }
  };

  const handleDownloadYaml = () => {
    if (!currentWorkflowId) {
      showToast('请先保存工作流', 'error');
      return;
    }
    
    api.downloadWorkflowYaml(currentWorkflowId);
    showToast('YAML 文件下载已开始', 'success');
  };

  return (
    <div className="h-screen flex">
      <AgentPalette onDragStart={handleDragStart} />
      
      <div className="flex-1 flex flex-col">
        <div className="bg-white border-b border-gray-200 p-4 flex justify-between items-center">
          <div className="flex items-center gap-4">
            <h1 className="text-xl font-bold">工作流设计器</h1>
            {editingName ? (
              <div className="flex items-center gap-2">
                <input
                  type="text"
                  value={tempName}
                  onChange={(e) => setTempName(e.target.value)}
                  onKeyPress={(e) => {
                    if (e.key === 'Enter') handleSaveName();
                    if (e.key === 'Escape') handleCancelEdit();
                  }}
                  className="px-2 py-1 border border-blue-500 rounded focus:outline-none"
                  autoFocus
                />
                <button
                  onClick={handleSaveName}
                  className="text-green-600 hover:text-green-700 text-sm"
                >
                  ✓
                </button>
                <button
                  onClick={handleCancelEdit}
                  className="text-red-600 hover:text-red-700 text-sm"
                >
                  ✗
                </button>
              </div>
            ) : (
              <span 
                className="text-sm text-gray-600 cursor-pointer hover:text-blue-600 flex items-center gap-2"
                onClick={handleStartEdit}
              >
                {currentWorkflowName}
                <span className="text-xs text-gray-400">✎</span>
                {currentWorkflowId && <span className="text-xs text-green-600">✓ 已保存</span>}
              </span>
            )}
          </div>
          <div className="space-x-2">
            <button
              onClick={createNewWorkflow}
              className="bg-gray-500 hover:bg-gray-600 text-white px-4 py-2 rounded"
            >
              新建
            </button>
            <button
              onClick={() => setShowWorkflowList(true)}
              className="bg-gray-500 hover:bg-gray-600 text-white px-4 py-2 rounded"
            >
              加载
            </button>
            <button
              onClick={onSave}
              disabled={saving}
              className="bg-green-500 hover:bg-green-600 text-white px-4 py-2 rounded disabled:opacity-50"
            >
              {saving ? '保存中...' : '保存'}
            </button>
            <button
              onClick={handleExportYaml}
              className="bg-purple-500 hover:bg-purple-600 text-white px-4 py-2 rounded"
              disabled={!currentWorkflowId}
              title="导出为 Agent Framework YAML"
            >
              导出YAML
            </button>
            <button
              onClick={onExecute}
              className="bg-blue-500 hover:bg-blue-600 text-white px-4 py-2 rounded"
            >
              执行
            </button>
          </div>
        </div>

        <div className="flex-1" ref={reactFlowWrapper}>
          <ReactFlow
            nodes={nodes}
            edges={edges}
            onNodesChange={onNodesChange}
            onEdgesChange={onEdgesChange}
            onConnect={onConnect}
            onInit={setReactFlowInstance}
            onDrop={onDrop}
            onDragOver={onDragOver}
            nodeTypes={nodeTypes}
            fitView
          >
            <Controls />
            <MiniMap />
            <Background variant={BackgroundVariant.Dots} gap={12} size={1} />
          </ReactFlow>
        </div>
      </div>

      {showExecutionPanel && currentWorkflowId && (
        <ExecutionPanel
          workflowId={currentWorkflowId}
          parameters={{}}
          onClose={() => setShowExecutionPanel(false)}
        />
      )}

      {showWorkflowList && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg shadow-xl max-w-4xl w-full max-h-[80vh] overflow-y-auto p-6">
            <div className="flex justify-between items-center mb-4">
              <h2 className="text-2xl font-bold">选择工作流</h2>
              <button
                onClick={() => setShowWorkflowList(false)}
                className="text-gray-500 hover:text-gray-700 text-2xl"
              >
                ×
              </button>
            </div>
            <WorkflowList onLoadWorkflow={loadWorkflow} />
          </div>
        </div>
      )}

      {editingConditionNode && (
        <ConditionNodeConfig
          nodeId={editingConditionNode}
          initialData={
            nodes.find((n) => n.id === editingConditionNode)?.data || {
              condition: '',
              label: '条件判断',
            }
          }
          onSave={handleSaveCondition}
          onCancel={() => setEditingConditionNode(null)}
        />
      )}

      {editingAgentNode && (
        <AgentNodeConfig
          nodeId={editingAgentNode}
          initialData={
            nodes.find((n) => n.id === editingAgentNode)?.data || {}
          }
          onSave={handleSaveAgent}
          onCancel={() => setEditingAgentNode(null)}
        />
      )}

      {showYamlPreview && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg shadow-xl max-w-4xl w-full mx-4 max-h-[80vh] flex flex-col">
            <div className="flex items-center justify-between p-4 border-b">
              <h3 className="text-lg font-semibold">Agent Framework YAML 预览</h3>
              <button
                onClick={() => setShowYamlPreview(false)}
                className="text-gray-400 hover:text-gray-600"
              >
                ✕
              </button>
            </div>
            <div className="flex-1 overflow-auto p-4">
              <pre className="bg-gray-50 p-4 rounded text-sm font-mono overflow-x-auto whitespace-pre">
                {yamlContent}
              </pre>
            </div>
            <div className="flex gap-2 p-4 border-t">
              <button
                onClick={() => {
                  navigator.clipboard.writeText(yamlContent);
                  showToast('YAML 已复制到剪贴板', 'success');
                }}
                className="bg-blue-500 hover:bg-blue-600 text-white px-4 py-2 rounded"
              >
                复制到剪贴板
              </button>
              <button
                onClick={handleDownloadYaml}
                className="bg-green-500 hover:bg-green-600 text-white px-4 py-2 rounded"
              >
                下载 YAML 文件
              </button>
              <button
                onClick={() => setShowYamlPreview(false)}
                className="bg-gray-500 hover:bg-gray-600 text-white px-4 py-2 rounded ml-auto"
              >
                关闭
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
