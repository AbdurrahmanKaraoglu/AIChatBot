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

                // 1. System Prompt
                var systemPrompt = BuildSystemPrompt(userContext);

                // 2.  RAG (Belge Arama)
                var relevantDocs = _rag.SearchDocuments(request.Message);
                var ragContext = _rag.FormatDocumentsAsContext(relevantDocs);

                // 3. Geçmişi al
                var messages = _memory.GetHistory(request.SessionId);

                // 4. System prompt ekle (yoksa)
                if (!messages.Any(m => m.Role == ChatRole.System))
                {
                    var fullPrompt = systemPrompt + (string.IsNullOrEmpty(ragContext) ? "" : "\n\n" + ragContext);
                    messages.Insert(0, new ChatMessage(ChatRole.System, fullPrompt));
                }

                // 5. Yeni mesajı ekle
                var userMessage = new ChatMessage(ChatRole.User, request.Message);
                messages.Add(userMessage);
                _memory.AddMessage(request.SessionId, userMessage);

                // 6. LLM'e gönder
                _logger.LogInformation("LLM'e istek atılıyor...");

                // ✅ GetResponseAsync kullanımı (IChatClient'in metodu)
                Microsoft.Extensions.AI.ChatResponse llmResponse = await _chatClient.GetResponseAsync(messages);

                // ✅ llmResponse.Message property'si var
                var assistantMessage = llmResponse.Message;

                // 7. Cevabı kaydet
                _memory.AddMessage(request.SessionId, assistantMessage);

                // 8.  Kendi formatınıza çevirip döndürün
                return new AIChatBot.Models.ChatResponse
                {
                    SessionId = request.SessionId,
                    Answer = assistantMessage.Text ?? "Cevap üretilemedi.",
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