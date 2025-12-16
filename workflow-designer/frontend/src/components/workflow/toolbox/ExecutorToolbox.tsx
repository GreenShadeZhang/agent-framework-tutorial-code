/**
 * 执行器工具箱组件
 * 提供拖拽添加各类执行器节点的功能
 * 参考 AutoGen Studio 的组件面板设计
 */

import React, { useState, useCallback } from 'react';
import type { ExecutorType } from '../../../types/workflow';
import { ExecutorTypeGroups } from '../../../types/workflow';
import { useWorkflowStore } from '../../../store/workflowStore';

// ==================== 类型定义 ====================

interface ExecutorTypeItem {
  readonly type: string;
  readonly label: string;
  readonly icon: string;
  readonly description: string;
}

interface ToolboxCategory {
  id: string;
  name: string;
  icon: string;
  items: readonly ExecutorTypeItem[];
}

// ==================== 工具箱分类 ====================

const toolboxCategories: ToolboxCategory[] = [
  { id: 'agents', name: '智能体', icon: '🤖', items: ExecutorTypeGroups.agents },
  { id: 'controlFlow', name: '流程控制', icon: '🔀', items: ExecutorTypeGroups.controlFlow },
  { id: 'stateManagement', name: '状态管理', icon: '📝', items: ExecutorTypeGroups.stateManagement },
  { id: 'messages', name: '消息', icon: '💬', items: ExecutorTypeGroups.messages },
  { id: 'conversation', name: '会话', icon: '🗣️', items: ExecutorTypeGroups.conversation },
  { id: 'humanInput', name: '人工输入', icon: '👤', items: ExecutorTypeGroups.humanInput },
];

// ==================== 子组件 ====================

interface ToolboxItemProps {
  type: ExecutorType;
  icon: string;
  label: string;
  description: string;
  onDragStart: (type: ExecutorType) => void;
  onDragEnd: () => void;
}

const ToolboxItem: React.FC<ToolboxItemProps> = ({
  type,
  icon,
  label,
  description,
  onDragStart,
  onDragEnd,
}) => {
  const handleDragStart = useCallback(
    (event: React.DragEvent) => {
      event.dataTransfer.setData('application/reactflow', type);
      event.dataTransfer.effectAllowed = 'move';
      onDragStart(type);
    },
    [type, onDragStart]
  );

  return (
    <div
      draggable
      onDragStart={handleDragStart}
      onDragEnd={onDragEnd}
      className="
        flex items-center gap-3 p-3 rounded-lg cursor-grab
        bg-white border border-gray-200 shadow-sm
        hover:border-blue-300 hover:shadow-md hover:bg-blue-50
        active:cursor-grabbing active:shadow-lg
        transition-all duration-150
        group
      "
      title={description}
    >
      <span className="text-2xl flex-shrink-0 group-hover:scale-110 transition-transform">
        {icon}
      </span>
      <div className="min-w-0 flex-1">
        <div className="font-medium text-gray-800 text-sm truncate">{label}</div>
        <div className="text-xs text-gray-500 truncate">{description}</div>
      </div>
    </div>
  );
};

interface ToolboxCategoryPanelProps {
  category: ToolboxCategory;
  isExpanded: boolean;
  onToggle: () => void;
  onDragStart: (type: ExecutorType) => void;
  onDragEnd: () => void;
}

const ToolboxCategoryPanel: React.FC<ToolboxCategoryPanelProps> = ({
  category,
  isExpanded,
  onToggle,
  onDragStart,
  onDragEnd,
}) => {
  return (
    <div className="border-b border-gray-200 last:border-b-0">
      {/* 分类标题 */}
      <button
        onClick={onToggle}
        className="
          w-full flex items-center justify-between px-4 py-3
          hover:bg-gray-50 transition-colors
          text-left
        "
      >
        <div className="flex items-center gap-2">
          <span className="text-lg">{category.icon}</span>
          <span className="font-semibold text-gray-700">{category.name}</span>
          <span className="text-xs text-gray-400 bg-gray-100 px-2 py-0.5 rounded-full">
            {category.items.length}
          </span>
        </div>
        <svg
          className={`w-5 h-5 text-gray-400 transition-transform ${isExpanded ? 'rotate-180' : ''}`}
          fill="none"
          stroke="currentColor"
          viewBox="0 0 24 24"
        >
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
        </svg>
      </button>

      {/* 分类内容 */}
      {isExpanded && (
        <div className="px-3 pb-3 space-y-2">
          {category.items.map((item) => (
            <ToolboxItem
              key={item.type}
              type={item.type as ExecutorType}
              icon={item.icon}
              label={item.label}
              description={item.description}
              onDragStart={onDragStart}
              onDragEnd={onDragEnd}
            />
          ))}
        </div>
      )}
    </div>
  );
};

// ==================== 搜索组件 ====================

interface ToolboxSearchProps {
  value: string;
  onChange: (value: string) => void;
}

