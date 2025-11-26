using Microsoft.Extensions.AI;

namespace AIChatBot.Services
{
    public class ConversationMemoryService
    {
        private readonly Dictionary<string, List<ChatMessage>> _conversations = new();

        public void AddMessage(string sessionId, ChatMessage message)
        {
            if (!_conversations.ContainsKey(sessionId))
            {
                _conversations[sessionId] = new List<ChatMessage>();
            }
            _conversations[sessionId].Add(message);
        }

        public List<ChatMessage> GetHistory(string sessionId)
        {
            return _conversations.ContainsKey(sessionId)
                ? _conversations[sessionId]
                : new List<ChatMessage>();
        }

        public void ClearSession(string sessionId)
        {
            if (_conversations.ContainsKey(sessionId))
                _conversations.Remove(sessionId);
        }
    }
}