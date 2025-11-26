// Tools/GetProductInfoTool.cs
using AIChatBot.Repository.KnowledgeBase;
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
            _logger.LogInformation($"[TOOL] GetProductInfo called: ProductId={productId}");

            // Repository'den ürün bilgisini çek
            var query = $"SELECT * FROM Products WHERE ProductId = {productId}";
            var products = await _repo.SearchDocuments(productId.ToString());

            if (!products.Any())
                return $"Ürün ID {productId} bulunamadı. ";

            var product = products.First();
            return $"Ürün: {product.Title}\n{product.Content}";
        }
    }
}