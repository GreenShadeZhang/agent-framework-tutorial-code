import { useState, useEffect, useRef } from 'react';

interface ExecutionEvent {
  type: string;
  nodeId?: string;
  nodeName?: string;
  status: string;
  message?: string;
  data?: Record<string, any>;
  timestamp: string;
}

interface ExecutionPanelProps {
  workflowId: string;
  parameters: Record<string, any>;
  onClose: () => void;
}

export default function ExecutionPanel({ workflowId, onClose }: ExecutionPanelProps) {
  const [events, setEvents] = useState<ExecutionEvent[]>([]);
  const [isExecuting, setIsExecuting] = useState(false);
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const eventsEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    // 创建新的 AbortController（每次 mount 都创建新的）
    const abortController = new AbortController();
    
    startExecution(abortController.signal);
    
    return () => {
      // 卸载时取消请求
      abortController.abort();
    };
  }, []);

  useEffect(() => {
    eventsEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [events]);

  const startExecution = async (signal: AbortSignal) => {
    setIsExecuting(true);
    setEvents([]);
    setError(null);

    try {
      const apiUrl = import.meta.env.VITE_API_URL || 'http://localhost:5000/api';
      const url = `${apiUrl}/workflows/${workflowId}/execute-framework`;

      const response = await fetch(url, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ userInput: 'Hello, workflow execution' }),
        signal,
      });

      if (!response.ok) {
        throw new Error(`服务器错误: ${response.status} ${response.statusText}`);
      }

      const reader = response.body?.getReader();
      const decoder = new TextDecoder();

      if (!reader) {
        throw new Error('响应体为空');
      }

      let buffer = '';
      
      while (true) {
        const { done, value } = await reader.read();
        if (done) {
          console.log('📡 Stream ended');
          break;
        }

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        
        // 保留最后一行（可能不完整）
        buffer = lines.pop() || '';

        for (const line of lines) {
          if (line.startsWith('data: ')) {
            const data = line.substring(6).trim();
            if (!data) continue;
            
            try {
              const event: ExecutionEvent = JSON.parse(data);
              console.log('📩 Received event:', event.type, event.message);
              
              setEvents((prev) => [...prev, event]);

              // 更新进度
              if (event.data?.progress) {
                setProgress(event.data.progress as number);
              }

              // 检查是否完成或失败
              if (event.type === 'WorkflowCompleted') {
                console.log('✅ Workflow completed');
                setIsExecuting(false);
                setProgress(100);
              } else if (event.type === 'WorkflowFailed') {
                console.log('❌ Workflow failed:', event.message);
                setIsExecuting(false);
                setError(event.message || '工作流执行失败');
              }
            } catch (err) {
              console.error('❌ Failed to parse event:', err, 'Data:', data);
            }
          }
        }
      }
      
      // Stream 结束但没有收到完成事件
      if (isExecuting) {
        console.log('⚠️ Stream ended without completion event');
        setIsExecuting(false);
        if (events.length === 0) {
          setError('未收到任何执行事件');
        }
      }
      
    } catch (error: any) {
      console.error('❌ Execution error:', error);
      
      if (error.name === 'AbortError') {
        console.log('🛑 Execution cancelled by user');
        return;
      }
      
      const errorMessage = error.message || '未知错误';
      setError(errorMessage);
      setEvents((prev) => [
        ...prev,
        {
          type: 'WorkflowFailed',
          status: 'Failed',
          message: errorMessage,
          timestamp: new Date().toISOString(),
        },
      ]);
      setIsExecuting(false);
    }
  };

  const getEventIcon = (type: string) => {
    switch (type) {
      case 'WorkflowStarted':
        return '▶️';
      case 'WorkflowCompleted':
        return '✅';
      case 'WorkflowFailed':
        return '❌';
      case 'NodeStarted':
        return '🔵';
      case 'NodeCompleted':
        return '✔️';
      case 'NodeFailed':
        return '❗';
      case 'ProgressUpdate':
        return '📊';
      default:
        return '📝';
    }
  };

  const getEventColor = (type: string) => {
    switch (type) {
      case 'WorkflowStarted':
      case 'NodeStarted':
        return 'text-blue-600';
      case 'WorkflowCompleted':
      case 'NodeCompleted':
        return 'text-green-600';
      case 'WorkflowFailed':
      case 'NodeFailed':
        return 'text-red-600';
      case 'ProgressUpdate':
        return 'text-gray-600';
      default:
        return 'text-gray-600';
    }
  };

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg shadow-xl w-[800px] max-h-[80vh] flex flex-col">
        {/* Header */}
        <div className="p-4 border-b border-gray-200 flex justify-between items-center">
          <div>
            <h2 className="text-xl font-bold">工作流执行</h2>
            <div className="text-sm">
              {error ? (
                <span className="text-red-600">❌ 执行失败</span>
              ) : isExecuting ? (
                <span className="text-blue-600">🔄 执行中...</span>
              ) : events.length > 0 ? (
                <span className="text-green-600">✅ 执行完成</span>
              ) : (
                <span className="text-gray-500">准备中...</span>
              )}
            </div>
          </div>
          <button
            onClick={onClose}
            className="text-gray-500 hover:text-gray-700 transition-colors"
            title="关闭"
          >
            <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Progress Bar */}
        {isExecuting && (
          <div className="px-4 pt-4">
            <div className="bg-gray-200 rounded-full h-2">
              <div
                className="bg-blue-500 h-2 rounded-full transition-all duration-300"
                style={{ width: `${progress}%` }}
              />
            </div>
            <div className="text-sm text-gray-600 mt-1 text-center">{progress}%</div>
          </div>
        )}

        {/* Error Alert */}
        {error && (
          <div className="mx-4 mt-4 p-4 bg-red-50 border border-red-200 rounded-lg">
            <div className="flex items-center gap-2 text-red-800">
              <span className="text-xl">⚠️</span>
              <span className="font-semibold">执行错误</span>
            </div>
            <div className="text-sm text-red-700 mt-1">{error}</div>
          </div>
        )}

        {/* Events List */}
        <div className="flex-1 overflow-y-auto p-4 space-y-2">
          {events.length === 0 && !error && (
            <div className="text-center text-gray-500 py-8">
              {isExecuting ? '等待事件...' : '暂无事件'}
            </div>
          )}
          {events.map((event, index) => (
            <div
              key={index}
              className="flex items-start gap-3 p-3 bg-gray-50 rounded-lg"
            >
              <span className="text-xl">{getEventIcon(event.type)}</span>
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2">
                  <span className={`font-semibold ${getEventColor(event.type)}`}>
                    {event.nodeName || event.type}
                  </span>
                  <span className="text-xs text-gray-500">
                    {new Date(event.timestamp).toLocaleTimeString()}
                  </span>
                </div>
                {event.message && (
                  <div className="text-sm text-gray-700 mt-1">{event.message}</div>
                )}
                {event.data && Object.keys(event.data).length > 0 && (
                  <details className="mt-2">
                    <summary className="text-xs text-blue-600 cursor-pointer">
                      查看详情
                    </summary>
                    <pre className="text-xs bg-gray-100 p-2 rounded mt-1 overflow-x-auto">
                      {JSON.stringify(event.data, null, 2)}
                    </pre>
                  </details>
                )}
              </div>
            </div>
          ))}
          <div ref={eventsEndRef} />
        </div>

        {/* Footer */}
        <div className="p-4 border-t border-gray-200 flex justify-between items-center">
          <div className="text-sm text-gray-600">
            {events.length} 个事件
          </div>
          <button
            onClick={onClose}
            className="bg-blue-500 hover:bg-blue-600 text-white px-4 py-2 rounded disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            关闭
          </button>
        </div>
      </div>
    </div>
  );
}
