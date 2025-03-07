using System.Linq.Expressions;
using JetBrains.Annotations;
using JsonApiDotNetCore.Queries.Expressions;

namespace JsonApiDotNetCore.Queries.Internal.QueryableBuilding;

/// <summary>
/// Transforms <see cref="PaginationExpression" /> into <see cref="Queryable.Skip{TSource}" /> and
/// <see cref="Queryable.Take{TSource}(IQueryable{TSource},int)" /> calls.
/// </summary>
[PublicAPI]
public class SkipTakeClauseBuilder : QueryClauseBuilder<object?>
{
    private readonly Expression _source;
    private readonly Type _extensionType;

    public SkipTakeClauseBuilder(Expression source, LambdaScope lambdaScope, Type extensionType)
        : base(lambdaScope)
    {
        ArgumentGuard.NotNull(source);
        ArgumentGuard.NotNull(extensionType);

        _source = source;
        _extensionType = extensionType;
    }

    public Expression ApplySkipTake(PaginationExpression expression)
    {
        ArgumentGuard.NotNull(expression);

        return Visit(expression, null);
    }

    public override Expression VisitPagination(PaginationExpression expression, object? argument)
    {
        Expression skipTakeExpression = _source;

        if (expression.PageSize != null)
        {
            int skipValue = (expression.PageNumber.OneBasedValue - 1) * expression.PageSize.Value;

            if (skipValue > 0)
            {
                skipTakeExpression = ExtensionMethodCall(skipTakeExpression, "Skip", skipValue);
            }

            skipTakeExpression = ExtensionMethodCall(skipTakeExpression, "Take", expression.PageSize.Value);
        }

        return skipTakeExpression;
    }

    private Expression ExtensionMethodCall(Expression source, string operationName, int value)
    {
        Expression constant = value.CreateTupleAccessExpressionForConstant(typeof(int));

        return Expression.Call(_extensionType, operationName, LambdaScope.Parameter.Type.AsArray(), source, constant);
    }
}
