// C:\DOSYALAR\AI.NET\AIChatBot\AIChatBot\Repository\IChatMemoryRepository.cs
using Microsoft.Extensions.AI;

namespace AIChatBot.Repository.ChatMemory
{
    public interface IChatMemoryRepository
    {
        Task SaveMessageAsync(string sessionId, string userId, string userName, string role, string content);
        Task<List<ChatMessage>> GetHistoryAsync(string sessionId);
        Task ClearSessionAsync(string sessionId);
    }
}