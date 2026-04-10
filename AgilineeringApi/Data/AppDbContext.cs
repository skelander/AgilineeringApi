using AgilineeringApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AgilineeringApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<PostPreview> PostPreviews => Set<PostPreview>();
    public DbSet<PreviewComment> PreviewComments => Set<PreviewComment>();
    public DbSet<Image> Images => Set<Image>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Username).IsUnique();
            entity.Property(u => u.Username).HasMaxLength(100);
            entity.Property(u => u.PasswordHash).HasMaxLength(128);
            entity.Property(u => u.Role).HasMaxLength(50);
        });

        modelBuilder.Entity<Post>(entity =>
        {
            entity.HasIndex(p => p.Slug).IsUnique();
            entity.HasIndex(p => p.AuthorId);
            entity.HasIndex(p => new { p.Published, p.CreatedAt });
            entity.Property(p => p.Title).HasMaxLength(300);
            entity.Property(p => p.Slug).HasMaxLength(300);
            entity.HasOne(p => p.Author)
                .WithMany(u => u.Posts)
                .HasForeignKey(p => p.AuthorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasIndex(t => t.Slug).IsUnique();
            entity.HasIndex(t => t.Name).IsUnique();
            entity.Property(t => t.Name).HasMaxLength(100);
            entity.Property(t => t.Slug).HasMaxLength(100);
        });

        modelBuilder.Entity<Post>()
            .HasMany(p => p.Tags)
            .WithMany(t => t.Posts)
            .UsingEntity("PostTag");

        modelBuilder.Entity<PreviewComment>(entity =>
        {
            entity.HasIndex(c => c.PreviewId);
            entity.Property(c => c.Body).HasMaxLength(5000);
            entity.HasOne(c => c.Preview)
                .WithMany()
                .HasForeignKey(c => c.PreviewId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Image>(entity =>
        {
            entity.HasIndex(i => i.Filename).IsUnique();
            entity.Property(i => i.Filename).HasMaxLength(260);
            entity.Property(i => i.ContentType).HasMaxLength(100);
        });

        modelBuilder.Entity<PostPreview>(entity =>
        {
            entity.HasIndex(pp => pp.Token).IsUnique();
            entity.HasIndex(pp => pp.PostId);
            entity.Property(pp => pp.Token).HasMaxLength(32);
            entity.Property(pp => pp.PasswordHash).HasMaxLength(128);
            entity.HasOne(pp => pp.Post)
                .WithMany()
                .HasForeignKey(pp => pp.PostId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
