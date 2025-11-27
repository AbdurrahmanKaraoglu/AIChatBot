// C:\DOSYALAR\AI.NET\AIChatBot\AIChatBot\Controllers\ToolTestController.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

namespace AIChatBot.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ToolTestController : ControllerBase
    {
        private readonly IEnumerable<AITool> _tools;
        private readonly ILogger<ToolTestController> _logger;

        public ToolTestController(
            IEnumerable<AITool> tools,
            ILogger<ToolTestController> logger)
        {
            _tools = tools;
            _logger = logger;
        }

        [HttpGet("registered-tools")]
        public ActionResult GetRegisteredTools()
        {
            // ✅ AITool sınıfında Metadata yok, basit liste döndür
            return Ok(new
            {
                count = _tools.Count(),
                message = _tools.Any()
                    ? "Tool'lar kayıtlı"
                    : "Henüz tool kaydedilmemiş (Normal - llama3. 1 gerektirir)"
            });
        }

        [HttpGet("health")]
        public ActionResult HealthCheck()
        {
            return Ok(new
            {
                status = "Healthy",
                toolCount = _tools.Count(),
                toolsEnabled = _tools.Any(),
                timestamp = DateTime.UtcNow
            });
        }
    }
}