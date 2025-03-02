using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ChatFileApp.Data
{
    // Enum to denote the type of conversation
    public enum ConversationType
    {
        Private, // Conversation with exactly two participants
        Group // Group conversation with multiple participants
    }

    public class ApplicationUser : IdentityUser
    {
        // Navigation property for messages sent by the user
        public ICollection<Message> Messages { get; set; }

        // Navigation property for the conversations this user participates in
        public ICollection<ConversationUser> ConversationUsers { get; set; }
    }

    // Represents a chat conversation, either private or group
    public class Conversation
    {
        public int Id { get; set; }

        // Type of conversation (private or group)
        public ConversationType Type { get; set; }

        // Optional conversation name (applicable for group chats)
        public string Name { get; set; }

        // Navigation property for the participants in the conversation
        public ICollection<ConversationUser> ConversationUsers { get; set; }

        // Navigation property for the messages in the conversation
        public ICollection<Message> Messages { get; set; }
    }

    // Join entity for the many-to-many relationship between users and conversations
    public class ConversationUser
    {
        // Composite key: ConversationId and UserId

        public int ConversationId { get; set; }
        public Conversation Conversation { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
    }

    public class Message
    {
        public int Id { get; set; }

        // Content of the message
        public string Content { get; set; }

        // The user who sent the message
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        // The conversation (either private or group) to which this message belongs
        public int ConversationId { get; set; }
        public Conversation Conversation { get; set; }

        // Timestamp for when the message was sent
        public DateTime SentAt { get; set; }
    }

    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSet for messages
        public DbSet<Message> Messages { get; set; }

        // DbSet for conversations
        public DbSet<Conversation> Conversations { get; set; }

        // DbSet for conversation participants
        public DbSet<ConversationUser> ConversationUsers { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure composite primary key for ConversationUser
            builder.Entity<ConversationUser>()
                .HasKey(cu => new { cu.ConversationId, cu.UserId });

            // Configure relationship: Conversation to ConversationUser (one-to-many)
            builder.Entity<ConversationUser>()
                .HasOne(cu => cu.Conversation)
                .WithMany(c => c.ConversationUsers)
                .HasForeignKey(cu => cu.ConversationId);

            // Configure relationship: ApplicationUser to ConversationUser (one-to-many)
            builder.Entity<ConversationUser>()
                .HasOne(cu => cu.User)
                .WithMany(u => u.ConversationUsers)
                .HasForeignKey(cu => cu.UserId);
        }
    }
}