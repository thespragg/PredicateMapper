using System.Linq.Expressions;
using System.Reflection;
using PredicateMapper.Exceptions;
using PredicateMapper.Expressions;

namespace PredicateMapper.Mapping;

/// <summary>
/// Creates an expression mapping configuration between a source and its destination.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TDestination">The destination type.</typeparam>
public abstract class EntityMapper<TSource, TDestination> : IEntityMapper
{
    private readonly struct MappingEntry(MemberInfo sourceMember, object? nestedMapper = null)
    {
        public MemberInfo SourceMember { get; } = sourceMember;
        public object? NestedMapper { get; } = nestedMapper;
    }

    private readonly Dictionary<MemberInfo, MappingEntry> _mappings = new();

    /// <summary>
    /// Initialises a new instance of <see cref="EntityMapper{TSource,TDestination}"/>.
    /// </summary>
    protected EntityMapper()
    {
        // ReSharper disable once VirtualMemberCallInConstructor
        Configure();
        Validate();
    }

    /// <summary>
    /// Defines the mappings between <typeparamref name="TSource"/> properties and their <typeparamref name="TDestination"/> counterparts.
    /// </summary>
    protected abstract void Configure();

    /// <summary>
    /// Maps a <typeparamref name="TSource"/> property to a <typeparamref name="TDestination"/> property of the same type.
    /// </summary>
    /// <param name="source">A lambda selecting the source property.</param>
    /// <param name="destination">A lambda selecting the destination property.</param>
    /// <typeparam name="TProperty">The property type.</typeparam>
    protected void Map<TProperty>(
        Expression<Func<TSource, TProperty>> source,
        Expression<Func<TDestination, TProperty>> destination)
    {
        var srcMember = ((MemberExpression)source.Body).Member;
        var destMember = ((MemberExpression)destination.Body).Member;
        RegisterMapping(destMember, srcMember, null);
    }

    /// <summary>
    /// Maps a nested source object to a destination object using a provided mapper instance.
    /// </summary>
    /// <param name="source">A lambda selecting the source navigation property.</param>
    /// <param name="destination">A lambda selecting the destination navigation property.</param>
    /// <param name="mapper">The mapper to use for the nested object.</param>
    /// <typeparam name="TSourceProp">The source navigation property type.</typeparam>
    /// <typeparam name="TDestProp">The destination navigation property type.</typeparam>
    protected void Map<TSourceProp, TDestProp>(
        Expression<Func<TSource, TSourceProp?>> source,
        Expression<Func<TDestination, TDestProp?>> destination,
        EntityMapper<TSourceProp, TDestProp> mapper)
        where TSourceProp : class
        where TDestProp : class
    {
        var srcMember = ((MemberExpression)source.Body).Member;
        var destMember = ((MemberExpression)destination.Body).Member;
        RegisterMapping(destMember, srcMember, mapper);
    }

    /// <summary>
    /// Maps a nested source object to a destination object using a provided mapper factory.
    /// The factory is invoked eagerly during construction.
    /// </summary>
    /// <param name="source">A lambda selecting the source navigation property.</param>
    /// <param name="destination">A lambda selecting the destination navigation property.</param>
    /// <param name="mapperFactory">A factory that returns the mapper for the nested object.</param>
    /// <typeparam name="TSourceProp">The source navigation property type.</typeparam>
    /// <typeparam name="TDestProp">The destination navigation property type.</typeparam>
    protected void Map<TSourceProp, TDestProp>(
        Expression<Func<TSource, TSourceProp?>> source,
        Expression<Func<TDestination, TDestProp?>> destination,
        Func<EntityMapper<TSourceProp, TDestProp>> mapperFactory)
        where TSourceProp : class
        where TDestProp : class
    {
        Map(source, destination, mapperFactory());
    }

