/**
 * 工作流画布组件
 * 基于 React Flow 实现的可视化工作流编辑器
 */

import React, { useCallback, useRef, useMemo, useEffect } from 'react';
import ReactFlow, {
  Controls,
  MiniMap,
  Background,
  BackgroundVariant,
  Panel,
  MarkerType,
} from 'reactflow';
import type {
  Connection,
  Edge,
  Node,
  NodeChange,
  EdgeChange,
  ReactFlowInstance,
} from 'reactflow';
import 'reactflow/dist/style.css';

import type { 
  ExecutorType,
  WorkflowNodeData,
} from '../../types/workflow';
import { useWorkflowStore, selectWorkflow, selectIsExecuting, selectIsDirty } from '../../store/workflowStore';
import { nodeTypes } from './nodes/ExecutorNode';
import ExecutorConfigModal from './modals/ExecutorConfigModal';

// ==================== 边样式 ====================

const defaultEdgeOptions = {
  type: 'smoothstep',
  animated: false,
  style: { stroke: '#94a3b8', strokeWidth: 2 },
  markerEnd: {
    type: MarkerType.ArrowClosed,
    color: '#94a3b8',
  },
};

// ==================== 工具栏组件 ====================

interface ToolbarProps {
  onSave: () => void;
  onExportJson: () => void;
  onExportYaml: () => void;
  onValidate: () => void;
  onClear: () => void;
  onUndo: () => void;
  onRedo: () => void;
  canUndo: boolean;
  canRedo: boolean;
  isDirty: boolean;
  isExecuting: boolean;
}

