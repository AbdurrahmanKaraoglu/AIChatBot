// Services/ToolContextManager.cs
using AIChatBot.Models;

namespace AIChatBot.Services
{
    public static class ToolContextManager
    {
        private static readonly AsyncLocal<ToolContext?> _context = new();

        public static void SetContext(ToolContext context)
        {
            _context.Value = context;
        }

        public static ToolContext GetContext()
        {
            if (_context.Value == null)
                throw new InvalidOperationException("ToolContext set edilmemiş!");

            return _context.Value;
        }

        public static void ClearContext()
        {
            _context.Value = null;
        }
    }
}