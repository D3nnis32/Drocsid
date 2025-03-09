using System.Text.Json;
using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Channel> Channels { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Attachment> Attachments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User configurations
        modelBuilder.Entity<User>()
            .HasKey(u => u.Id);
            
        modelBuilder.Entity<User>()
            .Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(50);
            
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();
            
        // Channel configurations
        modelBuilder.Entity<Channel>()
            .HasKey(c => c.Id);
            
        modelBuilder.Entity<Channel>()
            .Property(e => e.MemberIds)
            .HasConversion(
                v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                v => JsonSerializer.Deserialize<List<Guid>>(v, new JsonSerializerOptions()) ?? new List<Guid>()
            )
            .Metadata.SetValueComparer(new ValueComparer<List<Guid>>(
                (c1, c2) => c1.SequenceEqual(c2),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()));
            
        // Message configurations
        modelBuilder.Entity<Message>()
            .HasKey(m => m.Id);
            
        modelBuilder.Entity<Message>()
            .Property(m => m.Content)
            .HasMaxLength(5000);
            
        // Attachment configurations
        modelBuilder.Entity<Attachment>()
            .HasKey(a => a.Id);
            
        modelBuilder.Entity<Attachment>()
            .Property(a => a.Filename)
            .IsRequired()
            .HasMaxLength(255);
            
        modelBuilder.Entity<Attachment>()
            .Property(a => a.Path)
            .IsRequired();
    }
}