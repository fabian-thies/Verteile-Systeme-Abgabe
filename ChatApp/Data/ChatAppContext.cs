using ChatApp.Data;
using Microsoft.EntityFrameworkCore;

public class ChatAppContext : DbContext
{
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Conversation> Conversations { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=chatapp.db");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure many-to-many relationship between Conversation and User.
        modelBuilder.Entity<Conversation>()
            .HasMany(c => c.Users)
            .WithMany(u => u.Conversations)
            .UsingEntity(j => j.ToTable("ConversationUsers"));

        // Configure one-to-many relationship between Conversation and ChatMessage.
        modelBuilder.Entity<Conversation>()
            .HasMany(c => c.ChatMessages)
            .WithOne(cm => cm.Conversation)
            .HasForeignKey(cm => cm.ConversationId);
    }
}