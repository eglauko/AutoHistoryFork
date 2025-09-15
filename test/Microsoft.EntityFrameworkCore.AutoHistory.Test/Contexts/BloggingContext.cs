using System.Collections.Generic;

namespace Microsoft.EntityFrameworkCore.AutoHistoryTests.Contexts;


public class BloggingContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }
    public DbSet<NotTracked> NotTracked { get; set; }
    public DbSet<NotTracked2> NotTracked2 { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseInMemoryDatabase("test");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.EnableAutoHistory(
            configure: options => options
                .ConfigureType<Blog>(typeOptions =>
                {
                    typeOptions.WithExcludeProperty(b => b.ExcludedProperty);
                })
                .ConfigureType<NotTracked2>(typeOptions =>
                {
                    typeOptions.WithExcludeFromHistory();
                }));
    }
}

class CustomAutoHistory : AutoHistoryTestHandle
{
    public string CustomField { get; set; }
}

public class Blog
{
    public int BlogId { get; set; }
    public string Url { get; set; }

    [ExcludeFromHistory]
    public string PrivateURL { get; set; }

    public string ExcludedProperty { get; set; }

    public List<Post> Posts { get; set; }
}

public class Post
{
    public int PostId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public int? NumViews { get; set; } = null;
    public int BlogId { get; set; }
    public Blog Blog { get; set; }
}

[ExcludeFromHistory]
public class NotTracked
{
    public int NotTrackedId { get; set; }
    public string Title { get; set; }
}

public class NotTracked2
{
    public int NotTracked2Id { get; set; }
    public string Title { get; set; }
}