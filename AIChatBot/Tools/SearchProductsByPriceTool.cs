using AIChatBot.Models;
using AIChatBot.Repository.KnowledgeBase;
using AIChatBot.Services;
using System.ComponentModel;

namespace AIChatBot.Tools
{
    /// <summary>
    /// Fiyat aralığına göre ürün arar
    /// </summary>
    public class SearchProductsByPriceTool
    {
        private readonly IKnowledgeBaseRepository _repository;
        private readonly ILogger<SearchProductsByPriceTool> _logger;

        public SearchProductsByPriceTool(
            IKnowledgeBaseRepository repository,
            ILogger<SearchProductsByPriceTool> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        [Description("Belirli bir fiyat aralığında ürün arar.  Kategori filtresi de eklenebilir.")]
        public async Task<string> Execute(
            [Description("Minimum fiyat (TL)")] decimal? minPrice = null,
            [Description("Maximum fiyat (TL)")] decimal? maxPrice = null,
            [Description("Kategori filtresi (opsiyonel)")] string? category = null,
            [Description("Arama kelimesi (opsiyonel)")] string? query = null)
        {
            try
            {
                _logger.LogInformation(
                    "[TOOL] SearchProductsByPrice: Min={MinPrice}, Max={MaxPrice}, Category={Category}, Query={Query}",
                    minPrice, maxPrice, category, query
                );

                // RBAC kontrolü
                var context = TryGetToolContext();

                // SmartProductSearch kullan
                var products = await _repository.SmartProductSearch(
                    query ?? "",
                    minPrice,
                    maxPrice,
                    category
                );

                if (!products.Any())
                {
                    _logger.LogWarning("[TOOL] Fiyat aralığında ürün bulunamadı");
                    return "❌ Belirtilen kriterlerde ürün bulunamadı.";
                }

                // RBAC: Customer sadece izinli ürünleri görsün
                if (context != null && context.Role == "Customer" && context.AllowedProductIds.Any())
                {
                    products = products.Where(p => context.AllowedProductIds.Contains(p.Id)).ToList();
                }

                _logger.LogInformation("[TOOL] ✅ {Count} ürün bulundu", products.Count);

                // Formatlama
                var response = $"✅ **{products.Count} Ürün Bulundu**\n\n";

                int index = 1;
                foreach (var product in products.Take(10)) // Max 10 ürün göster
                {
                    response += $"{index}. **{product.Title}**\n";
                    response += $"   💰 Fiyat: {product.Price: N2} TL\n";
                    response += $"   🏷️ Kategori:  {product.Category}\n";

                    var preview = product.Content.Length > 80
                        ? product.Content.Substring(0, 80) + "..."
                        : product.Content;
                    response += $"   📝 {preview}\n\n";

                    index++;
                }

                if (products.Count > 10)
                {
                    response += $"_... ve {products.Count - 10} ürün daha_\n";
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TOOL-ERROR] SearchProductsByPrice hatası");
                return $"❌ Hata: {ex.Message}";
            }
        }

        private ToolContext? TryGetToolContext()
        {
            try
            {
                return ToolContextManager.GetContext();
            }
            catch
            {
                return null;
            }
        }
    }
}