using System.Security.Claims;
using ChatFileApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChatFileApp.Pages.Chat
{
    [Authorize]
    public class New : PageModel
    {
        private readonly ApplicationDbContext _context;

        public New(ApplicationDbContext context) =>
            _context = context;

        public List<ApplicationUser> Users { get; set; } = new();

        [BindProperty]
        public List<string> SelectedUserIds { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Users = await _context.Users
                .Where(u => u.Id != currentUserId)
                .ToListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (SelectedUserIds == null || SelectedUserIds.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "No user selected.");
                await OnGetAsync();
                return Page();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Check if it's a private conversation (exactly one selected user)
            if (SelectedUserIds.Count == 1)
            {
                var otherUserId = SelectedUserIds.First();

                // Search for an existing private conversation with exactly two participants
                var existingConversation = await _context.Conversations
                    .Include(c => c.ConversationUsers)
                    .Where(c => c.Type == ConversationType.Private &&
                        c.ConversationUsers.Any(cu => cu.UserId == currentUserId) &&
                        c.ConversationUsers.Any(cu => cu.UserId == otherUserId) &&
                        c.ConversationUsers.Count == 2)
                    .FirstOrDefaultAsync();

                if (existingConversation != null)
                {
                    return RedirectToPage("/Chat/Chat", new { chatId = existingConversation.Id });
                }
            }

            var type = (SelectedUserIds.Count == 1)
                ? ConversationType.Private
                : ConversationType.Group;

            var conversation = new Conversation
            {
                Type = type,
                Name = (type == ConversationType.Group) ? "Group Chat" : "Private Chat"
            };

            _context.Conversations.Add(conversation);
            await _context.SaveChangesAsync();

            var conversationUsers = new List<ConversationUser>
            {
                new ConversationUser
                {
                    UserId = currentUserId,
                    ConversationId = conversation.Id
                }
            };

            foreach (var userId in SelectedUserIds)
            {
                conversationUsers.Add(new ConversationUser
                {
                    UserId = userId,
                    ConversationId = conversation.Id
                });
            }

            _context.Set<ConversationUser>().AddRange(conversationUsers);
            await _context.SaveChangesAsync();

            return RedirectToPage("/Chat/Chat", new { chatId = conversation.Id });
        }
    }
}