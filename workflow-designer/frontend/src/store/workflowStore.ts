/**
 * 工作流状态管理 Store
 * 基于 Zustand 实现，参考 AutoGen Studio 模式
 */

import { create } from 'zustand';
import { subscribeWithSelector } from 'zustand/middleware';
import type { 
  DeclarativeWorkflowDefinition,
  ExecutorDefinition,
  EdgeGroupDefinition,
  EdgeDefinition,
  VariableDefinition,
  ExecutorType,
  EdgeGroupType,
  ExecutionEvent,
  ExecutorState,
  Position,
} from '../types/workflow';
import { createExecutorDefinition } from '../types/workflow';

// ==================== 状态类型 ====================

interface HistoryEntry {
  workflow: DeclarativeWorkflowDefinition;
  timestamp: number;
  description: string;
}

interface WorkflowState {
  // 当前工作流
  workflow: DeclarativeWorkflowDefinition | null;
  
  // 选中状态
  selectedExecutorId: string | null;
  selectedEdgeId: string | null;
  
  // 历史记录 (用于撤销/重做)
  history: HistoryEntry[];
  historyIndex: number;
  maxHistorySize: number;
  
  // 执行状态
  isExecuting: boolean;
  executorStates: Record<string, ExecutorState>;
  executionEvents: ExecutionEvent[];
  
  // 拖拽状态
  isDragging: boolean;
  draggedType: ExecutorType | null;
  
  // 视图状态
  zoom: number;
  panPosition: Position;
  showMinimap: boolean;
  showGrid: boolean;
  
  // 编辑器状态
  isDirty: boolean;
  lastSavedAt: string | null;
  
  // 模态框状态
  isConfigModalOpen: boolean;
  configModalExecutorId: string | null;
  isVariableModalOpen: boolean;
}

interface WorkflowActions {
  // 工作流管理
  createNewWorkflow: (name: string, description?: string) => void;
  loadWorkflow: (workflow: DeclarativeWorkflowDefinition) => void;
  saveWorkflow: () => Promise<boolean>;
  clearWorkflow: () => void;
  
  // 执行器管理
  addExecutor: (type: ExecutorType, position: Position, name?: string) => string;
  updateExecutor: (id: string, updates: Partial<ExecutorDefinition>) => void;
  deleteExecutor: (id: string) => void;
  duplicateExecutor: (id: string) => string | null;
  moveExecutor: (id: string, position: Position) => void;
  
  // 边管理
  addEdge: (sourceId: string, targetId: string, condition?: string, label?: string) => string | null;
  updateEdge: (edgeId: string, updates: Partial<EdgeDefinition>) => void;
  deleteEdge: (edgeId: string) => void;
  
  // 边组管理
  createEdgeGroup: (sourceId: string, type: EdgeGroupType) => string;
  updateEdgeGroup: (groupId: string, updates: Partial<EdgeGroupDefinition>) => void;
  deleteEdgeGroup: (groupId: string) => void;
  
  // 变量管理
  addVariable: (variable: VariableDefinition) => void;
  updateVariable: (name: string, updates: Partial<VariableDefinition>) => void;
  deleteVariable: (name: string) => void;
  
  // 选择管理
  selectExecutor: (id: string | null) => void;
  selectEdge: (id: string | null) => void;
  clearSelection: () => void;
  
  // 历史管理
  pushHistory: (description: string) => void;
  undo: () => void;
  redo: () => void;
  canUndo: () => boolean;
  canRedo: () => boolean;
  
  // 执行状态管理
  startExecution: () => void;
  stopExecution: () => void;
  setExecutorState: (id: string, state: ExecutorState) => void;
  addExecutionEvent: (event: ExecutionEvent) => void;
  clearExecutionState: () => void;
  
  // 拖拽管理
  startDrag: (type: ExecutorType) => void;
  endDrag: () => void;
  
  // 视图管理
  setZoom: (zoom: number) => void;
  setPanPosition: (position: Position) => void;
  toggleMinimap: () => void;
  toggleGrid: () => void;
  fitView: () => void;
  
  // 模态框管理
  openConfigModal: (executorId: string) => void;
  closeConfigModal: () => void;
  openVariableModal: () => void;
  closeVariableModal: () => void;
  
