using Microsoft.Extensions.AI;

namespace AIChatBot.Services
{
    public class ConversationMemoryService
    {
        private readonly Dictionary<string, List<ChatMessage>> _conversations = new();
        private readonly object _lock = new object();

        public void AddMessage(string sessionId, ChatMessage message)
        {
            lock (_lock)
            {
                if (!_conversations.ContainsKey(sessionId))
                {
                    _conversations[sessionId] = new List<ChatMessage>();
                }

                // ✅ Duplicate kontrolü (aynı mesaj arka arkaya eklenmişse engelle)
                var history = _conversations[sessionId];

                // Son mesaj ile aynıysa ekleme
                if (history.Any() &&
                    history.Last().Role == message.Role &&
                    history.Last().Text == message.Text)
                {
                    return; // Duplicate, ekleme
                }

                _conversations[sessionId].Add(message);
            }
        }

        public List<ChatMessage> GetHistory(string sessionId)
        {
            lock (_lock)
            {
                return _conversations.ContainsKey(sessionId)
                    ? new List<ChatMessage>(_conversations[sessionId]) // Copy döndür
                    : new List<ChatMessage>();
            }
        }

        public void ClearSession(string sessionId)
        {
            lock (_lock)
            {
                if (_conversations.ContainsKey(sessionId))
                    _conversations.Remove(sessionId);
            }
        }
    }
}