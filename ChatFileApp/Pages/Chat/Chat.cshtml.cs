using ChatFileApp.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChatFileApp.Pages.Chat
{
    public class ChatModel : PageModel
    {
        // Injecting the ApplicationDbContext to access the database
        private readonly ApplicationDbContext _context;

        public ChatModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // List to hold messages for the current conversation
        public List<Message>? Messages { get; set; }
        
        public int ChatId { get; set; }

        public Conversation? Conversation { get; set; }

        // OnGetAsync retrieves the conversation and its messages based on chatId from the URL
        public async Task<IActionResult> OnGetAsync(int chatId)
        {
            ChatId = chatId;
            
            // Retrieve the conversation including its messages and the associated users
            Conversation = await _context.Conversations
                .Include(c => c.Messages)
                .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(c => c.Id == chatId);

            // If no conversation is found, return a NotFound result
            if (Conversation == null)
            {
                return NotFound();
            }

            // Order messages by the SentAt timestamp in ascending order
            Messages = Conversation.Messages.OrderBy(m => m.SentAt).ToList();

            return Page();
        }
    }
}