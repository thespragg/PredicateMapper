using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using PredicateMapper.Mapping;
using Microsoft.EntityFrameworkCore;

namespace PredicateMapper.Tests.Integration.EfCore;

public enum AccountStatus
{
    Active,
    Suspended,
    Closed
}

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

    private static readonly JsonSerializerOptions EnumAsString = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountEntity>(e =>
        {
            e.HasKey(a => a.Id);
            // Enum stored as string — the common pitfall.
            // Mappers that wrap enum comparisons in an int cast break here
            // because EF translates the column as text and can't reconcile
            // CAST("Status" AS INTEGER) = 0 against a string column.
            e.Property(a => a.Status)
                .HasConversion(
                    x => JsonSerializer.Serialize(x, EnumAsString),
                    x => JsonSerializer.Deserialize<AccountStatus>(x, EnumAsString));
            e.Property(a => a.OptionalStatus)
                .HasConversion(
                    x => JsonSerializer.Serialize(x, EnumAsString),
                    x => (AccountStatus?)JsonSerializer.Deserialize<AccountStatus>(x, EnumAsString));
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
        var sql = _db.Accounts.Where(rewritten).ToQueryString();

        Assert.DoesNotContain("CAST", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Active", sql, StringComparison.OrdinalIgnoreCase);
    }

    // Same pitfall but nullable. A mapper that unwraps the nullable enum to its
    // underlying int before comparing will break the same way.
    [Fact]
    public void Map_NullableEnumStoredAsString_DoesNotIntroduceIntCast()
    {
        var mapper = new AccountMapper();
        Expression<Func<AccountDto, bool>> predicate = dst => dst.OptionalStatus == AccountStatus.Suspended;

        var rewritten = mapper.MapExpression(predicate);
        var sql = _db.Accounts.Where(rewritten).ToQueryString();

        Assert.DoesNotContain("CAST", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Suspended", sql, StringComparison.OrdinalIgnoreCase);
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

        Assert.DoesNotContain("JOIN", sql, StringComparison.OrdinalIgnoreCase);
    }

    // The JOIN check alone is not enough — verify the owned column is referenced by
    // its correct inlined name (e.g. Contact_CountryCode), not some invented alias.
    [Fact]
    public void Map_OwnedTypePropertyAccess_UsesCorrectColumnName()
    {
        var mapper = new UserWithContactMapper();
        Expression<Func<UserWithContactDto, bool>> predicate = dst => dst.Contact.CountryCode == "GB";

        var rewritten = mapper.MapExpression(predicate);
        var sql = _db.Users.Where(rewritten).ToQueryString();

        Assert.Contains("Contact_CountryCode", sql, StringComparison.OrdinalIgnoreCase);
    }

    // The int-cast pitfall applies equally to inequality comparisons.
    [Fact]
    public void Map_EnumInequality_StoredAsString_DoesNotIntroduceIntCast()
    {
        var mapper = new AccountMapper();
        Expression<Func<AccountDto, bool>> predicate = dst => dst.Status != AccountStatus.Closed;

        var rewritten = mapper.MapExpression(predicate);
        var sql = _db.Accounts.Where(rewritten).ToQueryString();

        Assert.DoesNotContain("CAST", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Closed", sql, StringComparison.OrdinalIgnoreCase);
    }

    // Pitfall: a local collection of enum values passed to Contains must produce
    // a SQL IN clause with the string-converted values, not integer casts.
    [Fact]
    public void Map_EnumCollectionContains_StoredAsString_DoesNotIntroduceIntCast()
    {
        var mapper = new AccountMapper();
        var statuses = new List<AccountStatus> { AccountStatus.Active, AccountStatus.Suspended };
        Expression<Func<AccountDto, bool>> predicate = dst => statuses.Contains(dst.Status);

        var rewritten = mapper.MapExpression(predicate);
        var sql = _db.Accounts.Where(rewritten).ToQueryString();

        Assert.DoesNotContain("CAST", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Active", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Suspended", sql, StringComparison.OrdinalIgnoreCase);
    }

    // A null check on a nullable enum should translate to IS NULL, not a cast or comparison.
    [Fact]
    public void Map_NullableEnumNullCheck_TranslatesToIsNull()
    {
        var mapper = new AccountMapper();
        Expression<Func<AccountDto, bool>> predicate = dst => dst.OptionalStatus == null;

        var rewritten = mapper.MapExpression(predicate);
        var sql = _db.Accounts.Where(rewritten).ToQueryString();

        Assert.Contains("IS NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CAST", sql, StringComparison.OrdinalIgnoreCase);
    }

    // Mixed predicate: enum column and int column in the same WHERE clause.
    // Exercises that the rewriter doesn't corrupt type information when both
    // member types appear together.
    [Fact]
    public void Map_CompoundPredicate_EnumAndInt_TranslatesCorrectly()
    {
        var mapper = new AccountMapper();
        Expression<Func<AccountDto, bool>> predicate = dst => dst.Status == AccountStatus.Active && dst.Id > 5;

        var rewritten = mapper.MapExpression(predicate);
        var sql = _db.Accounts.Where(rewritten).ToQueryString();

        Assert.DoesNotContain("CAST", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Active", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("5", sql);
    }

    // Execution: verify the rewritten query actually returns the right rows,
    // not just that the SQL is structurally valid.
    [Fact]
    public void Map_EnumStoredAsString_ReturnsCorrectRows()
    {
        _db.Accounts.AddRange(
            new AccountEntity { Id = 1, Status = AccountStatus.Active },
            new AccountEntity { Id = 2, Status = AccountStatus.Suspended },
            new AccountEntity { Id = 3, Status = AccountStatus.Closed }
        );
        _db.SaveChanges();

        var mapper = new AccountMapper();
        Expression<Func<AccountDto, bool>> predicate = dst => dst.Status == AccountStatus.Active;
        var rewritten = mapper.MapExpression(predicate);

        var results = _db.Accounts.Where(rewritten).ToList();

        Assert.Single(results);
        Assert.Equal(1, results[0].Id);
    }

    // Execution: verify null checks return only rows where the column is SQL NULL.
    [Fact]
    public void Map_NullableEnumNullCheck_ReturnsCorrectRows()
    {
        _db.Accounts.AddRange(
            new AccountEntity { Id = 1, Status = AccountStatus.Active, OptionalStatus = null },
            new AccountEntity { Id = 2, Status = AccountStatus.Suspended, OptionalStatus = AccountStatus.Closed }
        );
        _db.SaveChanges();

        var mapper = new AccountMapper();
        Expression<Func<AccountDto, bool>> predicate = dst => dst.OptionalStatus == null;
        var rewritten = mapper.MapExpression(predicate);

        var results = _db.Accounts.Where(rewritten).ToList();

        Assert.Single(results);
        Assert.Equal(1, results[0].Id);
    }

    // Execution: verify owned type filter returns the right rows against real data.
    [Fact]
    public void Map_OwnedTypePropertyAccess_ReturnsCorrectRows()
    {
        _db.Users.AddRange(
            new UserWithContactEntity { Id = 1, Name = "Alice", Contact = new ContactInfoEntity { PhoneNumber = "111", CountryCode = "GB" } },
            new UserWithContactEntity { Id = 2, Name = "Bob", Contact = new ContactInfoEntity { PhoneNumber = "222", CountryCode = "US" } }
        );
        _db.SaveChanges();

        var mapper = new UserWithContactMapper();
        Expression<Func<UserWithContactDto, bool>> predicate = dst => dst.Contact.CountryCode == "GB";
        var rewritten = mapper.MapExpression(predicate);

        var results = _db.Users.Where(rewritten).ToList();

        Assert.Single(results);
        Assert.Equal(1, results[0].Id);
    }
}