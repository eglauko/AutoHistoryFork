# AutoHistoryFork
Um plugin para **Microsoft.EntityFrameworkCore** que suporta o registro autom�tico do hist�rico de altera��es de dados.

Este fork funciona tamb�m para entidades adicionadas (Added).

Vers�o 9.0 (Microsoft.EntityFrameworkCore.AutoHistoryFork 9.x) suporta **.NET 8.0** e **.NET 9.0** (com suas respectivas vers�es EF Core 8/9).

Para aplica��es que visam **.NET 5.0**, **.NET 6.0** ou **.NET 7.0**, use a vers�o **7.x** deste fork, que suporta esses frameworks e suas vers�es correspondentes do EF Core.

---
## Mudan�a incompat�vel (v9+): par�metro de tipo gen�rico obrigat�rio em EnableAutoHistory
A partir desta vers�o os m�todos de extens�o `modelBuilder.EnableAutoHistory()` exigem pelo menos o par�metro gen�rico do `DbContext`:

Antes (v7.x / API antiga):
```csharp
modelBuilder.EnableAutoHistory();
modelBuilder.EnableAutoHistory(options => { /*...*/ });
modelBuilder.EnableAutoHistory<CustomAutoHistory>(options => { /*...*/ });
```
Agora (v9+):
```csharp
modelBuilder.EnableAutoHistory<BloggingContext>();
modelBuilder.EnableAutoHistory<BloggingContext>(options => { /*...*/ });
modelBuilder.EnableAutoHistory<BloggingContext, CustomAutoHistory>(options => { /*...*/ });
```
> O primeiro par�metro gen�rico � SEMPRE o tipo do seu `DbContext`. O segundo (opcional) � o tipo customizado que herda de `AutoHistory`.

---
# Como usar

`AutoHistoryFork` registra todo o hist�rico de mudan�as de dados em uma tabela chamada `AutoHistories`. Esta tabela registra hist�rico de `UPDATE`, `DELETE` e, opcionalmente, de `ADD`.

Este fork adiciona dois campos extras em rela��o � vers�o original: `UserName` e `ApplicationName` (e agora tamb�m opcional `GroupId`).

### 1. Instalar o pacote
```powershell
PM> Install-Package Microsoft.EntityFrameworkCore.AutoHistoryFork
```

### 2. Habilitar AutoHistory
Use o m�todo gen�rico informando o tipo do seu `DbContext`:
```csharp
public class BloggingContext : DbContext
{
    public BloggingContext(DbContextOptions<BloggingContext> options) : base(options) {}

    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // enable auto history functionality (minimal)
        modelBuilder.EnableAutoHistory<BloggingContext>();
    }
}
```

### 3. Garantir AutoHistory antes de SaveChanges
```csharp
bloggingContext.EnsureAutoHistory(); // Modified & Deleted entities
```

### 4. Registrar automaticamente Modified/Deleted sobrescrevendo SaveChanges
```csharp
public class BloggingContext : DbContext
{
    public BloggingContext(DbContextOptions<BloggingContext> options) : base(options) {}

    public override int SaveChanges()
    {
        this.EnsureAutoHistory(); // Modified & Deleted
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        this.EnsureAutoHistory();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.EnableAutoHistory<BloggingContext>();
    }
}
```

### 5. Registrando entidades Added (salvamento em duas fases)
```csharp
public class BloggingContext : DbContext
{
    public override int SaveChanges()
    {
        var addedEntities = ChangeTracker
            .Entries()
            .Where(e => e.State == EntityState.Added)
            .ToArray();

        var hasAdded = addedEntities.Any();
        IDbContextTransaction? transaction = null;
        if (hasAdded)
            transaction = Database.BeginTransaction();

        // First ensure history for Modified/Deleted before persistence changes states
        this.EnsureAutoHistory();
        int changes = base.SaveChanges();

        if (hasAdded)
        {
            // Now Added entities have real keys -> create Added history
            this.EnsureAddedHistory(addedEntities);
            changes += base.SaveChanges();
            transaction!.Commit();
        }

        return changes;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.EnableAutoHistory<BloggingContext>();
    }
}
```

