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