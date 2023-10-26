using System;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Desable the property or class from recording history.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
public class ExcludeFromHistoryAttribute : Attribute { }