---
# Usar o nome do usu�rio atual
```csharp
public override int SaveChanges()
{
    this.EnsureAutoHistory(currentUserName);
    return base.SaveChanges();
}
```
Entidades Added:
```csharp
this.EnsureAddedHistory(addedEntities, currentUserName);
```

---
# Usando um DbContext separado para salvar hist�rico
```csharp
public override int SaveChanges()
{
    var addedEntities = ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToArray();

    // write Modified/Deleted history to an external history context
    this.EnsureAutoHistory(historyDbContext, currentUserName);
    var changes = base.SaveChanges();

    historyDbContext.EnsureAddedHistory(addedEntities, currentUserName);
    historyDbContext.TrySaveChanges(); // (custom extension you implement)

    return changes;
}
```

---
# Application Name
Configurar via par�metro ou options. Lembre de especificar agora o tipo do DbContext.
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.EnableAutoHistory<BloggingContext>("MyApplicationName");
}
```
Desabilitar mapeamento da coluna:
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.EnableAutoHistory<BloggingContext>(options =>
    {
        options.MapApplicationName = false;
    });
}
```
Ou manter a coluna e passar nome fixo:
```csharp
modelBuilder.EnableAutoHistory<BloggingContext>("MyFixedAppName");
```

---
# Entidade AutoHistory personalizada
```csharp
class CustomAutoHistory : AutoHistory
{
    public string CustomField { get; set; }
}

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.EnableAutoHistory<BloggingContext, CustomAutoHistory>(options => { });
}
```
Fornecendo factory customizada ao garantir hist�rico:
```csharp
db.EnsureAutoHistory<BloggingContext, CustomAutoHistory>(() => new CustomAutoHistory
{
    CustomField = "CustomValue"
});
```
> As propriedades herdadas de `AutoHistory` s�o preenchidas automaticamente pelo framework.

Para entidades Added (mesmo padr�o de overload j� dispon�vel):
```csharp
this.EnsureAddedHistory<BloggingContext, CustomAutoHistory>(() => new CustomAutoHistory { CustomField = "X" }, addedEntries);
```

---
# Excluindo propriedades / entidades do hist�rico
Quatro maneiras:
1. Atributo na propriedade `[ExcludeFromHistory]`
2. Atributo na classe (exclui a entidade inteira)
3. Fluent API para excluir uma propriedade (`WithExcludeProperty`)
4. Fluent API para excluir uma entidade (`WithExcludeFromHistory`)

Exemplo:
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.EnableAutoHistory<BloggingContext>(options => options
        .ConfigureType<Blog>(t =>
        {
            t.WithExcludeProperty(b => b.ExcludedProperty);
        })
        .ConfigureType<NotTracked2>(t => t.WithExcludeFromHistory()));
}

public class Blog
{
    public int BlogId { get; set; }
    public string Url { get; set; }
    [ExcludeFromHistory]
    public string PrivateURL { get; set; }
    public string ExcludedProperty { get; set; }
    public List<Post> Posts { get; set; }
}

[ExcludeFromHistory]
public class NotTracked { /* ... */ }
public class NotTracked2 { /* ... */ }
```
Regras (resumo):
- Se somente propriedades exclu�das mudaram, nenhuma entrada de hist�rico � gerada.
- Uma entidade totalmente exclu�da nunca gera hist�rico.
- Conjunto final de exclus�es = uni�o de atributos + configura��o fluente.

---
# GroupId (Agrupando hist�ricos relacionados)
Permite agrupar (ex.: Blog + Posts) compartilhando um identificador l�gico.

Habilitar globalmente e configurar por tipo:
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.EnableAutoHistory<BloggingContext>(options => options
        .WithGroupId(true)
        .ConfigureType<Blog>(t => t.WithGroupProperty(nameof(Blog.BlogId)))
        .ConfigureType<Post>(t => t.WithGroupProperty(nameof(Post.BlogId))));
}
```
Durante `SaveChanges` (incluindo Added):
```csharp
public override int SaveChanges()
{
    var added = ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToArray();
    var hasAdded = added.Any();
    IDbContextTransaction? tx = null;
    if (hasAdded) tx = Database.BeginTransaction();

    this.EnsureAutoHistory(); // Modified & Deleted
    var changes = base.SaveChanges();

    if (hasAdded)
    {
        this.EnsureAddedHistory(added);
        changes += base.SaveChanges();
        tx!.Commit();
    }
    return changes;
}
```
Consulta agrupada:
```csharp
var blogId = 42;
var grouped = context.Set<AutoHistory>()
    .Where(h => h.GroupId == blogId.ToString())
    .OrderBy(h => h.ChangedOn)
    .ToList();
```
Fallback: se `WithGroupId` n�o foi habilitado ou um tipo n�o definiu `WithGroupProperty`, `GroupId` permanece `null`.

