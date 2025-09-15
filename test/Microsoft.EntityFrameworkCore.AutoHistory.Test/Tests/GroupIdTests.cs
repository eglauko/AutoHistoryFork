using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.AutoHistoryTests.Contexts;
using Xunit;
using System.Linq;
using System.Collections.Generic;

namespace Microsoft.EntityFrameworkCore.AutoHistoryTests.Tests;

public class GroupIdTests
{
    private static GroupDbContext CreateContext()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var db = new GroupDbContext(conn, new CurrentUser());
        db.Database.EnsureCreated();
        db.Database.Migrate();

        return db;
    }

    [Fact]
    public void AddedEntities_Should_Have_GroupId_Set_On_History()
    {
        using var db = CreateContext();

        var blog = new Blog
        {
            Url = "http://groupid/add-blog",
            Posts = new List<Post>
            {
                new Post { Title = "Post 1", Content = "Content 1" },
                new Post { Title = "Post 2", Content = "Content 2" }
            }
        };

        db.Blogs.Add(blog);
        var changes = db.SaveChanges(); // Will create Added history for Blog + Posts

        // Blog + 2 posts = 3 entities added -> 3 Added history rows
        // Each Added history row should have GroupId = BlogId (configured group property)
        var histories = db.Set<AutoHistory>().Where(h => h.TableName == "Blogs" || h.TableName == "Posts").ToList();
        Assert.Equal(3, histories.Count);
        Assert.All(histories, h => Assert.Equal(blog.BlogId.ToString(), h.GroupId));
    }

    [Fact]
    public void ModifiedEntity_Should_Preserve_GroupId_On_History()
    {
        using var db = CreateContext();

        var blog = new Blog
        {
            Url = "http://groupid/modify-blog",
            Posts = new List<Post>
            {
                new Post { Title = "Post 1", Content = "Initial" }
            }
        };

        db.Blogs.Add(blog);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        // Load and modify post
        var loadedPost = db.Posts.First();
        loadedPost.Content = "Updated content";
        var changes = db.SaveChanges(); // Modified history for Post

        var postHistory = db.Set<AutoHistory>()
            .Where(h => h.TableName == "Posts" && h.Kind == EntityState.Modified)
            .Single();

        Assert.Equal(blog.BlogId.ToString(), postHistory.GroupId);
    }

    [Fact]
    public void DeletedEntity_Should_Have_GroupId_On_History()
    {
        using var db = CreateContext();

        var blog = new Blog
        {
            Url = "http://groupid/delete-blog",
            Posts = new List<Post>
            {
                new Post { Title = "Post 1", Content = "Content" }
            }
        };

        db.Blogs.Add(blog);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        var existing = db.Blogs.Include(b => b.Posts).First();
        db.Remove(existing); // Cascade delete posts (if configured via FK; Post has BlogId)
        db.SaveChanges();    // Deleted history for Blog and Post

        var deletedHistories = db.Set<AutoHistory>()
            .Where(h => (h.TableName == "Blogs" || h.TableName == "Posts") && h.Kind == EntityState.Deleted)
            .ToList();

        // Expect 2 deleted histories (blog + single post)
        Assert.Equal(2, deletedHistories.Count);
        Assert.All(deletedHistories, h => Assert.Equal(blog.BlogId.ToString(), h.GroupId));
    }

    [Fact]
    public void UnchangedEntity_Should_Not_Create_Modified_History()
    {
        using var db = CreateContext();

        var blog = new Blog
        {
            Url = "http://groupid/unchanged-blog"
        };

        db.Blogs.Add(blog);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        // Attach and save without modifications
        var existing = db.Blogs.First();
        db.SaveChanges();

        // Only Added history expected (no Modified)
        var histories = db.Set<AutoHistory>()
            .Where(h => h.TableName == "Blogs")
            .ToList();

        // Exactly 1 (Added) history
        Assert.Single(histories);
        Assert.Equal(blog.BlogId.ToString(), histories[0].GroupId);
        Assert.Equal(EntityState.Added, histories[0].Kind);
    }

    [Fact]
    public void MultiplePosts_Modified_Should_Share_Same_GroupId()
    {
        using var db = CreateContext();

        var blog = new Blog
        {
            Url = "http://groupid/multi-mod",
            Posts = new List<Post>
            {
                new Post { Title = "P1", Content = "C1" },
                new Post { Title = "P2", Content = "C2" }
            }
        };

        db.Blogs.Add(blog);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        var posts = db.Posts.OrderBy(p => p.PostId).ToList();
        posts[0].Content = "C1-Updated";
        posts[1].Title = "P2-Updated";
        db.SaveChanges();

        var modifiedHistories = db.Set<AutoHistory>()
            .Where(h => h.TableName == "Posts" && h.Kind == EntityState.Modified)
            .ToList();

        Assert.Equal(2, modifiedHistories.Count);
        Assert.All(modifiedHistories, h => Assert.Equal(blog.BlogId.ToString(), h.GroupId));
    }
}