    /// <summary>
    /// Maps a source collection to a destination collection using a provided mapper instance.
    /// </summary>
    /// <param name="source">A lambda selecting the source collection property.</param>
    /// <param name="destination">A lambda selecting the destination collection property.</param>
    /// <param name="mapper">The mapper to use for each element.</param>
    /// <typeparam name="TSourceElement">The source element type.</typeparam>
    /// <typeparam name="TDestElement">The destination element type.</typeparam>
    protected void Map<TSourceElement, TDestElement>(
        Expression<Func<TSource, IEnumerable<TSourceElement>>> source,
        Expression<Func<TDestination, IEnumerable<TDestElement>>> destination,
        EntityMapper<TSourceElement, TDestElement> mapper)
        where TSourceElement : class
        where TDestElement : class
    {
        var srcMember = ((MemberExpression)source.Body).Member;
        var destMember = ((MemberExpression)destination.Body).Member;
        RegisterMapping(destMember, srcMember, mapper);
    }

    /// <summary>
    /// Maps a source collection to a destination collection using a provided mapper factory.
    /// The factory is invoked eagerly during construction.
    /// </summary>
    /// <param name="source">A lambda selecting the source collection property.</param>
    /// <param name="destination">A lambda selecting the destination collection property.</param>
    /// <param name="mapperFactory">A factory that returns the mapper for each element.</param>
    /// <typeparam name="TSourceElement">The source element type.</typeparam>
    /// <typeparam name="TDestElement">The destination element type.</typeparam>
    protected void Map<TSourceElement, TDestElement>(
        Expression<Func<TSource, IEnumerable<TSourceElement>>> source,
        Expression<Func<TDestination, IEnumerable<TDestElement>>> destination,
        Func<EntityMapper<TSourceElement, TDestElement>> mapperFactory)
        where TSourceElement : class
        where TDestElement : class
    {
        Map(source, destination, mapperFactory());
    }

    /// <summary>
    /// Rewrites a predicate expressed in terms of <typeparamref name="TDestination"/> into an
    /// equivalent predicate expressed in terms of <typeparamref name="TSource"/>.
    /// </summary>
    /// <param name="predicate">The destination predicate to rewrite.</param>
    /// <returns>An equivalent predicate over <typeparamref name="TSource"/>.</returns>
    /// <exception cref="UnsupportedExpressionException">
    /// Thrown when the predicate contains an expression that cannot be rewritten.
    /// </exception>
    public Expression<Func<TSource, bool>> MapExpression(Expression<Func<TDestination, bool>> predicate)
    {
        var srcParam = Expression.Parameter(typeof(TSource), predicate.Parameters[0].Name);
        var visitor = new ExpressionMapperVisitor(predicate.Parameters[0], srcParam, this);
        var newBody = visitor.Visit(predicate.Body)!;
        return Expression.Lambda<Func<TSource, bool>>(newBody, srcParam);
    }

    Type IEntityMapper.SourceType => typeof(TSource);

    bool IEntityMapper.TryGetMapping(
        MemberInfo destMember,
        out MemberInfo? srcMember,
        out IEntityMapper? nestedMapper
    )
    {
        if (_mappings.TryGetValue(destMember, out var entry))
        {
            srcMember = entry.SourceMember;
            nestedMapper = entry.NestedMapper as IEntityMapper;
            return true;
        }

        srcMember = null;
        nestedMapper = null;
        return false;
    }

    private void RegisterMapping(MemberInfo destMember, MemberInfo srcMember, object? nestedMapper)
    {
        if (_mappings.ContainsKey(destMember))
            throw new InvalidMappingException($"Destination member '{destMember.Name}' is already mapped.");

        _mappings[destMember] = new MappingEntry(srcMember, nestedMapper);
    }

    private void Validate()
    {
        var unmapped = typeof(TDestination)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && !_mappings.ContainsKey(p))
            .Select(p => p.Name)
            .ToList();

        if (unmapped.Count > 0)
            throw new InvalidMappingException(
                $"The following destination members are not mapped: {string.Join(", ", unmapped)}");
    }
}