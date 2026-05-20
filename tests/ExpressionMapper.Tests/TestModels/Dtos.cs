namespace ExpressionMapper.Tests.TestModels;

public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int Age { get; set; }
    public string? Email { get; set; }
    public AddressDto? Address { get; set; }
    public List<OrderDto> Orders { get; set; } = [];
    public List<UserDto> Children { get; set; } = [];
    public SubscriptionDto? Subscription { get; set; }
    public List<string> Tags { get; set; } = [];
    public int? Score { get; set; }
}
 
public class AddressDto
{
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}
 
public class OrderDto
{
    public decimal Total { get; set; }
    public bool IsPaid { get; set; }
}
 
public class SubscriptionDto
{
    public string PlanName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

