/**
 * Schema 表单渲染器
 * 根据 JSON Schema 动态生成表单
 * 参考 Agent Framework DevUI 的 schema-form-renderer
 */

import React, { useCallback, useMemo } from 'react';
import type { PropertySchema, JsonSchemaDefinition } from '../../types/workflow';

// ==================== 类型定义 ====================

interface SchemaFormRendererProps {
  schema: JsonSchemaDefinition;
  value: Record<string, unknown>;
  onChange: (value: Record<string, unknown>) => void;
  disabled?: boolean;
  className?: string;
}

interface FieldRendererProps {
  name: string;
  schema: PropertySchema;
  value: unknown;
  onChange: (name: string, value: unknown) => void;
  required?: boolean;
  disabled?: boolean;
  path?: string;
}

// ==================== 工具函数 ====================

const getDefaultValue = (schema: PropertySchema): unknown => {
  if (schema.default !== undefined) return schema.default;
  
  switch (schema.type) {
    case 'string':
      return '';
    case 'number':
    case 'integer':
      return 0;
    case 'boolean':
      return false;
    case 'array':
      return [];
    case 'object':
      return {};
    default:
      return null;
  }
};

// ==================== 字段组件 ====================

/**
 * 字符串字段
 */
const StringField: React.FC<FieldRendererProps> = ({
  name,
  schema,
  value,
  onChange,
  required,
  disabled,
}) => {
  const handleChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
      onChange(name, e.target.value);
    },
    [name, onChange]
  );

  // 多行文本
  if (schema.format === 'textarea') {
    return (
      <textarea
        value={(value as string) || ''}
        onChange={handleChange}
        disabled={disabled}
        required={required}
        placeholder={schema.description}
        rows={4}
        className="
          w-full px-3 py-2 rounded-lg border border-gray-300
          focus:border-blue-500 focus:ring-2 focus:ring-blue-100
          disabled:bg-gray-100 disabled:cursor-not-allowed
          text-sm resize-y
        "
      />
    );
  }

  // 密码
  if (schema.format === 'password') {
    return (
      <input
        type="password"
        value={(value as string) || ''}
        onChange={handleChange}
        disabled={disabled}
        required={required}
        placeholder={schema.description}
        className="
          w-full px-3 py-2 rounded-lg border border-gray-300
          focus:border-blue-500 focus:ring-2 focus:ring-blue-100
          disabled:bg-gray-100 disabled:cursor-not-allowed
          text-sm
        "
      />
    );
  }

  // URI
  if (schema.format === 'uri') {
    return (
      <input
        type="url"
        value={(value as string) || ''}
        onChange={handleChange}
        disabled={disabled}
        required={required}
        placeholder={schema.description || 'https://example.com'}
        className="
          w-full px-3 py-2 rounded-lg border border-gray-300
          focus:border-blue-500 focus:ring-2 focus:ring-blue-100
          disabled:bg-gray-100 disabled:cursor-not-allowed
          text-sm font-mono
        "
      />
    );
  }

  // 代码/模板
  if (schema.format === 'code' || schema.format === 'template') {
    return (
      <textarea
        value={(value as string) || ''}
        onChange={handleChange}
        disabled={disabled}
        required={required}
        placeholder={schema.description}
        rows={6}
        className="
          w-full px-3 py-2 rounded-lg border border-gray-300
          focus:border-blue-500 focus:ring-2 focus:ring-blue-100
          disabled:bg-gray-100 disabled:cursor-not-allowed
          text-sm font-mono resize-y
          bg-gray-50
        "
      />
    );
  }

  // 普通文本
  return (
    <input
      type="text"
      value={(value as string) || ''}
      onChange={handleChange}
      disabled={disabled}
      required={required}
      placeholder={schema.description}
      className="
        w-full px-3 py-2 rounded-lg border border-gray-300
        focus:border-blue-500 focus:ring-2 focus:ring-blue-100
        disabled:bg-gray-100 disabled:cursor-not-allowed
        text-sm
      "
    />
  );
};

/**
 * 数字字段
 */
