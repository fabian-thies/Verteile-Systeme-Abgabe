using ChatFileApp.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ChatFileApp.Pages.Chat
{
    public class ChatModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ChatModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<Message>? Messages { get; set; }
        
        public int ChatId { get; set; }

        public Conversation? Conversation { get; set; }

        [BindProperty]
        public string? NewMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int chatId)
        {
            ChatId = chatId;

            Conversation = await _context.Conversations
                .Include(c => c.Messages)
                .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(c => c.Id == chatId);

            if (Conversation == null)
            {
                return NotFound();
            }

            Messages = Conversation.Messages.OrderBy(m => m.SentAt).ToList();

            return Page();
        }

        // Optionally remove or retain the OnPostAsync method for fallback if needed.
        public async Task<IActionResult> OnPostAsync(int chatId)
        {
            ChatId = chatId;

            Conversation = await _context.Conversations
                .Include(c => c.Messages)
                .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(c => c.Id == chatId);

            if (Conversation == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(NewMessage))
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var message = new Message
                {
                    Content = NewMessage,
                    ConversationId = chatId,
                    SentAt = DateTime.UtcNow,
                    UserId = userId ?? "anonymous"
                };

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage(new { chatId = chatId });
        }
    }
}