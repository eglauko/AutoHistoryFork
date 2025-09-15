# AutoHistoryFork
A plugin for **Microsoft.EntityFrameworkCore** to support automatically recording data changes history.

This fork works for added entities.

Version 9.0 (Microsoft.EntityFrameworkCore.AutoHistoryFork 9.x) supports **.NET 8.0** and **.NET 9.0** (with their respective EF Core 8/9 versions).

For applications targeting **.NET 5.0**, **.NET 6.0** or **.NET 7.0**, use package version **7.x** of this fork,
which supports those frameworks and their corresponding EF Core versions.

# How to use

`AutoHistoryFork` will recording all the data changing history in one `Table` named `AutoHistories`, this table will recording data
`UPDATE`, `DELETE` history and, if you want, `ADD` history.

This Fork brings two additional fields to the original version: `UserName` and `ApplicationName`.

1. Install AutoHistoryFork Package

Run the following command in the `Package Manager Console` to install Microsoft.EntityFrameworkCore.AutoHistoryFork

`PM> Install-Package Microsoft.EntityFrameworkCore.AutoHistoryFork`

2. Enable AutoHistoryFork

```csharp
public class BloggingContext : DbContext
{
    public BloggingContext(DbContextOptions<BloggingContext> options)
        : base(options)
    { }

    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // enable auto history functionality.
        modelBuilder.EnableAutoHistory();
    }
}
```

3. Ensure AutoHistory in DbContext. This must be called before `bloggingContext.SaveChanges()` or `bloggingContext.SaveChangesAsync()`.

```csharp
bloggingContext.EnsureAutoHistory()
```

If you want to record data changes for all entities (except for Added - entities), just override `SaveChanges` and `SaveChangesAsync` methods and call `EnsureAutoHistory()` inside overridden version:

```csharp
public class BloggingContext : DbContext
{
    public BloggingContext(DbContextOptions<BloggingContext> options)
        : base(options)
    { }
    
    public override int SaveChanges()
    {
        this.EnsureAutoHistory();
        return base.SaveChanges();
    }
    
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        this.EnsureAutoHistory();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // enable auto history functionality.
        modelBuilder.EnableAutoHistory();
    }
}
```

4. If you also want to record Added - Entities, which is not possible per default, override `SaveChanges` and `SaveChangesAsync` methods this way:

```csharp
public class BloggingContext : DbContext
{
    public override int SaveChanges()
    {
        var addedEntities = ChangeTracker
            .Entries()
            .Where(e => e.State == EntityState.Added)
            .ToArray(); // remember added entries,

        var hasAdded = addedEntities.Any();
        IDbContextTransaction transaction = null;

        // if has added entities, begin transaction
        if (hasAdded)
            transaction = Database.BeginTransaction();

        // before EF Core is assigning valid Ids (it does on save changes, 
        // when ids equal zero) and setting their state to 
        // Unchanged (it does on every save changes)
        this.EnsureAutoHistory();
        int changes = base.SaveChanges();

        if (hasAdded)
        {
            // after "SaveChanges" added enties now have gotten valid ids (if it was necessary)
            // and the history for them can be ensured and be saved with another "SaveChanges"
            this.EnsureAddedHistory(addedEntities);
            changes += base.SaveChanges();

            transaction.Commit();
        }

        return changes;
    }   
}
```

# Use Current User Name

You can use the user name of the current user passing the user name to the `EnsureAutoHistory` method.

```csharp
public override int SaveChanges()
{
    this.EnsureAutoHistory(currentUserName); // currentUserName can be a field of the DbContext
    return base.SaveChanges();
}
```

The `EnsureAddedHistory` method also accepts the name of the current user.

```csharp
this.EnsureAddedHistory(addedEntities, currentUserName);
```

# Using other DbContext for saving AutoHistory

You can use other DbContext for saving AutoHistory by passing the DbContext to the `EnsureAutoHistory` method.

```csharp

public override int SaveChanges()
{
    var addedEntities = ChangeTracker
        .Entries()
        .Where(e => e.State == EntityState.Added)
        .ToArray(); // remember added entries,

    this.EnsureAutoHistory(historyDbContext, currentUserName); // historyDbContext and currentUserName can be fields of the DbContext
    var changes = base.SaveChanges();

    historyDbContext.EnsureAddedHistory(addedEntities, currentUserName);
    historyDbContext.TrySaveChanges(); // extension method that tries to save changes and, in the event of an error, logs the error and does not throw an exception
                                       // it is not implemented by default, you can implement it yourself

    return changes;
}

```

# Application Name

The application name is defined in the `AutoHistoryOptions` class.

The default value is the name of the entry assembly.

You can configure the application name by passing it as a parameter in the `EnableAutoHistory` extension method for
`ModelBuilder` or by configuring `AutoHistoryOptions`.

