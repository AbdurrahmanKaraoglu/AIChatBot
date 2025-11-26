using Microsoft.AspNetCore.Mvc;
using AIChatBot.Models;
using AIChatBot.Services;
using Microsoft.Extensions.AI; // ChatRole için gerekli

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
        public ActionResult GetHistory([FromQuery] string sessionId)
        {
            var history = _chatService.GetSessionHistory(sessionId);
            // ChatMessage'ı anonim objeye çeviriyoruz ki JSON serileştirme düzgün olsun
            var formatted = history.Select(m => new
            {
                role = m.Role.ToString(),
                content = m.Text
            });

            return Ok(new { sessionId, messages = formatted });
        }

        [HttpDelete("clear")]
        public ActionResult ClearSession([FromQuery] string sessionId)
        {
            _chatService.ClearSession(sessionId);
            return Ok(new { message = "Temizlendi" });
        }

        [HttpGet("search")]
        public ActionResult SearchDocs([FromQuery] string query)
        {
            return Ok(_rag.SearchDocuments(query ?? ""));
        }
    }
}