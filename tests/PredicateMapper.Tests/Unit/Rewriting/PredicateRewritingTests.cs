using System.Linq.Expressions;
using PredicateMapper.Exceptions;
using PredicateMapper.Mapping;
using PredicateMapper.Tests.TestModels;
using PredicateMapper.Tests.Unit.Configuration;

namespace PredicateMapper.Tests.Unit.Rewriting;

public class PredicateRewritingTests
{
    private readonly UserMapper _mapper = new();
 
    [Fact]
    public void Map_SimpleEquality_RewritesCorrectly()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Id == 1;
        var rewritten = _mapper.MapExpression(predicate);
 
        Assert.True(Invoke(rewritten, new UserEntity { UserId = 1 }));
        Assert.False(Invoke(rewritten, new UserEntity { UserId = 2 }));
    }
 
    [Fact]
    public void Map_StringEquality_RewritesCorrectly()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Name == "Alice";
        var rewritten = _mapper.MapExpression(predicate);
 
        Assert.True(Invoke(rewritten, new UserEntity { FullName = "Alice" }));
        Assert.False(Invoke(rewritten, new UserEntity { FullName = "Bob" }));
    }
 
    [Fact]
    public void Map_GreaterThan_RewritesCorrectly()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Age > 18;
        var rewritten = _mapper.MapExpression(predicate);
 
        Assert.True(Invoke(rewritten, new UserEntity { Age = 19 }));
        Assert.False(Invoke(rewritten, new UserEntity { Age = 18 }));
    }
 
    [Fact]
    public void Map_LessThanOrEqual_RewritesCorrectly()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Age <= 65;
        var rewritten = _mapper.MapExpression(predicate);
 
        Assert.True(Invoke(rewritten, new UserEntity { Age = 65 }));
        Assert.False(Invoke(rewritten, new UserEntity { Age = 66 }));
    }
 
    [Fact]
    public void Map_AndAlso_BothSidesRewritten()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.IsActive && dst.Name == "Alice";
        var rewritten = _mapper.MapExpression(predicate);
 
        Assert.True(Invoke(rewritten, new UserEntity { IsEnabled = true, FullName = "Alice" }));
        Assert.False(Invoke(rewritten, new UserEntity { IsEnabled = false, FullName = "Alice" }));
        Assert.False(Invoke(rewritten, new UserEntity { IsEnabled = true, FullName = "Bob" }));
    }
 
    [Fact]
    public void Map_OrElse_BothSidesRewritten()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.IsActive || dst.Name == "Alice";
        var rewritten = _mapper.MapExpression(predicate);
 
        Assert.True(Invoke(rewritten, new UserEntity { IsEnabled = false, FullName = "Alice" }));
        Assert.True(Invoke(rewritten, new UserEntity { IsEnabled = true, FullName = "Bob" }));
        Assert.False(Invoke(rewritten, new UserEntity { IsEnabled = false, FullName = "Bob" }));
    }
 
    [Fact]
    public void Map_UnaryNot_RewritesCorrectly()
    {
        Expression<Func<UserDto, bool>> predicate = dst => !dst.IsActive;
        var rewritten = _mapper.MapExpression(predicate);
 
        Assert.True(Invoke(rewritten, new UserEntity { IsEnabled = false }));
        Assert.False(Invoke(rewritten, new UserEntity { IsEnabled = true }));
    }
 
    [Fact]
    public void Map_NullCheck_NotNull_RewritesCorrectly()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Email != null;
        var rewritten = _mapper.MapExpression(predicate);
 
        Assert.True(Invoke(rewritten, new UserEntity { Email = "a@b.com" }));
        Assert.False(Invoke(rewritten, new UserEntity { Email = null }));
    }
 
    [Fact]
    public void Map_NullCheck_IsNull_RewritesCorrectly()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Email == null;
        var rewritten = _mapper.MapExpression(predicate);
 
        Assert.True(Invoke(rewritten, new UserEntity { Email = null }));
        Assert.False(Invoke(rewritten, new UserEntity { Email = "a@b.com" }));
    }
 
    [Fact]
    public void Map_NavigationPropertyChain_RewritesCorrectly()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Address!.City == "London";
        var rewritten = _mapper.MapExpression(predicate);
 
        Assert.True(Invoke(rewritten, new UserEntity { Address = new AddressEntity { City = "London" } }));
        Assert.False(Invoke(rewritten, new UserEntity { Address = new AddressEntity { City = "Paris" } }));
    }
 
    [Fact]
    public void Map_NullGuardedNavigationChain_RewritesCorrectly()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Address != null && dst.Address.City == "London";
        var rewritten = _mapper.MapExpression(predicate);
 
        Assert.True(Invoke(rewritten, new UserEntity { Address = new AddressEntity { City = "London" } }));
        Assert.False(Invoke(rewritten, new UserEntity { Address = null }));
    }
 
    [Fact]
    public void Map_StringStartsWith_RewritesCorrectly()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Name.StartsWith("Al");
        var rewritten = _mapper.MapExpression(predicate);
 
        Assert.True(Invoke(rewritten, new UserEntity { FullName = "Alice" }));
        Assert.False(Invoke(rewritten, new UserEntity { FullName = "Bob" }));
    }
 
    [Fact]
    public void Map_StringEndsWith_RewritesCorrectly()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Name.EndsWith("ice");
        var rewritten = _mapper.MapExpression(predicate);
 
        Assert.True(Invoke(rewritten, new UserEntity { FullName = "Alice" }));
        Assert.False(Invoke(rewritten, new UserEntity { FullName = "Bob" }));
    }
 
    [Fact]
    public void Map_StringContains_RewritesCorrectly()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Name.Contains("lic");
        var rewritten = _mapper.MapExpression(predicate);
 
        Assert.True(Invoke(rewritten, new UserEntity { FullName = "Alice" }));
        Assert.False(Invoke(rewritten, new UserEntity { FullName = "Bob" }));
    }
 
    [Fact]
    public void Map_CollectionAny_RewritesCorrectly()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Orders.Any(o => o.Total > 100);
        var rewritten = _mapper.MapExpression(predicate);
 
        Assert.True(Invoke(rewritten, new UserEntity { OrderEntities = [new OrderEntity { TotalAmount = 150 }] }));
        Assert.False(Invoke(rewritten, new UserEntity { OrderEntities = [new OrderEntity { TotalAmount = 50 }] }));
    }
 
    [Fact]
    public void Map_CollectionAll_RewritesCorrectly()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Orders.All(o => o.IsPaid);
        var rewritten = _mapper.MapExpression(predicate);
 
        Assert.True(Invoke(rewritten, new UserEntity { OrderEntities = [new OrderEntity { IsPaid = true }, new OrderEntity { IsPaid = true }] }));
        Assert.False(Invoke(rewritten, new UserEntity { OrderEntities = [new OrderEntity { IsPaid = true }, new OrderEntity { IsPaid = false }] }));
    }
 
    [Fact]
    public void Map_CollectionContains_RewritesCorrectly()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Tags.Contains("vip");
        var rewritten = _mapper.MapExpression(predicate);
 
        Assert.True(Invoke(rewritten, new UserEntity { Tags = ["vip", "member"] }));
        Assert.False(Invoke(rewritten, new UserEntity { Tags = ["member"] }));
    }
 
    [Fact]
    public void Map_CapturedPrimitiveClosure_RewritesCorrectly()
    {
        var capturedId = 42;
        Expression<Func<UserDto, bool>> predicate = dst => dst.Id == capturedId;
        var rewritten = _mapper.MapExpression(predicate);
 
        Assert.True(Invoke(rewritten, new UserEntity { UserId = 42 }));
        Assert.False(Invoke(rewritten, new UserEntity { UserId = 1 }));
    }
 
    [Fact]
    public void Map_CapturedStringClosure_RewritesCorrectly()
    {
        var capturedName = "Alice";
        Expression<Func<UserDto, bool>> predicate = dst => dst.Name == capturedName;
        var rewritten = _mapper.MapExpression(predicate);
 
        Assert.True(Invoke(rewritten, new UserEntity { FullName = "Alice" }));
        Assert.False(Invoke(rewritten, new UserEntity { FullName = "Bob" }));
    }
 
    [Fact]
    public void Map_NullableValueType_IsNull_RewritesCorrectly()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Score == null;
        var rewritten = _mapper.MapExpression(predicate);

        Assert.True(Invoke(rewritten, new UserEntity { Score = null }));
        Assert.False(Invoke(rewritten, new UserEntity { Score = 5 }));
    }

    [Fact]
    public void Map_NullableValueType_HasValue_RewritesCorrectly()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Score == 100;
        var rewritten = _mapper.MapExpression(predicate);

        Assert.True(Invoke(rewritten, new UserEntity { Score = 100 }));
        Assert.False(Invoke(rewritten, new UserEntity { Score = 99 }));
        Assert.False(Invoke(rewritten, new UserEntity { Score = null }));
    }

    [Fact]
    public void Map_CompoundNavigationProperties_RewritesCorrectly()
    {
        Expression<Func<UserDto, bool>> predicate =
            dst => dst.Address!.City == "London" && dst.Subscription!.PlanName == "Pro";
        var rewritten = _mapper.MapExpression(predicate);

        Assert.True(Invoke(rewritten, new UserEntity
        {
            Address = new AddressEntity { City = "London" },
            Subscription = new SubscriptionEntity { PlanName = "Pro" }
        }));
        Assert.False(Invoke(rewritten, new UserEntity
        {
            Address = new AddressEntity { City = "London" },
            Subscription = new SubscriptionEntity { PlanName = "Basic" }
        }));
    }

    [Fact]
    public void Map_StringIsNullOrEmpty_ThrowsUnsupportedExpressionException()
    {
        Expression<Func<UserDto, bool>> predicate = dst => string.IsNullOrEmpty(dst.Name);
        Assert.Throws<UnsupportedExpressionException>(() => _mapper.MapExpression(predicate));
    }

    [Fact]
    public void Map_CollectionCount_ThrowsUnsupportedExpressionException()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Orders.Count() > 2;
        Assert.Throws<UnsupportedExpressionException>(() => _mapper.MapExpression(predicate));
    }

    [Fact]
    public void Map_UnsupportedExpression_ThrowsUnsupportedExpressionException()
    {
        Expression<Func<UserDto, bool>> predicate = dst => dst.Name.ToUpper() == "ALICE";
        Assert.Throws<UnsupportedExpressionException>(() => _mapper.MapExpression(predicate));
    }

    // Covers: VisitMember throw + TryGetMapping false path.
    // A read-only property has no setter so Validate() skips it, but accessing it in
    // a predicate must still throw rather than silently produce a broken expression.
    [Fact]
    public void Map_UnmappedReadOnlyProperty_ThrowsUnsupportedExpressionException()
    {
        var mapper = new ReadOnlyPropMapper();
        Expression<Func<ReadOnlyPropDto, bool>> predicate = dst => dst.Label == "x";
        Assert.Throws<UnsupportedExpressionException>(() => mapper.MapExpression(predicate));
    }

    // Covers: GetNestedMapperForExpression returns null when the collection root is not destParam.
    // A local list captured in a closure is not a member of the destination — the visitor
    // cannot locate an element mapper for it.
    [Fact]
    public void Map_AnyOnLocalCollectionNotOnDestParam_ThrowsUnsupportedExpressionException()
    {
        var localOrders = new List<OrderDto>();
        Expression<Func<UserDto, bool>> predicate = dst => localOrders.Any(o => o.Total > 100);
        Assert.Throws<UnsupportedExpressionException>(() => _mapper.MapExpression(predicate));
    }

    // Covers: Enumerable.Contains static call (line 122 of IsSupportedMethod).
    // When the collection is typed as IEnumerable<T> the compiler emits Enumerable.Contains
    // rather than List<T>.Contains, which is a different code path in IsSupportedMethod.
    [Fact]
    public void Map_EnumerableContainsStaticCall_RewritesCorrectly()
    {
        IEnumerable<int> ids = new[] { 1, 2, 3 };
        Expression<Func<UserDto, bool>> predicate = dst => ids.Contains(dst.Id);
        var rewritten = _mapper.MapExpression(predicate);
        Assert.True(Invoke(rewritten, new UserEntity { UserId = 2 }));
        Assert.False(Invoke(rewritten, new UserEntity { UserId = 5 }));
    }

    // Covers: unsupported method whose arguments do not touch destParam passes through
    // (VisitMethodCall line 62) and the wildcard arm of TouchesDestParam (line 143).
    [Fact]
    public void Map_UnsupportedMethodNotTouchingDestParam_PassesThrough()
    {
        var nonEmpty = "non-empty";
        Expression<Func<UserDto, bool>> predicate = dst => dst.Id > 0 && string.IsNullOrEmpty(nonEmpty);
        var rewritten = _mapper.MapExpression(predicate);
        Assert.False(Invoke(rewritten, new UserEntity { UserId = 1 }));
    }

    // Covers: BinaryExpression arm (line 140) and UnaryExpression arm (line 141) of
    // TouchesDestParam. The bool result of `dst.Id > 0` is boxed to object by the
    // compiler, producing Convert(BinaryExpression) — a UnaryExpression wrapping a
    // BinaryExpression — as an argument to string.Format.
    [Fact]
    public void Map_UnsupportedMethodWithBinaryArgTouchingDestParam_Throws()
    {
        Expression<Func<UserDto, bool>> predicate = dst => string.Format("{0}", dst.Id > 0) == "True";
        Assert.Throws<UnsupportedExpressionException>(() => _mapper.MapExpression(predicate));
    }

    // Covers: UnaryExpression arm (line 141) of TouchesDestParam independently —
    // `!dst.IsActive` is a UnaryExpression without an outer BinaryExpression wrapper.
    [Fact]
    public void Map_UnsupportedMethodWithUnaryArgTouchingDestParam_Throws()
    {
        Expression<Func<UserDto, bool>> predicate = dst => string.Format("{0}", !dst.IsActive) == "False";
        Assert.Throws<UnsupportedExpressionException>(() => _mapper.MapExpression(predicate));
    }

    private static bool Invoke<T>(Expression<Func<T, bool>> expression, T target)
        => expression.Compile()(target);
}

public class ReadOnlyPropEntity { public int Id { get; set; } }

public class ReadOnlyPropDto
{
    public int Id { get; set; }
    public string Label => "x"; // no setter: Validate() skips it, but it is still unmapped
}

public class ReadOnlyPropMapper : EntityMapper<ReadOnlyPropEntity, ReadOnlyPropDto>
{
    protected override void Configure() => Map(src => src.Id, dst => dst.Id);
}
 
public class UserMapper : EntityMapper<UserEntity, UserDto>
{
    protected override void Configure()
    {
        Map(src => src.UserId, dst => dst.Id);
        Map(src => src.FullName, dst => dst.Name);
        Map(src => src.IsEnabled, dst => dst.IsActive);
        Map(src => src.Age, dst => dst.Age);
        Map(src => src.Email, dst => dst.Email);
        Map(src => src.Tags, dst => dst.Tags);
        Map(src => src.Score, dst => dst.Score);
        Map(src => src.Address, dst => dst.Address, new AddressMapper());
        Map(src => src.OrderEntities, dst => dst.Orders, new OrderMapper());
        Map(src => src.Subscription, dst => dst.Subscription, new SubscriptionMapper());
        Map(src => src.Children, dst => dst.Children, () => this);
    }
}
