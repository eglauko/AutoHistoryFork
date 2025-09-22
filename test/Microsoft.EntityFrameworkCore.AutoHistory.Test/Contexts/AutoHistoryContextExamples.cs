using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.AutoHistoryTests.Contexts;

public sealed class AutoHistoryContext : AutoHistoryContextBase
{
    public AutoHistoryContext(DbContextOptions<AutoHistoryContext> options, ILogger<AutoHistoryContext> logger)
        :base(options, logger, "MyApplicationName")
    {
    }
}

public abstract class AutoHistoryContextBase : DbContext
{
    private readonly ILogger logger;
    private readonly string? applicationName;

    protected AutoHistoryContextBase(DbContextOptions options, ILogger logger, string? applicationName = null)
        :base(options)
    {
        this.logger = logger;
        this.applicationName = applicationName;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.EnableAutoHistory<AutoHistoryContextBase>(applicationName);

        base.OnModelCreating(modelBuilder);
    }

    public int TrySaveChanges()
    {
        try
        {
            return SaveChanges();
        }
        catch(Exception ex) 
        {
            logger.LogError("An error ocurred while saving history changes", ex);
            return 0;
        }
    }

    public async Task<int> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError("An error ocurred while saving history changes", ex);
            return 0;
        }
    }
}


public class MyApplicationDbContext : DbContext
{
    private readonly string currentUserName;
    private readonly AutoHistoryContext historyContext;

    public MyApplicationDbContext(IUserCredentials currentUser, AutoHistoryContext historyContext)
    {
        currentUserName = currentUser.GetUserName();
        this.historyContext = historyContext;
    }

    public override int SaveChanges()
    {
        var addedEntities = ChangeTracker
            .Entries()
            .Where(e => e.State == EntityState.Added)
            .ToArray();

        this.EnsureAutoHistory(historyContext, currentUserName);

        int changes = base.SaveChanges();

        historyContext.EnsureAddedHistory(addedEntities, currentUserName);
        historyContext.TrySaveChanges();
        
        return changes;
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        var addedEntities = ChangeTracker
            .Entries()
            .Where(e => e.State == EntityState.Added)
            .ToArray();

        this.EnsureAutoHistory(historyContext, currentUserName);

        int changes = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        
        historyContext.EnsureAddedHistory(addedEntities, currentUserName);
        await historyContext.TrySaveChangesAsync(cancellationToken);

        return changes;
    }
}