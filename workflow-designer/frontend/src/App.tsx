import { useState } from 'react';
import AgentList from './components/AgentList';
import WorkflowCanvas from './components/WorkflowCanvas';
import ToastContainer from './components/ToastContainer';
import WorkflowDesignerPage from './pages/WorkflowDesignerPage';

function App() {
  const [activeTab, setActiveTab] = useState<'agents' | 'workflow' | 'declarative'>('declarative');

  return (
    <div className="min-h-screen bg-gray-50">
      {/* 声明式工作流设计器使用全屏布局 */}
      {activeTab === 'declarative' ? (
        <div className="h-screen flex flex-col">
          <nav className="bg-white shadow-sm border-b border-gray-200 flex-shrink-0">
            <div className="max-w-full mx-auto px-4 sm:px-6 lg:px-8">
              <div className="flex justify-between h-14">
                <div className="flex space-x-8">
                  <div className="flex items-center">
                    <h1 className="text-xl font-bold text-gray-900">
                      Workflow Designer
                    </h1>
                  </div>
                  <div className="flex space-x-4">
                    <button
                      onClick={() => setActiveTab('agents')}
                      className="inline-flex items-center px-4 py-2 border-b-2 border-transparent text-sm font-medium text-gray-500 hover:text-gray-700 hover:border-gray-300"
                    >
                      智能体管理
                    </button>
                    <button
                      onClick={() => setActiveTab('workflow')}
                      className="inline-flex items-center px-4 py-2 border-b-2 border-transparent text-sm font-medium text-gray-500 hover:text-gray-700 hover:border-gray-300"
                    >
                      简单工作流
                    </button>
                    <button
                      onClick={() => setActiveTab('declarative')}
                      className="inline-flex items-center px-4 py-2 border-b-2 border-blue-500 text-sm font-medium text-blue-600"
                    >
                      声明式工作流
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </nav>
          <main className="flex-1 overflow-hidden">
            <WorkflowDesignerPage />
          </main>
          <ToastContainer />
        </div>
      ) : (
        <>
          <nav className="bg-white shadow-sm border-b border-gray-200">
            <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
              <div className="flex justify-between h-16">
                <div className="flex space-x-8">
                  <div className="flex items-center">
                    <h1 className="text-xl font-bold text-gray-900">
                      Workflow Designer
                    </h1>
                  </div>
                  <div className="flex space-x-4">
                    <button
                      onClick={() => setActiveTab('agents')}
                      className={`inline-flex items-center px-4 py-2 border-b-2 text-sm font-medium ${
                        activeTab === 'agents'
                          ? 'border-blue-500 text-blue-600'
                          : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                      }`}
                    >
                      智能体管理
                    </button>
                    <button
                      onClick={() => setActiveTab('workflow')}
                      className={`inline-flex items-center px-4 py-2 border-b-2 text-sm font-medium ${
                        activeTab === 'workflow'
                          ? 'border-blue-500 text-blue-600'
                          : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                      }`}
                    >
                      简单工作流
                    </button>
                    <button
                      onClick={() => setActiveTab('declarative')}
                      className="inline-flex items-center px-4 py-2 border-b-2 border-transparent text-sm font-medium text-gray-500 hover:text-gray-700 hover:border-gray-300"
                    >
                      声明式工作流
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </nav>

          <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
            {activeTab === 'agents' ? <AgentList /> : <WorkflowCanvas />}
          </main>
          
          <ToastContainer />
        </>
      )}
    </div>
  );
}

export default App;
