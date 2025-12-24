using AIChatBot.Models;
using AIChatBot.Repository.ChatMemory;
using AIChatBot.Tools;
using Microsoft.Extensions.AI;
using System.Text.RegularExpressions;

// ✅ Alias kullanarak çakışmayı çöz
using AIResponse = Microsoft.Extensions.AI.ChatResponse;
using AppChatResponse = AIChatBot.Models.ChatResponse;

namespace AIChatBot.Services
{
    public class ChatService
    {
        private readonly IChatClient _chatClient;
        private readonly IChatMemoryRepository _memoryRepository;
        private readonly RagService _rag;
        private readonly IEnumerable<AITool> _tools;
        private readonly IConfiguration _configuration;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IChatClient chatClient,
            IChatMemoryRepository memoryRepository,
            RagService rag,
            IEnumerable<AITool> tools,
            IConfiguration configuration,
            ILoggerFactory loggerFactory,
            ILogger<ChatService> logger)
        {
            _chatClient = chatClient;
            _memoryRepository = memoryRepository;
            _rag = rag;
            _tools = tools;
            _configuration = configuration;
            _loggerFactory = loggerFactory;
            _logger = logger;
        }

        /// <summary>
        /// Ana mesaj işleme metodu
        /// </summary>
        public async Task<AppChatResponse> ProcessMessageAsync(
            ChatRequest request,
            UserContext userContext)
        {
            try
            {
                // RBAC context set
                SetToolContext(request, userContext);

                _logger.LogInformation(
                    "[CHAT] Session:{SessionId}, User:{UserId}, Role:{Role}, Message:{Message}",
                    request.SessionId,
                    userContext.UserId,
                    request.Role,
                    request.Message
                );

                // ✅ 1. ÖNCE MANUEL TOOL DISPATCH DENİYOR
                var (toolExecuted, toolResult, toolName) = await TryManualToolDispatch(request.Message);

                string finalAnswer;
                var usedTools = new List<string>();

                if (toolExecuted && toolResult != null)
                {
                    // ✅ TOOL BAŞARIYLA ÇALIŞTI
                    usedTools.Add(toolName!);

                    _logger.LogInformation("[MANUAL-TOOL] ✅ Tool başarıyla çalıştı: {ToolName}", toolName);

                    // Tool sonucunu doğrudan LLM'e gönder (formatlama için)
                    var messages = new List<ChatMessage>
                    {
                        new ChatMessage(ChatRole.System, $@"
Sen bir müşteri hizmetleri asistanısın.  Aşağıdaki tool sonucunu kullanıcıya düzgün bir şekilde sun:

**Tool Sonucu:**
{toolResult}

Kurallar:
- Türkçe cevap ver
- Tool sonucunu aynen kullan, değiştirme
- Emoji kullan
- Kısa ve öz ol
"),
                        new ChatMessage(ChatRole.User, request.Message)
                    };

                    var chatOptions = new ChatOptions
                    {
                        Temperature = 0.3f,
                        MaxOutputTokens = 500
                    };

                    try
                    {
                        // ✅ AIResponse kullan (Microsoft.Extensions.AI. ChatResponse)
                        AIResponse llmResponse = await _chatClient.GetResponseAsync(messages, chatOptions);

                        // ✅ ChatResponse'dan text'i al
                        finalAnswer = ExtractTextFromResponse(llmResponse) ?? toolResult;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[LLM-ERROR] Tool sonucu formatlama hatası");
                        // Fallback:  Tool sonucunu direkt kullan
                        finalAnswer = toolResult;
                    }
                }
                else
                {
                    // ✅ 2. TOOL ÇALIŞMADI, NORMAL RAG + LLM AKIŞI
                    _logger.LogInformation("[CHAT] Manuel tool tetiklenmedi, RAG + LLM akışı başlıyor");

                    // RAG search
                    var relevantDocs = await _rag.SemanticSearchAsync(request.Message, topK: 3);

                    if (!relevantDocs.Any())
                    {
                        _logger.LogWarning("[RAG] Vector search boş, keyword search deneniyor.. .");
                        relevantDocs = await _rag.SearchDocumentsAsync(request.Message);
                    }

                    var ragContext = _rag.FormatDocumentsAsContext(relevantDocs);
                    _logger.LogInformation("[RAG] {Count} belge bulundu", relevantDocs.Count);

                    // System prompt
                    var systemPrompt = BuildSystemPrompt(userContext, ragContext);

                    // Conversation history
                    var history = await _memoryRepository.GetHistoryAsync(request.SessionId);
                    var messages = history.Where(m => m.Role != ChatRole.System).ToList();

                    messages.Insert(0, new ChatMessage(ChatRole.System, systemPrompt));
                    messages.Add(new ChatMessage(ChatRole.User, request.Message));

                    // LLM call (tool'suz)
                    var chatOptions = new ChatOptions
                    {
                        Temperature = 0.3f,
                        TopP = 0.9f,
                        MaxOutputTokens = 2000
                    };

                    _logger.LogInformation("[LLM] İstek gönderiliyor (tool'suz)");

                    try
                    {
                        // ✅ AIResponse kullan
                        AIResponse llmResponse = await _chatClient.GetResponseAsync(messages, chatOptions);

                        // ✅ Text'i çıkar
                        finalAnswer = ExtractTextFromResponse(llmResponse) ?? "Üzgünüm, bir hata oluştu. ";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[LLM-ERROR] LLM çağrısı hatası");
                        finalAnswer = "Üzgünüm, şu anda yanıt veremiyorum.  Lütfen daha sonra tekrar deneyin.";
                    }
                }

                // Mesajları kaydet
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
                    finalAnswer
                );

                _logger.LogDebug("[DB] Mesajlar kaydedildi: Session={SessionId}", request.SessionId);

                // ✅ AppChatResponse döndür (AIChatBot.Models.ChatResponse)
                return new AppChatResponse
                {
                    SessionId = request.SessionId,
                    Answer = finalAnswer,
                    UsedTools = usedTools,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CHAT-ERROR] Session:{SessionId}", request.SessionId);

                return new AppChatResponse
                {
                    SessionId = request.SessionId,
                    Answer = "",
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
            finally
            {
                ClearToolContext();
            }
        }

        /// <summary>
        /// ChatResponse'dan text'i çıkarır (Microsoft.Extensions.AI.ChatResponse)
        /// </summary>
        private string? ExtractTextFromResponse(AIResponse response)
        {
            try
            {
                if (response == null)
                    return null;

                // ✅ Yöntem 1: ToString() (Ollama için çalışıyor)
                var responseText = response.ToString();
                if (!string.IsNullOrWhiteSpace(responseText))
                {
                    _logger.LogDebug("[EXTRACT-TEXT] ToString() başarılı: {Length} karakter", responseText.Length);
                    return responseText;
                }

                // ✅ Yöntem 2: Reflection ile Message.Text (fallback)
                var message = response.GetType().GetProperty("Message")?.GetValue(response);
                if (message != null)
                {
                    var text = message.GetType().GetProperty("Text")?.GetValue(message) as string;
                    if (!string.IsNullOrEmpty(text))
                    {
                        _logger.LogDebug("[EXTRACT-TEXT] Message.Text başarılı");
                        return text;
                    }
                }

                // ✅ Yöntem 3: Choices array (bazı modellerde)
                var choices = response.GetType().GetProperty("Choices")?.GetValue(response);
                if (choices != null && choices is System.Collections.IEnumerable enumerable)
                {
                    foreach (var choice in enumerable)
                    {
                        var choiceMessage = choice.GetType().GetProperty("Message")?.GetValue(choice);
                        if (choiceMessage != null)
                        {
                            var text = choiceMessage.GetType().GetProperty("Text")?.GetValue(choiceMessage) as string;
                            if (!string.IsNullOrEmpty(text))
                            {
                                _logger.LogDebug("[EXTRACT-TEXT] Choices[]. Message.Text başarılı");
                                return text;
                            }
                        }
                    }
                }

                _logger.LogWarning("[EXTRACT-TEXT] Hiçbir yöntem çalışmadı");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EXTRACT-TEXT-ERROR] Text çıkarma hatası");
                return null;
            }
        }

        /// <summary>
        /// Manuel Tool Dispatcher - Keyword matching ile tool'ları tetikler
        /// </summary>
        private async Task<(bool executed, string? result, string? toolName)> TryManualToolDispatch(string userMessage)
        {
            var lower = userMessage.ToLower();

            // 1. İADE POLİTİKASI
            if ((lower.Contains("iade") || lower.Contains("iptal") || lower.Contains("geri gönder")) &&
                (lower.Contains("politika") || lower.Contains("süre") || lower.Contains("kural") ||
                 lower.Contains("nasıl") || lower.Contains("nedir")))
            {
                _logger.LogInformation("[MANUAL-TOOL] 🔧 GetReturnPolicyTool tetiklendi");

                try
                {
                    var tool = new GetReturnPolicyTool(
                        _configuration,
                        _loggerFactory.CreateLogger<GetReturnPolicyTool>()
                    );
                    var result = await tool.Execute();
                    return (true, result, "GetReturnPolicyTool");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[MANUAL-TOOL-ERROR] GetReturnPolicyTool hatası");
                    return (false, null, null);
                }
            }

            // 2. ÖDEME YÖNTEMLERİ
            if ((lower.Contains("ödeme") || lower.Contains("taksit") || lower.Contains("kredi kartı") ||
                 lower.Contains("banka kartı") || lower.Contains("havale")) &&
                (lower.Contains("yöntem") || lower.Contains("seçenek") || lower.Contains("nasıl") ||
                 lower.Contains("hangi")))
            {
                _logger.LogInformation("[MANUAL-TOOL] 🔧 GetPaymentMethodsTool tetiklendi");

                try
                {
                    var tool = new GetPaymentMethodsTool(
                        _configuration,
                        _loggerFactory.CreateLogger<GetPaymentMethodsTool>()
                    );
                    var result = await tool.Execute();
                    return (true, result, "GetPaymentMethodsTool");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[MANUAL-TOOL-ERROR] GetPaymentMethodsTool hatası");
                    return (false, null, null);
                }
            }

            // 3. KARGO ÜCRETİ HESAPLAMA
            // 3.  KARGO ÜCRETİ HESAPLAMA
            if ((lower.Contains("kargo") || lower.Contains("teslimat") || lower.Contains("gönderim")) &&
                (lower.Contains("ücret") || lower.Contains("fiyat") || lower.Contains("kaç") ||
                 lower.Contains("ne kadar")))
            {
                _logger.LogInformation("[MANUAL-TOOL] 🔧 CalculateShippingTool tetikleniyor");

                try
                {
                    // ✅ Regex düzeltildi (boşluklar kaldırıldı)
                    var match = Regex.Match(userMessage, @"(\d+(?:[.,]\d+)?)\s*(?:TL|tl|lira)?");

                    var tool = new CalculateShippingTool(
                        _configuration,
                        _loggerFactory.CreateLogger<CalculateShippingTool>()
                    );

                    if (match.Success && decimal.TryParse(match.Groups[1].Value.Replace(',', '.'), out var amount))
                    {
                        _logger.LogInformation("[MANUAL-TOOL] 🔧 CalculateShippingTool tetiklendi:  Amount={Amount}", amount);
                        var result = await tool.Execute(amount);
                        return (true, result, "CalculateShippingTool");
                    }
                    else
                    {
                        // Fiyat bulunamadı, örnek hesaplama yap
                        _logger.LogInformation("[MANUAL-TOOL] 🔧 CalculateShippingTool (örnek 500 TL)");
                        var result = await tool.Execute(500);
                        var noteResult = result + "\n\n_Not: Sipariş tutarınızı belirtirseniz kesin hesaplama yapabilirim._";
                        return (true, noteResult, "CalculateShippingTool");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[MANUAL-TOOL-ERROR] CalculateShippingTool hatası");
                    return (false, null, null);
                }
            }

            // Tool tetiklenmedi
            return (false, null, null);
        }

        /// <summary>
        /// RBAC tool context'i set eder
        /// </summary>
        private void SetToolContext(ChatRequest request, UserContext userContext)
        {
            var toolContext = new ToolContext
            {
                UserId = userContext.UserId,
                Role = request.Role ?? userContext.Role ?? "User",
                AllowedProductIds = userContext.AllowedProductIds ?? new List<int>()
            };

            ToolContextManager.SetContext(toolContext);

            _logger.LogInformation(
                "[RBAC] 🔐 Context set:  UserId={UserId}, Role={Role}, AllowedProducts={Count}",
                toolContext.UserId,
                toolContext.Role,
                toolContext.AllowedProductIds.Count
            );
        }

        /// <summary>
        /// Tool context'i temizler
        /// </summary>
        private void ClearToolContext()
        {
            try
            {
                ToolContextManager.ClearContext();
                _logger.LogDebug("[RBAC-CONTEXT] Context temizlendi");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[RBAC-CONTEXT] Context temizleme hatası (ignore)");
            }
        }

        /// <summary>
        /// System prompt oluşturur
        /// </summary>
        private string BuildSystemPrompt(UserContext userContext, string ragContext)
        {
            var prompt = @"
Sen bir müşteri hizmetleri asistanısın. Kullanıcı sorularına aşağıdaki kuralları uygulayarak cevap ver: 

📚 **BİLGİ BANKASI:**
" + (string.IsNullOrWhiteSpace(ragContext) ? "Bilgi yok." : ragContext) + @"

👤 **KULLANICI:**
- UserID: " + userContext.UserId + @"
- Role: " + userContext.Role + @"

📝 **KURALLAR:**
- Türkçe cevap ver
- Kısa ve öz ol
- Emoji kullan (📦 💰 ✅ ❌)
- Fiyatları 'TL' ile göster
- Bilgi bankasındaki bilgileri kullan
- Bilgi yoksa 'Bu konuda bilgim yok' de
";
            return prompt;
        }

        /// <summary>
        /// Session geçmişini getirir
        /// </summary>
        public async Task<List<ChatMessage>> GetSessionHistoryAsync(string sessionId)
        {
            try
            {
                _logger.LogInformation("[HISTORY] Session geçmişi istendi: {SessionId}", sessionId);
                return await _memoryRepository.GetHistoryAsync(sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HISTORY-ERROR] Session:{SessionId}", sessionId);
                return new List<ChatMessage>();
            }
        }

        /// <summary>
        /// Session'ı temizler
        /// </summary>
        public async Task ClearSessionAsync(string sessionId)
        {
            try
            {
                _logger.LogInformation("[CLEAR] Session temizleniyor: {SessionId}", sessionId);
                await _memoryRepository.ClearSessionAsync(sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CLEAR-ERROR] Session:{SessionId}", sessionId);
                throw;
            }
        }
    }
}