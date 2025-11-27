using AIChatBot.Services;
using AIChatBot.Models;
using System.ComponentModel;

namespace AIChatBot.Tools
{
    public class SearchRAGTool
    {
        private readonly RagService _ragService;
        private readonly ILogger<SearchRAGTool> _logger;

        public SearchRAGTool(RagService ragService, ILogger<SearchRAGTool> logger)
        {
            _ragService = ragService;
            _logger = logger;
        }

        [Description("Bilgi bankasında semantic search yapar (RAG)")]
        public async Task<string> Execute(
            [Description("Arama sorgusu")] string query,
            [Description("Kaç sonuç dönsün (varsayılan 3)")] int topK = 3)
        {
            _logger.LogInformation("[TOOL] SearchRAG called: Query='{Query}', TopK={TopK}", query, topK);

            // ✅ RBAC: Context'i al (opsiyonel - RAG herkese açık)
            ToolContext? context = null;
            try
            {
                context = ToolContextManager.GetContext();

                _logger.LogInformation(
                    "[RBAC] User:{UserId}, Role:{Role} searching: '{Query}'",
                    context.UserId,
                    context.Role,
                    query
                );
            }
            catch (InvalidOperationException)
            {
                _logger.LogWarning("[RBAC] ToolContext bulunamadı, anonim arama yapılıyor");
                // RAG arama herkese açık, context yoksa devam et
            }

            // ✅ RBAC: Rol bazlı kısıtlama (isteğe bağlı)
            // Örnek: Customer'lar günde max 100 arama yapabilir (DB'de sayaç tutulmalı)
            if (context != null && context.Role == "Customer")
            {
                // TODO: Rate limiting kontrolü (günlük arama sayısı)
                _logger.LogDebug(
                    "[RBAC] Customer UserId:{UserId} performing search (rate limiting: TODO)",
                    context.UserId
                );
            }

            // ✅ RAG arama
            try
            {
                var results = await _ragService.SemanticSearchAsync(query, topK);

                if (!results.Any())
                {
                    _logger.LogWarning("[TOOL] SearchRAG: No results for query '{Query}'", query);
                    return "❌ İlgili bilgi bulunamadı.";
                }

                _logger.LogInformation("[TOOL] SearchRAG: {Count} results found", results.Count);

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TOOL-ERROR] SearchRAG hatası: Query='{Query}'", query);
                return $"❌ Arama hatası: {ex.Message}";
            }
        }
    }
}