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

                // 1. RAG - Belge arama
                var relevantDocs = _rag.SearchDocuments(request.Message);
                var ragContext = _rag.FormatDocumentsAsContext(relevantDocs);

                _logger.LogInformation($"RAG Sonucu: {relevantDocs.Count} belge bulundu");

                // 2. System prompt
                var systemPrompt = BuildSystemPrompt(userContext, ragContext);

                // 3. Geçmişi al (System mesaj hariç - her seferinde yeniden oluşturacağız)
                var messages = _memory.GetHistory(request.SessionId)
                    .Where(m => m.Role != ChatRole.System)
                    .ToList();

                // 4. System prompt'u ekle (en başa)
                messages.Insert(0, new ChatMessage(ChatRole.System, systemPrompt));

                // 5.  Kullanıcı mesajını ekle
                var userMessage = new ChatMessage(ChatRole.User, request.Message);
                messages.Add(userMessage);

                _logger.LogInformation("LLM'e istek atılıyor.. .");

                // 6. LLM'den cevap al
                var responseText = "";
                await foreach (var update in _chatClient.GetStreamingResponseAsync(messages))
                {
                    responseText += update.Text;
                }

                _logger.LogInformation($"LLM Cevabı: {responseText}");

                // 7. ✅ Sadece user ve assistant mesajlarını kaydet (duplicate önleme)
                _memory.AddMessage(request.SessionId, userMessage);

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

        private string BuildSystemPrompt(UserContext userContext, string ragContext)
        {
            // ✅ Çok daha temiz ve doğal prompt
            var prompt = "Sen profesyonel bir müşteri destek temsilcisisin. ";

            // ✅ RAG context varsa doğrudan bilgi olarak sun
            if (!string.IsNullOrEmpty(ragContext))
            {
                prompt += $"\n\n{ragContext}\n\n";
                prompt += "Yukarıdaki bilgileri kullanarak müşteriye kısa ve net cevaplar ver.  ";
            }

            prompt += "Sadece Türkçe konuş. Nazik ve yardımsever ol. ";

            return prompt;
        }

        public List<ChatMessage> GetSessionHistory(string sessionId) => _memory.GetHistory(sessionId);
        public void ClearSession(string sessionId) => _memory.ClearSession(sessionId);
    }
}