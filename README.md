# AutoHistoryFork
A plugin for **Microsoft.EntityFrameworkCore** to support automatically recording data changes history.

This fork **works** for added entities and support `.Net5.0`, `.Net6.0` and `.Net7.0`, with their respective EFCore versions.

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

# Application Name

The application name is defined in the `AutoHistoryOptions` class.

The default value is the name of the entry assembly.

Tu podes configurar o nome da aplica��o passando por par�metro no m�tod `EnableAutoHistory` do `ModelBuilder`
ou configurando o `AutoHistoryOptions`.

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // enable auto history functionality and set the application name
    modelBuilder.EnableAutoHistory("MyApplicationName");
}
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
You can now excluded properties from being saved into the AutoHistory tables by adding a custom attribute[ExcludeFromHistoryAttribute] attribute to your model properties. 


```csharp
    public class Blog
    {        
        [ExcludeFromHistory]
        public string PrivateURL { get; set; }
    }
```
