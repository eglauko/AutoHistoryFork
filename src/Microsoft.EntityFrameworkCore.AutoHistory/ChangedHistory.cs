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
        return JsonSerializer.Serialize(this, ChangedHistorySerializationContext.Default.ChangedHistory);
    }

    public static ChangedHistory Deserialize(string json)
    {
        return JsonSerializer.Deserialize(json, ChangedHistorySerializationContext.Default.ChangedHistory)!;
    }
}

[JsonSerializable(typeof(ChangedHistory))]
public partial class ChangedHistorySerializationContext : JsonSerializerContext { }