  // 导入导出
  exportToJson: () => string;
  importFromJson: (json: string) => boolean;
  exportToYaml: () => Promise<string>;
  importFromYaml: (yaml: string) => Promise<boolean>;
  
  // 辅助方法
  getExecutor: (id: string) => ExecutorDefinition | undefined;
  getExecutorsByType: (type: ExecutorType) => ExecutorDefinition[];
  getConnectedExecutors: (id: string) => ExecutorDefinition[];
  getIncomingEdges: (executorId: string) => EdgeDefinition[];
  getOutgoingEdges: (executorId: string) => EdgeDefinition[];
  validateWorkflow: () => ValidationResult;
}

interface ValidationResult {
  isValid: boolean;
  errors: ValidationError[];
  warnings: ValidationWarning[];
}

interface ValidationError {
  type: 'missing_start' | 'orphan_node' | 'cycle' | 'missing_config' | 'invalid_connection';
  message: string;
  executorId?: string;
  edgeId?: string;
}

interface ValidationWarning {
  type: 'unreachable' | 'no_end' | 'empty_config';
  message: string;
  executorId?: string;
}

// ==================== 默认值 ====================

const defaultWorkflow: DeclarativeWorkflowDefinition = {
  id: crypto.randomUUID(),
  name: '新工作流',
  description: '',
  version: '1.0.0',
  startExecutorId: '',
  maxIterations: 100,
  executors: [],
  edgeGroups: [],
  inputSpec: {
    typeName: 'WorkflowInput',
    schema: { type: 'object', properties: {} }
  },
  outputSpec: {
    typeName: 'WorkflowOutput', 
    schema: { type: 'object', properties: {} }
  },
  variables: [],
  metadata: {},
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
};

const initialState: WorkflowState = {
  workflow: null,
  selectedExecutorId: null,
  selectedEdgeId: null,
  history: [],
  historyIndex: -1,
  maxHistorySize: 50,
  isExecuting: false,
  executorStates: {},
  executionEvents: [],
  isDragging: false,
  draggedType: null,
  zoom: 1,
  panPosition: { x: 0, y: 0 },
  showMinimap: true,
  showGrid: true,
  isDirty: false,
  lastSavedAt: null,
  isConfigModalOpen: false,
  configModalExecutorId: null,
  isVariableModalOpen: false,
};

// ==================== Store 创建 ====================

