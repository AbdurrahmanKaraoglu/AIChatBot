using AIChatBot.Models;
using AIChatBot.Repository.KnowledgeBase;
using AIChatBot.Services;
using System.ComponentModel;

namespace AIChatBot.Tools
{
    /// <summary>
    /// Belirli bir ürünün detaylı bilgisini getirir
    /// </summary>
    public class GetProductDetailsTool
    {
        private readonly IKnowledgeBaseRepository _repository;
        private readonly ILogger<GetProductDetailsTool> _logger;

        public GetProductDetailsTool(
            IKnowledgeBaseRepository repository,
            ILogger<GetProductDetailsTool> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        [Description("Belirli bir ürünün ID'sine veya adına göre detaylı bilgisini getirir")]
        public async Task<string> Execute(
            [Description("Ürün ID'si (varsa)")] int? productId = null,
            [Description("Ürün adı (ID yoksa)")] string? productName = null)
        {
            try
            {
                _logger.LogInformation(
                    "[TOOL] GetProductDetails:  ID={ProductId}, Name={ProductName}",
                    productId,
                    productName
                );

                // RBAC kontrolü
                var context = TryGetToolContext();

                Document? product = null;

                // ID ile arama
                if (productId.HasValue)
                {
                    product = await _repository.GetDocumentByIdAsync(productId.Value);
                }
                // İsim ile arama
                else if (!string.IsNullOrWhiteSpace(productName))
                {
                    var results = await _repository.SearchDocuments(productName);
                    product = results.FirstOrDefault();
                }
                else
                {
                    return "❌ Lütfen ürün ID'si veya adını belirtin. ";
                }

                if (product == null)
                {
                    _logger.LogWarning("[TOOL] Ürün bulunamadı: ID={ProductId}, Name={ProductName}", productId, productName);
                    return "❌ Ürün bulunamadı.";
                }

                // RBAC:  Kullanıcının erişim kontrolü
                if (context != null && context.Role == "Customer")
                {
                    if (context.AllowedProductIds.Any() && !context.AllowedProductIds.Contains(product.Id))
                    {
                        _logger.LogWarning(
                            "[RBAC-BLOCKED] User:{UserId} tried to access Product:{ProductId}",
                            context.UserId,
                            product.Id
                        );
                        return "⛔ Bu ürüne erişim yetkiniz yok.";
                    }
                }

                // Detaylı formatlama
                var details = $"📦 **{product.Title}**\n\n" +
                              $"📝 **Açıklama:**\n{product.Content}\n\n" +
                              $"🏷️ **Kategori:** {product.Category}\n" +
                              $"🔖 **Etiketler:** {product.Tags}\n";

                if (product.Price.HasValue)
                {
                    details += $"💰 **Fiyat:** {product.Price.Value:N2} TL\n";
                }

                details += $"📅 **Kayıt Tarihi:** {product.CreatedDate:dd. MM.yyyy}\n";

                _logger.LogInformation("[TOOL] ✅ Ürün detayı döndürüldü: {Title}", product.Title);

                return details;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TOOL-ERROR] GetProductDetails hatası");
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