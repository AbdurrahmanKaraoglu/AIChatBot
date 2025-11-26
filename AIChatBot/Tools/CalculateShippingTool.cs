// Tools/CalculateShippingTool. cs
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;

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
            _logger.LogInformation($"[TOOL] CalculateShipping called: Amount={orderAmount}");

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

                            if (cost == 0)
                                return $"Kargo ücretsiz!  Teslimat süresi: {minDays}-{maxDays} iş günü. ";
                            else
                                return $"Kargo ücreti: {cost} TL.  Teslimat süresi: {minDays}-{maxDays} iş günü.";
                        }
                    }
                }
            }

            return "Kargo bilgisi bulunamadı.";
        }
    }
}