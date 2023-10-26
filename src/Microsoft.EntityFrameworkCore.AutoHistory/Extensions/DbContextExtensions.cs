using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;

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

    internal static TAutoHistory AutoHistory<TAutoHistory>(this EntityEntry entry, Func<TAutoHistory> createHistoryFactory, string userName)
        where TAutoHistory : AutoHistory
    {
        if (IsEntityExcluded(entry))
        {
            return null;
        }

        var properties = GetPropertiesWithoutExcluded(entry);
        if (entry.State == EntityState.Modified && !properties.Any(p => p.IsModified))
        {
            return null;
        }

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

    private static bool IsEntityExcluded(EntityEntry entry) =>
        entry.Metadata.ClrType.GetCustomAttributes(typeof(ExcludeFromHistoryAttribute), true).Any();

    private static IEnumerable<PropertyEntry> GetPropertiesWithoutExcluded(EntityEntry entry)
    {
        // Get the mapped properties for the entity type.
        // (include shadow properties, not include navigations & references)
        var excludedProperties = entry.Metadata.ClrType.GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(ExcludeFromHistoryAttribute), true).Any())
                .Select(p => p.Name);

        var properties = entry.Properties.Where(f => !excludedProperties.Contains(f.Metadata.Name));
        return properties;
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
        {
            return null;
        }

        var properties = GetPropertiesWithoutExcluded(entry);

        dynamic json = new System.Dynamic.ExpandoObject();
        foreach (var prop in properties)
        {
            ((IDictionary<string, object>)json)[prop.Metadata.Name] = prop.OriginalValue ?? null;
        }
        var history = createHistoryFactory();
        history.TableName = entry.Metadata.GetTableName();
        history.UserName = userName;
        history.RowId = entry.PrimaryKey();
        history.Kind = EntityState.Added;
        history.Changed = JsonSerializer.Serialize(json, AutoHistoryOptions.Instance.JsonSerializerOptions);
        return history;
    }

    private static string PrimaryKey(this EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();

        var values = new List<object>();
        foreach (var property in key.Properties)
        {
            var value = entry.Property(property.Name).CurrentValue;
            if (value != null)
            {
                values.Add(value);
            }
        }

        return string.Join(",", values);
    }

    private static void WriteHistoryModifiedState(AutoHistory history, EntityEntry entry, IEnumerable<PropertyEntry> properties)
    {
        dynamic json = new System.Dynamic.ExpandoObject();
        dynamic bef = new System.Dynamic.ExpandoObject();
        dynamic aft = new System.Dynamic.ExpandoObject();

        PropertyValues databaseValues = null;
        foreach (var prop in properties)
        {
            if (prop.IsModified)
            {
                if (prop.OriginalValue != null)
                {
                    if (!prop.OriginalValue.Equals(prop.CurrentValue))
                    {
                        ((IDictionary<string, object>)bef)[prop.Metadata.Name] = prop.OriginalValue;
                    }
                    else
                    {
                        databaseValues ??= entry.GetDatabaseValues();
                        var originalValue = databaseValues.GetValue<object>(prop.Metadata.Name);
                        ((IDictionary<string, object>)bef)[prop.Metadata.Name] = originalValue;
                    }
                }
                else
                {
                    ((IDictionary<string, object>)bef)[prop.Metadata.Name] = null;
                }

                ((IDictionary<string, object>)aft)[prop.Metadata.Name] = prop.CurrentValue;
            }
        }

        ((IDictionary<string, object>)json)["before"] = bef;
        ((IDictionary<string, object>)json)["after"] = aft;

        history.RowId = entry.PrimaryKey();
        history.Kind = EntityState.Modified;
        history.Changed = JsonSerializer.Serialize(json, AutoHistoryOptions.Instance.JsonSerializerOptions);
    }

    private static void WriteHistoryDeletedState(AutoHistory history, EntityEntry entry, IEnumerable<PropertyEntry> properties)
    {
        dynamic json = new System.Dynamic.ExpandoObject();

        foreach (var prop in properties)
        {
            ((IDictionary<string, object>)json)[prop.Metadata.Name] = prop.OriginalValue;
        }
        history.RowId = entry.PrimaryKey();
        history.Kind = EntityState.Deleted;
        history.Changed = JsonSerializer.Serialize(json, AutoHistoryOptions.Instance.JsonSerializerOptions);
    }
}
