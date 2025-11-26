using Microsoft.AspNetCore.Mvc;
using AIChatBot.Models;
using AIChatBot.Services;
using Microsoft.Extensions.AI;

namespace AIChatBot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ChatService _chatService;
        private readonly RagService _rag;

        public ChatController(ChatService chatService, RagService rag)
        {
            _chatService = chatService;
            _rag = rag;
        }

        [HttpPost("message")]
        public async Task<ActionResult<Models.ChatResponse>> SendMessage([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message) || string.IsNullOrWhiteSpace(request.SessionId))
                return BadRequest(new { error = "Mesaj ve SessionId zorunludur" });

            var userContext = new UserContext
            {
                UserId = request.UserId ?? "anon",
                UserName = "Ziyaretçi"
            };

            var response = await _chatService.ProcessMessageAsync(request, userContext);
            return response.Success ? Ok(response) : StatusCode(500, response);
        }

        [HttpGet("history")]
        public async Task<ActionResult> GetHistory([FromQuery] string sessionId)
        {
            // ✅ Async metod kullan
            var history = await _chatService.GetSessionHistoryAsync(sessionId);

            var formatted = history.Select(m => new
            {
                role = m.Role.ToString(),
                content = m.Text
            });

            return Ok(new { sessionId, messages = formatted });
        }

        [HttpDelete("clear")]
        public async Task<ActionResult> ClearSession([FromQuery] string sessionId)
        {
            // ✅ Async metod kullan
            await _chatService.ClearSessionAsync(sessionId);
            return Ok(new { message = "Temizlendi" });
        }

        [HttpGet("search")]
        public async Task<ActionResult> SearchDocs([FromQuery] string query)
        {
            // ✅ Async metod kullan
            var documents = await _rag.SearchDocumentsAsync(query ?? "");
            return Ok(documents);
        }
    }
}