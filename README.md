# Expression Mapping Library

[![CI](https://github.com/thespragg/ExpressionMapper/actions/workflows/ci.yml/badge.svg)](https://github.com/thespragg/ExpressionMapper/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/thespragg/ExpressionMapper/graph/badge.svg)](https://codecov.io/gh/thespragg/ExpressionMapper)
[![NuGet](https://img.shields.io/nuget/v/ExpressionMapper.svg)](https://www.nuget.org/packages/ExpressionMapper/)

A lightweight library for rewriting `Expression<Func<TDestination, bool>>` predicates into `Expression<Func<TSource, bool>>` predicates. Generates SQL-translatable expressions for use where you want to expose generic higher-order functions without leaking your internal persistence model.

---

## Quick Start

Define a mapper for each source/destination pair by subclassing `EntityMapper<TSource, TDestination>` and implementing `Configure()`:

```csharp
public class UserMapper : EntityMapper<UserEntity, UserDto>
{
    protected override void Configure()
    {
        Map(src => src.UserId,   dst => dst.Id);
        Map(src => src.FullName, dst => dst.Name);
        Map(src => src.IsEnabled, dst => dst.IsActive);
    }
}
```

Use it in your repository:

```csharp
public IEnumerable<UserDto> Find(Expression<Func<UserDto, bool>> predicate)
{
    var mapper = new UserMapper();
    var mappedPredicate = mapper.Map(predicate);
    return _dbSet.Where(mappedPredicate).Select(src => MapToDto(src));
}
```

---

## Nested Mappers

When a property is itself a mapped type, pass a mapper instance for the nested type:

```csharp
public class UserMapper : EntityMapper<UserEntity, UserDto>
{
    protected override void Configure()
    {
        Map(src => src.UserId,       dst => dst.Id);
        Map(src => src.FullName,     dst => dst.Name);
        Map(src => src.Address,      dst => dst.Address,      new AddressMapper());
        Map(src => src.OrderEntities, dst => dst.Orders,      new OrderMapper());
    }
}
```

For self-referential types, pass a factory to defer construction:

```csharp
public class CategoryMapper : EntityMapper<CategoryEntity, CategoryDto>
{
    protected override void Configure()
    {
        Map(src => src.Id,       dst => dst.Id);
        Map(src => src.Name,     dst => dst.Name);
        Map(src => src.Children, dst => dst.Children, () => this);
    }
}
```

---

## Supported Predicate Patterns

These are the expression node types the library rewrites. All are verified to produce valid SQL via EF Core.

| Pattern | Example |
|---|---|
| Member access equality | `dst.Id == id` |
| Comparison operators | `dst.Age > 18` |
| Boolean binary (`&&`, `\|\|`) | `dst.IsActive && dst.Age > 18` |
| Unary negation | `!dst.IsActive` |
| Null checks | `dst.Address != null` |
| Navigation property chaining | `dst.Address.City == "London"` |
| String methods | `dst.Name.StartsWith("A")` |
| Collection: `Any` | `dst.Orders.Any(o => o.Total > 100)` |
| Collection: `All` | `dst.Orders.All(o => o.IsPaid)` |
| Collection: `Contains` | `dst.Tags.Contains("vip")` |
| Captured primitive closures | `dst.Id == capturedVar` |

---

## Validation

All destination members must be mapped. The library validates this at construction time — misconfigured mappers throw `InvalidMappingException` before any predicate is ever rewritten:

```csharp
// Throws InvalidMappingException: unmapped destination members: Name, IsActive
public class IncompleteMapper : EntityMapper<UserEntity, UserDto>
{
    protected override void Configure()
    {
        Map(src => src.UserId, dst => dst.Id);
        // Name and IsActive not mapped — caught immediately on instantiation
    }
}
```

Unmapped source members are safe to omit — they will never appear in a destination expression.

---

## Known Limitations

- Method calls beyond `Any`, `All`, `Contains`, and the string methods `StartsWith`, `EndsWith`, `Contains` are not rewritten and will throw `UnsupportedExpressionException` at map time
- Composite source expressions (e.g. mapping `src.First + " " + src.Last` to `dst.FullName`) are not supported in v1 — both sides of a mapping must be simple member access
- Closures over complex captured objects should be tested against your ORM — captured primitives and local collections are handled correctly

---

## How It Fits Into The Repository Pattern

This library sits inside repository implementations, invisible to consumers. The service layer works exclusively with DTO expressions. The repository is the only location that knows a persistence model exists.

```
Service Layer  →  Expression<Func<UserDto, bool>>
Repository     →  EntityMapper rewrites to Expression<Func<UserEntity, bool>>
ORM / Database →  SQL
```