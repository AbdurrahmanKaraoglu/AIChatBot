using AIChatBot.Models;
using AIChatBot.Repository.ChatMemory;
using Microsoft.Extensions.AI;

namespace AIChatBot.Services
{
    public class ChatService
    {
        private readonly IChatClient _chatClient;
        private readonly IChatMemoryRepository _memoryRepository;
        private readonly RagService _rag;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IChatClient chatClient,
            IChatMemoryRepository memoryRepository,
            RagService rag,
            ILogger<ChatService> logger)
        {
            _chatClient = chatClient;
            _memoryRepository = memoryRepository;
            _rag = rag;
            _logger = logger;
        }

        public async Task<AIChatBot.Models.ChatResponse> ProcessMessageAsync(ChatRequest request, UserContext userContext)
        {
            try
            {
                _logger.LogInformation($"Yeni mesaj: {request.SessionId}");

                // 1. RAG - Belge arama (veritabanından)
                var relevantDocs = await _rag.SearchDocumentsAsync(request.Message);

                // ✅ DEBUG: Bulunan belge sayısını logla
                _logger.LogInformation($"RAG Sonucu: {relevantDocs.Count} belge bulundu");

                if (relevantDocs.Any())
                {
                    foreach (var doc in relevantDocs)
                    {
                        _logger.LogInformation($"  - Belge: {doc.Title} (ID: {doc.Id})");
                    }
                }
                else
                {
                    _logger.LogWarning($"⚠️ RAG: '{request.Message}' için belge bulunamadı!");
                }

                var ragContext = _rag.FormatDocumentsAsContext(relevantDocs);

                // ✅ DEBUG: RAG context'i logla
                if (!string.IsNullOrEmpty(ragContext))
                {
                    _logger.LogInformation($"RAG Context:\n{ragContext}");
                }
                else
                {
                    _logger.LogWarning("⚠️ RAG Context boş!");
                }

                // 2. System prompt
                var systemPrompt = BuildSystemPrompt(userContext, ragContext);

                // ✅ DEBUG: System prompt'u logla
                _logger.LogInformation($"System Prompt:\n{systemPrompt}");

                // 3. Geçmişi al (veritabanından)
                var messages = await _memoryRepository.GetHistoryAsync(request.SessionId);
                messages = messages.Where(m => m.Role != ChatRole.System).ToList();

                // 4. System prompt'u ekle
                messages.Insert(0, new ChatMessage(ChatRole.System, systemPrompt));

                // 5.  Kullanıcı mesajını ekle
                var userMessage = new ChatMessage(ChatRole.User, request.Message);
                messages.Add(userMessage);

                _logger.LogInformation("LLM'e istek atılıyor...");

                // 6. LLM'den cevap al
                var responseText = "";
                await foreach (var update in _chatClient.GetStreamingResponseAsync(messages))
                {
                    responseText += update.Text;
                }

                _logger.LogInformation($"LLM Cevabı: {responseText}");

                // 7.  Mesajları veritabanına kaydet
                await _memoryRepository.SaveMessageAsync(
                    request.SessionId,
                    userContext.UserId,
                    userContext.UserName,
                    "user",
                    request.Message
                );

                await _memoryRepository.SaveMessageAsync(
                    request.SessionId,
                    userContext.UserId,
                    userContext.UserName,
                    "assistant",
                    responseText
                );

                return new AIChatBot.Models.ChatResponse
                {
                    SessionId = request.SessionId,
                    Answer = responseText,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Hata: {ex.Message}\nStackTrace: {ex.StackTrace}");
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
            var prompt = @"Sen bir müşteri destek asistanısın. 

KURALLAR:
1.  SADECE bilgi bankasındaki bilgileri kullan
2.  Bilmediğin şeyi ASLA uydurma
3. Türkçe konuş

⚠️ ÖNEMLİ FİYAT KURALI:
- Eğer kullanıcı 'kampanya', 'indirim', 'kış' kelimesini kullanıyorsa:
  → SADECE kampanya belgesindeki '→' işaretinden SONRA​KI fiyatı söyle
  → '960 TL' gibi indirimli fiyatı kullan
- Normal fiyat sorarsa normal belgedeki fiyatı söyle";

            if (!string.IsNullOrEmpty(ragContext))
            {
                prompt += $"\n\n{ragContext}\n\n";
                prompt += "SADECE yukarıdaki bilgileri kullan! ";
            }

            return prompt;
        }

        public async Task<List<ChatMessage>> GetSessionHistoryAsync(string sessionId)
        {
            return await _memoryRepository.GetHistoryAsync(sessionId);
        }

        public async Task ClearSessionAsync(string sessionId)
        {
            await _memoryRepository.ClearSessionAsync(sessionId);
        }
    }
}