Here's an example:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // enable auto history functionality and set the application name
    modelBuilder.EnableAutoHistory("MyApplicationName");
}
```

## Disabling the ApplicationName column mapping
By default the `ApplicationName` property is mapped to a column in the history table.
You can disable this behavior (for example, to reduce table width if you do not need to store it)
by setting the new option `MapApplicationName` to `false`.
    
When `MapApplicationName` is `false`:
- The `ApplicationName` column is not created/mapped.
- The in-memory `AutoHistory.ApplicationName` property will not be persisted.
- Existing migrations may need to be adjusted manually (drop the column) if you previously had it mapped.

Example disabling the mapping while still providing a logical application name in code:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.EnableAutoHistory(options =>
    {
        options.MapApplicationName = false; // do not map column
        // other options...
    });
}
```

If you need to set a fixed application name and keep the column, just leave `MapApplicationName` with its default value (`true`) and optionally pass the name:

```csharp
modelBuilder.EnableAutoHistory("MyFixedAppName"); // MapApplicationName stays true (default)
```

# Use Custom AutoHistory Entity
You can use a custom auto history entity by extending the Microsoft.EntityFrameworkCore.AutoHistory class.

```csharp
class CustomAutoHistory : AutoHistory
{
    public String CustomField { get; set; }
}
```

Then register it in the db context like follows:
```csharp
modelBuilder.EnableAutoHistory<CustomAutoHistory>(o => { });
```

Then provide a custom history entity creating factory when calling EnsureAutoHistory. The example shows using the
factory directly, but you should use a service here that fills out your history extended properties(The properties inherited from `AutoHistory` will be set by the framework automatically).
```csharp
db.EnsureAutoHistory(() => new CustomAutoHistory()
{
    CustomField = "CustomValue"
});
```

# Excluding properties from AutoHistory
You can exclude properties or an entire entity from being recorded in history in four ways:

1. Attribute on a property
2. Attribute on the class (excludes the entire entity)
3. Fluent configuration to exclude a property (`WithExcludeProperty`)
4. Fluent configuration to exclude the entire entity (`WithExcludeFromHistory`)

## 1. Exclude a property via attribute
Add the `[ExcludeFromHistory]` attribute to any property that should not produce history.
```csharp
public class Blog
{        
    [ExcludeFromHistory]
    public string PrivateURL { get; set; }
}
```
If an update only modifies excluded properties, no history record will be created.

## 2. Exclude the entire entity via attribute
Applying the attribute to the class removes all properties of that entity from history tracking.
```csharp
[ExcludeFromHistory]
public class NotTracked
{
    public int NotTrackedId { get; set; }
    public string Title { get; set; }
}
```
Any change (Added/Modified/Deleted) to `NotTracked` will not produce history.

## 3. Exclude a property via options (fluent API)
Use `WithExcludeProperty` inside the configuration passed to `EnableAutoHistory`.
Supports either a property name or a strongly-typed lambda expression.
```csharp
modelBuilder.EnableAutoHistory(options => options
    .ConfigureType<Blog>(typeOptions =>
    {
        // using expression
        typeOptions.WithExcludeProperty(b => b.PrivateURL);
        // or using name
        // typeOptions.WithExcludeProperty(nameof(Blog.PrivateURL));
    }));
```
Internally this adds the property name to the `ExcludeProperties` list of `AutoHistoryTypeOptions`. When generating changes, if only excluded properties were modified, no history is saved (see test `Entity_Update_AutoHistory_Exclude_Only_Modified_Property_Changed_Test`).

## 4. Exclude the entire entity via options
Use `WithExcludeFromHistory` to mark the entity as completely ignored.
```csharp
modelBuilder.EnableAutoHistory(options => options
    .ConfigureType<NotTracked2>(t => t.WithExcludeFromHistory()));
```
Just like the class-level attribute, no changes to `NotTracked2` will generate history (see tests `Excluded_Entity_Update_AutoHistory_OnlyModified_Changed_Test` and `_2`).

## Combining strategies
- If the entity is marked for total exclusion (attribute or `WithExcludeFromHistory`), individual property exclusions are irrelevant.
- You can combine property attributes with fluent configuration for other properties.
- The final set of excluded properties is the union of those declared via attributes and via options.

## Complete example
Adapted from the test context:
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.EnableAutoHistory(options => options
        .ConfigureType<Blog>(t =>
        {
            t.WithExcludeProperty(b => b.ExcludedProperty); // via options
        })
        .ConfigureType<NotTracked2>(t =>
        {
            t.WithExcludeFromHistory(); // exclude entire entity
        }));
}

public class Blog
{
    public int BlogId { get; set; }
    public string Url { get; set; }

    [ExcludeFromHistory]              // property attribute
    public string PrivateURL { get: set; }

    public string ExcludedProperty { get; set; } // excluded via options
}

[ExcludeFromHistory]                 // class attribute
public class NotTracked
{
    public int NotTrackedId { get; set; }
    public string Title { get; set; }
}

