using System.Linq.Expressions;
using ExpressionMapper.Mapping;
using Microsoft.EntityFrameworkCore;

namespace ExpressionMapper.Tests.Integration.EfCore;

public enum AccountStatus { Active, Suspended, Closed }
 
public class AccountEntity
{
    public int Id { get; set; }
    public AccountStatus Status { get; set; }
    public AccountStatus? OptionalStatus { get; set; }
}
 
public class AccountDto
{
    public int Id { get; set; }
    public AccountStatus Status { get; set; }
    public AccountStatus? OptionalStatus { get; set; }
}
 
// Owned type — stored in the same table as UserEntity, no FK/join
public class ContactInfoEntity
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
}
 
public class ContactInfoDto
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
}
 
public class UserWithContactEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ContactInfoEntity Contact { get; set; } = new();
}
 
public class UserWithContactDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ContactInfoDto Contact { get; set; } = new();
}
 
// --- Mappers ---
 
public class AccountMapper : EntityMapper<AccountEntity, AccountDto>
{
    protected override void Configure()
    {
        Map(src => src.Id, dst => dst.Id);
        Map(src => src.Status, dst => dst.Status);
        Map(src => src.OptionalStatus, dst => dst.OptionalStatus);
    }
}
 
public class ContactInfoMapper : EntityMapper<ContactInfoEntity, ContactInfoDto>
{
    protected override void Configure()
    {
        Map(src => src.PhoneNumber, dst => dst.PhoneNumber);
        Map(src => src.CountryCode, dst => dst.CountryCode);
    }
}
 
public class UserWithContactMapper : EntityMapper<UserWithContactEntity, UserWithContactDto>
{
    protected override void Configure()
    {
        Map(src => src.Id, dst => dst.Id);
        Map(src => src.Name, dst => dst.Name);
        Map(src => src.Contact, dst => dst.Contact, new ContactInfoMapper());
    }
}
 
// --- DbContext ---
 
public class EdgeCaseDbContext(DbContextOptions<EdgeCaseDbContext> options) : DbContext(options)
{
    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();
    public DbSet<UserWithContactEntity> Users => Set<UserWithContactEntity>();
 
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountEntity>(e =>
        {
            e.HasKey(a => a.Id);
            // Enum stored as string — the common pitfall.
            // Mappers that wrap enum comparisons in an int cast break here
            // because EF translates the column as text and can't reconcile
            // CAST("Status" AS INTEGER) = 0 against a string column.
            e.Property(a => a.Status).HasConversion<string>();
            e.Property(a => a.OptionalStatus).HasConversion<string>();
        });
 
        modelBuilder.Entity<UserWithContactEntity>(e =>
        {
            e.HasKey(u => u.Id);
            // Owned type — Contact columns live in the Users table.
            // Mappers that treat owned types as navigation properties
            // can introduce a join that EF Core can't resolve.
            e.OwnsOne(u => u.Contact);
        });
    }
}

 
public class EdgeCaseSqlTranslationTests : IDisposable
{
    private readonly EdgeCaseDbContext _db;
 
    public EdgeCaseSqlTranslationTests()
    {
        var options = new DbContextOptionsBuilder<EdgeCaseDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
 
        _db = new EdgeCaseDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }
 
    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
 
    // Pitfall: mapper introduces an (int) cast on the enum value before comparing.
    // EF Core stores the column as TEXT (HasConversion<string>), so it translates
    // the comparison as "Status" = 'Active'. An int cast produces
    // CAST("Status" AS INTEGER) = 0, which SQLite silently returns 0 rows for
    // and other providers throw on entirely.
    [Fact]
    public void Map_EnumStoredAsString_DoesNotIntroduceIntCast()
    {
        var mapper = new AccountMapper();
        Expression<Func<AccountDto, bool>> predicate = dst => dst.Status == AccountStatus.Active;
 
        var rewritten = mapper.MapExpression(predicate);
        var ex = Record.Exception(() => _db.Accounts.Where(rewritten).ToQueryString());
 
        Assert.Null(ex);
    }
 
    // Same pitfall but nullable. A mapper that unwraps the nullable enum to its
    // underlying int before comparing will break the same way.
    [Fact]
    public void Map_NullableEnumStoredAsString_DoesNotIntroduceIntCast()
    {
        var mapper = new AccountMapper();
        Expression<Func<AccountDto, bool>> predicate = dst => dst.OptionalStatus == AccountStatus.Suspended;
 
        var rewritten = mapper.MapExpression(predicate);
        var ex = Record.Exception(() => _db.Accounts.Where(rewritten).ToQueryString());
 
        Assert.Null(ex);
    }
 
    // Pitfall: local collection Contains must translate to a SQL IN clause.
    // The rewriter must preserve the captured list as a constant, not attempt
    // to walk into it as if it were a mapped member.
    [Fact]
    public void Map_LocalCollectionContains_TranslatesToSqlInClause()
    {
        var mapper = new AccountMapper();
        var ids = new List<int> { 1, 2, 3 };
        Expression<Func<AccountDto, bool>> predicate = dst => ids.Contains(dst.Id);
 
        var rewritten = mapper.MapExpression(predicate);
        var sql = _db.Accounts.Where(rewritten).ToQueryString();
 
        // Verify it actually produced an IN clause rather than throwing
        Assert.Contains("IN", sql, StringComparison.OrdinalIgnoreCase);
    }
 
    // Pitfall: owned types are not navigations — they have no FK and require no join.
    // A mapper that rewrites the owned type access as if it were a regular navigation
    // property can cause EF Core to attempt a join on a nonexistent table.
    [Fact]
    public void Map_OwnedTypePropertyAccess_DoesNotProduceSpuriousJoin()
    {
        var mapper = new UserWithContactMapper();
        Expression<Func<UserWithContactDto, bool>> predicate = dst => dst.Contact.CountryCode == "GB";
 
        var rewritten = mapper.MapExpression(predicate);
        var sql = _db.Users.Where(rewritten).ToQueryString();
 
        // Owned type columns are in the same table — no JOIN should appear
        Assert.DoesNotContain("JOIN", sql, StringComparison.OrdinalIgnoreCase);
    }
}
