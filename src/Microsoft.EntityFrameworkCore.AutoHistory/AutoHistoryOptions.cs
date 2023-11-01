using System;
using System.Runtime.CompilerServices;

#if NET5_0
using System.Text.Json;
#endif

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
    /// The name of the application that made the change. Default: EntryAssemblyName.
    /// </summary>
    public string ApplicationName { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; }

    /// <summary>
    /// The factory for the DateTime. Default: () => DateTime.UtcNow.
    /// </summary>
    public Func<DateTime> DateTimeFactory { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; } 
        = [MethodImpl(MethodImplOptions.AggressiveInlining)] static () => DateTime.UtcNow;

#if NET5_0

    /// <summary>
    /// The json setting for the 'Changed' column
    /// </summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; }

#endif

}
