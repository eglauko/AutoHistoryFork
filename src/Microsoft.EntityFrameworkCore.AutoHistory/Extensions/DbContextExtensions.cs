using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Represents a plugin for Microsoft.EntityFrameworkCore to support automatically recording data changes history.
/// </summary>
public static class DbContextExtensions
{
    private static readonly Func<AutoHistory> DefaultHistoryFactory = () => new AutoHistory()
    {
        ApplicationName = AutoHistoryOptions.Instance.ApplicationName,
        Created = AutoHistoryOptions.Instance.DateTimeFactory(),
    };

    private static readonly ConcurrentDictionary<Type, Tuple<bool, string[]>> cache = new();
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<IProperty>> keysCache = new();

    private static readonly Func<Type, Tuple<bool, string[]>> valueFactory = CreateCache;

    private static Tuple<bool, string[]> CreateCache(Type key)
    {
        // Get the ExcludeFromHistoryAttribute attribute for the entity type.
        bool exclude = key.GetCustomAttributes(typeof(ExcludeFromHistoryAttribute), true).Any();

        // Get the mapped properties for the entity type.
        // (include shadow properties, not include navigations & references)
        var excludedProperties = key.GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(ExcludeFromHistoryAttribute), true).Any())
                .Select(p => p.Name)
                .ToArray();

        return new Tuple<bool, string[]>(exclude, excludedProperties.ToArray());
    }

    /// <summary>
    /// Ensures the automatic history.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="userName">The user name that make the change.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnsureAutoHistory(this DbContext context, string userName = null)
    {
        EnsureAutoHistory(context, DefaultHistoryFactory, userName);
    }

    public static void EnsureAutoHistory<TAutoHistory>(this DbContext context, Func<TAutoHistory> createHistoryFactory, string userName = null)
        where TAutoHistory : AutoHistory
    {
        // Must ToArray() here for excluding the AutoHistory model.
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted)
            .ToArray();

        foreach (var entry in entries)
        {
            var autoHistory = entry.AutoHistory(createHistoryFactory, userName);
            if (autoHistory != null)
            {
                context.Add(autoHistory);
            }
        }
    }

    internal static TAutoHistory AutoHistory<TAutoHistory>(this EntityEntry entry, 
        Func<TAutoHistory> createHistoryFactory, 
        string userName)
        where TAutoHistory : AutoHistory
    {
        if (IsEntityExcluded(entry))
            return null;

        var properties = GetPropertiesWithoutExcluded(entry);
        if (entry.State == EntityState.Modified && !properties.Any(p => p.IsModified))
            return null;

        var history = createHistoryFactory();
        history.TableName = entry.Metadata.GetTableName();
        history.UserName = userName;
        
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

    private static bool IsEntityExcluded(EntityEntry entry)
        => cache.GetOrAdd(entry.Metadata.ClrType, valueFactory).Item1;
        

    private static IEnumerable<PropertyEntry> GetPropertiesWithoutExcluded(EntityEntry entry)
    {
        var excludedProperties = cache.GetOrAdd(entry.Metadata.ClrType, valueFactory).Item2;
        return entry.Properties.Where(f => !excludedProperties.Contains(f.Metadata.Name));
    }

    /// <summary>
    /// Ensures the history for added entries
    /// </summary>
    /// <param name="context"></param>
    /// <param name="addedEntries"></param>
    /// <param name="userName"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnsureAddedHistory(
        this DbContext context,
        EntityEntry[] addedEntries,
        string userName = null)
    {
        EnsureAddedHistory(context, DefaultHistoryFactory, addedEntries, userName);
    }

    public static void EnsureAddedHistory<TAutoHistory>(
        this DbContext context,
        Func<TAutoHistory> createHistoryFactory,
        EntityEntry[] addedEntries,
        string userName = null)
        where TAutoHistory : AutoHistory
    {
        foreach (var entry in addedEntries)
        {
            var autoHistory = entry.AddedHistory(createHistoryFactory, userName);
            if (autoHistory != null)
            {
                context.Add(autoHistory);
            }
        }
    }

    internal static TAutoHistory AddedHistory<TAutoHistory>(
        this EntityEntry entry,
        Func<TAutoHistory> createHistoryFactory,
        string userName)
        where TAutoHistory : AutoHistory
    {
        if (IsEntityExcluded(entry))
            return null;

        var properties = GetPropertiesWithoutExcluded(entry);

        var changes = new ChangedHistory();
        foreach (var prop in properties)
        {
            changes[prop.Metadata.Name] = new string[] { prop.OriginalValue?.ToString() };
        }

        var history = createHistoryFactory();
        history.TableName = entry.Metadata.GetTableName();
        history.UserName = userName;
        history.RowId = entry.PrimaryKey();
        history.Kind = EntityState.Added;
        history.Changed = changes.Serialize();
        return history;
    }

    private static string PrimaryKey(this EntityEntry entry)
    {
        var keys = keysCache.GetOrAdd(entry.Metadata.ClrType, type => entry.Metadata.FindPrimaryKey().Properties);

        if (keys.Count == 1)
            return entry.Property(keys[0].Name).CurrentValue?.ToString() ?? string.Empty;

        var sb = new StringBuilder();
        for (int i = 0; i < keys.Count; i++)
        {
            if (i > 0)
                sb.Append(',');
            sb.Append(entry.Property(keys[i].Name).CurrentValue?.ToString() ?? string.Empty);
        }

        return sb.ToString();
    }

    private static void WriteHistoryModifiedState(AutoHistory history, EntityEntry entry, IEnumerable<PropertyEntry> properties)
    {
        var changes = new ChangedHistory();

        PropertyValues databaseValues = null;
        foreach (var prop in properties)
        {
            if (!prop.IsModified)
                continue;

            var values = new string[2];

            changes[prop.Metadata.Name] = values;

            if (Equals(prop.OriginalValue, prop.CurrentValue))
            {
                databaseValues ??= entry.GetDatabaseValues();
                values[0] = databaseValues.GetValue<object>(prop.Metadata.Name)?.ToString();
            }
            else
            {
                values[0] = prop.OriginalValue?.ToString();
            }

            values[1] = prop.CurrentValue?.ToString();            
        }

        history.RowId = entry.PrimaryKey();
        history.Kind = EntityState.Modified;
        history.Changed = changes.Serialize();
    }

    private static void WriteHistoryDeletedState(AutoHistory history, EntityEntry entry, IEnumerable<PropertyEntry> properties)
    {
        var changes = new ChangedHistory();

        foreach (var prop in properties)
        {
            changes[prop.Metadata.Name] = new string[] { prop.OriginalValue?.ToString() };
        }

        history.RowId = entry.PrimaryKey();
        history.Kind = EntityState.Deleted;
        history.Changed = changes.Serialize();
    }
}
