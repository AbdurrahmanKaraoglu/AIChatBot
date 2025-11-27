// C:\DOSYALAR\AI.NET\AIChatBot\AIChatBot\Repository\ChatMemoryRepository.cs
using Microsoft.Data.SqlClient;  // ✅ System.Data.SqlClient → Microsoft.Data.SqlClient
using Microsoft.Extensions.AI;
using System.Data;

namespace AIChatBot.Repository.ChatMemory
{
    public class ChatMemoryRepository : IChatMemoryRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<ChatMemoryRepository> _logger;

        public ChatMemoryRepository(IConfiguration configuration, ILogger<ChatMemoryRepository> _logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException(nameof(configuration));
            this._logger = _logger;
        }

        public async Task SaveMessageAsync(string sessionId, string userId, string userName, string role, string content)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();

                    // 1. Session güncelle/oluştur
                    using (SqlCommand cmdSession = new SqlCommand("sp_UpsertChatSession", conn))
                    {
                        cmdSession.CommandType = CommandType.StoredProcedure;
                        cmdSession.Parameters.Add(new SqlParameter("@SessionId", sessionId));
                        cmdSession.Parameters.Add(new SqlParameter("@UserId", userId ?? "anonymous"));
                        cmdSession.Parameters.Add(new SqlParameter("@UserName", userName ?? "Guest"));

                        await cmdSession.ExecuteNonQueryAsync();
                    }

                    // 2.  Mesajı kaydet
                    using (SqlCommand cmdMessage = new SqlCommand("sp_SaveChatMessage", conn))
                    {
                        cmdMessage.CommandType = CommandType.StoredProcedure;
                        cmdMessage.Parameters.Add(new SqlParameter("@SessionId", sessionId));
                        cmdMessage.Parameters.Add(new SqlParameter("@Role", role));
                        cmdMessage.Parameters.Add(new SqlParameter("@Content", content));

                        await cmdMessage.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"SaveMessageAsync hatası: {ex.Message}");
            }
        }

        public async Task<List<ChatMessage>> GetHistoryAsync(string sessionId)
        {
            List<ChatMessage> messages = new List<ChatMessage>();

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_GetChatHistory", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@SessionId", sessionId));

                        await conn.OpenAsync();

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string role = reader.GetString(reader.GetOrdinal("Role"));
                                string content = reader.GetString(reader.GetOrdinal("Content"));

                                ChatRole chatRole = role.ToLower() switch
                                {
                                    "user" => ChatRole.User,
                                    "assistant" => ChatRole.Assistant,
                                    "system" => ChatRole.System,
                                    _ => ChatRole.User
                                };

                                messages.Add(new ChatMessage(chatRole, content));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"GetHistoryAsync hatası: {ex.Message}");
            }

            return messages;
        }

        public async Task ClearSessionAsync(string sessionId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_ClearChatSession", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@SessionId", sessionId));

                        await conn.OpenAsync();
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"ClearSessionAsync hatası: {ex.Message}");
            }
        }
    }
}