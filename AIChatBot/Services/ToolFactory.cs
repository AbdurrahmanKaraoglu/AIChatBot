// Services/ToolFactory.cs

using AIChatBot.Repository.KnowledgeBase;
using AIChatBot.Tools;
using Microsoft.Extensions.AI;
using Serilog;

namespace AIChatBot.Services
{
    /// <summary>
    /// Scoped tool'ları oluşturan factory
    /// </summary>
    public class ToolFactory
    {
        private readonly IKnowledgeBaseRepository _knowledgeBaseRepo;
        private readonly RagService _ragService;
        private readonly IConfiguration _configuration;
        private readonly ILoggerFactory _loggerFactory;

        public ToolFactory(
            IKnowledgeBaseRepository knowledgeBaseRepo,
            RagService ragService,
            IConfiguration configuration,
            ILoggerFactory loggerFactory)
        {
            _knowledgeBaseRepo = knowledgeBaseRepo;
            _ragService = ragService;
            _configuration = configuration;
            _loggerFactory = loggerFactory;
        }

        public List<AITool> CreateTools()
        {
            var tools = new List<AITool>();

            try
            {
                // 🔧 1. SearchRAGTool
                var searchRAGTool = new SearchRAGTool(
                    _ragService,
                    _loggerFactory.CreateLogger<SearchRAGTool>()
                );
                var searchRAGAITool = AIFunctionFactory.Create(
                    searchRAGTool.Execute,
                    name: "SearchRAGTool",
                    description: "Bilgi bankasında semantic search yapar.  Genel bilgi sorguları için kullanılır."
                );
                tools.Add(searchRAGAITool);

                // 🔧 2. GetProductDetailsTool
                var getProductDetailsTool = new GetProductDetailsTool(
                    _knowledgeBaseRepo,
                    _loggerFactory.CreateLogger<GetProductDetailsTool>()
                );
                var getProductDetailsAITool = AIFunctionFactory.Create(
                    getProductDetailsTool.Execute,
                    name: "GetProductDetailsTool",
                    description: "Belirli bir ürünün detaylı bilgisini getirir (ID veya isme göre)"
                );
                tools.Add(getProductDetailsAITool);

                // 🔧 3. SearchProductsByPriceTool
                var searchProductsByPriceTool = new SearchProductsByPriceTool(
                    _knowledgeBaseRepo,
                    _loggerFactory.CreateLogger<SearchProductsByPriceTool>()
                );
                var searchProductsByPriceAITool = AIFunctionFactory.Create(
                    searchProductsByPriceTool.Execute,
                    name: "SearchProductsByPriceTool",
                    description: "Fiyat aralığına ve kategoriye göre ürün arar"
                );
                tools.Add(searchProductsByPriceAITool);

                // 🔧 4. GetCategoryListTool
                var getCategoryListTool = new GetCategoryListTool(
                    _knowledgeBaseRepo,
                    _loggerFactory.CreateLogger<GetCategoryListTool>()
                );
                var getCategoryListAITool = AIFunctionFactory.Create(
                    getCategoryListTool.Execute,
                    name: "GetCategoryListTool",
                    description: "Sistemdeki tüm ürün kategorilerini listeler"
                );
                tools.Add(getCategoryListAITool);

                // 🔧 5. CalculateTotalPriceTool (Scoped değil, singleton olabilir)
                var calculateTotalPriceTool = new CalculateTotalPriceTool(
                    _loggerFactory.CreateLogger<CalculateTotalPriceTool>()
                );
                var calculateTotalPriceAITool = AIFunctionFactory.Create(
                    calculateTotalPriceTool.Execute,
                    name: "CalculateTotalPriceTool",
                    description: "Ürün fiyatlarının toplamını hesaplar"
                );
                tools.Add(calculateTotalPriceAITool);

                // 🔧 6. GetReturnPolicyTool (IConfiguration gerekiyor)
                var getReturnPolicyTool = new GetReturnPolicyTool(
                    _configuration,
                    _loggerFactory.CreateLogger<GetReturnPolicyTool>()
                );
                var getReturnPolicyAITool = AIFunctionFactory.Create(
                    getReturnPolicyTool.Execute,
                    name: "GetReturnPolicyTool",
                    description: "İade politikası bilgilerini getirir"
                );
                tools.Add(getReturnPolicyAITool);

                // 🔧 7. GetPaymentMethodsTool (IConfiguration gerekiyor)
                var getPaymentMethodsTool = new GetPaymentMethodsTool(
                    _configuration,
                    _loggerFactory.CreateLogger<GetPaymentMethodsTool>()
                );
                var getPaymentMethodsAITool = AIFunctionFactory.Create(
                    getPaymentMethodsTool.Execute,
                    name: "GetPaymentMethodsTool",
                    description: "Mevcut ödeme yöntemlerini listeler"
                );
                tools.Add(getPaymentMethodsAITool);

                Log.Information("[TOOL-FACTORY] ✅ {Count} tool oluşturuldu", tools.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TOOL-FACTORY] ❌ Tool oluşturma hatası");
            }

            return tools;
        }
    }
}