using System.Reflection;

namespace ExpressionMapper.Expressions;

internal interface IEntityMapper
{
    Type SourceType { get; }

    bool TryGetMapping(
        MemberInfo destMember,
        out MemberInfo? srcMember,
        out IEntityMapper? nestedMapper
    );
}