export const useWorkflowStore = create<WorkflowState & WorkflowActions>()(
  subscribeWithSelector((set, get) => ({
    ...initialState,

    // ==================== 工作流管理 ====================
    
    createNewWorkflow: (name, description) => {
      const workflow: DeclarativeWorkflowDefinition = {
        ...defaultWorkflow,
        id: crypto.randomUUID(),
        name,
        description: description || '',
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      };
      
      set({
        workflow,
        selectedExecutorId: null,
        selectedEdgeId: null,
        history: [],
        historyIndex: -1,
        isDirty: false,
        executorStates: {},
        executionEvents: [],
      });
      
      get().pushHistory('创建新工作流');
    },

    loadWorkflow: (workflow) => {
      set({
        workflow,
        selectedExecutorId: null,
        selectedEdgeId: null,
        history: [],
        historyIndex: -1,
        isDirty: false,
        executorStates: {},
        executionEvents: [],
      });
      
      get().pushHistory('加载工作流');
    },

    saveWorkflow: async () => {
      const { workflow } = get();
      if (!workflow) return false;
      
      try {
        const updatedWorkflow = {
          ...workflow,
          updatedAt: new Date().toISOString(),
        };
        
        // TODO: 调用 API 保存
        const response = await fetch('/api/workflows', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(updatedWorkflow),
        });
        
        if (response.ok) {
          set({
            workflow: updatedWorkflow,
            isDirty: false,
            lastSavedAt: new Date().toISOString(),
          });
          return true;
        }
        return false;
      } catch (error) {
        console.error('保存工作流失败:', error);
        return false;
      }
    },

    clearWorkflow: () => {
      set(initialState);
    },

    // ==================== 执行器管理 ====================

    addExecutor: (type, position, name) => {
      const { workflow } = get();
      if (!workflow) return '';
      
      const executor = createExecutorDefinition(type, position, name);
      
      const updatedWorkflow = {
        ...workflow,
        executors: [...workflow.executors, executor],
        startExecutorId: workflow.executors.length === 0 ? executor.id : workflow.startExecutorId,
        updatedAt: new Date().toISOString(),
      };
      
      set({
        workflow: updatedWorkflow,
        selectedExecutorId: executor.id,
        isDirty: true,
      });
      
      get().pushHistory(`添加 ${executor.name}`);
      
      return executor.id;
    },

    updateExecutor: (id, updates) => {
      const { workflow } = get();
      if (!workflow) return;
      
      const updatedExecutors = workflow.executors.map(e =>
        e.id === id ? { ...e, ...updates } : e
      );
      
      set({
        workflow: {
          ...workflow,
          executors: updatedExecutors,
          updatedAt: new Date().toISOString(),
        },
        isDirty: true,
      });
      
      get().pushHistory(`更新执行器`);
    },

    deleteExecutor: (id) => {
      const { workflow } = get();
      if (!workflow) return;
      
      const executor = workflow.executors.find(e => e.id === id);
      if (!executor) return;
      
      // 删除相关的边组
      const updatedEdgeGroups = workflow.edgeGroups
        .filter(g => g.sourceExecutorId !== id)
        .map(g => ({
          ...g,
          edges: g.edges.filter(e => e.targetExecutorId !== id),
        }));
      
      const updatedWorkflow = {
        ...workflow,
        executors: workflow.executors.filter(e => e.id !== id),
        edgeGroups: updatedEdgeGroups,
        startExecutorId: workflow.startExecutorId === id ? '' : workflow.startExecutorId,
        updatedAt: new Date().toISOString(),
      };
      
      set({
        workflow: updatedWorkflow,
        selectedExecutorId: null,
        isDirty: true,
      });
      
      get().pushHistory(`删除 ${executor.name}`);
    },

    duplicateExecutor: (id) => {
      const { workflow } = get();
      if (!workflow) return null;
      
      const executor = workflow.executors.find(e => e.id === id);
      if (!executor) return null;
      
      const newExecutor: ExecutorDefinition = {
        ...executor,
        id: crypto.randomUUID(),
        name: `${executor.name} (副本)`,
        position: {
          x: executor.position.x + 50,
          y: executor.position.y + 50,
        },
      };
      
      set({
        workflow: {
          ...workflow,
          executors: [...workflow.executors, newExecutor],
          updatedAt: new Date().toISOString(),
        },
        selectedExecutorId: newExecutor.id,
        isDirty: true,
      });
      
      get().pushHistory(`复制 ${executor.name}`);
      
      return newExecutor.id;
    },

    moveExecutor: (id, position) => {
      const { workflow } = get();
      if (!workflow) return;
      
      const updatedExecutors = workflow.executors.map(e =>
        e.id === id ? { ...e, position } : e
      );
      
      set({
        workflow: {
          ...workflow,
          executors: updatedExecutors,
        },
        isDirty: true,
      });
    },

    // ==================== 边管理 ====================

    addEdge: (sourceId, targetId, condition, label) => {
      const { workflow } = get();
      if (!workflow) return null;
      
      // 检查是否已存在边组
      let edgeGroup = workflow.edgeGroups.find(g => g.sourceExecutorId === sourceId);
      
      const newEdge: EdgeDefinition = {
        id: crypto.randomUUID(),
        targetExecutorId: targetId,
        condition,
        label,
      };
      
      let updatedEdgeGroups: EdgeGroupDefinition[];
      
      if (edgeGroup) {
        // 添加到现有边组
        updatedEdgeGroups = workflow.edgeGroups.map(g =>
          g.id === edgeGroup!.id
            ? { ...g, edges: [...g.edges, newEdge] }
            : g
        );
      } else {
        // 创建新边组
        const newEdgeGroup: EdgeGroupDefinition = {
          id: crypto.randomUUID(),
          type: 'Single',
          sourceExecutorId: sourceId,
          edges: [newEdge],
        };
        updatedEdgeGroups = [...workflow.edgeGroups, newEdgeGroup];
      }
      
      set({
        workflow: {
          ...workflow,
          edgeGroups: updatedEdgeGroups,
          updatedAt: new Date().toISOString(),
        },
        isDirty: true,
      });
      
      get().pushHistory('添加连接');
      
      return newEdge.id;
    },

    updateEdge: (edgeId, updates) => {
      const { workflow } = get();
      if (!workflow) return;
      
      const updatedEdgeGroups = workflow.edgeGroups.map(g => ({
        ...g,
        edges: g.edges.map(e =>
          e.id === edgeId ? { ...e, ...updates } : e
        ),
      }));
      
      set({
        workflow: {
          ...workflow,
          edgeGroups: updatedEdgeGroups,
          updatedAt: new Date().toISOString(),
        },
        isDirty: true,
      });
    },

    deleteEdge: (edgeId) => {
      const { workflow } = get();
      if (!workflow) return;
      
      const updatedEdgeGroups = workflow.edgeGroups
        .map(g => ({
          ...g,
          edges: g.edges.filter(e => e.id !== edgeId),
        }))
        .filter(g => g.edges.length > 0);
      
      set({
        workflow: {
          ...workflow,
          edgeGroups: updatedEdgeGroups,
          updatedAt: new Date().toISOString(),
        },
        selectedEdgeId: null,
        isDirty: true,
      });
      
      get().pushHistory('删除连接');
    },

    // ==================== 边组管理 ====================

    createEdgeGroup: (sourceId, type) => {
      const { workflow } = get();
      if (!workflow) return '';
      
      const newEdgeGroup: EdgeGroupDefinition = {
        id: crypto.randomUUID(),
        type,
        sourceExecutorId: sourceId,
        edges: [],
      };
      
      set({
        workflow: {
          ...workflow,
          edgeGroups: [...workflow.edgeGroups, newEdgeGroup],
          updatedAt: new Date().toISOString(),
        },
        isDirty: true,
      });
      
      return newEdgeGroup.id;
    },

    updateEdgeGroup: (groupId, updates) => {
      const { workflow } = get();
      if (!workflow) return;
      
      const updatedEdgeGroups = workflow.edgeGroups.map(g =>
        g.id === groupId ? { ...g, ...updates } : g
      );
      
      set({
        workflow: {
          ...workflow,
          edgeGroups: updatedEdgeGroups,
          updatedAt: new Date().toISOString(),
        },
        isDirty: true,
      });
    },

    deleteEdgeGroup: (groupId) => {
      const { workflow } = get();
      if (!workflow) return;
      
      set({
        workflow: {
          ...workflow,
          edgeGroups: workflow.edgeGroups.filter(g => g.id !== groupId),
          updatedAt: new Date().toISOString(),
        },
        isDirty: true,
      });
      
      get().pushHistory('删除边组');
    },

    // ==================== 变量管理 ====================

    addVariable: (variable) => {
      const { workflow } = get();
      if (!workflow) return;
      
      set({
        workflow: {
          ...workflow,
          variables: [...workflow.variables, variable],
          updatedAt: new Date().toISOString(),
        },
        isDirty: true,
      });
      
      get().pushHistory(`添加变量 ${variable.name}`);
    },

    updateVariable: (name, updates) => {
      const { workflow } = get();
      if (!workflow) return;
      
      const updatedVariables = workflow.variables.map(v =>
        v.name === name ? { ...v, ...updates } : v
      );
      
      set({
        workflow: {
          ...workflow,
          variables: updatedVariables,
          updatedAt: new Date().toISOString(),
        },
        isDirty: true,
      });
    },

    deleteVariable: (name) => {
      const { workflow } = get();
      if (!workflow) return;
      
      set({
        workflow: {
          ...workflow,
          variables: workflow.variables.filter(v => v.name !== name),
          updatedAt: new Date().toISOString(),
        },
        isDirty: true,
      });
      
      get().pushHistory(`删除变量 ${name}`);
    },

    // ==================== 选择管理 ====================

    selectExecutor: (id) => {
      set({
        selectedExecutorId: id,
        selectedEdgeId: null,
      });
    },

    selectEdge: (id) => {
      set({
        selectedExecutorId: null,
        selectedEdgeId: id,
      });
    },

    clearSelection: () => {
      set({
        selectedExecutorId: null,
        selectedEdgeId: null,
      });
    },

    // ==================== 历史管理 ====================

    pushHistory: (description) => {
      const { workflow, history, historyIndex, maxHistorySize } = get();
      if (!workflow) return;
      
      const newEntry: HistoryEntry = {
        workflow: JSON.parse(JSON.stringify(workflow)),
        timestamp: Date.now(),
        description,
      };
      
      // 截断历史到当前位置
      const newHistory = history.slice(0, historyIndex + 1);
      newHistory.push(newEntry);
      
      // 限制历史大小
      if (newHistory.length > maxHistorySize) {
        newHistory.shift();
      }
      
      set({
        history: newHistory,
        historyIndex: newHistory.length - 1,
      });
    },

    undo: () => {
      const { history, historyIndex } = get();
      if (historyIndex <= 0) return;
      
      const previousEntry = history[historyIndex - 1];
      
      set({
        workflow: JSON.parse(JSON.stringify(previousEntry.workflow)),
        historyIndex: historyIndex - 1,
        isDirty: true,
      });
    },

    redo: () => {
      const { history, historyIndex } = get();
      if (historyIndex >= history.length - 1) return;
      
      const nextEntry = history[historyIndex + 1];
      
      set({
        workflow: JSON.parse(JSON.stringify(nextEntry.workflow)),
        historyIndex: historyIndex + 1,
        isDirty: true,
      });
    },

    canUndo: () => {
      const { historyIndex } = get();
      return historyIndex > 0;
    },

    canRedo: () => {
      const { history, historyIndex } = get();
      return historyIndex < history.length - 1;
    },

    // ==================== 执行状态管理 ====================

    startExecution: () => {
      set({
        isExecuting: true,
        executorStates: {},
        executionEvents: [],
      });
    },

    stopExecution: () => {
      set({
        isExecuting: false,
      });
    },

    setExecutorState: (id, state) => {
      set((s) => ({
        executorStates: {
          ...s.executorStates,
          [id]: state,
        },
      }));
    },

    addExecutionEvent: (event) => {
      set((s) => ({
        executionEvents: [...s.executionEvents, event],
      }));
    },

    clearExecutionState: () => {
      set({
        isExecuting: false,
        executorStates: {},
        executionEvents: [],
      });
    },

    // ==================== 拖拽管理 ====================

    startDrag: (type) => {
      set({
        isDragging: true,
        draggedType: type,
      });
    },

    endDrag: () => {
      set({
        isDragging: false,
        draggedType: null,
      });
    },

    // ==================== 视图管理 ====================

    setZoom: (zoom) => {
      set({ zoom: Math.max(0.1, Math.min(2, zoom)) });
    },

    setPanPosition: (position) => {
      set({ panPosition: position });
    },

    toggleMinimap: () => {
      set((s) => ({ showMinimap: !s.showMinimap }));
    },

    toggleGrid: () => {
      set((s) => ({ showGrid: !s.showGrid }));
    },

    fitView: () => {
      // 这个会由 React Flow 处理
      set({ zoom: 1, panPosition: { x: 0, y: 0 } });
    },

    // ==================== 模态框管理 ====================

    openConfigModal: (executorId) => {
      set({
        isConfigModalOpen: true,
        configModalExecutorId: executorId,
      });
    },

    closeConfigModal: () => {
      set({
        isConfigModalOpen: false,
        configModalExecutorId: null,
      });
    },

    openVariableModal: () => {
      set({ isVariableModalOpen: true });
    },

    closeVariableModal: () => {
      set({ isVariableModalOpen: false });
    },

    // ==================== 导入导出 ====================

    exportToJson: () => {
      const { workflow } = get();
      if (!workflow) return '{}';
      return JSON.stringify(workflow, null, 2);
    },

    importFromJson: (json) => {
      try {
        const workflow = JSON.parse(json) as DeclarativeWorkflowDefinition;
        get().loadWorkflow(workflow);
        return true;
      } catch (error) {
        console.error('导入 JSON 失败:', error);
        return false;
      }
    },

    exportToYaml: async () => {
      const { workflow } = get();
      if (!workflow) return '';
      
      try {
        const response = await fetch('/api/workflows/export-yaml', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(workflow),
        });
        
        if (response.ok) {
          return await response.text();
        }
        return '';
      } catch (error) {
        console.error('导出 YAML 失败:', error);
        return '';
      }
    },

    importFromYaml: async (yaml) => {
      try {
        // 使用新的声明式工作流 API，会自动保存到数据库
        const response = await fetch('/api/declarative-workflows/import-yaml', {
          method: 'POST',
          headers: { 'Content-Type': 'text/yaml' },
          body: yaml,
        });
        
        if (response.ok) {
          const workflow = await response.json() as DeclarativeWorkflowDefinition;
          get().loadWorkflow(workflow);
          return true;
        }
        return false;
      } catch (error) {
        console.error('导入 YAML 失败:', error);
        return false;
      }
    },

    // ==================== 辅助方法 ====================

    getExecutor: (id) => {
      const { workflow } = get();
      return workflow?.executors.find(e => e.id === id);
    },

    getExecutorsByType: (type) => {
      const { workflow } = get();
      return workflow?.executors.filter(e => e.type === type) || [];
    },

    getConnectedExecutors: (id) => {
      const { workflow } = get();
      if (!workflow) return [];
      
      const connectedIds = new Set<string>();
      
      // 获取出边目标
      workflow.edgeGroups
        .filter(g => g.sourceExecutorId === id)
        .forEach(g => g.edges.forEach(e => connectedIds.add(e.targetExecutorId)));
      
      // 获取入边来源
      workflow.edgeGroups
        .filter(g => g.edges.some(e => e.targetExecutorId === id))
        .forEach(g => connectedIds.add(g.sourceExecutorId));
      
      return workflow.executors.filter(e => connectedIds.has(e.id));
    },

    getIncomingEdges: (executorId) => {
      const { workflow } = get();
      if (!workflow) return [];
      
      const edges: EdgeDefinition[] = [];
      workflow.edgeGroups.forEach(g => {
        g.edges.filter(e => e.targetExecutorId === executorId).forEach(e => edges.push(e));
      });
      
      return edges;
    },

    getOutgoingEdges: (executorId) => {
      const { workflow } = get();
      if (!workflow) return [];
      
      const edgeGroup = workflow.edgeGroups.find(g => g.sourceExecutorId === executorId);
      return edgeGroup?.edges || [];
    },

    validateWorkflow: () => {
      const { workflow } = get();
      const errors: ValidationError[] = [];
      const warnings: ValidationWarning[] = [];
      
      if (!workflow) {
        return { isValid: false, errors: [{ type: 'missing_start', message: '没有工作流' }], warnings: [] };
      }
      
      // 检查是否有起始节点
      if (!workflow.startExecutorId) {
        errors.push({ type: 'missing_start', message: '未设置起始执行器' });
      } else if (!workflow.executors.find(e => e.id === workflow.startExecutorId)) {
        errors.push({ type: 'missing_start', message: '起始执行器不存在' });
      }
      
      // 检查孤立节点
      const connectedIds = new Set<string>();
      workflow.edgeGroups.forEach(g => {
        connectedIds.add(g.sourceExecutorId);
        g.edges.forEach(e => connectedIds.add(e.targetExecutorId));
      });
      
      workflow.executors.forEach(e => {
        if (!connectedIds.has(e.id) && e.id !== workflow.startExecutorId) {
          warnings.push({
            type: 'unreachable',
            message: `执行器 "${e.name}" 不可达`,
            executorId: e.id,
          });
        }
      });
      
      // 检查无效连接
      workflow.edgeGroups.forEach(g => {
        if (!workflow.executors.find(e => e.id === g.sourceExecutorId)) {
          errors.push({
            type: 'invalid_connection',
            message: '边组的源执行器不存在',
          });
        }
        
        g.edges.forEach(edge => {
          if (!workflow.executors.find(e => e.id === edge.targetExecutorId)) {
            errors.push({
              type: 'invalid_connection',
              message: '边的目标执行器不存在',
              edgeId: edge.id,
            });
          }
        });
      });
      
      return {
        isValid: errors.length === 0,
        errors,
        warnings,
      };
    },
  }))
);

// ==================== 选择器 ====================

export const selectWorkflow = (state: WorkflowState) => state.workflow;
export const selectExecutors = (state: WorkflowState) => state.workflow?.executors || [];
export const selectEdgeGroups = (state: WorkflowState) => state.workflow?.edgeGroups || [];
export const selectVariables = (state: WorkflowState) => state.workflow?.variables || [];
export const selectSelectedExecutor = (state: WorkflowState & WorkflowActions) => {
  if (!state.selectedExecutorId || !state.workflow) return null;
  return state.workflow.executors.find(e => e.id === state.selectedExecutorId) || null;
};
export const selectIsExecuting = (state: WorkflowState) => state.isExecuting;
export const selectIsDirty = (state: WorkflowState) => state.isDirty;
