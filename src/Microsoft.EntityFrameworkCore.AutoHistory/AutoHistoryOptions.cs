using System.Runtime.CompilerServices;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// This class provides options for setting up auto history.
/// </summary>
public sealed class AutoHistoryOptions
{
    /// <summary>
    /// The shared instance of the AutoHistoryOptions.
    /// </summary>
    
    internal static AutoHistoryOptions Instance { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; } = new();

    /// <summary>
    /// Prevent constructor from being called eternally.
    /// </summary>
    private AutoHistoryOptions() { }

    /// <summary>
    /// The maximum length of the 'Changed' column. <c>null</c> will use default setting 8000 unless ChangedVarcharMax is true
    /// in which case the column will be varchar(max). Default: null.
    /// </summary>
    public int? ChangedMaxLength { get; set; }

    /// <summary>
    /// Set this to true to enforce ChangedMaxLength. If this is false, ChangedMaxLength will be ignored.
    /// Default: true.
    /// </summary>
    public bool LimitChangedLength { get; set; } = true;

    /// <summary>
    /// The max length for the row id column. Default: 50.
    /// </summary>
    public int RowIdMaxLength { get; set; } = AutoHistory.Defaults.RowIdMaxLength;

    /// <summary>
    /// The max length for the group id column. Default: 50.
    /// </summary>
    public int GroupIdMaxLength { get; set; } = AutoHistory.Defaults.GroupIdMaxLength;

    /// <summary>
    /// The max length for the table column. Default: 128.
    /// </summary>
    public int TableMaxLength { get; set; } = AutoHistory.Defaults.TableMaxLength;

    /// <summary>
    /// The max length for the user name column. Default: 50.
    /// </summary>
    public int UserNameMaxLength { get; set; } = AutoHistory.Defaults.UserNameMaxLength;

    /// <summary>
    /// The max length for the application name column. Default: 128.
    /// </summary>
    public int ApplicationNameMaxLength { get; set; } = AutoHistory.Defaults.ApplicationNameMaxLength;

    /// <summary>
    /// Defines whether to map the application name. If true, the ApplicationName property will be mapped to a column.
    /// Default: true.
    /// </summary>
    public bool MapApplicationName { get; set; } = true;

    /// <summary>
    /// The name of the application that made the change. Default: EntryAssemblyName.
    /// </summary>
    public string? ApplicationName { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; }

    /// <summary>
    /// The factory for the DateTime. Default: () => DateTime.UtcNow.
    /// </summary>
    public Func<DateTime> DateTimeFactory { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; } 
        = [MethodImpl(MethodImplOptions.AggressiveInlining)] static () => DateTime.UtcNow;

    /// <summary>
    /// <para>
    ///     Determines whether to use an alternative Group Id property as the row identifier.
    ///     <br />
    ///     It's useful to aggregate history records by a business key instead of the primary key.
    /// </para>
    /// </summary>
    public bool UseGroupId { get; set; }

    /// <summary>
    /// Configuration options for specific entity types.
    /// </summary>
    public Dictionary<Type, AutoHistoryTypeOptions> TypeOptions { get; } = [];

    /// <summary>
    /// Configure the property <see cref="UseGroupId"/>.
    /// </summary>
    /// <param name="useGroupId">
    ///     Determines whether to use an alternative Group Id property as the row identifier.
    /// </param>
    /// <returns>
    ///     The same <see cref="AutoHistoryOptions"/> instance so that multiple calls can be chained.
    /// </returns>
    public AutoHistoryOptions WithGroupId(bool useGroupId = true)
    {
        UseGroupId = useGroupId;
        return this;
    }

    /// <summary>
    /// Configures options for a specific entity type.
    /// </summary>
    /// <typeparam name="T">The entity type to configure.</typeparam>
    /// <param name="configure">The action to configure the type options.</param>
    /// <returns>The same <see cref="AutoHistoryOptions"/> instance so that multiple calls can be chained.</returns>
    public AutoHistoryOptions ConfigureType<T>(Action<AutoHistoryTypeOptions<T>> configure) where T : class
    {
        var type = typeof(T);
        if (!(TypeOptions.TryGetValue(type, out var options) && options is AutoHistoryTypeOptions<T> typedOptions))
        {
            typedOptions = new AutoHistoryTypeOptions<T> { EntityType = type };
            TypeOptions[type] = typedOptions;
        }

        configure(typedOptions);
        return this;
    }
}
