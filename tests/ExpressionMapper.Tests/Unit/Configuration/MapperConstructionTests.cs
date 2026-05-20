using ExpressionMapper.Exceptions;
using ExpressionMapper.Mapping;
using ExpressionMapper.Tests.TestModels;

namespace ExpressionMapper.Tests.Unit.Configuration;
public class MapperConstructionTests
{
    [Fact]
    public void Constructor_WhenAllDestinationMembersAreMapped_DoesNotThrow()
    {
        var ex = Record.Exception(() => new FullyMappedUserMapper());
        Assert.Null(ex);
    }
 
    [Fact]
    public void Constructor_WhenAllNestedMappersAreFactoryBased_DoesNotThrow()
    {
        var ex = Record.Exception(() => new UserMapperWithFactoryNestedMappers());
        Assert.Null(ex);
    }
 
    [Fact]
    public void Constructor_WhenSelfReferentialFactoryProvided_DoesNotThrow()
    {
        var ex = Record.Exception(() => new SelfReferentialUserMapper());
        Assert.Null(ex);
    }
 
    [Fact]
    public void Constructor_WhenDestinationMemberIsUnmapped_ThrowsInvalidMappingException()
    {
        Assert.Throws<InvalidMappingException>(() => new PartiallyMappedUserMapper());
    }
 
    [Fact]
    public void Constructor_InvalidMappingException_IncludesUnmappedMemberName()
    {
        var ex = Assert.Throws<InvalidMappingException>(() => new PartiallyMappedUserMapper());
        Assert.Contains("Name", ex.Message);
    }
 
    [Fact]
    public void Constructor_WhenMultipleMembersUnmapped_ExceptionListsAllOfThem()
    {
        var ex = Assert.Throws<InvalidMappingException>(() => new EmptyUserMapper());
        Assert.Contains("Id", ex.Message);
        Assert.Contains("Name", ex.Message);
        Assert.Contains("IsActive", ex.Message);
        Assert.Contains("Score", ex.Message);
    }

    [Fact]
    public void Constructor_WhenDestinationMemberIsMappedTwice_ThrowsInvalidMappingException()
    {
        Assert.Throws<InvalidMappingException>(() => new DoubleMappedUserMapper());
    }

    [Fact]
    public void Constructor_WhenNestedFactoryMapperIsInvalid_ThrowsInvalidMappingException()
    {
        Assert.Throws<InvalidMappingException>(() => new UserMapperWithInvalidNestedFactory());
    }
 
    private class FullyMappedUserMapper : EntityMapper<UserEntity, UserDto>
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

    private class UserMapperWithFactoryNestedMappers : EntityMapper<UserEntity, UserDto>
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
            Map(src => src.Address, dst => dst.Address, () => new AddressMapper());
            Map(src => src.OrderEntities, dst => dst.Orders, () => new OrderMapper());
            Map(src => src.Subscription, dst => dst.Subscription, () => new SubscriptionMapper());
            Map(src => src.Children, dst => dst.Children, () => this);
        }
    }

    private class SelfReferentialUserMapper : EntityMapper<UserEntity, UserDto>
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

    private class PartiallyMappedUserMapper : EntityMapper<UserEntity, UserDto>
    {
        protected override void Configure()
        {
            Map(src => src.UserId, dst => dst.Id);
        }
    }

    private class EmptyUserMapper : EntityMapper<UserEntity, UserDto>
    {
        protected override void Configure() { }
    }

    private class DoubleMappedUserMapper : EntityMapper<UserEntity, UserDto>
    {
        protected override void Configure()
        {
            Map(src => src.UserId, dst => dst.Id);
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

    private class InvalidAddressMapper : EntityMapper<AddressEntity, AddressDto>
    {
        protected override void Configure() { }
    }

    private class UserMapperWithInvalidNestedFactory : EntityMapper<UserEntity, UserDto>
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
            Map(src => src.Address, dst => dst.Address, () => new InvalidAddressMapper());
            Map(src => src.OrderEntities, dst => dst.Orders, new OrderMapper());
            Map(src => src.Subscription, dst => dst.Subscription, new SubscriptionMapper());
            Map(src => src.Children, dst => dst.Children, () => this);
        }
    }
}
 
public class AddressMapper : EntityMapper<AddressEntity, AddressDto>
{
    protected override void Configure()
    {
        Map(src => src.City, dst => dst.City);
        Map(src => src.Country, dst => dst.Country);
    }
}
 
public class OrderMapper : EntityMapper<OrderEntity, OrderDto>
{
    protected override void Configure()
    {
        Map(src => src.TotalAmount, dst => dst.Total);
        Map(src => src.IsPaid, dst => dst.IsPaid);
    }
}
 
public class SubscriptionMapper : EntityMapper<SubscriptionEntity, SubscriptionDto>
{
    protected override void Configure()
    {
        Map(src => src.PlanName, dst => dst.PlanName);
        Map(src => src.IsActive, dst => dst.IsActive);
    }
}
