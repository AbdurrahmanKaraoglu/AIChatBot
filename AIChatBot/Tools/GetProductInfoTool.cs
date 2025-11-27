using AIChatBot.Repository.KnowledgeBase;
using AIChatBot.Services;
using AIChatBot.Models;
using System.ComponentModel;

namespace AIChatBot.Tools
{
    public class GetProductInfoTool
    {
        private readonly IKnowledgeBaseRepository _repo;
        private readonly ILogger<GetProductInfoTool> _logger;

        public GetProductInfoTool(
            IKnowledgeBaseRepository repo,
            ILogger<GetProductInfoTool> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        [Description("Ürün ID'sine göre detaylı ürün bilgilerini getirir")]
        public async Task<string> Execute(
            [Description("Ürün ID'si")] int productId)
        {
            _logger.LogInformation("[TOOL] GetProductInfo called: ProductId={ProductId}", productId);

            // ✅ RBAC: Context'i al
            ToolContext? context = null;
            try
            {
                context = ToolContextManager.GetContext();

                _logger.LogInformation(
                    "[RBAC] User:{UserId}, Role:{Role}, Accessing ProductId:{ProductId}",
                    context.UserId,
                    context.Role,
                    productId
                );
            }
            catch (InvalidOperationException)
            {
                _logger.LogWarning("[RBAC] ToolContext bulunamadı, erişim reddedildi");
                return "❌ Yetkilendirme hatası: Oturum bilgisi bulunamadı. ";
            }

            // ✅ RBAC: Yetki kontrolü
            if (context.Role == "Customer")
            {
                // Customer sadece izin verilen ürünlere erişebilir
                if (!context.AllowedProductIds.Contains(productId))
                {
                    _logger.LogWarning(
                        "[RBAC-DENIED] User:{UserId} (Role:Customer) tried to access ProductId:{ProductId}.  Allowed:[{AllowedIds}]",
                        context.UserId,
                        productId,
                        string.Join(", ", context.AllowedProductIds)
                    );

                    throw new UnauthorizedAccessException(
                        $"Bu ürüne erişim yetkiniz yok. Sadece şu ürünlere erişebilirsiniz: {string.Join(", ", context.AllowedProductIds)}"
                    );
                }

                _logger.LogInformation(
                    "[RBAC-ALLOWED] Customer UserId:{UserId} accessing allowed ProductId:{ProductId}",
                    context.UserId,
                    productId
                );
            }
            else if (context.Role == "Admin" || context.Role == "Moderator")
            {
                // Admin ve Moderator tüm ürünlere erişebilir
                _logger.LogInformation(
                    "[RBAC-ALLOWED] {Role} UserId:{UserId} has full access to ProductId:{ProductId}",
                    context.Role,
                    context.UserId,
                    productId
                );
            }
            else
            {
                // Bilinmeyen rol
                _logger.LogWarning(
                    "[RBAC-DENIED] Unknown role '{Role}' for UserId:{UserId}",
                    context.Role,
                    context.UserId
                );

                throw new UnauthorizedAccessException($"Geçersiz rol: {context.Role}");
            }

            // ✅ Yetki kontrolü geçti, ürün bilgisini getir
            try
            {
                var products = await _repo.SearchDocuments(productId.ToString());

                if (!products.Any())
                {
                    _logger.LogWarning("[TOOL] ProductId {ProductId} bulunamadı", productId);
                    return $"❌ Ürün ID {productId} veritabanında bulunamadı.";
                }

                var product = products.First();
                _logger.LogInformation("[TOOL] ProductId {ProductId} bilgisi döndürüldü", productId);

                return $"✅ Ürün Bilgisi:\n📦 {product.Title}\n📝 {product.Content}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TOOL-ERROR] ProductId:{ProductId} getirme hatası", productId);
                return $"❌ Ürün bilgisi alınırken hata oluştu: {ex.Message}";
            }
        }
    }
}