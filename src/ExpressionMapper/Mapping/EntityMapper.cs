using System.Linq.Expressions;

namespace ExpressionMapper.Mapping;

public abstract class EntityMapper<TSource, TDestination>
{
    protected EntityMapper()
    {
        // ReSharper disable once VirtualMemberCallInConstructor
        Configure();
        Validate();
    }

    protected abstract void Configure();

    protected void Map<TProperty>(
        Expression<Func<TSource, TProperty>> source,
        Expression<Func<TDestination, TProperty>> destination)
    {
        throw new NotImplementedException();
    }

    protected void Map<TSourceProp, TDestProp>(
        Expression<Func<TSource, TSourceProp?>> source,
        Expression<Func<TDestination, TDestProp?>> destination,
        EntityMapper<TSourceProp, TDestProp> mapper)
        where TSourceProp : class
        where TDestProp : class
    {
        throw new NotImplementedException();
    }

    protected void Map<TSourceProp, TDestProp>(
        Expression<Func<TSource, TSourceProp?>> source,
        Expression<Func<TDestination, TDestProp?>> destination,
        Func<EntityMapper<TSourceProp, TDestProp>> mapperFactory)
        where TSourceProp : class
        where TDestProp : class
    {
        throw new NotImplementedException();
    }

    protected void Map<TSourceElement, TDestElement>(
        Expression<Func<TSource, IEnumerable<TSourceElement>>> source,
        Expression<Func<TDestination, IEnumerable<TDestElement>>> destination,
        EntityMapper<TSourceElement, TDestElement> mapper)
        where TSourceElement : class
        where TDestElement : class
    {
        throw new NotImplementedException();
    }

    protected void Map<TSourceElement, TDestElement>(
        Expression<Func<TSource, IEnumerable<TSourceElement>>> source,
        Expression<Func<TDestination, IEnumerable<TDestElement>>> destination,
        Func<EntityMapper<TSourceElement, TDestElement>> mapperFactory)
        where TSourceElement : class
        where TDestElement : class
    {
        throw new NotImplementedException();
    }

    public Expression<Func<TSource, bool>> Map(Expression<Func<TDestination, bool>> predicate)
    {
        throw new NotImplementedException();
    }

    private void Validate()
    {
        throw new NotImplementedException();
    }
}