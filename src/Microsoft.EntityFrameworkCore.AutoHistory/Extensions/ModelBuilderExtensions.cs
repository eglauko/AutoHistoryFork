using System;
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
    /// <param name="modelBuilder">The <see cref="ModelBuilder"/> to enable auto history feature.</param>
    /// <param name="applicationName">The name of the application that made the change. Default: EntryAssemblyName.</param>
    /// <param name="changedMaxLength">The maximum length of the 'Changed' column. <c>null</c> will use default setting 2048.</param>
    /// <param name="limitChangedLength">The value indicating whether limit the length of the 'Changed' column. Default: false.</param>
    /// <returns>The <see cref="ModelBuilder"/> had enabled auto history feature.</returns>
    public static ModelBuilder EnableAutoHistory(this ModelBuilder modelBuilder, 
        string applicationName = null,
        int? changedMaxLength = null, 
        bool? limitChangedLength = null
    )
    {
        return EnableAutoHistory<AutoHistory>(modelBuilder, o =>
        {
            if (string.IsNullOrWhiteSpace(applicationName))
                o.ApplicationName = applicationName;
            if (changedMaxLength.HasValue)
                o.ChangedMaxLength = changedMaxLength.Value;
            if (limitChangedLength.HasValue)
                o.LimitChangedLength = limitChangedLength.Value;
        });
    }

    /// <summary>
    /// Enables the automatic recording change history.
    /// </summary>
    /// <param name="modelBuilder">The <see cref="ModelBuilder"/> to enable auto history feature.</param>
    /// <param name="configure">The action to configure the auto history options.</param>
    /// <returns>The <see cref="ModelBuilder"/> had enabled auto history feature.</returns>
    public static ModelBuilder EnableAutoHistory(this ModelBuilder modelBuilder, Action<AutoHistoryOptions> configure)
    {
        return EnableAutoHistory<AutoHistory>(modelBuilder, configure);
    }

    /// <summary>
    /// Enables the automatic recording change history.
    /// </summary>
    /// <typeparam name="TAutoHistory">The type of the auto history.</typeparam>
    /// <param name="modelBuilder">The <see cref="ModelBuilder"/> to enable auto history feature.</param>
    /// <param name="configure">The action to configure the auto history options.</param>
    /// <returns>The <see cref="ModelBuilder"/> had enabled auto history feature.</returns>
    public static ModelBuilder EnableAutoHistory<TAutoHistory>(this ModelBuilder modelBuilder, Action<AutoHistoryOptions> configure)
        where TAutoHistory : AutoHistory
    {
        var options = AutoHistoryOptions.Instance;
        configure?.Invoke(options);

        // Set default application name if not set
        options.ApplicationName ??= Assembly.GetEntryAssembly()?.GetName().Name;

        modelBuilder.Entity<TAutoHistory>(b =>
        {
            b.Property(c => c.RowId).IsRequired().HasMaxLength(options.RowIdMaxLength);
            b.Property(c => c.TableName).IsRequired().HasMaxLength(options.TableMaxLength);
            b.Property(c => c.ApplicationName).IsRequired().HasMaxLength(options.ApplicationNameMaxLength);
            b.Property(c => c.UserName).HasMaxLength(options.UserNameMaxLength);

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
