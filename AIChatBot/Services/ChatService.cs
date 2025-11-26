using AIChatBot.Models;
using Microsoft.Extensions.AI;

namespace AIChatBot.Services
{
    public class ChatService
    {
        private readonly IChatClient _chatClient;
        private readonly ConversationMemoryService _memory;
        private readonly RagService _rag;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IChatClient chatClient,
            ConversationMemoryService memory,
            RagService rag,
            ILogger<ChatService> logger)
        {
            _chatClient = chatClient;
            _memory = memory;
            _rag = rag;
            _logger = logger;
        }

        public async Task<AIChatBot.Models.ChatResponse> ProcessMessageAsync(ChatRequest request, UserContext userContext)
        {
            try
            {
                _logger.LogInformation($"Yeni mesaj: {request.SessionId}");

                var systemPrompt = BuildSystemPrompt(userContext);
                var relevantDocs = _rag.SearchDocuments(request.Message);
                var ragContext = _rag.FormatDocumentsAsContext(relevantDocs);

                var messages = _memory.GetHistory(request.SessionId);

                if (!messages.Any(m => m.Role == ChatRole.System))
                {
                    var fullPrompt = systemPrompt + (string.IsNullOrEmpty(ragContext) ? "" : "\n\n" + ragContext);
                    messages.Insert(0, new ChatMessage(ChatRole.System, fullPrompt));
                }

                var userMessage = new ChatMessage(ChatRole.User, request.Message);
                messages.Add(userMessage);
                _memory.AddMessage(request.SessionId, userMessage);

                _logger.LogInformation("LLM'e istek atılıyor...");

                // ✅ Streaming kullanarak metni topla (daha güvenli)
                var responseText = "";
                await foreach (var update in _chatClient.GetStreamingResponseAsync(messages))
                {
                    responseText += update.Text;
                }

                // ✅ ChatMessage oluştur
                var assistantMessage = new ChatMessage(ChatRole.Assistant, responseText);
                _memory.AddMessage(request.SessionId, assistantMessage);

                return new AIChatBot.Models.ChatResponse
                {
                    SessionId = request.SessionId,
                    Answer = responseText,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Hata: {ex.Message}");
                return new AIChatBot.Models.ChatResponse
                {
                    SessionId = request.SessionId,
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private string BuildSystemPrompt(UserContext userContext)
        {
            return $@"Sen yardımcı bir asistansın.
Kullanıcı: {userContext.UserName} ({userContext.Role})
Dili: Türkçe kullan.
Cevapları kısa ve net ver.";
        }

        public List<ChatMessage> GetSessionHistory(string sessionId) => _memory.GetHistory(sessionId);
        public void ClearSession(string sessionId) => _memory.ClearSession(sessionId);
    }
}