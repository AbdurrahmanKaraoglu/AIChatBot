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
        private readonly IEnumerable<AIFunction> _tools;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IChatClient chatClient,
            IChatMemoryRepository memoryRepository,
            RagService rag,
            IEnumerable<AIFunction> tools,
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
            // ✅ Tool context'i set et (RBAC için)
            try
            {
                SetToolContext(request, userContext);

                _logger.LogInformation(
                    "[CHAT] Session:{SessionId}, User:{UserId}, Role:{Role}, Message:{Message}",
                    request.SessionId,
                    userContext.UserId,
                    request.Role ?? "Customer",
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

                // 3. Geçmişi al (System mesajları hariç)
                var messages = await _memoryRepository.GetHistoryAsync(request.SessionId);
                messages = messages.Where(m => m.Role != ChatRole.System).ToList();

                // 4. System prompt'u ekle
                messages.Insert(0, new ChatMessage(ChatRole.System, systemPrompt));

                // 5.  Kullanıcı mesajını ekle
                messages.Add(new ChatMessage(ChatRole.User, request.Message));

                // ✅ 6.  ChatOptions ile tool'ları ekle
                var chatOptions = new ChatOptions
                {
                    Tools = _tools?.ToList() ?? new List<AIFunction>(),
                    Temperature = 0.3f,
                    TopP = 0.9f
                };

                _logger.LogInformation(
                    "[LLM] Istek gönderiliyor...  Tool Count:{ToolCount}",
                    chatOptions.Tools.Count
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
                                "[TOOL-CALL] {ToolName}({Arguments})",
                                content.Name,
                                string.Join(", ", content.Arguments?.Select(kvp => $"{kvp.Key}={kvp.Value}") ?? Enumerable.Empty<string>())
                            );

                            if (!usedTools.Contains(content.Name))
                                usedTools.Add(content.Name);
                        }
                    }

                    // Metin yanıtı biriktir
                    if (!string.IsNullOrEmpty(update.Text))
                        responseText += update.Text;
                }

                _logger.LogInformation("[LLM] Cevap alındı: {ResponseLength} karakter", responseText.Length);

                // 8.  Mesajları veritabanına kaydet
                await SaveMessagesAsync(request, userContext, responseText);

                return new AIChatBot.Models.ChatResponse
                {
                    SessionId = request.SessionId,
                    Answer = responseText,
                    Success = true,
                    UsedTools = usedTools
                };
            }
            catch (UnauthorizedAccessException ex)
            {
                // RBAC hatası
                _logger.LogWarning(
                    "[RBAC-DENIED] User:{UserId}, Role:{Role}, Error:{Error}",
                    userContext.UserId,
                    request.Role,
                    ex.Message
                );

                return new AIChatBot.Models.ChatResponse
                {
                    SessionId = request.SessionId,
                    Success = false,
                    ErrorMessage = $"Yetkilendirme Hatası: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[CHAT-ERROR] Session:{SessionId}, User:{UserId}, Message:{Message}",
                    request.SessionId,
                    userContext.UserId,
                    request.Message
                );

                return new AIChatBot.Models.ChatResponse
                {
                    SessionId = request.SessionId,
                    Success = false,
                    ErrorMessage = $"Sistem Hatası: {ex.Message}"
                };
            }
            finally
            {
                // ✅ Tool context'i temizle (memory leak önleme)
                ClearToolContext();
            }
        }

        /// <summary>
        /// Tool Context'i set eder (RBAC için)
        /// </summary>
        private void SetToolContext(ChatRequest request, UserContext userContext)
        {
            if (string.IsNullOrEmpty(userContext.UserId))
                return;

            var context = new ToolContext
            {
                UserId = int.TryParse(userContext.UserId, out var uid) ? uid : 0,
                Role = request.Role ?? "Customer",
                AllowedProductIds = request.AllowedProductIds ?? new List<int>()
            };

            ToolContextManager.SetContext(context);

            _logger.LogDebug(
                "[TOOL-CONTEXT] UserId:{UserId}, Role:{Role}, AllowedProducts:{Products}",
                context.UserId,
                context.Role,
                string.Join(",", context.AllowedProductIds)
            );
        }

        /// <summary>
        /// Tool Context'i temizler
        /// </summary>
        private void ClearToolContext()
        {
            try
            {
                ToolContextManager.ClearContext();
            }
            catch
            {
                // Context zaten yoksa sessizce devam et
            }
        }

        /// <summary>
        /// System prompt oluşturur
        /// </summary>
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
- Normal fiyat sorarsa normal belgedeki fiyatı söyle

⚠️ TOOL KULLANIMI:
- Ürün bilgisi gerekiyorsa GetProductInfo tool'unu kullan
- Kargo hesabı gerekiyorsa CalculateShipping tool'unu kullan
- Döküman araması gerekiyorsa SearchRAG tool'unu kullan";

            if (!string.IsNullOrEmpty(ragContext))
            {
                prompt += $"\n\n{ragContext}\n\n";
                prompt += "⚠️ SADECE yukarıdaki bilgileri kullan!  Ek bilgi ekleme! ";
            }

            return prompt;
        }

        /// <summary>
        /// Mesajları veritabanına kaydeder
        /// </summary>
        private async Task SaveMessagesAsync(ChatRequest request, UserContext userContext, string responseText)
        {
            try
            {
                // User mesajını kaydet
                await _memoryRepository.SaveMessageAsync(
                    request.SessionId,
                    userContext.UserId,
                    userContext.UserName,
                    "user",
                    request.Message
                );

                // Assistant cevabını kaydet
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
                _logger.LogError(ex, "[DB-ERROR] Mesaj kaydetme hatası: Session={SessionId}", request.SessionId);
                // Mesaj kayıt hatası cevabı engellemez, sadece logla
            }
        }

        /// <summary>
        /// Session geçmişini getirir
        /// </summary>
        public async Task<List<ChatMessage>> GetSessionHistoryAsync(string sessionId)
        {
            _logger.LogInformation("[HISTORY] Session geçmişi istendi: {SessionId}", sessionId);
            return await _memoryRepository.GetHistoryAsync(sessionId);
        }

        /// <summary>
        /// Session'ı temizler
        /// </summary>
        public async Task ClearSessionAsync(string sessionId)
        {
            _logger.LogInformation("[CLEAR] Session temizleniyor: {SessionId}", sessionId);
            await _memoryRepository.ClearSessionAsync(sessionId);
        }
    }
}