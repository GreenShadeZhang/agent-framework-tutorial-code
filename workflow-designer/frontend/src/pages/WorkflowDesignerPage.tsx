/**
 * 工作流设计器主页面
 * 整合工具箱、画布和属性面板
 */

import React, { useEffect } from 'react';
import { ReactFlowProvider } from 'reactflow';
import { useWorkflowStore } from '../store/workflowStore';
import ExecutorToolbox from '../components/workflow/toolbox/ExecutorToolbox';
import WorkflowCanvas from '../components/workflow/WorkflowCanvas';

// ==================== 页面头部 ====================

interface HeaderProps {
  workflowName: string;
  isDirty: boolean;
  isExecuting: boolean;
  onNewWorkflow: () => void;
  onOpenWorkflow: () => void;
  onImportYaml: () => void;
  onExecute: () => void;
}

const Header: React.FC<HeaderProps> = ({ 
  workflowName, 
  isDirty, 
  isExecuting,
  onNewWorkflow, 
  onOpenWorkflow,
  onImportYaml,
  onExecute,
}) => {
  return (
    <header className="h-14 bg-white border-b border-gray-200 flex items-center justify-between px-4 shadow-sm">
      <div className="flex items-center gap-4">
        {/* Logo */}
        <div className="flex items-center gap-2">
          <span className="text-2xl">🔮</span>
          <span className="font-bold text-xl text-gray-800">工作流设计器</span>
        </div>
        
        {/* 工作流名称 */}
        <div className="flex items-center gap-2 pl-4 border-l border-gray-200">
          <span className="text-gray-600">{workflowName}</span>
          {isDirty && (
            <span className="w-2 h-2 bg-orange-500 rounded-full" title="有未保存的更改"></span>
          )}
        </div>
      </div>
      
      <div className="flex items-center gap-2">
        <button
          onClick={onNewWorkflow}
          className="flex items-center gap-1 px-3 py-2 text-sm text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
          </svg>
          新建
        </button>
        <button
          onClick={onOpenWorkflow}
          className="flex items-center gap-1 px-3 py-2 text-sm text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
          title="打开 JSON 文件"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 19a2 2 0 01-2-2V7a2 2 0 012-2h4l2 2h4a2 2 0 012 2v1M5 19h14a2 2 0 002-2v-5a2 2 0 00-2-2H9a2 2 0 00-2 2v5a2 2 0 01-2 2z" />
          </svg>
          打开 JSON
        </button>
        <button
          onClick={onImportYaml}
          className="flex items-center gap-1 px-3 py-2 text-sm text-white bg-purple-600 hover:bg-purple-700 rounded-lg transition-colors"
          title="导入 Agent Framework YAML 工作流"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
          </svg>
          导入 YAML
        </button>
        <button
          onClick={onExecute}
          disabled={isExecuting}
          className={`flex items-center gap-1 px-4 py-2 text-sm text-white rounded-lg transition-colors ${
            isExecuting 
              ? 'bg-gray-400 cursor-not-allowed' 
              : 'bg-green-600 hover:bg-green-700'
          }`}
        >
          {isExecuting ? (
            <>
              <svg className="w-4 h-4 animate-spin" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
              </svg>
              执行中...
            </>
          ) : (
            <>
              <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M14.752 11.168l-3.197-2.132A1 1 0 0010 9.87v4.263a1 1 0 001.555.832l3.197-2.132a1 1 0 000-1.664z" />
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              执行工作流
            </>
          )}
        </button>
      </div>
    </header>
  );
};

// ==================== 属性面板 ====================

