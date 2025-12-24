// Tools/GetPaymentMethodsTool.cs

using Microsoft.Data.SqlClient;
using System.ComponentModel;
using System.Data;

namespace AIChatBot.Tools
{
    /// <summary>
    /// Ödeme yöntemlerini listeler
    /// </summary>
    public class GetPaymentMethodsTool
    {
        private readonly string _connectionString;
        private readonly ILogger<GetPaymentMethodsTool> _logger;

        public GetPaymentMethodsTool(
            IConfiguration configuration,
            ILogger<GetPaymentMethodsTool> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _logger = logger;
        }

        [Description("Mevcut ödeme yöntemlerini listeler")]
        public async Task<string> Execute()
        {
            _logger.LogInformation("[TOOL] GetPaymentMethods called");

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    using (var cmd = new SqlCommand("sp_GetPaymentMethods", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        await conn.OpenAsync();

                        var response = "💳 **Ödeme Yöntemleri:**\n\n";
                        int index = 1;

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var methodName = reader.GetString(reader.GetOrdinal("MethodName"));
                                var description = reader.IsDBNull(reader.GetOrdinal("Description"))
                                    ? ""
                                    : reader.GetString(reader.GetOrdinal("Description"));
                                var hasInstallment = reader.GetBoolean(reader.GetOrdinal("HasInstallment"));
                                var maxInstallments = reader.IsDBNull(reader.GetOrdinal("MaxInstallments"))
                                    ? (int?)null
                                    : reader.GetInt32(reader.GetOrdinal("MaxInstallments"));

                                response += $"{index}. **{methodName}**\n";
                                if (!string.IsNullOrEmpty(description))
                                    response += $"   {description}\n";

                                if (hasInstallment && maxInstallments.HasValue)
                                    response += $"   ✅ Taksit: {maxInstallments.Value}'e kadar\n";

                                response += "\n";
                                index++;
                            }
                        }

                        _logger.LogInformation("[TOOL] ✅ {Count} payment method returned", index - 1);
                        return response;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TOOL-ERROR] GetPaymentMethods hatası");
                return $"❌ Hata: {ex.Message}";
            }
        }
    }
}