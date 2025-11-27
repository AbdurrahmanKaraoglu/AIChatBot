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
        private readonly IEnumerable<AITool> _tools;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IChatClient chatClient,
            IChatMemoryRepository memoryRepository,
            RagService rag,
            IEnumerable<AITool> tools,
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
                // RBAC: Tool context'i set et
                SetToolContext(request, userContext);

                _logger.LogInformation(
                    "[CHAT] Session:{SessionId}, User:{UserId}, Role:{Role}, Message:{Message}",
                    request.SessionId,
                    userContext.UserId,
                    request.Role,
                    request.Message
                );

                // 1. RAG - Semantic search
                var relevantDocs = await _rag.SemanticSearchAsync(request.Message, topK: 3);

                if (!relevantDocs.Any())
                {
                    _logger.LogWarning("[RAG] Vector search boş, keyword search deneniyor.. .");
                    relevantDocs = await _rag.SearchDocumentsAsync(request.Message);
                }

                var ragContext = _rag.FormatDocumentsAsContext(relevantDocs);
                _logger.LogInformation("[RAG] {Count} belge bulundu", relevantDocs.Count);

                // 2. System prompt oluştur
                var systemPrompt = BuildSystemPrompt(userContext, ragContext);

                // 3.  Geçmişi al
                var messages = await _memoryRepository.GetHistoryAsync(request.SessionId);
                messages = messages.Where(m => m.Role != ChatRole.System).ToList();

                // 4. System prompt ekle
                messages.Insert(0, new ChatMessage(ChatRole.System, systemPrompt));

                // 5.  Kullanıcı mesajını ekle
                messages.Add(new ChatMessage(ChatRole.User, request.Message));

                // 6. ChatOptions
                var chatOptions = new ChatOptions
                {
                    //Tools = _tools?.ToList(),
                    Temperature = 0.3f,
                    TopP = 0.9f,
                    MaxOutputTokens = 2000
                };

                _logger.LogInformation(
                    "[LLM] İstek gönderiliyor...  Tool Count:{ToolCount}",
                    chatOptions.Tools?.Count ?? 0
                );

                // ✅ 7. Tool calling loop (STREAMING)
                var responseText = "";
                var usedTools = new List<string>();
                var conversationMessages = messages.ToList();

                const int maxIterations = 5;
                int iteration = 0;

                while (iteration < maxIterations)
                {
                    iteration++;
                    _logger.LogDebug("[LLM] Iteration {Iteration}/{Max}", iteration, maxIterations);

                    ChatMessage? assistantMessage = null;
                    var currentText = "";
                    var hasToolCall = false;

                    await foreach (var update in _chatClient.GetStreamingResponseAsync(conversationMessages, chatOptions))
                    {
                        // Text topla
                        if (!string.IsNullOrEmpty(update.Text))
                        {
                            currentText += update.Text;
                        }

                        // Tool çağrısı var mı?
                        var functionCalls = update.Contents
                            ?.OfType<FunctionCallContent>()
                            .ToList() ?? new List<FunctionCallContent>();

                        if (functionCalls.Any())
                        {
                            hasToolCall = true;

                            foreach (var functionCall in functionCalls)
                            {
                                _logger.LogInformation(
                                    "[TOOL-CALL] Tool: {ToolName}, CallId: {CallId}",
                                    functionCall.Name,
                                    functionCall.CallId
                                );

                                if (!string.IsNullOrEmpty(functionCall.Name))
                                {
                                    if (!usedTools.Contains(functionCall.Name))
                                        usedTools.Add(functionCall.Name);
                                }
                            }

                            // Tool çağrısını conversation'a ekle
                            assistantMessage = new ChatMessage(ChatRole.Assistant, update.Contents?.ToList() ?? new List<AIContent>());
                            conversationMessages.Add(assistantMessage);
                        }
                    }

                    // Tool çağrısı yapıldıysa bir sonraki iterasyona geç
                    if (hasToolCall)
                    {
                        _logger.LogDebug("[LLM] Tool call detected, next iteration");
                        continue;
                    }

                    // Tool çağrısı yok, final cevap
                    responseText = currentText;
                    _logger.LogInformation("[LLM] Final cevap alındı: {Length} karakter", responseText.Length);
                    break;
                }

                if (iteration >= maxIterations)
                {
                    _logger.LogWarning("[LLM] Max iteration limit reached");
                    responseText = responseText.Length > 0
                        ? responseText
                        : "Üzgünüm, isteğinizi tamamlayamadım. Lütfen tekrar deneyin.";
                }

                // 8. Mesajları kaydet
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
                    ErrorMessage = $"⛔ Yetkilendirme Hatası: {ex.Message}"
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
            finally
            {
                ClearToolContext();
            }
        }

        private void SetToolContext(ChatRequest request, UserContext userContext)
        {
            if (string.IsNullOrEmpty(userContext.UserId))
            {
                _logger.LogWarning("[RBAC] UserId boş, context set edilmedi");
                return;
            }

            var context = new ToolContext
            {
                UserId = int.TryParse(userContext.UserId, out var uid) ? uid : 0,
                Role = request.Role ?? "Customer",
                AllowedProductIds = request.AllowedProductIds ?? new List<int>()
            };

            ToolContextManager.SetContext(context);

            _logger.LogInformation(
                "[RBAC-CONTEXT] UserId:{UserId}, Role:{Role}, AllowedProducts:[{Products}]",
                context.UserId,
                context.Role,
                string.Join(", ", context.AllowedProductIds)
            );
        }

        private void ClearToolContext()
        {
            try
            {
                ToolContextManager.ClearContext();
                _logger.LogDebug("[RBAC-CONTEXT] Context temizlendi");
            }
            catch { }
        }

        private string BuildSystemPrompt(UserContext userContext, string ragContext)
        {
            var prompt = @"Sen bir müşteri destek asistanısın. 

KURALLAR:
1.  SADECE bilgi bankasındaki bilgileri kullan
2.  Bilmediğin şeyi ASLA uydurma
3. Türkçe konuş
4. Yardımcı ve profesyonel ol

⚠️ TOOL KULLANIMI:
- Ürün bilgisi gerekiyorsa GetProductInfo tool'unu kullan
- Kargo hesabı gerekiyorsa CalculateShipping tool'unu kullan
- Döküman araması gerekiyorsa SearchRAG tool'unu kullan

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