const NumberField: React.FC<FieldRendererProps> = ({
  name,
  schema,
  value,
  onChange,
  required,
  disabled,
}) => {
  const handleChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const val = schema.type === 'integer' 
        ? parseInt(e.target.value, 10) 
        : parseFloat(e.target.value);
      onChange(name, isNaN(val) ? 0 : val);
    },
    [name, schema.type, onChange]
  );

  // 滑块
  if (schema.format === 'slider') {
    return (
      <div className="flex items-center gap-3">
        <input
          type="range"
          value={(value as number) || 0}
          onChange={handleChange}
          disabled={disabled}
          min={0}
          max={schema.type === 'integer' ? 100 : 1}
          step={schema.type === 'integer' ? 1 : 0.1}
          className="flex-1"
        />
        <span className="w-12 text-right text-sm text-gray-600 font-mono">
          {value as number}
        </span>
      </div>
    );
  }

  return (
    <input
      type="number"
      value={(value as number) ?? ''}
      onChange={handleChange}
      disabled={disabled}
      required={required}
      placeholder={schema.description}
      step={schema.type === 'integer' ? 1 : 'any'}
      className="
        w-full px-3 py-2 rounded-lg border border-gray-300
        focus:border-blue-500 focus:ring-2 focus:ring-blue-100
        disabled:bg-gray-100 disabled:cursor-not-allowed
        text-sm font-mono
      "
    />
  );
};

/**
 * 布尔字段
 */
const BooleanField: React.FC<FieldRendererProps> = ({
  name,
  schema,
  value,
  onChange,
  disabled,
}) => {
  const handleChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      onChange(name, e.target.checked);
    },
    [name, onChange]
  );

  return (
    <label className="flex items-center gap-2 cursor-pointer">
      <input
        type="checkbox"
        checked={(value as boolean) || false}
        onChange={handleChange}
        disabled={disabled}
        className="
          w-4 h-4 rounded border-gray-300
          text-blue-500 focus:ring-blue-500
          disabled:cursor-not-allowed
        "
      />
      <span className="text-sm text-gray-700">
        {schema.description || '启用'}
      </span>
    </label>
  );
};

/**
 * 枚举字段
 */
const EnumField: React.FC<FieldRendererProps> = ({
  name,
  schema,
  value,
  onChange,
  required,
  disabled,
}) => {
  const handleChange = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement>) => {
      onChange(name, e.target.value);
    },
    [name, onChange]
  );

  if (!schema.enum || schema.enum.length === 0) {
    return <span className="text-gray-500 text-sm">无可选项</span>;
  }

  return (
    <select
      value={(value as string) || ''}
      onChange={handleChange}
      disabled={disabled}
      required={required}
      className="
        w-full px-3 py-2 rounded-lg border border-gray-300
        focus:border-blue-500 focus:ring-2 focus:ring-blue-100
        disabled:bg-gray-100 disabled:cursor-not-allowed
        text-sm bg-white
      "
    >
      <option value="">请选择...</option>
      {schema.enum.map((option) => (
        <option key={option} value={option}>
          {option}
        </option>
      ))}
    </select>
  );
};

/**
 * 数组字段
 */
const ArrayField: React.FC<FieldRendererProps> = ({
  name,
  schema,
  value,
  onChange,
  disabled,
  path,
}) => {
  const items = (value as unknown[]) || [];
  const itemSchema = schema.items;

  const handleAdd = useCallback(() => {
    if (!itemSchema) return;
    const newItem = getDefaultValue(itemSchema);
    onChange(name, [...items, newItem]);
  }, [name, items, itemSchema, onChange]);

  const handleRemove = useCallback(
    (index: number) => {
      const newItems = items.filter((_, i) => i !== index);
      onChange(name, newItems);
    },
    [name, items, onChange]
  );

  const handleItemChange = useCallback(
    (index: number, newValue: unknown) => {
      const newItems = [...items];
      newItems[index] = newValue;
      onChange(name, newItems);
    },
    [name, items, onChange]
  );

  if (!itemSchema) {
    return <span className="text-gray-500 text-sm">未定义数组项类型</span>;
  }

  return (
    <div className="space-y-2">
      {items.map((item, index) => (
        <div
          key={index}
          className="flex items-start gap-2 p-2 bg-gray-50 rounded-lg border border-gray-200"
        >
          <div className="flex-1">
            <FieldRenderer
              name={`${index}`}
              schema={itemSchema}
              value={item}
              onChange={(_, val) => handleItemChange(index, val)}
              disabled={disabled}
              path={`${path || name}[${index}]`}
            />
          </div>
          <button
            type="button"
            onClick={() => handleRemove(index)}
            disabled={disabled}
            className="
              p-1.5 text-gray-400 hover:text-red-500
              disabled:opacity-50 disabled:cursor-not-allowed
            "
          >
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
      ))}
      
      <button
        type="button"
        onClick={handleAdd}
        disabled={disabled}
        className="
          flex items-center gap-1 px-3 py-1.5 text-sm
          text-blue-600 hover:text-blue-700 hover:bg-blue-50
          rounded-lg border border-dashed border-blue-300
          disabled:opacity-50 disabled:cursor-not-allowed
          transition-colors
        "
      >
        <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
        </svg>
        添加项
      </button>
    </div>
  );
};