public class NotTracked2             // excluded via options (fluent)
{
    public int NotTracked2Id { get; set; }
    public string Title { get; set; }
}
```
### Expected results (per tests)
- Changing only `PrivateURL` (property attribute) => no history.
- Changing `ExcludedProperty` plus another tracked property => 1 history entry containing only the non-excluded fields.
- Changing entities `NotTracked` or `NotTracked2` => no history.

# GroupId (Grouping Related Entity Histories)
The GroupId feature lets you logically group the history records of different entity types under a single identifier (e.g. a Blog and all of its Posts). This simplifies querying and displaying an aggregate change timeline.

## When to use
Use GroupId when you need to:
- Fetch all history rows for a root entity and its related children with one key.
- Display a unified audit trail for an aggregate.
- Correlate Added / Modified / Deleted operations across related tables.

## How it works
Each `AutoHistory` record exposes a nullable `GroupId` property:
```csharp
public string? GroupId { get; set; }
```
If grouping is enabled and the entity type has a configured group property, the value of that property (converted to string) is stored in `GroupId` for every history row of that entity.

## Enabling grouping globally
Enable grouping at the options level using `WithGroupId`:
```csharp
modelBuilder.EnableAutoHistory(options => options
    .WithGroupId(true)
    // other chained configuration...
);
```
`WithGroupId()` sets `UseGroupId = true` internally. If you skip this call, no `GroupId` will be populated even if per-type group properties are configured.

## Configuring the group property per entity
Each entity that participates in grouping must specify which of its properties supplies the group identifier using `WithGroupProperty`:
```csharp
modelBuilder.EnableAutoHistory(options => options
    .WithGroupId() // turn on grouping
    .ConfigureType<Blog>(t =>
        t.WithGroupProperty(nameof(Blog.BlogId))) // root aggregate id
    .ConfigureType<Post>(t =>
        t.WithGroupProperty(nameof(Post.BlogId))) // FK back to Blog
);
```
In this example:
- Blog history rows get `GroupId = BlogId`.
- Post history rows also get `GroupId = BlogId` (its foreign key), allowing Blog + Posts to share the same group identifier.

## Example context
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.EnableAutoHistory(options => options
        .WithGroupId(true)
        .ConfigureType<Blog>(t => t.WithGroupProperty(nameof(Blog.BlogId)))
        .ConfigureType<Post>(t => t.WithGroupProperty(nameof(Post.BlogId))));
}
```

## Example save logic (including Added entities)
```csharp
public override int SaveChanges()
{
    var added = ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToArray();
    var hasAdded = added.Any();
    IDbContextTransaction tx = null;
    if (hasAdded) tx = Database.BeginTransaction();

    this.EnsureAutoHistory();            // Modified & Deleted
    var changes = base.SaveChanges();

    if (hasAdded)
    {
        this.EnsureAddedHistory(added);  // Added (ids now available)
        changes += base.SaveChanges();
        tx!.Commit();
    }
    return changes;
}
```
All Added / Modified / Deleted history rows created inside that unit of work will have `GroupId` populated (when configured) using the now-known primary/foreign key values.

## Querying grouped history
Fetch all Blog + Posts history by BlogId:
```csharp
var blogId = 42;
var grouped = context.Set<AutoHistory>()
    .Where(h => h.GroupId == blogId.ToString())
    .OrderBy(h => h.ChangedOn)
    .ToList();
```

## Test expectations (summary)
The provided tests validate that:
- Added blog + posts each get `GroupId = Blog.BlogId`.
- Modified posts retain `GroupId = Blog.BlogId`.
- Deleted blog + posts rows both keep the same `GroupId`.
- Unchanged entities do not create extra history entries but the single Added entry has `GroupId`.
- Multiple modified children share the same `GroupId` value.

## Fallback behavior
If `UseGroupId` is off or an entity lacks a configured group property, `GroupId` remains `null` for that entity's history rows.

# ChangedHistory class

The `ChangedHistory` class is a dictionary that contains the changed properties of an entity.

The key is the property name and the value is an array os strings that contains the old and new values.

For the added entities, the `ChangedHistory` class contains only the new values, and the array has only one item.
The same happens for the deleted entities, but the array has only the old values.

For modified entities, the `ChangedHistory` class contains the old and new values, and the array has two items.
The first item is the old value and the second item is the new value.

The `ChangedHistory` class has a `Serialize` method that returns a JSON string of the dictionary.

Also, has a static `Deserialize` method that returns a `ChangedHistory` object from a JSON string.

# Changes from the original (Microsoft.EntityFrameworkCore.AutoHistory 6.0.0)

The function for saving histories of added entities didn't work in the original version, but this one does.

In the history entity, `AutoHistory`, the properties `UserName` and `ApplicationName` have been added.

The default length for the `Changed` column has been changed from 2048 to 8000 characters.

In the `AutoHistoryOptions` class, the `ApplicationName`, `DateTimeFactory`, `UserNameMaxLength` and `ApplicationNameMaxLength` properties have been added.
The `JsonSerializerOptions` property has been removed for version .Net6.0 onwards, although it still exists for version .Net5.0.

The `ChangedHistory` class was created, which is a dictionary containing the changed properties of an entity.
This changes the JSON format for the `Changed` field of the history entity.

The `EnsureAutoHistory` method optionally accepts the name of the current user and a second `DbContext` for recording histories.
