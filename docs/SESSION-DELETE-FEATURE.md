# 会话删除功能实现文档

## 概述
为 AgentGroupChat.Web 应用添加了会话删除功能，包括前端 UI 和后端级联删除逻辑。

## 功能特性

### 1. 级联删除
- 删除会话时自动删除该会话的所有相关消息
- 使用 LiteDB 的 `DeleteMany` 方法实现高效批量删除
- 后端记录删除的会话和消息数量

### 2. 前端交互优化
- 会话列表每项添加删除图标按钮（红色垃圾桶图标）
- 使用 MudBlazor 的确认对话框，防止误删
- 删除按钮不会触发会话切换（事件冒泡处理）

### 3. 智能会话切换
- 删除当前会话后自动切换到第一个可用会话
- 如果删除后没有会话，自动创建新会话
- 确保用户始终有可用的会话进行对话

## 代码修改详情

### 后端修改

#### `PersistedSessionService.cs`
```csharp
public void DeleteSession(string id)
{
    try
    {
        // 先删除该会话的所有消息（级联删除）
        var deletedMessagesCount = _messages.DeleteMany(m => m.SessionId == id);
        
        // 再删除会话本身
        var deleted = _sessions.Delete(id);
        
        // 从缓存中移除
        _hotCache.Remove(id);
        
        if (deleted)
        {
            _logger?.LogInformation("Deleted session {SessionId} and {MessageCount} related messages", 
                id, deletedMessagesCount);
        }
        else
        {
            _logger?.LogWarning("Session {SessionId} not found for deletion", id);
        }
    }
    catch (Exception ex)
    {
        _logger?.LogError(ex, "Error deleting session {SessionId}", id);
        throw;
    }
}
```

### 前端修改

#### `Home.razor` - UI 部分
```razor
<!-- 会话列表项中添加删除按钮 -->
<div class="d-flex align-center justify-space-between" style="width: 100%;">
    <div class="d-flex flex-column flex-grow-1" @onclick="() => LoadSession(session.Id)" style="cursor: pointer;">
        <MudText Typo="Typo.body1"><strong>@session.Name</strong></MudText>
        <MudText Typo="Typo.body2" Color="Color.Secondary">@session.LastUpdated.ToString("MMM dd, HH:mm")</MudText>
    </div>
    <MudIconButton Icon="@Icons.Material.Filled.Delete" 
                   Color="Color.Error" 
                   Size="Size.Small"
                   @onclick="() => DeleteSessionWithConfirmation(session.Id)"
                   title="删除会话"
                   Class="ml-2" />
</div>
```

#### `Home.razor` - 业务逻辑部分
```csharp
/// <summary>
/// 删除会话（带确认对话框）
/// </summary>
private async Task DeleteSessionWithConfirmation(string sessionId)
{
    var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
    if (session == null) return;

    var dialog = await DialogService.ShowMessageBox(
        "确认删除", 
        $"确定要删除会话 \"{session.Name}\" 吗？此操作将同时删除该会话的所有消息，且不可恢复。",
        yesText: "删除",
        cancelText: "取消");

    if (dialog == true)
    {
        await DeleteSession(sessionId);
    }
}

/// <summary>
/// 删除会话的核心逻辑
/// </summary>
private async Task DeleteSession(string sessionId)
{
    try
    {
        // 调用 API 删除会话（后端会级联删除消息）
        var success = await AgentHostClient.DeleteSessionAsync(sessionId);
        
        if (success)
        {
            // 从列表中移除
            var deletedSession = _sessions.FirstOrDefault(s => s.Id == sessionId);
            if (deletedSession != null)
            {
                _sessions.Remove(deletedSession);
            }

            // 如果删除的是当前会话，需要切换到其他会话
            if (_currentSession?.Id == sessionId)
            {
                if (_sessions.Count > 0)
                {
                    // 切换到第一个会话
                    await LoadSession(_sessions.First().Id);
                }
                else
                {
                    // 没有会话了，创建一个新会话
                    await CreateNewSessionAsync();
                }
            }

            StateHasChanged();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error deleting session: {ex.Message}");
    }
}
```

## 使用方法

1. **删除会话**
   - 在左侧会话列表中，将鼠标悬停在会话上
   - 点击右侧的红色删除图标
   - 在确认对话框中点击"删除"按钮

2. **安全机制**
   - 删除前会显示确认对话框，包含会话名称
   - 提示用户删除操作不可恢复
   - 可以点击"取消"按钮中止删除操作

3. **自动切换**
   - 删除当前正在查看的会话后，系统会自动切换到列表中的第一个会话
   - 如果删除后没有任何会话，系统会自动创建一个新会话

## API 端点

现有的 API 端点已经支持删除功能：

```
DELETE /api/sessions/{id}
```

此端点会：
1. 删除指定会话
2. 级联删除该会话的所有消息
3. 返回成功状态

## 依赖项

- **MudBlazor**: 用于 UI 组件（`MudIconButton`, `IDialogService`）
- **LiteDB**: 用于数据库操作（`DeleteMany`）
- **AgentHostClient**: 已有的 HTTP 客户端服务

## 注意事项

1. **数据安全**: 删除操作不可逆，请确保用户理解此操作的后果
2. **性能**: 使用 `DeleteMany` 批量删除消息，性能优于循环删除
3. **日志记录**: 后端会记录删除操作，包括删除的消息数量
4. **缓存管理**: 删除会话后会同步清除内存缓存

## 测试建议

1. 测试删除普通会话
2. 测试删除当前正在查看的会话
3. 测试删除最后一个会话（应自动创建新会话）
4. 测试取消删除操作
5. 验证消息确实被级联删除（通过 Debug API 端点）

## 扩展建议

### 可选的未来改进
1. **软删除**: 添加"回收站"功能，支持恢复误删的会话
2. **批量删除**: 支持选择多个会话一次性删除
3. **导出功能**: 删除前允许导出会话数据
4. **统计信息**: 在确认对话框中显示会话的消息数量
5. **撤销功能**: 删除后短时间内允许撤销操作

## 相关文件

- `src/AgentGroupChat.AgentHost/Services/PersistedSessionService.cs`
- `src/AgentGroupChat.Web/Components/Pages/Home.razor`
- `src/AgentGroupChat.Web/Services/AgentHostClient.cs`
- `src/AgentGroupChat.AgentHost/Program.cs`
