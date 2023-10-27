using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace Microsoft.EntityFrameworkCore.AutoHistoryTests.Contexts;

public class HistoryContext : DbContext
{
    private readonly SqliteConnection connection;
    private readonly string currentUserName;

    public HistoryContext(SqliteConnection connection, IUserCredentials currentUser)
    {
        this.connection = connection;
        currentUserName = currentUser.GetUserName();
    }

    public DbSet<Product> Products { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
            return;

        optionsBuilder.UseSqlite(connection);

        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.EnableAutoHistory("EntityFramework.AutoHistoryFork.Tests");

        modelBuilder.Entity<Product>()
            .ToTable("Products")
            .HasKey(p => p.Id);

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

    public static Product[] CreateProducts()
    {
        var products = new Product[22];

        var nomes = new string[]
        {
            "MUG CH0478 ANIMALS 330ML",
            "MUG CH0422 GIRAFFE 330ML",
            "HAPPINESS MUG CH0478 330ML",
            "DREAM JOURNAL J047C 14X21CM",
            "SUGAR JAR B042B SUCAR 1700ML",
            "COFFEE JAR B042C 1200ML",
            "ZOOBLES LJB11115 DROP PLAY",
            "HIGH CETIN STRETCH TSP 7737",
            "COUPLE GAME 140X190X30 SPICES",
            "JERSEY GLOSS MLP 8280 100% POLYESTER",
            "T-SHIRT 1731130056 1/3 BIG CITY MALE.",
            "BOLERO KHS12/08B 4/10 FEMALE",
            "FREEDOM DRESS 15209955 PINK",
            "CARS PAINTING KIT LJE8026",
            "MOOH DOUGH PLAYDOUGH LJB10106",
            "SHORTS 0631130054 1/3 BIG CITY MALE.",
            "ADVENTURE SNEAKERS PERSONALIZED",
            "SEDUCTION LIPSTICK 4G",
            "SPEED SNEAKERS 40 BLACK",
            "TEACHERS DAY MUG CH0378 330ML",
            "WINTER JACKET 4581424 CASUAL MALE",
            "STUDENTS NOTEBOOK 10X1 96F",
        };

        var random = new Random(0);

        for (int i = 0; i < 22; i++)
        {
            var price = new decimal((random.NextDouble() + 1) * (random.NextDouble() * 3) * 10 + 10);
            products[i] = new Product()
            {
                Name = nomes[i],
                Price = price
            };
        }

        return products;
    }
}

/// <summary>
/// Current authenticated user.
/// </summary>
public interface IUserCredentials
{
    /// <summary>
    /// The claim type for the user name.
    /// </summary>
    string UserNameClaimType { get; }

    /// <summary>
    /// The claim type for the user role.
    /// </summary>
    string RoleClaimType { get; }

    /// <summary>
    /// User Claims.
    /// </summary>
    IEnumerable<Claim> Claims { get; }

    /// <summary>
    /// The user roles.
    /// </summary>
    IEnumerable<string> GetRoles() => Claims.Where(c => c.Type == RoleClaimType).Select(c => c.Value);

    /// <summary>
    /// The user name.
    /// </summary>
    string GetUserName() => Claims.FirstOrDefault(c => c.Type == UserNameClaimType)?.Value;
}

public class Product
{
    public int Id { get; private set; }

    public string Name { get; set; }

    public decimal Price { get; set; }
}