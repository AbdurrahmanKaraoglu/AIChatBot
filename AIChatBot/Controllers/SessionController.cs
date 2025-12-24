// Controllers/SessionController.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace AIChatBot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SessionController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly ILogger<SessionController> _logger;

        public SessionController(
            IConfiguration configuration,
            ILogger<SessionController> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
            _logger = logger;
        }

        /// <summary>
        /// Session istatistikleri
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult> GetStats()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    using (var cmd = new SqlCommand("sp_GetSessionStats", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        await conn.OpenAsync();

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return Ok(new
                                {
                                    totalSessions = reader.GetInt32(0),
                                    uniqueUsers = reader.GetInt32(1),
                                    totalMessages = reader.GetInt32(2),
                                    avgMessagesPerSession = reader.GetDouble(3),
                                    lastActivity = reader.GetDateTime(4)
                                });
                            }
                        }
                    }
                }

                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SESSION-STATS-ERROR]");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Session detayı
        /// </summary>
        [HttpGet("{sessionId}")]
        public async Task<ActionResult> GetSessionDetail(string sessionId)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    using (var cmd = new SqlCommand("sp_GetSessionHistory", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@SessionId", sessionId);

                        await conn.OpenAsync();

                        // İlk result set:  Session bilgisi
                        object? sessionInfo = null;
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                sessionInfo = new
                                {
                                    sessionId = reader.GetString(0),
                                    userId = reader.IsDBNull(1) ? null : reader.GetString(1),
                                    userName = reader.IsDBNull(2) ? null : reader.GetString(2),
                                    startDate = reader.GetDateTime(3),
                                    lastActivityDate = reader.GetDateTime(4),
                                    messageCount = reader.GetInt32(5),
                                    isActive = reader.GetBoolean(6)
                                };
                            }

                            // İkinci result set: Messages
                            var messages = new List<object>();
                            if (await reader.NextResultAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    messages.Add(new
                                    {
                                        messageId = reader.GetInt64(0),
                                        role = reader.GetString(1),
                                        content = reader.GetString(2),
                                        createdDate = reader.GetDateTime(3)
                                    });
                                }
                            }

                            return Ok(new
                            {
                                session = sessionInfo,
                                messages
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SESSION-DETAIL-ERROR] SessionId:{SessionId}", sessionId);
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}