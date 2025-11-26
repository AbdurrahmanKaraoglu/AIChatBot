// Tools/SearchRAGTool.cs
using AIChatBot.Services;
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
            _logger.LogInformation($"[TOOL] SearchRAG called: Query='{query}', TopK={topK}");

            var results = await _ragService.SemanticSearchAsync(query, topK);

            if (!results.Any())
                return "İlgili bilgi bulunamadı.";

            var response = "Bulunan Bilgiler:\n";
            foreach (var doc in results)
            {
                response += $"\n• {doc.Title}: {doc.Content.Substring(0, Math.Min(100, doc.Content.Length))}.. .\n";
            }

            return response;
        }
    }
}