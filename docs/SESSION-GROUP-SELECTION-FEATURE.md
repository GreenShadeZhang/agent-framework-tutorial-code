# 会话创建时选择 Agent Group 功能实现

## 功能概述

实现了在创建新会话时强制用户选择指定的 Agent Group，确保每个会话都关联到正确的智能体组，防止使用错误的 agent group。

## 实现的功能

### 1. 后端修改

#### 1.1 模型修改
**文件**: `AgentGroupChat.AgentHost/Models/PersistedChatSession.cs`

添加了 `GroupId` 字段：
```csharp
/// <summary>
/// 所属的 Agent Group ID（必填）
/// </summary>
public string GroupId { get; set; } = string.Empty;
```

#### 1.2 服务修改
**文件**: `AgentGroupChat.AgentHost/Services/PersistedSessionService.cs`

修改 `CreateSession` 方法支持 `groupId` 参数：
```csharp
public PersistedChatSession CreateSession(string? name = null, string? groupId = null)
{
    // ...
    GroupId = groupId ?? string.Empty,
    // ...
}
```

#### 1.3 API 端点修改
**文件**: `AgentGroupChat.AgentHost/Program.cs`

- **创建会话端点**: 接受 `CreateSessionRequest` 包含可选的 `Name` 和 `GroupId`
- **获取会话端点**: 返回数据中包含 `GroupId`
- **发送消息端点**: 使用会话关联的 `GroupId` 发送消息

添加了新的请求模型：
```csharp
public record CreateSessionRequest(string? Name = null, string? GroupId = null);
```

### 2. 前端修改

#### 2.1 模型修改
**文件**: `AgentGroupChat.Web/Models/ChatSession.cs`

添加了 `GroupId` 属性：
```csharp
/// <summary>
/// 所属的 Agent Group ID
/// </summary>
public string GroupId { get; set; } = string.Empty;
```

#### 2.2 客户端服务修改
**文件**: `AgentGroupChat.Web/Services/AgentHostClient.cs`

修改 `CreateSessionAsync` 方法接受 `groupId` 参数：
```csharp
public async Task<ChatSession?> CreateSessionAsync(string? groupId = null, string? name = null)
{
    var request = new { Name = name, GroupId = groupId };
    // ...
}
```

#### 2.3 UI 组件修改

**文件**: `AgentGroupChat.Web/Components/Pages/Home.razor`

1. **加载 Agent Groups**：在初始化时加载所有可用的 agent groups
2. **创建会话流程**：
   - 检查是否有可用的 groups
   - 如果没有，提示用户初始化默认数据
   - 显示选择 group 的对话框
   - 使用选中的 groupId 创建会话

3. **显示 Group 信息**：
   - 在会话列表中显示每个会话所属的 group
   - 在聊天头部显示当前会话的 group

**新增文件**: `AgentGroupChat.Web/Components/Dialogs/SelectGroupDialog.razor`

创建了一个自定义对话框组件，用于选择 Agent Group：
- 使用 MudBlazor 的 RadioGroup 组件
- 显示每个 group 的名称、描述和包含的智能体数量
- 默认选择第一个 group
- 支持取消操作

## 用户体验流程

1. **用户点击 "New Chat" 按钮**
2. **系统检查可用的 Agent Groups**
   - 如果没有 groups，提示初始化默认数据
   - 如果初始化成功，继续下一步
3. **显示 Agent Group 选择对话框**
   - 展示所有可用的 groups
   - 显示每个 group 的详细信息
   - 默认选中第一个 group
4. **用户选择 Group 并确认**
   - 创建新会话并关联到选中的 group
   - 切换到新创建的会话
5. **会话列表和聊天界面显示 Group 信息**
   - 侧边栏会话列表显示每个会话所属的 group
   - 聊天头部显示当前会话的 group 标签

## 技术要点

### MudBlazor Dialog 使用
使用 MudBlazor 8.x 的对话框组件：
- `IMudDialogInstance` 作为级联参数
- `DialogService.ShowAsync<T>()` 显示自定义对话框
- `DialogParameters` 传递参数给对话框
- `DialogResult` 返回选择结果

### 数据流
```
前端 Home.razor 
  → SelectGroupDialog (选择 Group)
  → AgentHostClient.CreateSessionAsync(groupId)
  → AgentHost API /api/sessions (POST with groupId)
  → PersistedSessionService.CreateSession(name, groupId)
  → 保存到 LiteDB
```

### 消息发送流程
```
前端 SendMessage
  → AgentHostClient.SendMessageAsync(sessionId, message)
  → AgentHost API /api/chat (POST)
  → 获取 session.GroupId
  → AgentChatService.SendMessageAsync(message, sessionId, groupId)
  → WorkflowManager 使用正确的 Group 处理消息
```

## 优势

1. **防止错误**：强制选择 group，避免使用错误的智能体组
2. **清晰可见**：在 UI 中明确显示每个会话所属的 group
3. **灵活性**：支持多个 agent groups，用户可以根据需求选择
4. **数据完整性**：每个会话都必须关联到一个有效的 group
5. **向后兼容**：如果 groupId 为空，系统仍可正常工作（降级处理）

## 测试建议

1. **创建会话测试**
   - 测试在没有 groups 时的初始化流程
   - 测试选择不同的 groups 创建会话
   - 测试取消创建会话操作

2. **消息发送测试**
   - 验证消息是否使用正确的 group 处理
   - 测试不同 groups 的智能体响应

3. **UI 显示测试**
   - 验证会话列表中 group 信息的显示
   - 验证聊天头部 group 标签的显示
   - 测试切换不同会话时 group 信息的更新

4. **边界情况测试**
   - 测试只有一个 group 的情况
   - 测试 group 被删除后的会话行为
   - 测试并发创建会话的情况

## 后续改进建议

1. **会话命名**：允许用户在创建会话时自定义会话名称
2. **Group 过滤**：在会话列表中按 group 筛选会话
3. **Group 切换**：允许用户在已有会话中切换 group（需谨慎处理）
4. **默认 Group**：记住用户上次选择的 group 作为默认值
5. **Group 详情**：在对话框中显示更多 group 信息（如包含的具体智能体列表）
