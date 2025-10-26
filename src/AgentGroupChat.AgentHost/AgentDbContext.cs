using AgentGroupChat.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentGroupChat.AgentHost;

/// <summary>
/// Entity Framework Core DbContext for chat sessions and messages
/// Supports both SQLite and PostgreSQL providers
/// </summary>
public class AgentDbContext : DbContext
{
    public AgentDbContext(DbContextOptions<AgentDbContext> options) : base(options)
    {
    }

    public DbSet<PersistedChatSession> Sessions { get; set; } = null!;
    public DbSet<PersistedChatMessage> Messages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure PersistedChatSession entity
        modelBuilder.Entity<PersistedChatSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ThreadData).HasColumnType("TEXT");
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.LastUpdated).IsRequired();
            entity.Property(e => e.MessageCount).IsRequired();
            entity.Property(e => e.Version).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
            entity.Property(e => e.LastMessagePreview).HasMaxLength(500);
            entity.Property(e => e.LastMessageSender).HasMaxLength(100);

            // Indexes for better query performance
            entity.HasIndex(e => e.LastUpdated);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure PersistedChatMessage entity
        modelBuilder.Entity<PersistedChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SessionId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.MessageId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.SerializedMessage).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.MessageText).HasColumnType("TEXT");
            entity.Property(e => e.AgentId).HasMaxLength(100);
            entity.Property(e => e.AgentName).HasMaxLength(100);
            entity.Property(e => e.AgentAvatar).HasMaxLength(50);
            entity.Property(e => e.IsUser).IsRequired();
            entity.Property(e => e.ImageUrl).HasMaxLength(1000);
            entity.Property(e => e.Role).HasMaxLength(50);

            // Indexes for better query performance
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.SessionId, e.Timestamp });
        });
    }
}
