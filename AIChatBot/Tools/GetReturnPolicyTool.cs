// Tools/GetReturnPolicyTool.cs

using Microsoft.Data.SqlClient;
using System.ComponentModel;
using System.Data;

namespace AIChatBot.Tools
{
    /// <summary>
    /// İade politikası bilgilerini getirir
    /// </summary>
    public class GetReturnPolicyTool
    {
        private readonly string _connectionString;
        private readonly ILogger<GetReturnPolicyTool> _logger;

        public GetReturnPolicyTool(
            IConfiguration configuration,
            ILogger<GetReturnPolicyTool> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _logger = logger;
        }

        [Description("İade politikası bilgilerini getirir")]
        public async Task<string> Execute()
        {
            _logger.LogInformation("[TOOL] GetReturnPolicy called");

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    using (var cmd = new SqlCommand("sp_GetReturnPolicy", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        await conn.OpenAsync();

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var policyName = reader.GetString(reader.GetOrdinal("PolicyName"));
                                var days = reader.GetInt32(reader.GetOrdinal("ReturnPeriodDays"));
                                var conditions = reader.GetString(reader.GetOrdinal("Conditions"));
                                var cost = reader.IsDBNull(reader.GetOrdinal("ReturnShippingCost"))
                                    ? (decimal?)null
                                    : reader.GetDecimal(reader.GetOrdinal("ReturnShippingCost"));

                                var response = $"📦 **İade Politikası:  {policyName}**\n\n";
                                response += $"⏰ İade Süresi: {days} gün\n\n";
                                response += $"📋 Koşullar:\n{conditions}\n\n";

                                if (cost.HasValue)
                                    response += $"💰 İade Kargo Ücreti: {cost.Value:N2} TL\n";
                                else
                                    response += "💰 İade Kargo Ücreti: Ücretsiz\n";

                                _logger.LogInformation("[TOOL] ✅ Return policy returned");
                                return response;
                            }
                            else
                            {
                                return "❌ İade politikası bulunamadı. ";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TOOL-ERROR] GetReturnPolicy hatası");
                return $"❌ Hata:  {ex.Message}";
            }
        }
    }
}