---
# Formato de ChangedHistory
`ChangedHistory` � um dicion�rio: `PropertyName -> string[]`.
- Added: array com 1 posi��o (novo valor)
- Deleted: array com 1 posi��o (valor antigo)
- Modified: array com 2 posi��es (`[old, new]`)
Serializado como JSON em `AutoHistory.Changed`.

---
# Diferen�as vs original (Microsoft.EntityFrameworkCore.AutoHistory 6.0.0)
- Suporte funcional para entidades Added (`EnsureAddedHistory`).
- Campos adicionais: `UserName`, `ApplicationName`, `GroupId` (opcional via configura��o).
- Tamanho padr�o de `Changed` ampliado para 8000.
- Novas op��es em `AutoHistoryOptions`: `ApplicationName`, `DateTimeFactory`, `UserNameMaxLength`, `ApplicationNameMaxLength`, `MapApplicationName`, `UseGroupId` e configura��es por tipo (`ConfigureType`).
- Novo formato JSON (`ChangedHistory`).
- `EnsureAutoHistory` pode receber `userName` e/ou outro `DbContext` para persistir hist�rico.
- (v9+) API `EnableAutoHistory` agora exige o tipo `DbContext` como par�metro gen�rico.

---
# Dica de migra��o (de EnableAutoHistory n�o gen�rico antigo)
1. Adicione o tipo do contexto: `modelBuilder.EnableAutoHistory<BloggingContext>();`
2. Para tipo customizado: `modelBuilder.EnableAutoHistory<BloggingContext, CustomAutoHistory>(opts => { ... });`
3. Verifique se algum snippet antigo interno ainda usa a forma antiga.
4. Recrie migrations se a mudan�a vier com novas colunas (ex.: `GroupId`).

---
# Resumo de exemplos
| Cen�rio | Trecho |
|----------|--------|
| B�sico | `modelBuilder.EnableAutoHistory<BloggingContext>();` |
| Com op��es | `modelBuilder.EnableAutoHistory<BloggingContext>(o => o.WithGroupId());` |
| Entidade personalizada | `modelBuilder.EnableAutoHistory<BloggingContext, CustomAutoHistory>(o => { });` |
| Nome da aplica��o | `modelBuilder.EnableAutoHistory<BloggingContext>("AppName");` |
| Desabilitar coluna AppName | `modelBuilder.EnableAutoHistory<BloggingContext>(o => o.MapApplicationName = false);` |
| GroupId + por tipo | `modelBuilder.EnableAutoHistory<BloggingContext>(o => o.WithGroupId().ConfigureType<Blog>(t=>t.WithGroupProperty(nameof(Blog.BlogId))));` |

---
# Gloss�rio
- EnsureAutoHistory: Gera hist�rico para Modified/Deleted antes de `SaveChanges`.
- EnsureAddedHistory: Gera hist�rico para Added ap�s `SaveChanges` (quando chaves est�o dispon�veis).
- GroupId: Campo opcional para agrupar hist�rico de entidades relacionadas.
- ChangedHistory: Estrutura usada para armazenar as mudan�as (JSON).

---
# Licen�a
(Adicionar informa��o de licen�a aqui, se aplic�vel)
