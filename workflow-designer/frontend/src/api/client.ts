const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api';

export const api = {
  // Agents
  getAgents: async () => {
    const response = await fetch(`${API_BASE_URL}/agents`);
    if (!response.ok) throw new Error('Failed to fetch agents');
    return response.json();
  },

  getAgent: async (id: string) => {
    const response = await fetch(`${API_BASE_URL}/agents/${id}`);
    if (!response.ok) throw new Error('Failed to fetch agent');
    return response.json();
  },

  createAgent: async (agent: any) => {
    console.log('Creating agent with data:', JSON.stringify(agent, null, 2));
    const response = await fetch(`${API_BASE_URL}/agents`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(agent),
    });
    if (!response.ok) {
      const errorData = await response.json();
      console.error('Create agent error:', errorData);
      throw new Error('Failed to create agent: ' + JSON.stringify(errorData));
    }
    return response.json();
  },

  updateAgent: async (id: string, agent: any) => {
    const response = await fetch(`${API_BASE_URL}/agents/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(agent),
    });
    if (!response.ok) throw new Error('Failed to update agent');
    return response.json();
  },

  deleteAgent: async (id: string) => {
    const response = await fetch(`${API_BASE_URL}/agents/${id}`, {
      method: 'DELETE',
    });
    if (!response.ok) throw new Error('Failed to delete agent');
  },

  // Workflows
  getWorkflows: async () => {
    const response = await fetch(`${API_BASE_URL}/workflows`);
    if (!response.ok) throw new Error('Failed to fetch workflows');
    return response.json();
  },

  getWorkflow: async (id: string) => {
    const response = await fetch(`${API_BASE_URL}/workflows/${id}`);
    if (!response.ok) throw new Error('Failed to fetch workflow');
    return response.json();
  },

  createWorkflow: async (workflow: any) => {
    const response = await fetch(`${API_BASE_URL}/workflows`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(workflow),
    });
    if (!response.ok) throw new Error('Failed to create workflow');
    return response.json();
  },

  updateWorkflow: async (id: string, workflow: any) => {
    const response = await fetch(`${API_BASE_URL}/workflows/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(workflow),
    });
    if (!response.ok) throw new Error('Failed to update workflow');
    return response.json();
  },

  deleteWorkflow: async (id: string) => {
    const response = await fetch(`${API_BASE_URL}/workflows/${id}`, {
      method: 'DELETE',
    });
    if (!response.ok) throw new Error('Failed to delete workflow');
  },

  executeWorkflow: async (id: string, parameters: Record<string, any>) => {
    const response = await fetch(`${API_BASE_URL}/workflows/${id}/execute`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(parameters),
    });
    if (!response.ok) throw new Error('Failed to execute workflow');
    return response.json();
  },

  executeWorkflowWithFramework: async (id: string, userInput: string) => {
    const response = await fetch(`${API_BASE_URL}/workflows/${id}/execute-framework`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userInput }),
    });
    if (!response.ok) throw new Error('Failed to execute workflow with framework');
    return response;
  },

  exportWorkflowYaml: async (id: string) => {
    const response = await fetch(`${API_BASE_URL}/workflows/${id}/export-yaml`);
    if (!response.ok) throw new Error('Failed to export workflow to YAML');
    return response.json();
  },

  downloadWorkflowYaml: (id: string) => {
    window.open(`${API_BASE_URL}/workflows/${id}/download-yaml`, '_blank');
  },
};
