using AIChatBot.Models;
using AIChatBot.Services;
using Microsoft.Data.SqlClient;
using System.ComponentModel;
using System.Data;

namespace AIChatBot.Tools
{
    /// <summary>
    /// Sipariş tutarına göre kargo ücreti hesaplar
    /// </summary>
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
        public async Task<string> Execute(decimal orderAmount)
        {
            _logger.LogInformation("[TOOL] CalculateShipping called: Amount={Amount}", orderAmount);

            // ✅ RBAC:  Context'i al (opsiyonel - bu tool herkes kullanabilir)
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
            }

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    using (var cmd = new SqlCommand("sp_CalculateShipping", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@OrderAmount", orderAmount);

                        await conn.OpenAsync();

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var shippingCost = reader.GetDecimal(reader.GetOrdinal("ShippingCost"));
                                var deliveryDaysMin = reader.GetInt32(reader.GetOrdinal("DeliveryDaysMin"));
                                var deliveryDaysMax = reader.GetInt32(reader.GetOrdinal("DeliveryDaysMax"));
                                var description = reader.IsDBNull(reader.GetOrdinal("Description"))
                                    ? ""
                                    : reader.GetString(reader.GetOrdinal("Description"));

                                _logger.LogInformation(
                                    "[TOOL] Shipping calculated: Amount={Amount}, Cost={Cost}, Delivery={Min}-{Max} days",
                                    orderAmount,
                                    shippingCost,
                                    deliveryDaysMin,
                                    deliveryDaysMax
                                );

                                // ✅ DETAYLI FORMAT (LLM'e gönderilmeyecek, direkt kullanılacak)
                                var response = $@"🚚 **Kargo Bilgileri:**

💰 Kargo Ücreti: {shippingCost:N2} TL{(shippingCost == 0 ? " (ÜCRETSİZ KARGO)" : "")}
📦 Teslimat Süresi: {deliveryDaysMin}-{deliveryDaysMax} iş günü

{description}";

                                return response;
                            }
                            else
                            {
                                return "❌ Bu sipariş tutarı için kargo kuralı bulunamadı. ";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TOOL-ERROR] CalculateShipping hatası:  Amount={Amount}", orderAmount);
                return $"❌ Kargo hesaplama hatası: {ex.Message}";
            }
        }
    }
}