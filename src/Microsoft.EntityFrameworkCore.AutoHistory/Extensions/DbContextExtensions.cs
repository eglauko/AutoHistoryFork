using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Runtime.CompilerServices;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Represents a plugin for Microsoft.EntityFrameworkCore to support automatically recording data changes history.
/// </summary>
public static class DbContextExtensions
{
    /// <summary>
    /// Ensures the automatic history.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="userName">The user name that make the change.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnsureAutoHistory<TDbContext>(this TDbContext context, string? userName = null)
        where TDbContext : DbContext
    {
        var builder = AutoHistoryBuilder<TDbContext>.Instance;
        var createHistoryFactory = builder.DefaultHistoryFactory;
        InternalEnsureAutoHistory(context, context, builder, createHistoryFactory, userName);
    }

    /// <summary>
    /// Ensures the automatic history and add the history to the specified history context.
    /// </summary>
    /// <param name="context">The context that contains the changed entities.</param>
    /// <param name="historyContext">The context where the history will be added.</param>
    /// <param name="userName">The user name that make the change.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnsureAutoHistory<TDbContext>(this DbContext context, TDbContext historyContext, string? userName = null)
        where TDbContext : DbContext
    {
        var builder = AutoHistoryBuilder<TDbContext>.Instance;
        var createHistoryFactory = builder.DefaultHistoryFactory;
        InternalEnsureAutoHistory(context, historyContext, builder, createHistoryFactory, userName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnsureAutoHistory<TDbContext, TAutoHistory>(this TDbContext context, 
        Func<TAutoHistory> createHistoryFactory, 
        string? userName = null)
        where TDbContext : DbContext
        where TAutoHistory : AutoHistory
    {
        var builder = AutoHistoryBuilder<TDbContext>.Instance;
        InternalEnsureAutoHistory(context, context, builder, createHistoryFactory, userName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnsureAutoHistory<TDbContext, TAutoHistory>(
        this DbContext context,
        TDbContext historyContext,
        Func<TAutoHistory> createHistoryFactory,
        string? userName = null)
        where TDbContext : DbContext
        where TAutoHistory : AutoHistory
    {
        var builder = AutoHistoryBuilder<TDbContext>.Instance;
        InternalEnsureAutoHistory(context, historyContext, builder, createHistoryFactory, userName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void InternalEnsureAutoHistory<TDbContext, TAutoHistory>(
        DbContext context,
        TDbContext historyContext,
        AutoHistoryBuilder<TDbContext> builder,
        Func<TAutoHistory> createHistoryFactory, 
        string? userName = null)
        where TDbContext : DbContext
        where TAutoHistory : AutoHistory
    {
        // Must ToArray() here for excluding the AutoHistory model.
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted)
            .ToArray();

        foreach (var entry in entries)
        {
            var autoHistory = entry.AutoHistory(builder, createHistoryFactory, userName);
            if (autoHistory != null)
            {
                historyContext.Add(autoHistory);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TAutoHistory? AutoHistory<TDbContext, TAutoHistory>(
        this EntityEntry entry,
        AutoHistoryBuilder<TDbContext> builder,
        Func<TAutoHistory> createHistoryFactory, 
        string? userName = null)
        where TDbContext : DbContext
        where TAutoHistory : AutoHistory
    {
        if (builder.IsEntityExcluded(entry))
            return null;

        var properties = builder.GetPropertiesWithoutExcluded(entry);
        if (entry.State == EntityState.Modified && !properties.Any(p => p.IsModified))
            return null;

        var history = createHistoryFactory();
        history.TableName = entry.Metadata.GetTableName();
        history.UserName = userName;

        if (builder.UseGroupId())
            builder.SetGroupId(history, entry);

        history.RowId = builder.PrimaryKey(entry);

        switch (entry.State)
        {
            case EntityState.Modified:
                WriteHistoryModifiedState(history, entry, properties);
                break;
            case EntityState.Deleted:
                WriteHistoryDeletedState(history, entry, properties);
                break;
            default:
                throw new NotSupportedException("AutoHistory only support Deleted and Modified entity.");
        }

        return history;
    }

    /// <summary>
    /// Ensures the history for added entries.
    /// </summary>
    /// <param name="historyContext"></param>
    /// <param name="addedEntries"></param>
    /// <param name="userName"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnsureAddedHistory<TDbContext>(
        this TDbContext historyContext,
        EntityEntry[] addedEntries,
        string? userName = null)
        where TDbContext : DbContext
    {
        var builder = AutoHistoryBuilder<TDbContext>.Instance;
        var defaultHistoryFactory = builder.DefaultHistoryFactory;
        historyContext.InternalEnsureAddedHistory(builder, defaultHistoryFactory, addedEntries, userName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnsureAddedHistory<TDbContext, TAutoHistory>(
        this DbContext historyContext,
        Func<TAutoHistory> createHistoryFactory,
        EntityEntry[] addedEntries,
        string? userName = null)
        where TDbContext : DbContext
        where TAutoHistory : AutoHistory
    {
        var builder = AutoHistoryBuilder<TDbContext>.Instance;
        historyContext.InternalEnsureAddedHistory(builder, createHistoryFactory, addedEntries, userName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void InternalEnsureAddedHistory<TDbContext, TAutoHistory>(
        this DbContext historyContext,
        AutoHistoryBuilder<TDbContext> builder,
        Func<TAutoHistory> createHistoryFactory,
        EntityEntry[] addedEntries,
        string? userName = null)
        where TDbContext : DbContext
        where TAutoHistory : AutoHistory
    {
        foreach (var entry in addedEntries)
        {
            var autoHistory = entry.AddedHistory(builder, createHistoryFactory, userName);
            if (autoHistory != null)
            {
                historyContext.Add(autoHistory);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TAutoHistory? AddedHistory<TDbContext, TAutoHistory>(
        this EntityEntry entry,
        AutoHistoryBuilder<TDbContext> builder,
        Func<TAutoHistory> createHistoryFactory,
        string? userName)
        where TDbContext : DbContext
        where TAutoHistory : AutoHistory
    {
        if (builder.IsEntityExcluded(entry))
            return null;

        var properties = builder.GetPropertiesWithoutExcluded(entry);

        var changes = new ChangedHistory();
        foreach (var prop in properties)
        {
            changes[prop.Metadata.Name] = [prop.OriginalValue?.ToString()!];
        }

        var history = createHistoryFactory();
        history.TableName = entry.Metadata.GetTableName();
        history.UserName = userName;
        history.RowId = builder.PrimaryKey(entry);
        history.Kind = EntityState.Added;
        history.Changed = changes.Serialize();

        if (builder.UseGroupId())
            builder.SetGroupId(history, entry);

        return history;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteHistoryModifiedState(AutoHistory history, EntityEntry entry, IEnumerable<PropertyEntry> properties)
    {
        var changes = new ChangedHistory();

        PropertyValues? databaseValues = null;
        foreach (var prop in properties)
        {
            if (!prop.IsModified)
                continue;

            var values = new string[2];

            changes[prop.Metadata.Name] = values;

            if (Equals(prop.OriginalValue, prop.CurrentValue))
            {
                databaseValues ??= entry.GetDatabaseValues()!;
                values[0] = databaseValues.GetValue<object>(prop.Metadata.Name)?.ToString()!;
            }
            else
            {
                values[0] = prop.OriginalValue?.ToString()!;
            }

            values[1] = prop.CurrentValue?.ToString()!;
        }

        history.Kind = EntityState.Modified;
        history.Changed = changes.Serialize();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteHistoryDeletedState(AutoHistory history, EntityEntry entry, IEnumerable<PropertyEntry> properties)
    {
        var changes = new ChangedHistory();

        foreach (var prop in properties)
        {
            changes[prop.Metadata.Name] = [prop.OriginalValue?.ToString()!];
        }

        history.Kind = EntityState.Deleted;
        history.Changed = changes.Serialize();
    }
}