/**
 * 对象字段
 */
const ObjectField: React.FC<FieldRendererProps> = ({
  name,
  schema,
  value,
  onChange,
  disabled,
  path,
}) => {
  const objectValue = (value as Record<string, unknown>) || {};
  const properties = schema.nestedProperties || {};

  const handleFieldChange = useCallback(
    (fieldName: string, fieldValue: unknown) => {
      onChange(name, {
        ...objectValue,
        [fieldName]: fieldValue,
      });
    },
    [name, objectValue, onChange]
  );

  return (
    <div className="space-y-4 pl-4 border-l-2 border-gray-200">
      {Object.entries(properties).map(([fieldName, fieldSchema]) => (
        <div key={fieldName}>
          <label className="block mb-1">
            <span className="text-sm font-medium text-gray-700">{fieldName}</span>
            {fieldSchema.description && (
              <span className="text-xs text-gray-500 ml-2">{fieldSchema.description}</span>
            )}
          </label>
          <FieldRenderer
            name={fieldName}
            schema={fieldSchema}
            value={objectValue[fieldName]}
            onChange={handleFieldChange}
            disabled={disabled}
            path={`${path || name}.${fieldName}`}
          />
        </div>
      ))}
    </div>
  );
};

// ==================== 字段渲染器 ====================

const FieldRenderer: React.FC<FieldRendererProps> = (props) => {
  const { schema } = props;

  // 枚举类型
  if (schema.enum && schema.enum.length > 0) {
    return <EnumField {...props} />;
  }

  // 根据类型选择渲染器
  switch (schema.type) {
    case 'string':
      return <StringField {...props} />;
    case 'number':
    case 'integer':
      return <NumberField {...props} />;
    case 'boolean':
      return <BooleanField {...props} />;
    case 'array':
      return <ArrayField {...props} />;
    case 'object':
      return <ObjectField {...props} />;
    default:
      return <StringField {...props} />;
  }
};

// ==================== 主组件 ====================

export const SchemaFormRenderer: React.FC<SchemaFormRendererProps> = ({
  schema,
  value,
  onChange,
  disabled = false,
  className = '',
}) => {
  const properties = schema.properties || {};
  const required = new Set(schema.required || []);

  const handleFieldChange = useCallback(
    (fieldName: string, fieldValue: unknown) => {
      onChange({
        ...value,
        [fieldName]: fieldValue,
      });
    },
    [value, onChange]
  );

  // 按照 required 排序，必填项在前
  const sortedFields = useMemo(() => {
    const entries = Object.entries(properties);
    return entries.sort((a, b) => {
      const aRequired = required.has(a[0]);
      const bRequired = required.has(b[0]);
      if (aRequired && !bRequired) return -1;
      if (!aRequired && bRequired) return 1;
      return 0;
    });
  }, [properties, required]);

  if (sortedFields.length === 0) {
    return (
      <div className={`text-gray-500 text-sm text-center py-4 ${className}`}>
        无可配置属性
      </div>
    );
  }

  return (
    <div className={`space-y-4 ${className}`}>
      {sortedFields.map(([fieldName, fieldSchema]) => (
        <div key={fieldName} className="space-y-1">
          <label className="flex items-center gap-2">
            <span className="text-sm font-medium text-gray-700">{fieldName}</span>
            {required.has(fieldName) && (
              <span className="text-red-500 text-xs">*</span>
            )}
          </label>
          {fieldSchema.description && (
            <p className="text-xs text-gray-500 mb-1">{fieldSchema.description}</p>
          )}
          <FieldRenderer
            name={fieldName}
            schema={fieldSchema}
            value={value[fieldName]}
            onChange={handleFieldChange}
            required={required.has(fieldName)}
            disabled={disabled}
            path={fieldName}
          />
        </div>
      ))}
    </div>
  );
};

export default SchemaFormRenderer;
