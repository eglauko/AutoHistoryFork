namespace Microsoft.EntityFrameworkCore.AutoHistoryTests.Contexts;

public class GenericBloggingContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseInMemoryDatabase("test");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.EnableAutoHistory<GenericBloggingContext, CustomAutoHistory>(o => { });
    }
}