const Toolbar: React.FC<ToolbarProps> = ({
  onSave,
  onExportJson,
  onExportYaml,
  onValidate,
  onClear,
  onUndo,
  onRedo,
  canUndo,
  canRedo,
  isDirty,
  isExecuting,
}) => {
  const [showExportMenu, setShowExportMenu] = React.useState(false);
  const exportMenuRef = React.useRef<HTMLDivElement>(null);

  // 点击外部关闭菜单
  React.useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (exportMenuRef.current && !exportMenuRef.current.contains(event.target as globalThis.Node)) {
        setShowExportMenu(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  return (
    <Panel position="top-right" className="flex items-center gap-2 bg-white/90 backdrop-blur-sm p-2 rounded-lg shadow-lg border border-gray-200">
      {/* 撤销/重做 */}
      <div className="flex items-center gap-1 pr-2 border-r border-gray-200">
        <button
          onClick={onUndo}
          disabled={!canUndo}
          className="p-2 rounded-lg hover:bg-gray-100 disabled:opacity-50 disabled:cursor-not-allowed"
          title="撤销 (Ctrl+Z)"
        >
          <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6" />
          </svg>
        </button>
        <button
          onClick={onRedo}
          disabled={!canRedo}
          className="p-2 rounded-lg hover:bg-gray-100 disabled:opacity-50 disabled:cursor-not-allowed"
          title="重做 (Ctrl+Y)"
        >
          <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 10h-10a8 8 0 00-8 8v2M21 10l-6 6m6-6l-6-6" />
          </svg>
        </button>
      </div>

      {/* 保存 */}
      <button
        onClick={onSave}
        disabled={!isDirty || isExecuting}
        className={`
          flex items-center gap-1 px-3 py-2 rounded-lg transition-colors
          ${isDirty 
            ? 'bg-blue-600 text-white hover:bg-blue-700' 
            : 'bg-gray-100 text-gray-500'
          }
          disabled:opacity-50 disabled:cursor-not-allowed
        `}
        title="保存工作流"
      >
        <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7H5a2 2 0 00-2 2v9a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-3m-1 4l-3 3m0 0l-3-3m3 3V4" />
        </svg>
        保存
      </button>

      {/* 导出 */}
      <div className="relative" ref={exportMenuRef}>
        <button
          onClick={() => setShowExportMenu(!showExportMenu)}
          className="flex items-center gap-1 px-3 py-2 rounded-lg hover:bg-gray-100"
          title="导出"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
          </svg>
          导出
          <svg className={`w-3 h-3 transition-transform ${showExportMenu ? 'rotate-180' : ''}`} fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
          </svg>
        </button>
        {showExportMenu && (
          <div className="absolute right-0 top-full mt-1 bg-white rounded-lg shadow-lg border border-gray-200 py-1 min-w-[120px] z-50">
            <button
              onClick={() => { onExportJson(); setShowExportMenu(false); }}
              className="w-full px-4 py-2 text-left hover:bg-gray-100 text-sm"
            >
              导出 JSON
            </button>
            <button
              onClick={() => { onExportYaml(); setShowExportMenu(false); }}
              className="w-full px-4 py-2 text-left hover:bg-gray-100 text-sm"
            >
              导出 YAML
            </button>
          </div>
        )}
      </div>

      {/* 验证 */}
      <button
        onClick={onValidate}
        className="flex items-center gap-1 px-3 py-2 rounded-lg hover:bg-gray-100"
        title="验证工作流"
      >
        <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
        验证
      </button>

      {/* 清空 */}
      <button
        onClick={onClear}
        className="flex items-center gap-1 px-3 py-2 rounded-lg hover:bg-red-50 text-red-600"
        title="清空画布"
      >
        <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
        </svg>
      </button>
    </Panel>
  );
};

// ==================== 状态栏组件 ====================

interface StatusBarProps {
  nodeCount: number;
  edgeCount: number;
  isExecuting: boolean;
  workflowName: string;
}

const StatusBar: React.FC<StatusBarProps> = ({ nodeCount, edgeCount, isExecuting, workflowName }) => {
  return (
    <Panel position="bottom-left" className="flex items-center gap-4 bg-white/90 backdrop-blur-sm px-4 py-2 rounded-lg shadow-lg border border-gray-200 text-sm">
      <span className="font-medium text-gray-700">{workflowName}</span>
      <span className="text-gray-400">|</span>
      <span className="text-gray-600">{nodeCount} 节点</span>
      <span className="text-gray-600">{edgeCount} 连接</span>
      {isExecuting && (
        <>
          <span className="text-gray-400">|</span>
          <span className="flex items-center gap-1 text-green-600">
            <span className="w-2 h-2 bg-green-500 rounded-full animate-pulse"></span>
            执行中
          </span>
        </>
      )}
    </Panel>
  );
};

// ==================== 主画布组件 ====================

interface WorkflowCanvasProps {
  className?: string;
}

export const WorkflowCanvas: React.FC<WorkflowCanvasProps> = ({ className = '' }) => {
  const reactFlowWrapper = useRef<HTMLDivElement>(null);
  const [reactFlowInstance, setReactFlowInstance] = React.useState<ReactFlowInstance | null>(null);
  
  // Store 状态
  const workflow = useWorkflowStore(selectWorkflow);
  const isExecuting = useWorkflowStore(selectIsExecuting);
  const isDirty = useWorkflowStore(selectIsDirty);
  const showMinimap = useWorkflowStore((s) => s.showMinimap);
  const showGrid = useWorkflowStore((s) => s.showGrid);
  const selectedExecutorId = useWorkflowStore((s) => s.selectedExecutorId);
  const executorStates = useWorkflowStore((s) => s.executorStates);
  const isConfigModalOpen = useWorkflowStore((s) => s.isConfigModalOpen);
  const configModalExecutorId = useWorkflowStore((s) => s.configModalExecutorId);
  
  // Store 操作
  const addExecutor = useWorkflowStore((s) => s.addExecutor);
  const deleteExecutor = useWorkflowStore((s) => s.deleteExecutor);
  const moveExecutor = useWorkflowStore((s) => s.moveExecutor);
  const selectExecutor = useWorkflowStore((s) => s.selectExecutor);
  const addEdge = useWorkflowStore((s) => s.addEdge);
  const deleteEdge = useWorkflowStore((s) => s.deleteEdge);
  const saveWorkflow = useWorkflowStore((s) => s.saveWorkflow);
  const exportToJson = useWorkflowStore((s) => s.exportToJson);
  const exportToYaml = useWorkflowStore((s) => s.exportToYaml);
  const validateWorkflow = useWorkflowStore((s) => s.validateWorkflow);
  const clearWorkflow = useWorkflowStore((s) => s.clearWorkflow);
  const undo = useWorkflowStore((s) => s.undo);
  const redo = useWorkflowStore((s) => s.redo);
  const canUndo = useWorkflowStore((s) => s.canUndo);
  const canRedo = useWorkflowStore((s) => s.canRedo);
  const closeConfigModal = useWorkflowStore((s) => s.closeConfigModal);
  const openConfigModal = useWorkflowStore((s) => s.openConfigModal);
  const endDrag = useWorkflowStore((s) => s.endDrag);

  // 转换执行器为 React Flow 节点
  const nodes = useMemo((): Node<WorkflowNodeData>[] => {
    if (!workflow) return [];
    
    return workflow.executors.map((executor) => ({
      id: executor.id,
      type: 'executor',
      position: executor.position,
      selected: executor.id === selectedExecutorId,
      data: {
        executor,
        isSelected: executor.id === selectedExecutorId,
        isRunning: executorStates[executor.id] === 'running',
        hasError: executorStates[executor.id] === 'failed',
        onEdit: () => openConfigModal(executor.id),
        onDelete: () => deleteExecutor(executor.id),
      },
    }));
  }, [workflow, selectedExecutorId, executorStates, openConfigModal, deleteExecutor]);

  // 转换边组为 React Flow 边
  const edges = useMemo((): Edge[] => {
    if (!workflow) return [];
    
    const result: Edge[] = [];
    
    workflow.edgeGroups.forEach((group) => {
      group.edges.forEach((edge) => {
        const isConditionEdge = group.type === 'SwitchCase';
        const isTrueBranch = edge.label === 'true' || edge.condition?.includes('true');
        
        result.push({
          id: edge.id,
          source: group.sourceExecutorId,
          target: edge.targetExecutorId,
          sourceHandle: isConditionEdge ? (isTrueBranch ? 'true' : 'false') : undefined,
          type: 'smoothstep',
          animated: isExecuting && executorStates[group.sourceExecutorId] === 'running',
          style: {
            stroke: isConditionEdge 
              ? (isTrueBranch ? '#22c55e' : '#ef4444')
              : '#94a3b8',
            strokeWidth: 2,
          },
          label: edge.label,
          labelStyle: { fontSize: 12, fill: '#6b7280' },
          markerEnd: {
            type: MarkerType.ArrowClosed,
            color: isConditionEdge 
              ? (isTrueBranch ? '#22c55e' : '#ef4444')
              : '#94a3b8',
          },
        });
      });
    });
    
    return result;
  }, [workflow, isExecuting, executorStates]);

  // 处理节点变化
  const onNodesChange = useCallback(
    (changes: NodeChange[]) => {
      changes.forEach((change) => {
        if (change.type === 'position' && change.position) {
          moveExecutor(change.id, change.position);
        } else if (change.type === 'select') {
          if (change.selected) {
            selectExecutor(change.id);
          }
        }
      });
    },
    [moveExecutor, selectExecutor]
  );

  // 处理边变化
  const onEdgesChange = useCallback(
    (changes: EdgeChange[]) => {
      changes.forEach((change) => {
        if (change.type === 'remove') {
          deleteEdge(change.id);
        }
      });
    },
    [deleteEdge]
  );

  // 处理新连接
  const onConnect = useCallback(
    (connection: Connection) => {
      if (connection.source && connection.target) {
        const label = connection.sourceHandle || undefined;
        addEdge(connection.source, connection.target, undefined, label);
      }
    },
    [addEdge]
  );

  // 处理拖放
  const onDragOver = useCallback((event: React.DragEvent) => {
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
  }, []);

  const onDrop = useCallback(
    (event: React.DragEvent) => {
      event.preventDefault();
      
      const type = event.dataTransfer.getData('application/reactflow') as ExecutorType;
      if (!type || !reactFlowInstance || !reactFlowWrapper.current) {
        endDrag();
        return;
      }
      
      const bounds = reactFlowWrapper.current.getBoundingClientRect();
      const position = reactFlowInstance.project({
        x: event.clientX - bounds.left,
        y: event.clientY - bounds.top,
      });
      
      addExecutor(type, position);
      endDrag();
    },
    [reactFlowInstance, addExecutor, endDrag]
  );

  // 处理双击编辑
  const onNodeDoubleClick = useCallback(
    (_: React.MouseEvent, node: Node) => {
      openConfigModal(node.id);
    },
    [openConfigModal]
  );

  // 处理点击空白区域
  const onPaneClick = useCallback(() => {
    selectExecutor(null);
  }, [selectExecutor]);

  // 键盘快捷键
  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      // Ctrl+Z 撤销
      if (event.ctrlKey && event.key === 'z') {
        event.preventDefault();
        undo();
      }
      // Ctrl+Y 重做
      if (event.ctrlKey && event.key === 'y') {
        event.preventDefault();
        redo();
      }
      // Ctrl+S 保存
      if (event.ctrlKey && event.key === 's') {
        event.preventDefault();
        saveWorkflow();
      }
      // Delete 删除选中
      if (event.key === 'Delete' && selectedExecutorId) {
        event.preventDefault();
        deleteExecutor(selectedExecutorId);
      }
    };
    
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [undo, redo, saveWorkflow, selectedExecutorId, deleteExecutor]);

  // 工具栏回调
  const handleSave = useCallback(async () => {
    await saveWorkflow();
  }, [saveWorkflow]);

  const handleExportJson = useCallback(() => {
    const json = exportToJson();
    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${workflow?.name || 'workflow'}.json`;
    a.click();
    URL.revokeObjectURL(url);
  }, [exportToJson, workflow?.name]);

  const handleExportYaml = useCallback(async () => {
    const yaml = await exportToYaml();
    if (yaml) {
      const blob = new Blob([yaml], { type: 'text/yaml' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `${workflow?.name || 'workflow'}.yaml`;
      a.click();
      URL.revokeObjectURL(url);
    }
  }, [exportToYaml, workflow?.name]);

  const handleValidate = useCallback(() => {
    const result = validateWorkflow();
    if (result.isValid) {
      alert('✅ 工作流验证通过！');
    } else {
      const messages = result.errors.map((e) => `❌ ${e.message}`).join('\n');
      alert(`工作流验证失败:\n\n${messages}`);
    }
  }, [validateWorkflow]);

  const handleClear = useCallback(() => {
    if (confirm('确定要清空画布吗？此操作不可恢复。')) {
      clearWorkflow();
    }
  }, [clearWorkflow]);

  return (
    <div ref={reactFlowWrapper} className={`flex-1 h-full ${className}`}>
      <ReactFlow
        nodes={nodes}
        edges={edges}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        onConnect={onConnect}
        onInit={setReactFlowInstance}
        onDragOver={onDragOver}
        onDrop={onDrop}
        onNodeDoubleClick={onNodeDoubleClick}
        onPaneClick={onPaneClick}
        nodeTypes={nodeTypes}
        defaultEdgeOptions={defaultEdgeOptions}
        fitView
        fitViewOptions={{ padding: 0.2 }}
        snapToGrid
        snapGrid={[15, 15]}
        deleteKeyCode="Delete"
        selectionKeyCode="Shift"
        multiSelectionKeyCode="Control"
        className="bg-gray-50"
      >
        <Controls className="bg-white shadow-lg rounded-lg" />
        
        {showMinimap && (
          <MiniMap 
            nodeColor={(node) => {
              const data = node.data as WorkflowNodeData;
              if (data?.isRunning) return '#22c55e';
              if (data?.hasError) return '#ef4444';
              return '#94a3b8';
            }}
            className="bg-white shadow-lg rounded-lg"
          />
        )}
        
        {showGrid && (
          <Background 
            variant={BackgroundVariant.Dots} 
            gap={15} 
            size={1} 
            color="#d1d5db" 
          />
        )}
        
        <Toolbar
          onSave={handleSave}
          onExportJson={handleExportJson}
          onExportYaml={handleExportYaml}
          onValidate={handleValidate}
          onClear={handleClear}
          onUndo={undo}
          onRedo={redo}
          canUndo={canUndo()}
          canRedo={canRedo()}
          isDirty={isDirty}
          isExecuting={isExecuting}
        />
        
        <StatusBar
          nodeCount={nodes.length}
          edgeCount={edges.length}
          isExecuting={isExecuting}
          workflowName={workflow?.name || '未命名工作流'}
        />
      </ReactFlow>
      
      {/* 配置模态框 */}
      <ExecutorConfigModal
        isOpen={isConfigModalOpen}
        executorId={configModalExecutorId}
        onClose={closeConfigModal}
      />
    </div>
  );
};

export default WorkflowCanvas;
