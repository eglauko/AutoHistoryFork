using System;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Represents the entity change history.
/// </summary>
[ExcludeFromHistory]
public class AutoHistory
{
    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    /// <value>The id.</value>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the source row id.
    /// </summary>
    /// <value>The source row id.</value>
    public string RowId { get; set; }

    /// <summary>
    /// Gets or sets the name of the table.
    /// </summary>
    /// <value>The name of the table.</value>
    public string TableName { get; set; }

    /// <summary>
    /// Gets or sets the json about the changing.
    /// </summary>
    /// <value>The json about the changing.</value>
    public string Changed { get; set; }

    /// <summary>
    /// Gets or sets the user name.
    /// </summary>
    /// <value>The user name that made the change.</value>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the application name.
    /// </summary>
    /// <value>The name that identifies the application that made the change.</value>
    public string ApplicationName { get; set; }

    /// <summary>
    /// Gets or sets the change kind.
    /// </summary>
    /// <value>The change kind.</value>
    public EntityState Kind { get; set; }

    /// <summary>
    /// Gets or sets the create time.
    /// </summary>
    /// <value>The create time.</value>
    public DateTime Created { get; set; }
}

#pragma warning disable S2094 // An override of Object.Equals or Object.GetHashCode should not be called

/// <summary>
/// This class exists so we can reference AutoHistory in the test project. The class name collides with the namespace there.
/// </summary>
internal class AutoHistoryTestHandle : AutoHistory
{

}
