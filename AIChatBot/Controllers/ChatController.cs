using AIChatBot.Models;
using AIChatBot.Repository.KnowledgeBase;
using AIChatBot.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIChatBot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ChatService _chatService;
        private readonly RagService _rag;
        private readonly IKnowledgeBaseRepository _knowledgeBaseRepository;
        private readonly ILogger<ChatController> _logger;

        public ChatController(
            ChatService chatService,
            RagService rag,
            IKnowledgeBaseRepository knowledgeBaseRepository,
            ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _rag = rag;
            _knowledgeBaseRepository = knowledgeBaseRepository;
            _logger = logger;
        }

        [HttpPost("message")]
        public async Task<ActionResult<Models.ChatResponse>> SendMessage([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message) || string.IsNullOrWhiteSpace(request.SessionId))
            {
                return BadRequest(new { error = "Mesaj ve SessionId zorunludur" });
            }

            // ✅ RBAC: UserContext oluştur (Role bilgisi ile)
            var userContext = new UserContext
            {
                UserId = request.UserId ?? "anonymous",
                UserName = "Ziyaretçi",
                Role = request.Role ?? "Customer"  // ✅ Role eklendi
            };

            _logger.LogInformation(
                "[CONTROLLER] Message received: SessionId={SessionId}, UserId={UserId}, Role={Role}",
                request.SessionId,
                userContext.UserId,
                userContext.Role
            );

            var response = await _chatService.ProcessMessageAsync(request, userContext);

            return response.Success ? Ok(response) : StatusCode(500, response);
        }

        [HttpGet("history")]
        public async Task<ActionResult> GetHistory([FromQuery] string sessionId)
        {
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
            await _chatService.ClearSessionAsync(sessionId);
            return Ok(new { message = "Session temizlendi" });
        }

        [HttpGet("search")]
        public async Task<ActionResult> SearchDocs([FromQuery] string query)
        {
            var documents = await _rag.SearchDocumentsAsync(query ?? "");
            return Ok(documents);
        }

        [HttpPost("smart-search")]
        public async Task<ActionResult> SmartProductSearch([FromBody] SmartSearchRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Query))
                return BadRequest(new { error = "Query zorunludur" });

            var documents = await _knowledgeBaseRepository.SmartProductSearch(
                request.Query,
                request.MinPrice,
                request.MaxPrice,
                request.Category
            );

            return Ok(new
            {
                query = request.Query,
                filters = new
                {
                    minPrice = request.MinPrice,
                    maxPrice = request.MaxPrice,
                    category = request.Category
                },
                resultCount = documents.Count,
                products = documents
            });
        }
    }

    public class SmartSearchRequest
    {
        public string Query { get; set; } = string.Empty;
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? Category { get; set; }
    }
}