const PropertiesPanel: React.FC = () => {
  const selectedExecutorId = useWorkflowStore((s) => s.selectedExecutorId);
  const workflow = useWorkflowStore((s) => s.workflow);
  const updateExecutor = useWorkflowStore((s) => s.updateExecutor);
  const variables = workflow?.variables || [];
  
  const selectedExecutor = workflow?.executors.find(e => e.id === selectedExecutorId);
  
  if (!selectedExecutor) {
    return (
      <div className="w-72 bg-white border-l border-gray-200 flex flex-col">
        <div className="px-4 py-3 border-b border-gray-200 bg-gray-50">
          <h2 className="font-semibold text-gray-800">属性</h2>
        </div>
        <div className="flex-1 flex items-center justify-center text-gray-400 text-sm p-4 text-center">
          <div>
            <svg className="w-12 h-12 mx-auto mb-3 text-gray-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 15l-2 5L9 9l11 4-5 2zm0 0l5 5M7.188 2.239l.777 2.897M5.136 7.965l-2.898-.777M13.95 4.05l-2.122 2.122m-5.657 5.656l-2.12 2.122" />
            </svg>
            选择一个节点查看属性
          </div>
        </div>
        
        {/* 变量列表 */}
        <div className="border-t border-gray-200">
          <div className="px-4 py-3 bg-gray-50 flex items-center justify-between">
            <h3 className="font-semibold text-gray-800 text-sm">变量</h3>
            <button className="text-blue-600 hover:text-blue-700 text-sm">
              + 添加
            </button>
          </div>
          <div className="p-2 max-h-48 overflow-y-auto">
            {variables.length === 0 ? (
              <div className="text-gray-400 text-sm text-center py-4">
                暂无变量
              </div>
            ) : (
              <div className="space-y-1">
                {variables.map((v) => (
                  <div key={v.name} className="flex items-center justify-between px-2 py-1.5 bg-gray-50 rounded text-sm">
                    <span className="font-mono text-gray-700">{v.name}</span>
                    <span className="text-xs text-gray-500">{v.type}</span>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>
    );
  }
  
  return (
    <div className="w-72 bg-white border-l border-gray-200 flex flex-col">
      <div className="px-4 py-3 border-b border-gray-200 bg-gray-50">
        <h2 className="font-semibold text-gray-800">属性</h2>
      </div>
      
      <div className="flex-1 overflow-y-auto p-4 space-y-4">
        {/* 节点信息 */}
        <section>
          <h3 className="text-sm font-medium text-gray-700 mb-2">基本信息</h3>
          <div className="space-y-3">
            <div>
              <label className="block text-xs text-gray-500 mb-1">名称</label>
              <input
                type="text"
                value={selectedExecutor.name}
                onChange={(e) => updateExecutor(selectedExecutor.id, { name: e.target.value })}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <div>
              <label className="block text-xs text-gray-500 mb-1">描述</label>
              <textarea
                value={selectedExecutor.description || ''}
                onChange={(e) => updateExecutor(selectedExecutor.id, { description: e.target.value })}
                rows={2}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500 resize-none"
              />
            </div>
            <div>
              <label className="block text-xs text-gray-500 mb-1">类型</label>
              <div className="px-3 py-2 bg-gray-100 rounded-lg text-sm text-gray-700">
                {selectedExecutor.type}
              </div>
            </div>
            <div>
              <label className="block text-xs text-gray-500 mb-1">ID</label>
              <div className="px-3 py-2 bg-gray-100 rounded-lg text-xs font-mono text-gray-500 truncate">
                {selectedExecutor.id}
              </div>
            </div>
          </div>
        </section>
        
        {/* 位置 */}
        <section>
          <h3 className="text-sm font-medium text-gray-700 mb-2">位置</h3>
          <div className="grid grid-cols-2 gap-2">
            <div>
              <label className="block text-xs text-gray-500 mb-1">X</label>
              <input
                type="number"
                value={Math.round(selectedExecutor.position.x)}
                onChange={(e) => updateExecutor(selectedExecutor.id, { 
                  position: { ...selectedExecutor.position, x: parseInt(e.target.value) || 0 }
                })}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <div>
              <label className="block text-xs text-gray-500 mb-1">Y</label>
              <input
                type="number"
                value={Math.round(selectedExecutor.position.y)}
                onChange={(e) => updateExecutor(selectedExecutor.id, { 
                  position: { ...selectedExecutor.position, y: parseInt(e.target.value) || 0 }
                })}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-500"
              />
            </div>
          </div>
        </section>
      </div>
      
      {/* 变量列表 */}
      <div className="border-t border-gray-200">
        <div className="px-4 py-3 bg-gray-50 flex items-center justify-between">
          <h3 className="font-semibold text-gray-800 text-sm">变量</h3>
          <button className="text-blue-600 hover:text-blue-700 text-sm">
            + 添加
          </button>
        </div>
        <div className="p-2 max-h-32 overflow-y-auto">
          {variables.length === 0 ? (
            <div className="text-gray-400 text-sm text-center py-4">
              暂无变量
            </div>
          ) : (
            <div className="space-y-1">
              {variables.map((v) => (
                <div key={v.name} className="flex items-center justify-between px-2 py-1.5 bg-gray-50 rounded text-sm">
                  <span className="font-mono text-gray-700">{v.name}</span>
                  <span className="text-xs text-gray-500">{v.type}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

// ==================== 新建工作流对话框 ====================

interface NewWorkflowDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onCreate: (name: string, description: string) => void;
}

const NewWorkflowDialog: React.FC<NewWorkflowDialogProps> = ({ isOpen, onClose, onCreate }) => {
  const [name, setName] = React.useState('新工作流');
  const [description, setDescription] = React.useState('');
  
  const handleCreate = () => {
    if (name.trim()) {
      onCreate(name.trim(), description.trim());
      onClose();
    }
  };
  
  if (!isOpen) return null;
  
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/50" onClick={onClose} />
      <div className="relative bg-white rounded-xl shadow-2xl w-full max-w-md p-6">
        <h2 className="text-xl font-bold text-gray-800 mb-4">新建工作流</h2>
        
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">名称</label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
              placeholder="工作流名称"
              autoFocus
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">描述</label>
            <textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={3}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 resize-none"
              placeholder="可选描述"
            />
          </div>
        </div>
        
        <div className="flex justify-end gap-3 mt-6">
          <button
            onClick={onClose}
            className="px-4 py-2 text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50"
          >
            取消
          </button>
          <button
            onClick={handleCreate}
            className="px-4 py-2 text-white bg-blue-600 rounded-lg hover:bg-blue-700"
          >
            创建
          </button>
        </div>
      </div>
    </div>
  );
};

// ==================== YAML 导入对话框 ====================

import { workflowExamples, exampleCategories } from '../data/workflowExamples';
import type { WorkflowExample } from '../data/workflowExamples';

interface YamlImportDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onImport: (yaml: string) => void;
}

const YamlImportDialog: React.FC<YamlImportDialogProps> = ({ isOpen, onClose, onImport }) => {
  const [yamlContent, setYamlContent] = React.useState('');
  const [isLoading, setIsLoading] = React.useState(false);
  const [activeTab, setActiveTab] = React.useState<'examples' | 'custom'>('examples');
  const [selectedCategory, setSelectedCategory] = React.useState<string>('basic');
  const [selectedExample, setSelectedExample] = React.useState<WorkflowExample | null>(null);
  
  const handleImport = async () => {
    const contentToImport = activeTab === 'examples' && selectedExample 
      ? selectedExample.yaml 
      : yamlContent.trim();
      
    if (contentToImport) {
      setIsLoading(true);
      await onImport(contentToImport);
      setIsLoading(false);
      setYamlContent('');
      setSelectedExample(null);
      onClose();
    }
  };
  
  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      const text = await file.text();
      setYamlContent(text);
      setActiveTab('custom');
    }
  };

  const handleSelectExample = (example: WorkflowExample) => {
    setSelectedExample(example);
    setYamlContent(example.yaml);
  };

  const filteredExamples = workflowExamples.filter(e => e.category === selectedCategory);
  
  if (!isOpen) return null;
  
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/50" onClick={onClose} />
      <div className="relative bg-white rounded-xl shadow-2xl w-full max-w-5xl max-h-[90vh] flex flex-col">
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <h2 className="text-xl font-bold text-gray-800">导入 Agent Framework YAML</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Tab 切换 */}
        <div className="px-6 pt-4 border-b border-gray-200">
          <div className="flex gap-4">
            <button
              onClick={() => setActiveTab('examples')}
              className={`pb-3 px-1 text-sm font-medium border-b-2 transition-colors ${
                activeTab === 'examples'
                  ? 'border-purple-600 text-purple-600'
                  : 'border-transparent text-gray-500 hover:text-gray-700'
              }`}
            >
              📚 示例工作流
            </button>
            <button
              onClick={() => setActiveTab('custom')}
              className={`pb-3 px-1 text-sm font-medium border-b-2 transition-colors ${
                activeTab === 'custom'
                  ? 'border-purple-600 text-purple-600'
                  : 'border-transparent text-gray-500 hover:text-gray-700'
              }`}
            >
              ✏️ 自定义导入
            </button>
          </div>
        </div>
        
        <div className="flex-1 overflow-hidden flex">
          {activeTab === 'examples' ? (
            <>
              {/* 左侧：分类和示例列表 */}
              <div className="w-80 border-r border-gray-200 flex flex-col">
                {/* 分类标签 */}
                <div className="p-4 border-b border-gray-100">
                  <div className="flex flex-wrap gap-2">
                    {exampleCategories.map((cat) => (
                      <button
                        key={cat.id}
                        onClick={() => setSelectedCategory(cat.id)}
                        className={`px-3 py-1.5 text-xs font-medium rounded-full transition-colors ${
                          selectedCategory === cat.id
                            ? 'bg-purple-600 text-white'
                            : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
                        }`}
                      >
                        {cat.icon} {cat.name}
                      </button>
                    ))}
                  </div>
                </div>
                
                {/* 示例列表 */}
                <div className="flex-1 overflow-y-auto p-2">
                  <div className="space-y-2">
                    {filteredExamples.map((example) => (
                      <button
                        key={example.id}
                        onClick={() => handleSelectExample(example)}
                        className={`w-full text-left p-3 rounded-lg transition-colors ${
                          selectedExample?.id === example.id
                            ? 'bg-purple-50 border-2 border-purple-300'
                            : 'bg-gray-50 border-2 border-transparent hover:bg-gray-100'
                        }`}
                      >
                        <div className="flex items-center gap-2 mb-1">
                          <span className="text-lg">{example.icon}</span>
                          <span className="font-medium text-gray-800 text-sm">{example.name}</span>
                        </div>
                        <p className="text-xs text-gray-500 line-clamp-2">{example.description}</p>
                      </button>
                    ))}
                  </div>
                </div>
              </div>

              {/* 右侧：YAML 预览 */}
              <div className="flex-1 flex flex-col overflow-hidden">
                {selectedExample ? (
                  <>
                    <div className="p-4 border-b border-gray-100 bg-gray-50">
                      <div className="flex items-center gap-2">
                        <span className="text-2xl">{selectedExample.icon}</span>
                        <div>
                          <h3 className="font-semibold text-gray-800">{selectedExample.name}</h3>
                          <p className="text-sm text-gray-500">{selectedExample.description}</p>
                        </div>
                      </div>
                    </div>
                    <div className="flex-1 overflow-y-auto p-4">
                      <pre className="text-xs font-mono bg-gray-900 text-gray-100 p-4 rounded-lg overflow-x-auto whitespace-pre-wrap">
                        {selectedExample.yaml}
                      </pre>
                    </div>
                  </>
                ) : (
                  <div className="flex-1 flex items-center justify-center text-gray-400">
                    <div className="text-center">
                      <svg className="w-16 h-16 mx-auto mb-4 text-gray-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                      </svg>
                      <p>选择一个示例工作流</p>
                    </div>
                  </div>
                )}
              </div>
            </>
          ) : (
            /* 自定义导入 */
            <div className="flex-1 p-6 overflow-y-auto space-y-4">
              {/* 文件上传 */}
              <div className="flex items-center gap-2">
                <label className="px-4 py-2 bg-gray-100 text-gray-700 rounded-lg hover:bg-gray-200 cursor-pointer text-sm">
                  <input
                    type="file"
                    accept=".yaml,.yml"
                    onChange={handleFileUpload}
                    className="hidden"
                  />
                  📁 选择 YAML 文件
                </label>
                <span className="text-sm text-gray-500">或直接在下方粘贴 YAML 内容</span>
              </div>
              
              {/* YAML 编辑器 */}
              <div className="flex-1">
                <label className="block text-sm font-medium text-gray-700 mb-2">YAML 内容</label>
                <textarea
                  value={yamlContent}
                  onChange={(e) => setYamlContent(e.target.value)}
                  rows={20}
                  className="w-full px-4 py-3 font-mono text-sm border border-gray-300 rounded-lg focus:ring-2 focus:ring-purple-500 bg-gray-50"
                  placeholder={`粘贴 Agent Framework 工作流 YAML...

kind: Workflow
trigger:
  kind: OnConversationStart
  id: my_workflow
  actions:
    - kind: SendActivity
      id: greeting
      activity: "你好！"
    ...`}
                />
              </div>
            </div>
          )}
        </div>
        
        <div className="px-6 py-4 border-t border-gray-200 flex justify-between items-center">
          <div className="text-sm text-gray-500">
            {activeTab === 'examples' && selectedExample && (
              <span>已选择：<strong>{selectedExample.name}</strong></span>
            )}
            {activeTab === 'custom' && yamlContent && (
              <span>YAML 内容已填写</span>
            )}
          </div>
          <div className="flex gap-3">
            <button
              onClick={onClose}
              className="px-4 py-2 text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50"
            >
              取消
            </button>
            <button
              onClick={handleImport}
              disabled={
                (activeTab === 'examples' && !selectedExample) ||
                (activeTab === 'custom' && !yamlContent.trim()) ||
                isLoading
              }
              className={`px-4 py-2 text-white rounded-lg ${
                ((activeTab === 'examples' && selectedExample) || (activeTab === 'custom' && yamlContent.trim())) && !isLoading
                  ? 'bg-purple-600 hover:bg-purple-700'
                  : 'bg-gray-400 cursor-not-allowed'
              }`}
            >
              {isLoading ? '导入中...' : '导入工作流'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

// ==================== 执行结果对话框 ====================

interface ExecutionResultDialogProps {
  isOpen: boolean;
  result: { success: boolean; output?: string; error?: string } | null;
  onClose: () => void;
}

const ExecutionResultDialog: React.FC<ExecutionResultDialogProps> = ({ isOpen, result, onClose }) => {
  if (!isOpen || !result) return null;
  
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/50" onClick={onClose} />
      <div className="relative bg-white rounded-xl shadow-2xl w-full max-w-2xl max-h-[80vh] flex flex-col">
        <div className={`px-6 py-4 border-b flex items-center gap-3 ${
          result.success ? 'bg-green-50 border-green-200' : 'bg-red-50 border-red-200'
        }`}>
          <span className="text-2xl">{result.success ? '✅' : '❌'}</span>
          <h2 className="text-xl font-bold text-gray-800">
            {result.success ? '执行成功' : '执行失败'}
          </h2>
        </div>
        
        <div className="p-6 flex-1 overflow-y-auto">
          {result.success ? (
            <div className="space-y-4">
              <h3 className="text-sm font-medium text-gray-700">执行结果：</h3>
              <pre className="p-4 bg-gray-900 text-green-400 rounded-lg text-sm font-mono overflow-x-auto whitespace-pre-wrap">
                {result.output || '(无输出)'}
              </pre>
            </div>
          ) : (
            <div className="space-y-4">
              <h3 className="text-sm font-medium text-red-700">错误信息：</h3>
              <pre className="p-4 bg-red-50 text-red-700 rounded-lg text-sm font-mono overflow-x-auto whitespace-pre-wrap border border-red-200">
                {result.error || '未知错误'}
              </pre>
            </div>
          )}
        </div>
        
        <div className="px-6 py-4 border-t border-gray-200 flex justify-end">
          <button
            onClick={onClose}
            className="px-4 py-2 text-white bg-blue-600 rounded-lg hover:bg-blue-700"
          >
            关闭
          </button>
        </div>
      </div>
    </div>
  );
};

// ==================== 主页面 ====================

const WorkflowDesignerPage: React.FC = () => {
  const [isNewDialogOpen, setIsNewDialogOpen] = React.useState(false);
  const [isYamlImportOpen, setIsYamlImportOpen] = React.useState(false);
  const [isExecuting, setIsExecuting] = React.useState(false);
  const [executionResult, setExecutionResult] = React.useState<{ success: boolean; output?: string; error?: string } | null>(null);
  const [isResultDialogOpen, setIsResultDialogOpen] = React.useState(false);
  
  const workflow = useWorkflowStore((s) => s.workflow);
  const isDirty = useWorkflowStore((s) => s.isDirty);
  const createNewWorkflow = useWorkflowStore((s) => s.createNewWorkflow);
  const importFromJson = useWorkflowStore((s) => s.importFromJson);
  const importFromYaml = useWorkflowStore((s) => s.importFromYaml);
  
  // 初始化默认工作流
  useEffect(() => {
    if (!workflow) {
      createNewWorkflow('我的工作流', '使用工具箱拖拽节点开始设计');
    }
  }, [workflow, createNewWorkflow]);
  
  const handleNewWorkflow = () => {
    if (isDirty) {
      if (!confirm('有未保存的更改，确定要新建吗？')) {
        return;
      }
    }
    setIsNewDialogOpen(true);
  };
  
  const handleOpenWorkflow = () => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json';
    input.onchange = async (e) => {
      const file = (e.target as HTMLInputElement).files?.[0];
      if (file) {
        const text = await file.text();
        if (importFromJson(text)) {
          alert('工作流导入成功！');
        } else {
          alert('导入失败，请检查文件格式。');
        }
      }
    };
    input.click();
  };
  
  const handleImportYaml = async (yaml: string) => {
    const success = await importFromYaml(yaml);
    if (success) {
      alert('YAML 工作流导入成功！');
    } else {
      alert('导入失败，请检查 YAML 格式。');
    }
  };
  
  const handleExecuteWorkflow = async () => {
    if (!workflow) return;
    
    setIsExecuting(true);
    try {
      // 使用新的声明式工作流执行 API
      const response = await fetch(`/api/declarative-workflows/${workflow.id}/execute`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          input: '测试执行',
          userInput: '测试执行',
        }),
      });
      
      if (response.ok) {
        const result = await response.json();
        setExecutionResult({
          success: true,
          output: JSON.stringify(result, null, 2),
        });
      } else {
        const errorText = await response.text();
        setExecutionResult({
          success: false,
          error: `HTTP ${response.status}: ${errorText}`,
        });
      }
    } catch (error) {
      setExecutionResult({
        success: false,
        error: error instanceof Error ? error.message : '执行失败',
      });
    } finally {
      setIsExecuting(false);
      setIsResultDialogOpen(true);
    }
  };
  
  const handleCreateWorkflow = (name: string, description: string) => {
    createNewWorkflow(name, description);
  };
  
  return (
    <div className="h-screen flex flex-col bg-gray-100">
      <Header
        workflowName={workflow?.name || '未命名'}
        isDirty={isDirty}
        isExecuting={isExecuting}
        onNewWorkflow={handleNewWorkflow}
        onOpenWorkflow={handleOpenWorkflow}
        onImportYaml={() => setIsYamlImportOpen(true)}
        onExecute={handleExecuteWorkflow}
      />
      
      <div className="flex-1 flex overflow-hidden">
        <ReactFlowProvider>
          {/* 左侧工具箱 */}
          <ExecutorToolbox className="w-72 flex-shrink-0" />
          
          {/* 中间画布 */}
          <WorkflowCanvas className="flex-1" />
          
          {/* 右侧属性面板 */}
          <PropertiesPanel />
        </ReactFlowProvider>
      </div>
      
      {/* 新建对话框 */}
      <NewWorkflowDialog
        isOpen={isNewDialogOpen}
        onClose={() => setIsNewDialogOpen(false)}
        onCreate={handleCreateWorkflow}
      />
      
      {/* YAML 导入对话框 */}
      <YamlImportDialog
        isOpen={isYamlImportOpen}
        onClose={() => setIsYamlImportOpen(false)}
        onImport={handleImportYaml}
      />
      
      {/* 执行结果对话框 */}
      <ExecutionResultDialog
        isOpen={isResultDialogOpen}
        result={executionResult}
        onClose={() => setIsResultDialogOpen(false)}
      />
    </div>
  );
};

export default WorkflowDesignerPage;
