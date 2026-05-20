using System.Linq.Expressions;
using System.Reflection;
using PredicateMapper.Exceptions;

namespace PredicateMapper.Expressions;

internal sealed class ExpressionMapperVisitor(
    ParameterExpression destParam,
    ParameterExpression srcParam,
    IEntityMapper mapper
) : ExpressionVisitor
{
    protected override Expression VisitParameter(ParameterExpression node)
        => node == destParam ? srcParam : base.VisitParameter(node);

    protected override Expression VisitMember(MemberExpression node)
    {
        var chain = new List<MemberExpression>();
        Expression current = node;
        while (current is MemberExpression me)
        {
            chain.Add(me);
            current = me.Expression!;
        }

        if (current is not ParameterExpression param || param != destParam)
            return base.VisitMember(node);

        chain.Reverse(); 

        Expression result = srcParam;
        var currentMapper = mapper;

        foreach (var memberExpr in chain)
        {
            if (!currentMapper.TryGetMapping(memberExpr.Member, out var srcMember, out var nested))
                throw new UnsupportedExpressionException(
                    $"No mapping found for destination member '{memberExpr.Member.Name}'.");

            result = Expression.MakeMemberAccess(result, srcMember!);
            if (nested != null)
                currentMapper = nested;
        }

        return result;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        var method = node.Method;

        if (method.DeclaringType == typeof(Enumerable) && node.Arguments.Count == 2 && method.Name is "Any" or "All")
            return RewriteCollectionPredicateCall(node);

        if (IsSupportedMethod(method))
            return base.VisitMethodCall(node);

        if (TouchesDestParam(node))
            throw new UnsupportedExpressionException(
                $"Method '{method.Name}' is not supported in expression mapping.");

        return base.VisitMethodCall(node);
    }

    private Expression RewriteCollectionPredicateCall(MethodCallExpression node)
    {
        var rewrittenCollection = Visit(node.Arguments[0])!;

        var nestedMapper = GetNestedMapperForExpression(node.Arguments[0])
                           ?? throw new UnsupportedExpressionException(
                               "Cannot rewrite collection predicate: no element mapper registered for this collection.");

        var innerLambda = (LambdaExpression)node.Arguments[1];
        var destElementParam = innerLambda.Parameters[0];
        var srcElementParam = Expression.Parameter(nestedMapper.SourceType, destElementParam.Name);

        var innerVisitor = new ExpressionMapperVisitor(destElementParam, srcElementParam, nestedMapper);
        var rewrittenBody = innerVisitor.Visit(innerLambda.Body)!;
        var rewrittenLambda = Expression.Lambda(rewrittenBody, srcElementParam);

        var newMethod = node.Method.GetGenericMethodDefinition().MakeGenericMethod(nestedMapper.SourceType);
        return Expression.Call(null, newMethod, rewrittenCollection, rewrittenLambda);
    }

    private IEntityMapper? GetNestedMapperForExpression(Expression expr)
    {
        var chain = new List<MemberExpression>();
        Expression current = expr;
        while (current is MemberExpression me)
        {
            chain.Add(me);
            current = me.Expression!;
        }

        if (current is not ParameterExpression param || param != destParam)
            return null;

        chain.Reverse();

        IEntityMapper? nested = null;
        var currentMapper = mapper;

        foreach (var memberExpr in chain)
        {
            if (!currentMapper.TryGetMapping(memberExpr.Member, out _, out nested))
                return null;
            if (nested != null)
                currentMapper = nested;
        }

        return nested;
    }

    private static bool IsSupportedMethod(MethodInfo method)
    {
        var dt = method.DeclaringType;

        if (dt == typeof(string) && method.Name is "StartsWith" or "EndsWith" or "Contains")
            return true;

        if (dt == typeof(Enumerable) && method.Name == "Contains")
            return true;

        return dt is not null && method.Name == "Contains" && IsGenericCollectionType(dt);
    }

    private static bool IsGenericCollectionType(Type type)
    {
        if (!type.IsGenericType) return false;
        var def = type.GetGenericTypeDefinition();
        return def == typeof(List<>) || def == typeof(ICollection<>) || def == typeof(IList<>);
    }

    private bool TouchesDestParam(Expression expr) => expr switch
    {
        ParameterExpression p => p == destParam,
        MemberExpression m => m.Expression is not null && TouchesDestParam(m.Expression),
        MethodCallExpression mc => (mc.Object is not null && TouchesDestParam(mc.Object))
                                   || mc.Arguments.Any(TouchesDestParam),
        BinaryExpression b => TouchesDestParam(b.Left) || TouchesDestParam(b.Right),
        UnaryExpression u => TouchesDestParam(u.Operand),
        LambdaExpression l => TouchesDestParam(l.Body),
        _ => false
    };
}