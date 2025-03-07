using Drocsid.HenrikDennis2025.Core.Models;
using Microsoft.EntityFrameworkCore;
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

    public DbSet<Node> Nodes { get; set; }
    public DbSet<FileStorage> Files { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Node entity
        modelBuilder.Entity<Node>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Endpoint).IsRequired();
            b.Property(e => e.Region);
            b.Property(e => e.Metadata).HasColumnType("jsonb");
        });

        // Configure FileStorage entity
        modelBuilder.Entity<FileStorage>(b =>
        {
            b.HasKey(e => e.FileId);
            b.Property(e => e.FileName).IsRequired();
            b.Property(e => e.ContentType).IsRequired();
            b.Property(e => e.Size).IsRequired();
            b.Property(e => e.NodeIds).HasColumnType("jsonb");
            b.Property(e => e.Metadata).HasColumnType("jsonb");
        });
    }
}