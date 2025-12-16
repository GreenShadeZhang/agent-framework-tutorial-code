# Agent Framework YAML 工作流示例

本目录包含多个 Agent Framework 声明式工作流的 YAML 示例文件。这些示例展示了不同的工作流模式和智能体协作方式。

## 📁 示例列表

### 1. 并行执行多智能体 (`01-parallel-agents.yaml`)

**模式**: Fan-out / Fan-in 并行执行

**场景**: 三个领域专家（研究、营销、法务）同时分析用户问题，最后汇总结果。

**关键特性**:
- 多个智能体并行执行
- 结果聚合与综合分析
- 适用于需要多角度分析的场景

```
┌─────────┐     ┌──────────────┐
│ receive │────►│ research_agt │──┐
│  input  │     └──────────────┘  │
│         │     ┌──────────────┐  │    ┌───────────┐    ┌────────┐
│         │────►│marketing_agt │──┼───►│ aggregate │───►│ output │
│         │     └──────────────┘  │    └───────────┘    └────────┘
│         │     ┌──────────────┐  │
│         │────►│  legal_agt   │──┘
└─────────┘     └──────────────┘
```

---

### 2. 条件分支路由 (`02-conditional-routing.yaml`)

**模式**: Switch/Case 条件路由

**场景**: 智能客服系统，根据用户意图路由到不同专业处理分支。

**关键特性**:
- 意图分类
- 条件分支 (condition)
- 循环交互
- 多个专业智能体

```
                    ┌─────────────┐
                ┌──►│ product_agt │──┐
                │   └─────────────┘  │
┌──────────┐    │   ┌─────────────┐  │   ┌──────────┐
│ classify │────┼──►│   tech_agt  │──┼──►│ response │──► (循环)
│  intent  │    │   └─────────────┘  │   └──────────┘
└──────────┘    │   ┌─────────────┐  │
                ├──►│complaint_agt│──┤
                │   └─────────────┘  │
                │   ┌─────────────┐  │
                └──►│ general_agt │──┘
                    └─────────────┘
```

---

### 3. 迭代优化工作流 (`03-iterative-refinement.yaml`)

**模式**: Writer-Reviewer 循环

**场景**: 内容写作与评审迭代，直到内容质量达标或达到最大迭代次数。

**关键特性**:
- 循环迭代模式
- 质量评估与反馈
- 最大迭代次数控制
- 条件退出

```
┌────────┐    ┌────────┐    ┌──────────┐
│ Writer │───►│ Review │───►│ Approved?│
└────────┘    └────────┘    └──────────┘
     ▲                           │
     │    ┌─────────────┐       │ Yes
     └────│   No, Max?  │◄──────┤
          └─────────────┘       │
                │               ▼
                │          ┌─────────┐
                └─────────►│  Done   │
                   Max=3   └─────────┘
```

---

### 4. 多步骤数据分析 (`04-multi-step-analysis.yaml`)

**模式**: 顺序流水线

**场景**: 完整的数据分析流程：收集→清洗→分析→可视化→报告。

**关键特性**:
- 多步骤顺序执行
- 数据在步骤间传递
- 每个步骤有专业智能体
- 最终生成综合报告

```
┌─────────┐    ┌─────────┐    ┌─────────┐    ┌─────────┐    ┌────────┐
│ Collect │───►│  Clean  │───►│ Analyze │───►│   Viz   │───►│ Report │
└─────────┘    └─────────┘    └─────────┘    └─────────┘    └────────┘
```

---

### 5. Foreach 批量处理 (`05-foreach-processing.yaml`)

**模式**: Foreach 循环

**场景**: 批量处理多个任务，如批量翻译、批量分析等。

**关键特性**:
- 动态任务列表解析
- Foreach 循环迭代
- 结果收集与汇总
- 进度显示

```
┌───────────┐    ┌─────────────────────────┐    ┌─────────┐
│ Get Tasks │───►│ Foreach: Process Task  │───►│ Summary │
└───────────┘    │   ┌─────┐   ┌───────┐  │    └─────────┘
                 │   │Task1│──►│Result1│  │
                 │   ├─────┤   ├───────┤  │
                 │   │Task2│──►│Result2│  │
                 │   ├─────┤   ├───────┤  │
                 │   │Task3│──►│Result3│  │
                 │   └─────┘   └───────┘  │
                 └─────────────────────────┘
```

---

### 6. RAG 问答系统 (`06-rag-qa-workflow.yaml`)

**模式**: 检索增强生成 (RAG)

**场景**: 基于知识库的智能问答系统。

**关键特性**:
- 查询理解与优化
- 知识检索（模拟）
- 基于上下文的答案生成
- 置信度评估
- 持续对话

```
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌────────┐
│  Query   │───►│ Retrieve │───►│ Generate │───►│ Answer │
│ Optimize │    │ Context  │    │  Answer  │    │        │
└──────────┘    └──────────┘    └──────────┘    └────────┘
      ▲                                              │
      └──────────────────────────────────────────────┘
                        (继续对话)
```

---

## 🚀 如何使用

1. 在 Workflow Designer 中点击 "导入 YAML" 按钮
2. 选择一个示例文件
3. 工作流将自动导入并显示在画布上
4. 点击 "执行" 按钮测试工作流

## 📝 YAML 格式说明

### 基本结构

```yaml
kind: Workflow
metadata:
  name: workflow_name
  version: "1.0"
  description: 工作流描述

variables:
  - name: VariableName
    scope: Local  # Local 或 Conversation
    type: string  # string, number, boolean, array, object

executors:
  - id: executor_id
    type: ExecutorType
    properties:
      # 类型特定的属性
    edges:
      - targetId: next_executor
        condition: optional_condition  # 可选的条件表达式
```

### 支持的执行器类型

| 类型 | 说明 |
|------|------|
| `SetVariable` | 设置变量值 |
| `SendActivity` | 发送消息给用户 |
| `Question` | 向用户提问并获取输入 |
| `InvokeAzureAgent` | 调用 AI 智能体 |
| `Foreach` | 循环处理集合 |
| `Parallel` | 并行执行多个分支 |

### 变量引用语法

- `=Variable` 或 `=Local.Variable` - 整个值是变量引用
- `${Variable}` - 嵌入在字符串中的变量
- `$(Variable)` - 兼容格式

### 条件表达式

```yaml
edges:
  - targetId: branch_a
    condition: Local.Value == 'A'
  - targetId: branch_b
    condition: Local.Value == 'B'
  - targetId: default_branch  # 无条件，作为默认分支
```

---

## 🔧 扩展建议

1. **添加真实的知识库集成**: 将 RAG 示例中的模拟检索替换为实际的向量数据库调用
2. **添加外部 API 调用**: 使用 HTTP 执行器调用外部服务
3. **添加人工审核节点**: 在关键决策点添加人工审批步骤
4. **持久化对话历史**: 集成数据库存储对话上下文

---

## 📚 参考资料

- [Agent Framework 官方文档](https://github.com/microsoft/agent-framework)
- [声明式工作流格式规范](../docs/AGENT-FRAMEWORK-YAML-FORMAT.md)
