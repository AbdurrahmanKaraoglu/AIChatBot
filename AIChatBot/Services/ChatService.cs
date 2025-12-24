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
        private readonly Dictionary<string, AITool> _toolsDictionary;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IChatClient chatClient,
            IChatMemoryRepository memoryRepository,
            RagService rag,
            IEnumerable<AITool> tools,
            Dictionary<string, AITool> toolsDictionary,
            ILogger<ChatService> logger)
        {
            _chatClient = chatClient;
            _memoryRepository = memoryRepository;
            _rag = rag;
            _tools = tools;
            _toolsDictionary = toolsDictionary;
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

                // 3. Geçmişi al
                var messages = await _memoryRepository.GetHistoryAsync(request.SessionId);
                messages = messages.Where(m => m.Role != ChatRole.System).ToList();

                // 4. System prompt ekle
                messages.Insert(0, new ChatMessage(ChatRole.System, systemPrompt));

                // 5. Kullanıcı mesajını ekle
                messages.Add(new ChatMessage(ChatRole.User, request.Message));

                // 6. ChatOptions - TOOL CALLING AKTİF! 
                var chatOptions = new ChatOptions
                {
                    Tools = _tools?.ToList(),
                    Temperature = 0.3f,
                    TopP = 0.9f,
                    MaxOutputTokens = 2000
                };

                _logger.LogInformation(
                    "[LLM] İstek gönderiliyor...  Tool Count:{ToolCount}",
                    chatOptions.Tools?.Count ?? 0
                );

                // 7. Tool calling loop (STREAMING)
                var responseText = "";
                var usedTools = new List<string>();
                var conversationMessages = messages.ToList();

                const int maxIterations = 5;
                int iteration = 0;

                while (iteration < maxIterations)
                {
                    iteration++;
                    _logger.LogDebug("[LLM] 🔄 Iteration {Iteration}/{Max}", iteration, maxIterations);

                    var currentText = "";
                    var hasToolCall = false;
                    var toolCalls = new List<FunctionCallContent>();

                    // Streaming response
                    await foreach (var update in _chatClient.GetStreamingResponseAsync(conversationMessages, chatOptions))
                    {
                        // Text topla
                        if (!string.IsNullOrEmpty(update.Text))
                        {
                            currentText += update.Text;
                        }

                        // Tool call tespit
                        if (update.Contents.Any(c => c is FunctionCallContent))
                        {
                            hasToolCall = true;
                            var toolCallContent = update.Contents.OfType<FunctionCallContent>().First();
                            toolCalls.Add(toolCallContent);

                            _logger.LogInformation(
                                "[TOOL-CALL] 🔧 {ToolName}({Arguments})",
                                toolCallContent.Name,
                                toolCallContent.Arguments
                            );
                        }
                    }

                    // Tool call varsa
                    if (hasToolCall && toolCalls.Any())
                    {
                        foreach (var toolCall in toolCalls)
                        {
                            try
                            {
                                // Tool'u çalıştır
                                var toolResult = await ExecuteToolAsync(toolCall);

                                // Tool sonucunu mesajlara ekle
                                conversationMessages.Add(new ChatMessage(
                                    ChatRole.Tool,
                                    toolResult
                                ));

                                usedTools.Add(toolCall.Name);

                                _logger.LogInformation(
                                    "[TOOL-RESULT] ✅ {ToolName} sonucu eklendi",
                                    toolCall.Name
                                );
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(
                                    ex,
                                    "[TOOL-ERROR] ❌ {ToolName} çalıştırılamadı",
                                    toolCall.Name
                                );

                                // Hata mesajını da conversation'a ekle
                                conversationMessages.Add(new ChatMessage(
                                    ChatRole.Tool,
                                    $"Tool hatası: {ex.Message}"
                                ));
                            }
                        }

                        // Bir sonraki iterasyona geç
                        continue;
                    }

                    // Tool call yoksa, final cevap
                    responseText = currentText;
                    break;
                }

                // Timeout kontrolü
                if (iteration >= maxIterations)
                {
                    _logger.LogWarning("[LLM] ⚠️ Max iteration limit reached ({Max})", maxIterations);
                    responseText += "\n\n_[Sistem:  İşlem zaman aşımına uğradı]_";
                }

                _logger.LogInformation(
                    "[LLM] ✅ Cevap hazır. Used Tools: {Tools}",
                    string.Join(", ", usedTools)
                );

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
2. Bilmediğin şeyi ASLA uydurma
3. Türkçe konuş
4. Yardımcı ve profesyonel ol

⚠️ TOOL KULLANIMI:
- Ürün bilgisi gerekiyorsa GetProductDetailsTool kullan
- Fiyat aralığında arama için SearchProductsByPriceTool kullan
- Kategori listesi için GetCategoryListTool kullan
- Döküman araması için SearchRAGTool kullan
- Fiyat hesaplama için CalculateTotalPriceTool kullan";

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
            _logger.LogInformation("[HISTORY] Session geçmişi istendi:  {SessionId}", sessionId);
            return await _memoryRepository.GetHistoryAsync(sessionId);
        }

        /// <summary>
        /// Tool'u çalıştırır ve sonucu döndürür (Manuel dispatch)
        /// </summary>
        private async Task<string> ExecuteToolAsync(FunctionCallContent toolCall)
        {
            _logger.LogInformation(
                "[TOOL-EXEC] ⚙️ Executing: {ToolName}",
                toolCall.Name
            );

            try
            {
                // Tool adına göre switch-case ile direkt çağır
                string result = toolCall.Name switch
                {
                    "SearchRAGTool" => await ExecuteSearchRAGTool(toolCall.Arguments),
                    "GetProductDetailsTool" => await ExecuteGetProductDetailsTool(toolCall.Arguments),
                    "SearchProductsByPriceTool" => await ExecuteSearchProductsByPriceTool(toolCall.Arguments),
                    "GetCategoryListTool" => await ExecuteGetCategoryListTool(toolCall.Arguments),
                    "CalculateTotalPriceTool" => await ExecuteCalculateTotalPriceTool(toolCall.Arguments),
                    _ => $"❌ Bilinmeyen tool: {toolCall.Name}"
                };

                _logger.LogInformation(
                    "[TOOL-EXEC] ✅ {ToolName} başarılı",
                    toolCall.Name
                );

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[TOOL-EXEC] ❌ {ToolName} execution error",
                    toolCall.Name
                );

                return $"❌ Tool hatası: {ex.Message}";
            }
        }

        public async Task ClearSessionAsync(string sessionId)
        {
            _logger.LogInformation("[CLEAR] Session temizleniyor: {SessionId}", sessionId);
            await _memoryRepository.ClearSessionAsync(sessionId);
        }

        // Helper metodlar (önceki mesajımdan)
        private async Task<string> ExecuteSearchRAGTool(IDictionary<string, object?> args)
        {
            var query = args.ContainsKey("query") ? args["query"]?.ToString() ?? "" : "";
            var topK = args.ContainsKey("topK") ? Convert.ToInt32(args["topK"]) : 3;

            var results = await _rag.SemanticSearchAsync(query, topK);

            if (!results.Any())
                return "❌ İlgili bilgi bulunamadı.";

            var response = "✅ Bulunan Bilgiler:\n\n";
            int index = 1;
            foreach (var doc in results)
            {
                var preview = doc.Content.Length > 100
                    ? doc.Content.Substring(0, 100) + "..."
                    : doc.Content;
                response += $"{index}.  📄 **{doc.Title}**\n   {preview}\n\n";
                index++;
            }
            return response;
        }

        private Task<string> ExecuteGetProductDetailsTool(IDictionary<string, object?> args)
        {
            // TODO: Implement
            return Task.FromResult("GetProductDetailsTool executed");
        }

        private Task<string> ExecuteSearchProductsByPriceTool(IDictionary<string, object?> args)
        {
            // TODO:  Implement
            return Task.FromResult("SearchProductsByPriceTool executed");
        }

        private Task<string> ExecuteGetCategoryListTool(IDictionary<string, object?> args)
        {
            // TODO: Implement
            return Task.FromResult("GetCategoryListTool executed");
        }

        private Task<string> ExecuteCalculateTotalPriceTool(IDictionary<string, object?> args)
        {
            var pricesJson = args.ContainsKey("pricesJson") ? args["pricesJson"]?.ToString() ?? "[]" : "[]";

            try
            {
                var prices = System.Text.Json.JsonSerializer.Deserialize<List<decimal>>(pricesJson);
                if (prices == null || !prices.Any())
                    return Task.FromResult("❌ Geçerli fiyat listesi girilmedi.");

                var total = prices.Sum();
                return Task.FromResult($"🧮 Toplam:  {total:N2} TL ({prices.Count} ürün)");
            }
            catch
            {
                return Task.FromResult("❌ JSON parse hatası");
            }
        }
    }
}