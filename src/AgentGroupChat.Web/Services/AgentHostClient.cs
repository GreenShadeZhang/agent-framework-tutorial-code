using AgentGroupChat.Models;
using System.Net.Http.Json;

namespace AgentGroupChat.Web.Services;

/// <summary>
/// HTTP client for communicating with AgentHost API.
/// </summary>
public class AgentHostClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AgentHostClient> _logger;

    public AgentHostClient(HttpClient httpClient, ILogger<AgentHostClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Get all available agent profiles.
    /// </summary>
    public async Task<List<AgentProfile>> GetAgentsAsync()
    {
        try
        {
            var agents = await _httpClient.GetFromJsonAsync<List<AgentProfile>>("api/agents");
            return agents ?? new List<AgentProfile>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get agents from AgentHost");
            return new List<AgentProfile>();
        }
    }

    /// <summary>
    /// Get all chat sessions.
    /// </summary>
    public async Task<List<ChatSession>> GetSessionsAsync()
    {
        try
        {
            var sessions = await _httpClient.GetFromJsonAsync<List<ChatSession>>("api/sessions");
            return sessions ?? new List<ChatSession>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get sessions from AgentHost");
            return new List<ChatSession>();
        }
    }

    /// <summary>
    /// Create a new chat session.
    /// </summary>
    public async Task<ChatSession?> CreateSessionAsync()
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/sessions", new { });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ChatSession>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create session in AgentHost");
            return null;
        }
    }

    /// <summary>
    /// Get a specific chat session by ID.
    /// </summary>
    public async Task<ChatSession?> GetSessionAsync(string sessionId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ChatSession>($"api/sessions/{sessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get session {SessionId} from AgentHost", sessionId);
            return null;
        }
    }

    /// <summary>
    /// Send a chat message and get agent responses.
    /// </summary>
    public async Task<List<ChatMessage>> SendMessageAsync(string sessionId, string message)
    {
        try
        {
            var request = new ChatRequest(sessionId, message);
            var response = await _httpClient.PostAsJsonAsync("api/chat", request);
            response.EnsureSuccessStatusCode();
            
            var messages = await response.Content.ReadFromJsonAsync<List<ChatMessage>>();
            return messages ?? new List<ChatMessage>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to AgentHost");
            
            // Return error message
            return new List<ChatMessage>
            {
                new ChatMessage
                {
                    AgentId = "system",
                    AgentName = "System",
                    AgentAvatar = "⚠️",
                    Content = $"Error: {ex.Message}. Please check your configuration and ensure AgentHost is running.",
                    IsUser = false,
                    MessageType = "error"
                }
            };
        }
    }

    /// <summary>
    /// Delete a chat session.
    /// </summary>
    public async Task<bool> DeleteSessionAsync(string sessionId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/sessions/{sessionId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete session {SessionId}", sessionId);
            return false;
        }
    }

    /// <summary>
    /// Clear conversation in a session (keep session, remove messages).
    /// </summary>
    public async Task<bool> ClearConversationAsync(string sessionId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/sessions/{sessionId}/clear", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear conversation for session {SessionId}", sessionId);
            return false;
        }
    }

    /// <summary>
    /// Get conversation history for a session.
    /// </summary>
    public async Task<List<ChatMessage>> GetConversationHistoryAsync(string sessionId)
    {
        try
        {
            var messages = await _httpClient.GetFromJsonAsync<List<ChatMessage>>($"api/sessions/{sessionId}/messages");
            return messages ?? new List<ChatMessage>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get conversation history for session {SessionId}", sessionId);
            return new List<ChatMessage>();
        }
    }

    /// <summary>
    /// Get system statistics.
    /// </summary>
    public async Task<Dictionary<string, object>?> GetStatisticsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<Dictionary<string, object>>("api/stats");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get statistics from AgentHost");
            return null;
        }
    }
}

/// <summary>
/// Request model for chat API.
/// </summary>
public record ChatRequest(string SessionId, string Message);
