using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Dictionary of changed properties, where key is property name and value is array of old and new values.
/// </summary>
public sealed class ChangedHistory : Dictionary<string, string[]> 
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string Serialize()
    {
#if NET6_0_OR_GREATER
        return JsonSerializer.Serialize(this, ChangedHistorySerializationContext.Default.ChangedHistory);
#else
        return JsonSerializer.Serialize(this, AutoHistoryOptions.Instance.JsonSerializerOptions);
#endif
    }

    public static ChangedHistory Deserialize(string json)
    {
#if NET6_0_OR_GREATER
        return JsonSerializer.Deserialize<ChangedHistory>(json, ChangedHistorySerializationContext.Default.ChangedHistory);
#else
        return JsonSerializer.Deserialize<ChangedHistory>(json, AutoHistoryOptions.Instance.JsonSerializerOptions);
#endif
    }
}

#if NET6_0_OR_GREATER
[JsonSerializable(typeof(ChangedHistory))]
public partial class ChangedHistorySerializationContext : JsonSerializerContext { }
#endif