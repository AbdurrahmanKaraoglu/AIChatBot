using AIChatBot.Repository.KnowledgeBase;
using System.ComponentModel;

namespace AIChatBot.Tools
{
    /// <summary>
    /// Sistemdeki tüm kategorileri listeler
    /// </summary>
    public class GetCategoryListTool
    {
        private readonly IKnowledgeBaseRepository _repository;
        private readonly ILogger<GetCategoryListTool> _logger;

        public GetCategoryListTool(
            IKnowledgeBaseRepository repository,
            ILogger<GetCategoryListTool> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        [Description("Sistemdeki tüm ürün kategorilerini listeler")]
        public async Task<string> Execute()
        {
            try
            {
                _logger.LogInformation("[TOOL] GetCategoryList called");

                // Tüm belgeleri al
                var allDocuments = await _repository.GetAllDocuments();

                if (!allDocuments.Any())
                {
                    return "❌ Sistemde henüz kategori bulunmuyor.";
                }

                // Kategorileri grupla ve say
                var categories = allDocuments
                    .Where(d => !string.IsNullOrWhiteSpace(d.Category))
                    .GroupBy(d => d.Category)
                    .Select(g => new { Category = g.Key, Count = g.Count() })
                    .OrderByDescending(c => c.Count)
                    .ToList();

                if (!categories.Any())
                {
                    return "❌ Kategorisiz ürünler var. ";
                }

                _logger.LogInformation("[TOOL] ✅ {Count} kategori bulundu", categories.Count);

                // Formatlama
                var response = $"📂 **Sistemdeki Kategoriler ({categories.Count}):**\n\n";

                int index = 1;
                foreach (var cat in categories)
                {
                    response += $"{index}.  **{cat.Category}** ({cat.Count} ürün)\n";
                    index++;
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TOOL-ERROR] GetCategoryList hatası");
                return $"❌ Hata: {ex.Message}";
            }
        }
    }
}