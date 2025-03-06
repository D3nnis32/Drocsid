using Drocsid.HenrikDennis2025.Core.Models;

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
            .Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);
            
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
            .Property(a => a.FileName)
            .IsRequired()
            .HasMaxLength(255);
            
        modelBuilder.Entity<Attachment>()
            .Property(a => a.StoragePath)
            .IsRequired();
    }
}