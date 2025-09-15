using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore;

public sealed class AutoHistoryTypeOptions<TEntity> : AutoHistoryTypeOptions
{
    public AutoHistoryTypeOptions<TEntity> WithExcludeFromHistory()
    {
        ExcludeFromHistory = true;
        return this;
    }

    public AutoHistoryTypeOptions<TEntity> WithExcludeProperty(string propertyName)
    {
        if (ExcludeProperties is null)
            ExcludeProperties = [propertyName];
        else
            ExcludeProperties = ExcludeProperties.Append(propertyName).ToArray();

        return this;
    }

    public AutoHistoryTypeOptions<TEntity> WithExcludeProperty<TValue>(Expression<Func<TEntity, TValue>> propertyExpression)
    {
        if (propertyExpression.Body is not MemberExpression memberExpression)
            throw new ArgumentException("The expression is not a member access expression.", nameof(propertyExpression));

        var propertyName = memberExpression.Member.Name;

        return WithExcludeProperty(propertyName);
    }

    public AutoHistoryTypeOptions<TEntity> WithGroupProperty(string propertyName)
    {
        GroupProperty = propertyName;
        return this;
    }
}

/// <summary>
/// Options for a specific entity type.
/// </summary>
public class AutoHistoryTypeOptions
{
    /// <summary>
    /// The entity type these options apply to.
    /// </summary>
    public required Type EntityType { get; init; }

    /// <summary>
    /// Determines whether to exclude this entity type from history tracking.
    /// </summary>
    public bool ExcludeFromHistory { get; set; }

    /// <summary>
    /// Determines whether to ignore specific properties of this entity type from history tracking.
    /// </summary>
    public string[]? ExcludeProperties { get; set; }

    /// <summary>
    /// The name of the property to use as the group identifier when UseGroupId is enabled.
    /// </summary>
    public string? GroupProperty { get; set; }
}