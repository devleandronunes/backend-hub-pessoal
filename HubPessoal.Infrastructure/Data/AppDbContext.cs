using HubPessoal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HubPessoal.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<NoteFolder> NoteFolders => Set<NoteFolder>();
    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
           entity.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<NoteFolder>(entity =>
        {
            entity.HasIndex(f => new { f.ParentFolderId, f.Name }).IsUnique();

            entity.HasOne<NoteFolder>()
                .WithMany()
                .HasForeignKey(f => f.ParentFolderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Note>(entity =>
        {
            entity.HasIndex(n => new { n.FolderId, n.Title }).IsUnique();

            entity.HasOne<NoteFolder>()
                .WithMany()
                .HasForeignKey(n => n.FolderId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}