const ToolboxSearch: React.FC<ToolboxSearchProps> = ({ value, onChange }) => {
  return (
    <div className="relative px-4 py-3 border-b border-gray-200">
      <input
        type="text"
        placeholder="搜索执行器..."
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="
          w-full pl-9 pr-4 py-2 rounded-lg
          border border-gray-300 focus:border-blue-400 focus:ring-2 focus:ring-blue-100
          text-sm placeholder-gray-400
          transition-all
        "
      />
      <svg
        className="absolute left-7 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400"
        fill="none"
        stroke="currentColor"
        viewBox="0 0 24 24"
      >
        <path
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth={2}
          d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
        />
      </svg>
      {value && (
        <button
          onClick={() => onChange('')}
          className="absolute right-7 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      )}
    </div>
  );
};

// ==================== 搜索结果组件 ====================

interface SearchResultsProps {
  searchQuery: string;
  onDragStart: (type: ExecutorType) => void;
  onDragEnd: () => void;
}

const SearchResults: React.FC<SearchResultsProps> = ({ searchQuery, onDragStart, onDragEnd }) => {
  const query = searchQuery.toLowerCase();
  
  const filteredItems = toolboxCategories.flatMap((category) =>
    category.items.filter(
      (item) =>
        item.label.toLowerCase().includes(query) ||
        item.description.toLowerCase().includes(query) ||
        item.type.toLowerCase().includes(query)
    ).map((item) => ({ ...item, category: category.name }))
  );

  if (filteredItems.length === 0) {
    return (
      <div className="px-4 py-8 text-center text-gray-500">
        <svg className="w-12 h-12 mx-auto mb-3 text-gray-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9.172 16.172a4 4 0 015.656 0M9 10h.01M15 10h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
        <div>未找到匹配的执行器</div>
        <div className="text-xs mt-1">尝试其他关键词</div>
      </div>
    );
  }

  return (
    <div className="px-3 py-3 space-y-2">
      <div className="text-xs text-gray-500 px-1 mb-2">
        找到 {filteredItems.length} 个结果
      </div>
      {filteredItems.map((item) => (
        <ToolboxItem
          key={item.type}
          type={item.type as ExecutorType}
          icon={item.icon}
          label={item.label}
          description={`${item.category} · ${item.description}`}
          onDragStart={onDragStart}
          onDragEnd={onDragEnd}
        />
      ))}
    </div>
  );
};

// ==================== 主组件 ====================

interface ExecutorToolboxProps {
  className?: string;
}

export const ExecutorToolbox: React.FC<ExecutorToolboxProps> = ({ className = '' }) => {
  const [searchQuery, setSearchQuery] = useState('');
  const [expandedCategories, setExpandedCategories] = useState<Set<string>>(
    new Set(['agents', 'controlFlow'])
  );
  
  const startDrag = useWorkflowStore((state) => state.startDrag);
  const endDrag = useWorkflowStore((state) => state.endDrag);

  const toggleCategory = useCallback((categoryId: string) => {
    setExpandedCategories((prev) => {
      const next = new Set(prev);
      if (next.has(categoryId)) {
        next.delete(categoryId);
      } else {
        next.add(categoryId);
      }
      return next;
    });
  }, []);

  const handleDragStart = useCallback(
    (type: ExecutorType) => {
      startDrag(type);
    },
    [startDrag]
  );

  const handleDragEnd = useCallback(() => {
    endDrag();
  }, [endDrag]);

  const expandAll = useCallback(() => {
    setExpandedCategories(new Set(toolboxCategories.map((c) => c.id)));
  }, []);

  const collapseAll = useCallback(() => {
    setExpandedCategories(new Set());
  }, []);

  return (
    <div className={`flex flex-col bg-white border-r border-gray-200 ${className}`}>
      {/* 头部 */}
      <div className="px-4 py-3 border-b border-gray-200 bg-gray-50">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className="text-lg">🧰</span>
            <h2 className="font-semibold text-gray-800">执行器工具箱</h2>
          </div>
          <div className="flex items-center gap-1">
            <button
              onClick={expandAll}
              className="p-1.5 rounded hover:bg-gray-200 text-gray-600 text-xs"
              title="全部展开"
            >
              <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 8V4m0 0h4M4 4l5 5m11-1V4m0 0h-4m4 0l-5 5M4 16v4m0 0h4m-4 0l5-5m11 5l-5-5m5 5v-4m0 4h-4" />
              </svg>
            </button>
            <button
              onClick={collapseAll}
              className="p-1.5 rounded hover:bg-gray-200 text-gray-600 text-xs"
              title="全部折叠"
            >
              <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M20 12H4" />
              </svg>
            </button>
          </div>
        </div>
        <p className="text-xs text-gray-500 mt-1">拖拽节点到画布添加</p>
      </div>

      {/* 搜索 */}
      <ToolboxSearch value={searchQuery} onChange={setSearchQuery} />

      {/* 内容区域 */}
      <div className="flex-1 overflow-y-auto">
        {searchQuery ? (
          <SearchResults
            searchQuery={searchQuery}
            onDragStart={handleDragStart}
            onDragEnd={handleDragEnd}
          />
        ) : (
          toolboxCategories.map((category) => (
            <ToolboxCategoryPanel
              key={category.id}
              category={category}
              isExpanded={expandedCategories.has(category.id)}
              onToggle={() => toggleCategory(category.id)}
              onDragStart={handleDragStart}
              onDragEnd={handleDragEnd}
            />
          ))
        )}
      </div>

      {/* 底部提示 */}
      <div className="px-4 py-3 border-t border-gray-200 bg-gray-50">
        <div className="flex items-center gap-2 text-xs text-gray-500">
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <span>双击节点编辑配置</span>
        </div>
      </div>
    </div>
  );
};

export default ExecutorToolbox;
