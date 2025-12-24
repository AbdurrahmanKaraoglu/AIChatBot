using System.ComponentModel;
using System.Text.Json;

namespace AIChatBot.Tools
{
    /// <summary>
    /// Sepet toplamı hesaplar (basit matematik tool)
    /// </summary>
    public class CalculateTotalPriceTool
    {
        private readonly ILogger<CalculateTotalPriceTool> _logger;

        public CalculateTotalPriceTool(ILogger<CalculateTotalPriceTool> logger)
        {
            _logger = logger;
        }

        [Description("Ürün fiyatlarının toplamını hesaplar.  JSON array formatında fiyatlar alır:  [100, 250, 50]")]
        public Task<string> Execute(
            [Description("Fiyat listesi (JSON array)")] string pricesJson)
        {
            try
            {
                _logger.LogInformation("[TOOL] CalculateTotalPrice:  Prices={Prices}", pricesJson);

                // JSON parse
                var prices = JsonSerializer.Deserialize<List<decimal>>(pricesJson);

                if (prices == null || !prices.Any())
                {
                    return Task.FromResult("❌ Geçerli fiyat listesi girilmedi.");
                }

                // Toplam hesaplama
                var total = prices.Sum();
                var count = prices.Count;
                var average = prices.Average();

                _logger.LogInformation(
                    "[TOOL] ✅ Hesaplama:  {Count} ürün, Toplam={Total:N2} TL",
                    count,
                    total
                );

                var response = $"🧮 **Hesaplama Sonucu:**\n\n" +
                               $"📦 Ürün Sayısı: {count}\n" +
                               $"💰 Toplam: {total: N2} TL\n" +
                               $"📊 Ortalama: {average:N2} TL\n";

                return Task.FromResult(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TOOL-ERROR] CalculateTotalPrice hatası");
                return Task.FromResult($"❌ Hata:  {ex.Message}");
            }
        }
    }
}