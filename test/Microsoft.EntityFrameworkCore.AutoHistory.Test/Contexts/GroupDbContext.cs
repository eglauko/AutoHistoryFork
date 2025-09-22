using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Storage;
using System.Linq;

namespace Microsoft.EntityFrameworkCore.AutoHistoryTests.Contexts;

public class GroupDbContext : DbContext
{
    private readonly SqliteConnection connection;
    private readonly string currentUserName;

    public GroupDbContext(SqliteConnection connection, IUserCredentials currentUser)
    {
        this.connection = connection;
        currentUserName = currentUser.GetUserName();
    }

    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
            return;

        optionsBuilder.UseSqlite(connection);

        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.EnableAutoHistory<GroupDbContext>(
            configure: options => options
                .WithGroupId(true)
                .ConfigureType<Blog>(typeOptions =>
                {
                    typeOptions.WithGroupProperty(nameof(Blog.BlogId));
                })
                .ConfigureType<Post>(typeOptions =>
                {
                    typeOptions.WithGroupProperty(nameof(Post.BlogId));
                })
            );

        modelBuilder.Entity<Blog>()
            .ToTable("Blogs")
            .HasKey(b => b.BlogId);

        modelBuilder.Entity<Post>()
            .ToTable("Posts")
            .HasKey(p => p.PostId);

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        var addedEntities = ChangeTracker
            .Entries()
            .Where(e => e.State == EntityState.Added)
            .ToArray();

        var hasAdded = addedEntities.Any();
        IDbContextTransaction transaction = null;

        if (hasAdded)
            transaction = Database.BeginTransaction();

        this.EnsureAutoHistory(currentUserName);
        int changes = base.SaveChanges();

        if (hasAdded)
        {
            this.EnsureAddedHistory(addedEntities, currentUserName);
            changes += base.SaveChanges();

            transaction.Commit();
        }

        return changes;
    }
}
