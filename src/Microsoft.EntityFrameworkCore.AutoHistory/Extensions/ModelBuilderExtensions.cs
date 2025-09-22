using System.Reflection;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Represents a plugin for Microsoft.EntityFrameworkCore to support automatically recording data changes history.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Enables the automatic recording change history.
    /// </summary>
    /// <typeparam name="TDbContext">The type of the database context.</typeparam>
    /// <param name="modelBuilder">The <see cref="ModelBuilder"/> to enable auto history feature.</param>
    /// <param name="applicationName">The name of the application that made the change. Default: EntryAssemblyName.</param>
    /// <param name="changedMaxLength">The maximum length of the 'Changed' column. <c>null</c> will use default setting 2048.</param>
    /// <param name="limitChangedLength">The value indicating whether limit the length of the 'Changed' column. Default: false.</param>
    /// <param name="configure">The action to configure the auto history options.</param>
    /// <returns>The <see cref="ModelBuilder"/> had enabled auto history feature.</returns>
    public static ModelBuilder EnableAutoHistory<TDbContext>(this ModelBuilder modelBuilder, 
        string? applicationName = null,
        int? changedMaxLength = null, 
        bool? limitChangedLength = null,
        Action<AutoHistoryOptions>? configure = null)
        where TDbContext : DbContext
    {
        return EnableAutoHistory<TDbContext, AutoHistory>(modelBuilder, o =>
        {
            if (!string.IsNullOrWhiteSpace(applicationName))
                o.ApplicationName = applicationName;
            if (changedMaxLength.HasValue)
                o.ChangedMaxLength = changedMaxLength.Value;
            if (limitChangedLength.HasValue)
                o.LimitChangedLength = limitChangedLength.Value;

            configure?.Invoke(o);
        });
    }

    /// <summary>
    /// Enables the automatic recording change history.
    /// </summary>
    /// <typeparam name="TDbContext">The type of the database context.</typeparam>
    /// <param name="modelBuilder">The <see cref="ModelBuilder"/> to enable auto history feature.</param>
    /// <param name="configure">The action to configure the auto history options.</param>
    /// <returns>The <see cref="ModelBuilder"/> had enabled auto history feature.</returns>
    public static ModelBuilder EnableAutoHistory<TDbContext>(this ModelBuilder modelBuilder, Action<AutoHistoryOptions> configure)
        where TDbContext : DbContext
    {
        return EnableAutoHistory<TDbContext, AutoHistory>(modelBuilder, configure);
    }

    /// <summary>
    /// Enables the automatic recording change history.
    /// </summary>
    /// <typeparam name="TDbContext">The type of the database context.</typeparam>
    /// <typeparam name="TAutoHistory">The type of the auto history.</typeparam>
    /// <param name="modelBuilder">The <see cref="ModelBuilder"/> to enable auto history feature.</param>
    /// <param name="configure">The action to configure the auto history options.</param>
    /// <returns>The <see cref="ModelBuilder"/> had enabled auto history feature.</returns>
    public static ModelBuilder EnableAutoHistory<TDbContext, TAutoHistory>(this ModelBuilder modelBuilder, Action<AutoHistoryOptions> configure)
        where TDbContext : DbContext
        where TAutoHistory : AutoHistory
    {
        var options = AutoHistoryOptions.GetOptions<TDbContext>();
        configure?.Invoke(options);

        // Set default application name if not set
        if (string.IsNullOrWhiteSpace(options.ApplicationName))
            options.ApplicationName = Assembly.GetEntryAssembly()?.GetName().Name;

        modelBuilder.Entity<TAutoHistory>(b =>
        {
            b.Property(c => c.RowId).IsRequired().HasMaxLength(options.RowIdMaxLength);
            b.Property(c => c.TableName).IsRequired().HasMaxLength(options.TableMaxLength);
            b.Property(c => c.UserName).HasMaxLength(options.UserNameMaxLength);

            if (options.MapApplicationName)
                b.Property(c => c.ApplicationName).IsRequired().HasMaxLength(options.ApplicationNameMaxLength);
            else
                b.Ignore(c => c.ApplicationName);

            if (options.UseGroupId)
                b.Property(c => c.GroupId).HasMaxLength(options.GroupIdMaxLength);
            else
                b.Ignore(c => c.GroupId);

            if (options.LimitChangedLength)
            {
                var max = options.ChangedMaxLength ?? AutoHistory.Defaults.ChangedMaxLength;
                if (max <= 0)
                    max = AutoHistory.Defaults.ChangedMaxLength;

                b.Property(c => c.Changed).HasMaxLength(max);
            }
        });

        return modelBuilder;
    }
}
