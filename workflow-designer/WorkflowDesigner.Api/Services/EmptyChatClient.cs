using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace WorkflowDesigner.Api.Services;

/// <summary>
/// 空的 ChatClient 实现，用于没有配置 API 密钥时的测试
/// </summary>
public class EmptyChatClient : IChatClient
{
    public ChatClientMetadata Metadata => new("empty", new Uri("http://localhost"));

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(500, cancellationToken);
        
        var lastMessage = chatMessages.LastOrDefault()?.Text ?? "";
        var response = $"[模拟响应] 收到消息: {lastMessage}";

        return new ChatResponse([new ChatMessage(ChatRole.Assistant, response)]);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(500, cancellationToken);
        
        var lastMessage = chatMessages.LastOrDefault()?.Text ?? "";
        var response = $"[模拟响应] 收到消息: {lastMessage}";

        yield return new ChatResponseUpdate
        {
            Role = ChatRole.Assistant,
            Contents = [new TextContent(response)]
        };
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return null;
    }

    public void Dispose()
    {
        // No-op
    }
}
