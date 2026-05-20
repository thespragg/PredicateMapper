namespace ExpressionMapper.Tests.TestModels;

public class UserEntity
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int Age { get; set; }
    public string? Email { get; set; }
    public AddressEntity? Address { get; set; }
    public List<OrderEntity> OrderEntities { get; set; } = [];
    public List<UserEntity> Children { get; set; } = [];
    public SubscriptionEntity? Subscription { get; set; }
    public List<string> Tags { get; set; } = [];
}
 
public class AddressEntity
{
    public int Id { get; set; }
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}
 
public class OrderEntity
{
    public int Id { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsPaid { get; set; }
}
 
public class SubscriptionEntity
{
    public int Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}