using System.Linq.Expressions;

namespace AabuReports.Core.Extensions;

public static class ExpressionExtensions
{
    /// <summary> joins two predicates with &amp;&amp; into one that EF Core can translate to a single WHERE </summary>
    public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
    {
        var parameter = Expression.Parameter(typeof(T), left.Parameters[0].Name);

        var body = Expression.AndAlso(
            new ReplaceParameter(left.Parameters[0], parameter).Visit(left.Body)!,
            new ReplaceParameter(right.Parameters[0], parameter).Visit(right.Body)!);

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    /// <summary> makes both predicates use the same parameter </summary>
    private sealed class ReplaceParameter(ParameterExpression from, Expression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
