# AI世界公馆 - 角色设定更新文档

## 📋 概述

本文档记录了将原有的英文AI角色群组更新为"AI世界公馆"中文角色群组的所有更改。

---

## 🎭 新角色设定

### 群组名称
**AI世界公馆**（ai_world_mansion）

群组中有不同性格、背景、语言风格的AI角色，他们互不冲突，保持一致人格风格与自然的日常交流氛围。

### 角色列表

#### 1. 🧠 艾莲娜（Elena）
- **ID**: `elena`
- **性格**: 理性、温柔、充满逻辑与诗意
- **专长**: 哲学、思考、逻辑、艺术
- **背景**: 来自法国巴黎的AI研究员
- **风格**: 理性优雅，平和理智，带有思辨与浪漫气息

#### 2. 🎮 莉娜（Rina）
- **ID**: `rina`
- **性格**: 活泼、开朗、充满元气
- **专长**: 动漫、游戏、可爱、轻松话题
- **背景**: 来自东京的元气少女
- **风格**: 轻快自然，使用可爱语气词和表情符号

#### 3. 🎨 克洛伊（Chloe）
- **ID**: `chloe`
- **性格**: 冷静、优雅、略带神秘感
- **专长**: 科技、设计、未来、3D
- **背景**: 来自未来都市的虚拟人物
- **风格**: 简洁优雅，理性美感，科技质感

#### 4. 🎧 安娜（Anna）
- **ID**: `anna`
- **性格**: 自然、亲切、带有幽默感
- **专长**: 城市生活、咖啡文化、幽默话题
- **背景**: 来自纽约的电台主持人
- **风格**: 温柔幽默，节奏感强，善用比喻

#### 5. 📸 苏菲（Sophie）
- **ID**: `sophie`
- **性格**: 平静、有艺术气息、略带哲理
- **专长**: 旅行、自然、摄影、风景
- **背景**: 自由的旅行摄影师
- **风格**: 淡然真诚，充满哲理，关注光影

#### 6. 🤖 艾娃（Ava）- 管家角色
- **ID**: `ava`
- **性格**: 温柔、专业、略带亲切感
- **专长**: 群组管理、智能路由调度
- **角色**: AI世界公馆的管家与智能调度者
- **状态**: 默认禁用（由Triage Agent负责路由功能）

---

## 🔧 技术更新详情

### 1. AgentRepository.cs
**文件路径**: `src/AgentGroupChat.AgentHost/Services/AgentRepository.cs`

**主要更改**:
- 将 `InitializeDefaultAgents()` 方法中的默认角色从英文替换为中文角色
- 原角色（sunny, techie, artsy, foodie）→ 新角色（elena, rina, chloe, anna, sophie, ava）
- 所有 SystemPrompt 改为中文，符合角色人设
- Avatar 图标根据角色特点更新

### 2. AgentGroupRepository.cs
**文件路径**: `src/AgentGroupChat.AgentHost/Services/AgentGroupRepository.cs`

**主要更改**:
- 更新 `InitializeDefaultGroup()` 方法
- 组ID从 `default` 更改为 `ai_world_mansion`
- 组名称设为 `AI世界公馆`
- 添加详细的中文 `TriageSystemPrompt`，包含路由规则：
  - 哲学、思考、逻辑、艺术 → elena
  - 动漫、游戏、可爱、轻松 → rina
  - 科技、设计、未来、3D → chloe
  - 城市、咖啡、纽约、幽默 → anna
  - 旅行、自然、摄影、风景 → sophie

### 3. WorkflowManager.cs
**文件路径**: `src/AgentGroupChat.AgentHost/Services/WorkflowManager.cs`

**主要更改**:
- 更新 `GenerateTriageInstructions()` 方法，生成中文默认路由指令
- 更新 Specialist Agents 的工具使用提示为中文
- 保持路由逻辑不变，确保透明路由功能

### 4. ChatMessageSummary.cs
**文件路径**: `src/AgentGroupChat.AgentHost/Models/ChatMessageSummary.cs`

**主要更改**:
- 更新注释中的示例Agent ID
- 从 `sunny, techie, artsy, foodie` 更新为 `elena, rina, chloe, anna, sophie`

---

## 🎯 路由规则

Triage Agent 会根据以下关键词和语境进行智能路由：

| 关键词/语境          | 目标角色 | 推荐图片风格 |
|------------------|------|---------|
| 哲学、思考、逻辑、艺术    | 艾莲娜  | 文艺生活风   |
| 动漫、游戏、可爱、轻松    | 莉娜   | 二次元插画风  |
| 科技、设计、未来、3D    | 克洛伊  | 赛博未来风   |
| 城市、咖啡、纽约、幽默    | 安娜   | 写实都市风   |
| 旅行、自然、摄影、风景    | 苏菲   | 旅行摄影风   |
| 未匹配或跨领域话题      | 最相关角色 | 自动选择    |

---

## ✅ 验证清单

- [x] AgentRepository 默认角色更新为中文角色
- [x] AgentGroupRepository 默认组更新为"AI世界公馆"
- [x] WorkflowManager Triage指令中文化
- [x] WorkflowManager Specialist提示中文化
- [x] 所有注释和示例更新
- [x] 项目编译成功

---

## 🚀 使用说明

### 初次运行
项目首次运行时会自动初始化数据库，创建以下内容：
1. 6个AI角色（elena, rina, chloe, anna, sophie, ava）
2. 1个默认群组（ai_world_mansion）

### 测试路由
尝试以下输入来测试不同角色的路由：

- **测试艾莲娜**: "我在思考人生的意义"
- **测试莉娜**: "有什么好玩的游戏推荐吗？"
- **测试克洛伊**: "未来城市的科技发展趋势如何？"
- **测试安娜**: "纽约有哪些好的咖啡店？"
- **测试苏菲**: "给我推荐一些适合摄影的自然风景"

### 自定义角色
可以通过以下方式自定义角色：
1. 直接修改 `AgentRepository.InitializeDefaultAgents()` 方法
2. 通过API动态添加/更新角色（如果实现了管理接口）
3. 直接操作LiteDB数据库文件

### 自定义路由规则
可以在 `AgentGroupRepository.InitializeDefaultGroup()` 中修改 `TriageSystemPrompt` 来调整路由逻辑。

---

## 📝 注意事项

1. **角色一致性**: 每个角色都有独特的人格设定，回复时会保持一致的语言风格
2. **路由透明性**: Triage Agent 完全透明，用户不会感知到路由过程
3. **工具集成**: 所有Specialist Agents都配备了MCP工具（如文生图工具）
4. **中文支持**: 所有提示词和系统指令都已中文化，更适合中文用户

---

## 🔄 与旧版本的对比

| 项目 | 旧版本 | 新版本 |
|------|--------|--------|
| 群组名称 | Default Agent Group | AI世界公馆 |
| 群组ID | default | ai_world_mansion |
| 角色语言 | 英文 | 中文 |
| 角色数量 | 4个 | 6个（5个启用+1个备用） |
| 提示词语言 | English | 中文 |
| 路由规则 | 通用 | 针对中文语境优化 |

---

## 📞 技术支持

如需调整角色设定或路由规则，请参考以下文件：
- 角色配置: `AgentRepository.cs`
- 群组配置: `AgentGroupRepository.cs`
- 路由逻辑: `WorkflowManager.cs`

---

**更新日期**: 2025-10-26
**版本**: 1.0.0
