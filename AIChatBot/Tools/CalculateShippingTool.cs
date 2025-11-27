using AIChatBot.Services;
using AIChatBot.Models;
using Microsoft.Data.SqlClient;
using System.ComponentModel;
using System.Data;

namespace AIChatBot.Tools
{
    public class CalculateShippingTool
    {
        private readonly string _connectionString;
        private readonly ILogger<CalculateShippingTool> _logger;

        public CalculateShippingTool(
            IConfiguration configuration,
            ILogger<CalculateShippingTool> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _logger = logger;
        }

        [Description("Sipariş tutarına göre kargo ücretini hesaplar")]
        public async Task<string> Execute(
            [Description("Sipariş tutarı (TL)")] decimal orderAmount)
        {
            _logger.LogInformation("[TOOL] CalculateShipping called: Amount={Amount}", orderAmount);

            // ✅ RBAC: Context'i al (opsiyonel - bu tool herkes kullanabilir)
            ToolContext? context = null;
            try
            {
                context = ToolContextManager.GetContext();

                _logger.LogInformation(
                    "[RBAC] User:{UserId}, Role:{Role} calculating shipping for {Amount} TL",
                    context.UserId,
                    context.Role,
                    orderAmount
                );
            }
            catch (InvalidOperationException)
            {
                _logger.LogWarning("[RBAC] ToolContext bulunamadı, anonim kullanıcı olarak devam ediliyor");
                // Kargo hesaplama herkese açık, context yoksa devam et
            }

            // ✅ RBAC: Rol bazlı kısıtlama (isteğe bağlı)
            // Örnek: Customer'lar max 10,000 TL için kargo hesaplayabilir
            if (context != null && context.Role == "Customer" && orderAmount > 10000)
            {
                _logger.LogWarning(
                    "[RBAC-DENIED] Customer UserId:{UserId} tried to calculate shipping for {Amount} TL (limit: 10,000 TL)",
                    context.UserId,
                    orderAmount
                );

                return "❌ Müşteriler en fazla 10,000 TL'lik siparişler için kargo hesaplayabilir.  Daha fazlası için lütfen satış ekibiyle iletişime geçin.";
            }

            // ✅ Kargo hesaplama
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_CalculateShipping", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@OrderAmount", orderAmount));

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                decimal cost = reader.GetDecimal(reader.GetOrdinal("ShippingCost"));
                                int minDays = reader.GetInt32(reader.GetOrdinal("DeliveryDaysMin"));
                                int maxDays = reader.GetInt32(reader.GetOrdinal("DeliveryDaysMax"));

                                _logger.LogInformation(
                                    "[TOOL] Shipping calculated: Amount={Amount}, Cost={Cost}, Delivery={MinDays}-{MaxDays} days",
                                    orderAmount, cost, minDays, maxDays
                                );

                                if (cost == 0)
                                    return $"✅ Kargo ücretsiz! 🎉\n📦 Teslimat süresi: {minDays}-{maxDays} iş günü. ";
                                else
                                    return $"✅ Kargo ücreti: {cost} TL\n📦 Teslimat süresi: {minDays}-{maxDays} iş günü.";
                            }
                        }
                    }
                }

                _logger.LogWarning("[TOOL] Kargo kuralı bulunamadı: Amount={Amount}", orderAmount);
                return "❌ Kargo bilgisi bulunamadı.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TOOL-ERROR] Kargo hesaplama hatası: Amount={Amount}", orderAmount);
                return $"❌ Kargo hesaplama hatası: {ex.Message}";
            }
        }
    }
}