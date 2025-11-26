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
        private readonly IEnumerable<AITool> _tools;  // ✅ AIFunction → AITool
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IChatClient chatClient,
            IChatMemoryRepository memoryRepository,
            RagService rag,
            IEnumerable<AITool> tools,  // ✅ AIFunction → AITool
            ILogger<ChatService> logger)
        {
            _chatClient = chatClient;
            _memoryRepository = memoryRepository;
            _rag = rag;
            _tools = tools;
            _logger = logger;
        }

        public async Task<AIChatBot.Models.ChatResponse> ProcessMessageAsync(
            ChatRequest request,
            UserContext userContext)
        {
            try
            {
                _logger.LogInformation(
                    "[CHAT] Session:{SessionId}, User:{UserId}, Message:{Message}",
                    request.SessionId,
                    userContext.UserId,
                    request.Message
                );

                // 1. RAG - Semantic search
                var relevantDocs = await _rag.SemanticSearchAsync(request.Message, topK: 3);

                // Fallback: Keyword search (vector search boş dönerse)
                if (!relevantDocs.Any())
                {
                    _logger.LogWarning("[RAG] Vector search boş, keyword search deneniyor.. .");
                    relevantDocs = await _rag.SearchDocumentsAsync(request.Message);
                }

                var ragContext = _rag.FormatDocumentsAsContext(relevantDocs);

                _logger.LogInformation("[RAG] {Count} belge bulundu", relevantDocs.Count);

                // 2. System prompt oluştur
                var systemPrompt = BuildSystemPrompt(userContext, ragContext);

                // 3.  Geçmişi al (System mesajları hariç)
                var messages = await _memoryRepository.GetHistoryAsync(request.SessionId);
                messages = messages.Where(m => m.Role != ChatRole.System).ToList();

                // 4. System prompt'u ekle
                messages.Insert(0, new ChatMessage(ChatRole.System, systemPrompt));

                // 5.  Kullanıcı mesajını ekle
                messages.Add(new ChatMessage(ChatRole.User, request.Message));

                // ✅ 6. ChatOptions ile tool'ları ekle (AITool listesi)
                var chatOptions = new ChatOptions
                {
                    Tools = _tools?.ToList(),  // ✅ IEnumerable<AITool> → List<AITool>
                    Temperature = 0.3f,
                    TopP = 0.9f
                };

                _logger.LogInformation(
                    "[LLM] İstek gönderiliyor...  Tool Count:{ToolCount}",
                    chatOptions.Tools?.Count ?? 0
                );

                // ✅ 7. LLM'den streaming cevap al
                var responseText = "";
                var usedTools = new List<string>();

                await foreach (var update in _chatClient
                    .GetStreamingResponseAsync(messages, chatOptions)
                    .ConfigureAwait(false))
                {
                    // Tool çağrısı yapıldıysa logla
                    if (update.Contents?.Any(c => c is FunctionCallContent) == true)
                    {
                        foreach (var content in update.Contents.OfType<FunctionCallContent>())
                        {
                            _logger.LogInformation(
                                "[TOOL-CALL] {ToolName}",
                                content.Name
                            );

                            if (!string.IsNullOrEmpty(content.Name) && !usedTools.Contains(content.Name))
                                usedTools.Add(content.Name);
                        }
                    }

                    // Metin yanıtı biriktir
                    if (!string.IsNullOrEmpty(update.Text))
                        responseText += update.Text;
                }

                _logger.LogInformation("[LLM] Cevap alındı: {ResponseLength} karakter", responseText.Length);

                // 8. Mesajları veritabanına kaydet
                await SaveMessagesAsync(request, userContext, responseText);

                return new AIChatBot.Models.ChatResponse
                {
                    SessionId = request.SessionId,
                    Answer = responseText,
                    Success = true,
                    UsedTools = usedTools
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[CHAT-ERROR] Session:{SessionId}, User:{UserId}",
                    request.SessionId,
                    userContext.UserId
                );

                return new AIChatBot.Models.ChatResponse
                {
                    SessionId = request.SessionId,
                    Success = false,
                    ErrorMessage = $"Sistem Hatası: {ex.Message}"
                };
            }
        }

        private string BuildSystemPrompt(UserContext userContext, string ragContext)
        {
            var prompt = @"Sen bir müşteri destek asistanısın. 

KURALLAR:
1.  SADECE bilgi bankasındaki bilgileri kullan
2. Bilmediğin şeyi ASLA uydurma
3. Türkçe konuş
4. Yardımcı ve profesyonel ol

⚠️ ÖNEMLİ FİYAT KURALI:
- Eğer kullanıcı 'kampanya', 'indirim', 'kış' kelimesini kullanıyorsa:
  → SADECE kampanya belgesindeki '→' işaretinden SONRAKI fiyatı söyle
  → '960 TL' gibi indirimli fiyatı kullan
- Normal fiyat sorarsa normal belgedeki fiyatı söyle";

            if (!string.IsNullOrEmpty(ragContext))
            {
                prompt += $"\n\n{ragContext}\n\n";
                prompt += "⚠️ SADECE yukarıdaki bilgileri kullan!  Ek bilgi ekleme! ";
            }

            return prompt;
        }

        private async Task SaveMessagesAsync(ChatRequest request, UserContext userContext, string responseText)
        {
            try
            {
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

                _logger.LogDebug("[DB] Mesajlar kaydedildi: Session={SessionId}", request.SessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DB-ERROR] Mesaj kaydetme hatası");
            }
        }

        public async Task<List<ChatMessage>> GetSessionHistoryAsync(string sessionId)
        {
            _logger.LogInformation("[HISTORY] Session geçmişi istendi: {SessionId}", sessionId);
            return await _memoryRepository.GetHistoryAsync(sessionId);
        }

        public async Task ClearSessionAsync(string sessionId)
        {
            _logger.LogInformation("[CLEAR] Session temizleniyor: {SessionId}", sessionId);
            await _memoryRepository.ClearSessionAsync(sessionId);
        }
    }
}