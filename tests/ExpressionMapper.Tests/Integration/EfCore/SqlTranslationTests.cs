using System.Linq.Expressions;
using ExpressionMapper.Tests.TestModels;
using ExpressionMapper.Tests.Unit.Rewriting;
using Microsoft.EntityFrameworkCore;

namespace ExpressionMapper.Tests.Integration.EfCore;

public class SqlTranslationTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly UserMapper _mapper = new();
 
    public SqlTranslationTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
 
        _db = new TestDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }
 
    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
 
    [Fact]
    public void Map_SimpleEquality_TranslatesToSql()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Id == 1;
        AssertTranslatesToSql(predicate);
    }
 
    [Fact]
    public void Map_StringEquality_TranslatesToSql()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Name == "Alice";
        AssertTranslatesToSql(predicate);
    }
 
    [Fact]
    public void Map_GreaterThan_TranslatesToSql()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Age > 18;
        AssertTranslatesToSql(predicate);
    }
 
    [Fact]
    public void Map_AndAlso_TranslatesToSql()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.IsActive && dst.Name == "Alice";
        AssertTranslatesToSql(predicate);
    }
 
    [Fact]
    public void Map_OrElse_TranslatesToSql()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.IsActive || dst.Age > 18;
        AssertTranslatesToSql(predicate);
    }
 
    [Fact]
    public void Map_UnaryNot_TranslatesToSql()
    {
        Expression<Func<UserDto, bool>> predicate = dst => !dst.IsActive;
        AssertTranslatesToSql(predicate);
    }
 
    [Fact]
    public void Map_NullCheck_TranslatesToSql()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Email != null;
        AssertTranslatesToSql(predicate);
    }
 
    [Fact]
    public void Map_NavigationPropertyChain_TranslatesToSql()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Address!.City == "London";
        AssertTranslatesToSql(predicate);
    }
 
    [Fact]
    public void Map_StringStartsWith_TranslatesToSql()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Name.StartsWith("Al");
        AssertTranslatesToSql(predicate);
    }
 
    [Fact]
    public void Map_StringEndsWith_TranslatesToSql()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Name.EndsWith("ice");
        AssertTranslatesToSql(predicate);
    }
 
    [Fact]
    public void Map_StringContains_TranslatesToSql()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Name.Contains("lic");
        AssertTranslatesToSql(predicate);
    }
 
    [Fact]
    public void Map_CollectionAny_TranslatesToSql()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Orders.Any(o => o.Total > 100);
        AssertTranslatesToSql(predicate);
    }
 
    [Fact]
    public void Map_CollectionAll_TranslatesToSql()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Orders.All(o => o.IsPaid);
        AssertTranslatesToSql(predicate);
    }
 
    [Fact]
    public void Map_CapturedPrimitiveClosure_TranslatesToSql()
    {
        var capturedId = 42;
        Expression<Func<UserDto, bool>> predicate = dst => dst.Id == capturedId;
        AssertTranslatesToSql(predicate);
    }
 
    [Fact]
    public void Map_CompoundPredicate_TranslatesToSql()
    {
        Expression<Func<UserDto, bool>> predicate =
            dst => dst.IsActive && dst.Age > 18 && dst.Name.StartsWith("A");
        AssertTranslatesToSql(predicate);
    }
 
    private void AssertTranslatesToSql(Expression<Func<UserDto, bool>> predicate)
    {
        var rewritten = _mapper.Map(predicate);
        var ex = Record.Exception(() => _db.Users.Where(rewritten).ToQueryString());
        Assert.Null(ex);
    }
}
 
public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
 
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(e =>
        {
            e.HasKey(u => u.UserId);
            e.HasMany(u => u.OrderEntities).WithOne().HasForeignKey("UserId");
            e.HasMany(u => u.Children).WithOne().HasForeignKey("ParentUserId");
            e.HasOne(u => u.Address).WithOne().HasForeignKey<AddressEntity>("UserId");
            e.HasOne(u => u.Subscription).WithOne().HasForeignKey<SubscriptionEntity>("UserId");
        });
 
        modelBuilder.Entity<AddressEntity>().HasKey(a => a.Id);
        modelBuilder.Entity<OrderEntity>().HasKey(o => o.Id);
        modelBuilder.Entity<SubscriptionEntity>().HasKey(s => s.Id);
    }
}
