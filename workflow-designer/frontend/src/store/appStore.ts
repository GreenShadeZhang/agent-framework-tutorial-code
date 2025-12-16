import { create } from 'zustand';

export interface Agent {
  id: string;
  name: string;
  description: string;
  instructionsTemplate: string;
  type: 'Assistant' | 'WebSurfer' | 'Coder' | 'Custom';
  modelConfig: {
    model: string;
    temperature: number;
    maxTokens: number;
    topP: number;
  };
  tools: Array<{
    name: string;
    type: string;
    parameters: Record<string, any>;
  }>;
  metadata: Record<string, any>;
  createdAt: string;
  updatedAt: string;
}

export interface WorkflowNode {
  id: string;
  type: 'Start' | 'Agent' | 'Condition' | 'End';
  position: { x: number; y: number };
  data: Record<string, any>;
}

export interface WorkflowEdge {
  id: string;
  source: string;
  target: string;
  type: 'Direct' | 'Conditional';
  condition?: string;
}

export interface Workflow {
  id: string;
  name: string;
  description: string;
  version: string;
  nodes: WorkflowNode[];
  edges: WorkflowEdge[];
  parameters: Array<{
    name: string;
    type: string;
    required: boolean;
    defaultValue?: any;
    description: string;
  }>;
  yamlContent: string;
  workflowDump: string;
  metadata: Record<string, any>;
  isPublished: boolean;
  createdAt: string;
  updatedAt: string;
}

interface AppState {
  agents: Agent[];
  workflows: Workflow[];
  selectedAgent: Agent | null;
  selectedWorkflow: Workflow | null;
  
  setAgents: (agents: Agent[]) => void;
  setWorkflows: (workflows: Workflow[]) => void;
  setSelectedAgent: (agent: Agent | null) => void;
  setSelectedWorkflow: (workflow: Workflow | null) => void;
  
  addAgent: (agent: Agent) => void;
  updateAgent: (id: string, agent: Agent) => void;
  deleteAgent: (id: string) => void;
  
  addWorkflow: (workflow: Workflow) => void;
  updateWorkflow: (id: string, workflow: Workflow) => void;
  deleteWorkflow: (id: string) => void;
}

export const useAppStore = create<AppState>((set) => ({
  agents: [],
  workflows: [],
  selectedAgent: null,
  selectedWorkflow: null,
  
  setAgents: (agents) => set({ agents }),
  setWorkflows: (workflows) => set({ workflows }),
  setSelectedAgent: (agent) => set({ selectedAgent: agent }),
  setSelectedWorkflow: (workflow) => set({ selectedWorkflow: workflow }),
  
  addAgent: (agent) => set((state) => ({ agents: [...state.agents, agent] })),
  updateAgent: (id, agent) => set((state) => ({
    agents: state.agents.map((a) => (a.id === id ? agent : a))
  })),
  deleteAgent: (id) => set((state) => ({
    agents: state.agents.filter((a) => a.id !== id)
  })),
  
  addWorkflow: (workflow) => set((state) => ({ workflows: [...state.workflows, workflow] })),
  updateWorkflow: (id, workflow) => set((state) => ({
    workflows: state.workflows.map((w) => (w.id === id ? workflow : w))
  })),
  deleteWorkflow: (id) => set((state) => ({
    workflows: state.workflows.filter((w) => w.id !== id)
  })),
}));
