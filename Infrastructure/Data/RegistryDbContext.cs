using System.Text.Json;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using FileInfo = System.IO.FileInfo;

namespace Infrastructure.Data;

/// <summary>
/// Database context for the registry services
/// </summary>
public class RegistryDbContext : DbContext
{
    public RegistryDbContext(DbContextOptions<RegistryDbContext> options) 
        : base(options)
    {
    }

    // Node registry
    public DbSet<StorageNode> Nodes { get; set; }
    
    // File registry
    public DbSet<StoredFile> Files { get; set; }

    // User registry
    public DbSet<User> Users { get; set; }
    
    // Channel registry 
    public DbSet<Channel> Channels { get; set; }
    
    // Message registry
    public DbSet<Message> Messages { get; set; }
    public DbSet<Attachment> Attachments { get; set; }
    
    // Relationships and mappings
    public DbSet<UserChannel> UserChannels { get; set; }
    public DbSet<ChannelNode> ChannelNodes { get; set; }
    public DbSet<MessageLocation> MessageLocations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure StorageNode
        modelBuilder.Entity<StorageNode>(entity =>
        {
            // entity.ToTable("Nodes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Hostname).IsRequired();
            entity.Property(e => e.Endpoint).IsRequired();
            entity.Property(e => e.LastSeen).IsRequired();
            
            // Configure NodeStatus as a complex property
            entity.OwnsOne(e => e.Status, status =>
            {
                status.Property(s => s.IsHealthy).IsRequired();
                status.Property(s => s.CurrentLoad).IsRequired();
                status.Property(s => s.AvailableSpace).IsRequired();
                status.Property(s => s.ActiveConnections).IsRequired();
                status.Property(s => s.LastUpdated).IsRequired();
            });
            
            entity.Property(e => e.Region).IsRequired(false);
            entity.Property(e => e.TotalStorage).IsRequired();
            
            // Configure Tags as JSON array with value comparer
            entity.Property(e => e.Tags)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                    v => JsonSerializer.Deserialize<List<string>>(v, new JsonSerializerOptions()) ?? new List<string>(),
                    new ValueComparer<List<string>>(
                        (c1, c2) => c1.SequenceEqual(c2),
                        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        c => c.ToList()
                    )
                );

            // Configure Metadata as JSON object with value comparer
            entity.Property(e => e.Metadata)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, new JsonSerializerOptions()) ?? new Dictionary<string, string>(),
                    new ValueComparer<Dictionary<string, string>>(
                        (c1, c2) => c1.Count == c2.Count && !c1.Except(c2).Any(),
                        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.Key.GetHashCode(), v.Value.GetHashCode())),
                        c => new Dictionary<string, string>(c)
                    )
                );
        });

        // Configure StoredFile
        modelBuilder.Entity<StoredFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Filename).IsRequired();
            entity.Property(e => e.Size).IsRequired();
            entity.Property(e => e.ContentType).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ModifiedAt).IsRequired();
            entity.Property(e => e.OwnerId).IsRequired();
            entity.Property(e => e.Checksum).IsRequired(false);
            
            // Configure Tags as JSON array with value comparer
            entity.Property(e => e.Tags)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                    v => JsonSerializer.Deserialize<List<string>>(v, new JsonSerializerOptions()) ?? new List<string>()
                )
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (c1, c2) => c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));
            
            // Configure NodeLocations as JSON array with value comparer
            entity.Property(e => e.NodeLocations)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                    v => JsonSerializer.Deserialize<List<string>>(v, new JsonSerializerOptions()) ?? new List<string>()
                )
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (c1, c2) => c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));
            
            // Configure Metadata as JSON object with value comparer
            entity.Property(e => e.Metadata)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, new JsonSerializerOptions()) ?? new Dictionary<string, string>()
                )
                .Metadata.SetValueComparer(new ValueComparer<Dictionary<string, string>>(
                    (c1, c2) => c1.Count == c2.Count && !c1.Except(c2).Any(),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.Key.GetHashCode(), v.Value.GetHashCode())),
                    c => new Dictionary<string, string>(c)));
        });

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PasswordHash).HasMaxLength(100);
            entity.Property(e => e.Status);
            entity.Property(e => e.LastSeen);
            entity.Property(e => e.CreatedAt);
            entity.Property(e => e.UpdatedAt);
            entity.Property(e => e.PreferredRegion).HasMaxLength(50);
            entity.Property(e => e.CurrentNodeId).HasMaxLength(50);
        });

        // Configure Channel entity
        modelBuilder.Entity<Channel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Type);
            entity.Property(e => e.CreatedAt);
            entity.Property(e => e.UpdatedAt);
            
            // Configure MemberIds as JSON array with value comparer
            entity.Property(e => e.MemberIds)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                    v => JsonSerializer.Deserialize<List<Guid>>(v, new JsonSerializerOptions()) ?? new List<Guid>()
                )
                .Metadata.SetValueComparer(new ValueComparer<List<Guid>>(
                    (c1, c2) => c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToList()));
        });

        // Configure Message entity
        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ChannelId);
            entity.Property(e => e.SenderId);
            entity.Property(e => e.Content).HasColumnType("text");
            entity.Property(e => e.SentAt);
            entity.Property(e => e.EditedAt);
            
            // Relationship with Attachments
            entity.HasMany(e => e.Attachments)
                .WithOne()
                .HasForeignKey("MessageId")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Attachment entity
        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Filename).HasMaxLength(255);
            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.Property(e => e.Path).HasMaxLength(500);
            entity.Property(e => e.Size);
            entity.Property(e => e.UploadedAt);
        });

        // Configure UserChannel entity
        modelBuilder.Entity<UserChannel>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.ChannelId });
            entity.Property(e => e.JoinedAt);
            
            // Relationships
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne<Channel>()
                .WithMany()
                .HasForeignKey(e => e.ChannelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure ChannelNode entity
        modelBuilder.Entity<ChannelNode>(entity =>
        {
            entity.HasKey(e => new { e.ChannelId, e.NodeId });
            entity.Property(e => e.CreatedAt);
            
            // Relationships
            entity.HasOne<Channel>()
                .WithMany()
                .HasForeignKey(e => e.ChannelId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // We can't create a foreign key to Nodes because the NodeId is a string
            // and the Node table uses string primary keys
        });

        // Configure MessageLocation entity
        modelBuilder.Entity<MessageLocation>(entity =>
        {
            entity.HasKey(e => new { e.MessageId, e.NodeId });
            entity.Property(e => e.CreatedAt);
            
            // Relationships
            entity.HasOne<Message>()
                .WithMany()
                .HasForeignKey(e => e.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // We can't create a foreign key to Nodes because the NodeId is a string
            // and Message uses Guid as primary key
        });
    }
}