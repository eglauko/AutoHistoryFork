using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;

namespace Microsoft.EntityFrameworkCore;

internal class AutoHistoryBuilder<TDbContext>
    where TDbContext : DbContext
{
    private static readonly AutoHistoryBuilder<TDbContext> dbContextBuilder = new();

    internal static AutoHistoryBuilder<TDbContext> Instance 
    { 
        [MethodImpl(MethodImplOptions.AggressiveInlining)] get => dbContextBuilder; 
    }

    private readonly AutoHistoryOptions options;
    private readonly ConcurrentDictionary<Type, Tuple<bool, string[]>> cache = new();
    private readonly ConcurrentDictionary<Type, IReadOnlyList<IProperty>> keysCache = new();
    private readonly Func<Type, Tuple<bool, string[]>> valueFactory;

    public AutoHistoryBuilder()
    {
        options = AutoHistoryOptions.GetOptions<TDbContext>();
        valueFactory = CreateCache;

        DefaultHistoryFactory = () => new AutoHistory()
        {
            ApplicationName = options.ApplicationName,
            Created = options.DateTimeFactory(),
        };
    }

    internal Func<AutoHistory> DefaultHistoryFactory { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; }

    private Tuple<bool, string[]> CreateCache(Type key)
    {
        // Get the ExcludeFromHistoryAttribute attribute for the entity type.
        bool exclude = key.GetCustomAttributes(typeof(ExcludeFromHistoryAttribute), true).Length is not 0;

        // Get the mapped properties for the entity type.
        // (include shadow properties, not include navigations & references)
        var excludedProperties = key.GetProperties()
                .Where(static p => p.GetCustomAttributes(typeof(ExcludeFromHistoryAttribute), true).Length is not 0)
                .Select(p => p.Name)
                .ToArray();

        var typesOptions = options.TypesOptions;

        if (typesOptions.TryGetValue(key, out var typeOptions))
        {
            if (!exclude)
                exclude = typeOptions.ExcludeFromHistory;

            if (typeOptions.ExcludeProperties?.Length > 0)
            {
                var list = new List<string>(excludedProperties);
                foreach (var prop in typeOptions.ExcludeProperties.Where(p => !list.Contains(p)))
                    list.Add(prop);

                excludedProperties = list.ToArray();
            }
        }

        return new Tuple<bool, string[]>(exclude, [.. excludedProperties]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsEntityExcluded(EntityEntry entry)
        => cache.GetOrAdd(entry.Metadata.ClrType, valueFactory).Item1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IEnumerable<PropertyEntry> GetPropertiesWithoutExcluded(EntityEntry entry)
    {
        var excludedProperties = cache.GetOrAdd(entry.Metadata.ClrType, valueFactory).Item2;
        return entry.Properties.Where(f => !excludedProperties.Contains(f.Metadata.Name));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool UseGroupId() => options.UseGroupId;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetGroupId(AutoHistory history, EntityEntry entry)
    {
        var type = entry.Metadata.ClrType;
        if (type is null
            || !options.TypesOptions.TryGetValue(type, out var typeOptions)
            || typeOptions.GroupProperty is not string groupProperty)
            return;

        var groupIdProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == groupProperty);
        history.GroupId = groupIdProperty?.CurrentValue?.ToString();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string PrimaryKey(EntityEntry entry)
    {
        var keys = keysCache.GetOrAdd(entry.Metadata.ClrType, type => entry.Metadata.FindPrimaryKey()!.Properties);

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
}
