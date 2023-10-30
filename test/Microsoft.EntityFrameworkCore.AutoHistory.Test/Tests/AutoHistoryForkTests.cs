using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.AutoHistoryTests.Contexts;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Xunit;

namespace Microsoft.EntityFrameworkCore.AutoHistoryTests.Tests;

public class AutoHistoryForkTests
{
    private static HistoryContext CreateContext()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var db = new HistoryContext(conn, new CurrentUser());
        db.Database.EnsureCreated();
        db.Database.Migrate();

        return db;
    }

    [Fact]
    public void Should_Added_AutoHistory()
    {
        var db = CreateContext();

        var products = HistoryContext.CreateProducts();
        db.Products.AddRange(products);

        var changes = db.SaveChanges();
        db.ChangeTracker.Clear();
        Assert.Equal(44, changes);

        var history = db.Set<AutoHistory>().ToList();
        Assert.Equal(22, history.Count);

        var appName = history[0].ApplicationName;
        Assert.Equal("EntityFramework.AutoHistoryFork.Tests", appName);

        var userName = history[0].UserName;
        Assert.Equal("AutoHistoryForkTests", userName);
    }

    [Fact]
    public void Should_WriteComposedKey()
    {
        var db = CreateContext();

        var salesItem = new SalesItem()
        {
            SaleId = 1,
            ProductId = 1,
            Quantity = 1,
            Price = 1M
        };

        db.SalesItem.Add(salesItem);
        var changes = db.SaveChanges();
        db.ChangeTracker.Clear();

        Assert.Equal(2, changes);

        var history = db.Set<AutoHistory>().ToList();
        Assert.Single(history);

        var keys = history[0].RowId;
        Assert.Equal("1,1", keys);
    }

    [Fact]
    public void Should_WriteChangedHistory()
    {
        var db = CreateContext();

        var product = new Product()
        {
            Name = "Product 1",
            Price = 1M
        };

        db.Products.Add(product);
        var changes = db.SaveChanges();

        Assert.Equal(2, changes);

        product.Price = 2M;
        changes = db.SaveChanges();

        Assert.Equal(2, changes);

        var history = db.Set<AutoHistory>().ToList();
        Assert.Equal(2, history.Count);

        var changed = history[1].Changed;
        var chagedHistory = ChangedHistory.Deserialize(changed);
        Assert.Single(chagedHistory);
        Assert.Equal("1", chagedHistory["Price"][0]);
        Assert.Equal("2", chagedHistory["Price"][1]);
    }

    [Fact]
    public void Should_ExcludeExcludedPropertyFromHistory()
    {
        using var db = new BloggingContext();
        var blog = new Blog
        {
            Url = "http://blogs.msdn.com/adonet",
            Posts = new List<Post> {
                    new Post {
                        Title = "xUnit",
                        Content = "Post from xUnit test."
                    }
                },
            PrivateURL = "http://www.secret.com"
        };

        db.Add(blog);
        db.EnsureAutoHistory();
        db.SaveChanges();

        blog.PrivateURL = "http://new.secret.com";
        db.EnsureAutoHistory();
        db.SaveChanges();

        var history = db.Set<AutoHistory>().ToList();
        Assert.Empty(history);

        blog.PrivateURL = "http://newer.secret.com";
        blog.Url = "http://blogs.msdn.com/adonet-news";
        db.EnsureAutoHistory();
        db.SaveChanges();

        history = db.Set<AutoHistory>().ToList();
        Assert.Single(history);

        var changed = ChangedHistory.Deserialize(history[0].Changed);
        Assert.Single(changed);
        Assert.False(changed.ContainsKey("PrivateURL"));
    }
}

public class CurrentUser : IUserCredentials
{
    public string UserNameClaimType => ClaimTypes.Name;

    public string RoleClaimType => ClaimTypes.Role;

    public IEnumerable<Claim> Claims { get; } = new List<Claim>
    {
        new Claim(ClaimTypes.Name, "AutoHistoryForkTests"),
        new Claim(ClaimTypes.Role, "Admin")
    };
}