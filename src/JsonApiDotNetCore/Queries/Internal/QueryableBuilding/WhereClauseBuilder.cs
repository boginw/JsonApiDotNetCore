using System.Collections;
using System.Linq.Expressions;
using JetBrains.Annotations;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Resources.Internal;

namespace JsonApiDotNetCore.Queries.Internal.QueryableBuilding;

/// <summary>
/// Transforms <see cref="FilterExpression" /> into
/// <see cref="Queryable.Where{TSource}(IQueryable{TSource}, System.Linq.Expressions.Expression{System.Func{TSource,bool}})" /> calls.
/// </summary>
[PublicAPI]
public class WhereClauseBuilder : QueryClauseBuilder<object?>
{
    private static readonly CollectionConverter CollectionConverter = new();
    private static readonly ConstantExpression NullConstant = Expression.Constant(null);

    private readonly Expression _source;
    private readonly Type _extensionType;
    private readonly LambdaParameterNameFactory _nameFactory;

    public WhereClauseBuilder(Expression source, LambdaScope lambdaScope, Type extensionType, LambdaParameterNameFactory nameFactory)
        : base(lambdaScope)
    {
        ArgumentGuard.NotNull(source);
        ArgumentGuard.NotNull(extensionType);
        ArgumentGuard.NotNull(nameFactory);

        _source = source;
        _extensionType = extensionType;
        _nameFactory = nameFactory;
    }

    public Expression ApplyWhere(FilterExpression filter)
    {
        ArgumentGuard.NotNull(filter);

        LambdaExpression lambda = GetPredicateLambda(filter);

        return WhereExtensionMethodCall(lambda);
    }

    private LambdaExpression GetPredicateLambda(FilterExpression filter)
    {
        Expression body = Visit(filter, null);
        return Expression.Lambda(body, LambdaScope.Parameter);
    }

    private Expression WhereExtensionMethodCall(LambdaExpression predicate)
    {
        return Expression.Call(_extensionType, "Where", LambdaScope.Parameter.Type.AsArray(), _source, predicate);
    }

    public override Expression VisitHas(HasExpression expression, object? argument)
    {
        Expression property = Visit(expression.TargetCollection, argument);

        Type? elementType = CollectionConverter.FindCollectionElementType(property.Type);

        if (elementType == null)
        {
            throw new InvalidOperationException("Expression must be a collection.");
        }

        Expression? predicate = null;

        if (expression.Filter != null)
        {
            var lambdaScopeFactory = new LambdaScopeFactory(_nameFactory);
            using LambdaScope lambdaScope = lambdaScopeFactory.CreateScope(elementType);

            var builder = new WhereClauseBuilder(property, lambdaScope, typeof(Enumerable), _nameFactory);
            predicate = builder.GetPredicateLambda(expression.Filter);
        }

        return AnyExtensionMethodCall(elementType, property, predicate);
    }

    private static MethodCallExpression AnyExtensionMethodCall(Type elementType, Expression source, Expression? predicate)
    {
        return predicate != null
            ? Expression.Call(typeof(Enumerable), "Any", elementType.AsArray(), source, predicate)
            : Expression.Call(typeof(Enumerable), "Any", elementType.AsArray(), source);
    }

    public override Expression VisitIsType(IsTypeExpression expression, object? argument)
    {
        Expression property = expression.TargetToOneRelationship != null ? Visit(expression.TargetToOneRelationship, argument) : LambdaScope.Accessor;
        TypeBinaryExpression typeCheck = Expression.TypeIs(property, expression.DerivedType.ClrType);

        if (expression.Child == null)
        {
            return typeCheck;
        }

        UnaryExpression derivedAccessor = Expression.Convert(property, expression.DerivedType.ClrType);
        Expression filter = WithLambdaScopeAccessor(derivedAccessor, () => Visit(expression.Child, argument));

        return Expression.AndAlso(typeCheck, filter);
    }

    public override Expression VisitMatchText(MatchTextExpression expression, object? argument)
    {
        Expression property = Visit(expression.TargetAttribute, argument);

        if (property.Type != typeof(string))
        {
            throw new InvalidOperationException("Expression must be a string.");
        }

        Expression text = Visit(expression.TextValue, property.Type);

        if (expression.MatchKind == TextMatchKind.StartsWith)
        {
            return Expression.Call(property, "StartsWith", null, text);
        }

        if (expression.MatchKind == TextMatchKind.EndsWith)
        {
            return Expression.Call(property, "EndsWith", null, text);
        }

        return Expression.Call(property, "Contains", null, text);
    }

    public override Expression VisitAny(AnyExpression expression, object? argument)
    {
        Expression property = Visit(expression.TargetAttribute, argument);

        var valueList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(property.Type))!;

        foreach (LiteralConstantExpression constant in expression.Constants)
        {
            valueList.Add(constant.TypedValue);
        }

        ConstantExpression collection = Expression.Constant(valueList);
        return ContainsExtensionMethodCall(collection, property);
    }

    private static Expression ContainsExtensionMethodCall(Expression collection, Expression value)
    {
        return Expression.Call(typeof(Enumerable), "Contains", value.Type.AsArray(), collection, value);
    }

    public override Expression VisitLogical(LogicalExpression expression, object? argument)
    {
        var termQueue = new Queue<Expression>(expression.Terms.Select(filter => Visit(filter, argument)));

        if (expression.Operator == LogicalOperator.And)
        {
            return Compose(termQueue, Expression.AndAlso);
        }

        if (expression.Operator == LogicalOperator.Or)
        {
            return Compose(termQueue, Expression.OrElse);
        }

        throw new InvalidOperationException($"Unknown logical operator '{expression.Operator}'.");
    }

    private static BinaryExpression Compose(Queue<Expression> argumentQueue, Func<Expression, Expression, BinaryExpression> applyOperator)
    {
        Expression left = argumentQueue.Dequeue();
        Expression right = argumentQueue.Dequeue();

        BinaryExpression tempExpression = applyOperator(left, right);

        while (argumentQueue.Any())
        {
            Expression nextArgument = argumentQueue.Dequeue();
            tempExpression = applyOperator(tempExpression, nextArgument);
        }

        return tempExpression;
    }

    public override Expression VisitNot(NotExpression expression, object? argument)
    {
        Expression child = Visit(expression.Child, argument);
        return Expression.Not(child);
    }

    public override Expression VisitComparison(ComparisonExpression expression, object? argument)
    {
        Type commonType = ResolveCommonType(expression.Left, expression.Right);

        Expression left = WrapInConvert(Visit(expression.Left, argument), commonType);
        Expression right = WrapInConvert(Visit(expression.Right, argument), commonType);

        return expression.Operator switch
        {
            ComparisonOperator.Equals => Expression.Equal(left, right),
            ComparisonOperator.LessThan => Expression.LessThan(left, right),
            ComparisonOperator.LessOrEqual => Expression.LessThanOrEqual(left, right),
            ComparisonOperator.GreaterThan => Expression.GreaterThan(left, right),
            ComparisonOperator.GreaterOrEqual => Expression.GreaterThanOrEqual(left, right),
            _ => throw new InvalidOperationException($"Unknown comparison operator '{expression.Operator}'.")
        };
    }

    private Type ResolveCommonType(QueryExpression left, QueryExpression right)
    {
        Type leftType = ResolveFixedType(left);

        if (RuntimeTypeConverter.CanContainNull(leftType))
        {
            return leftType;
        }

        if (right is NullConstantExpression)
        {
            return typeof(Nullable<>).MakeGenericType(leftType);
        }

        Type? rightType = TryResolveFixedType(right);

        if (rightType != null && RuntimeTypeConverter.CanContainNull(rightType))
        {
            return rightType;
        }

        return leftType;
    }

    private Type ResolveFixedType(QueryExpression expression)
    {
        Expression result = Visit(expression, null);
        return result.Type;
    }

    private Type? TryResolveFixedType(QueryExpression expression)
    {
        if (expression is CountExpression)
        {
            return typeof(int);
        }

        if (expression is ResourceFieldChainExpression chain)
        {
            Expression child = Visit(chain, null);
            return child.Type;
        }

        return null;
    }

    private static Expression WrapInConvert(Expression expression, Type? targetType)
    {
        try
        {
            return targetType != null && expression.Type != targetType ? Expression.Convert(expression, targetType) : expression;
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidQueryException("Query creation failed due to incompatible types.", exception);
        }
    }

    public override Expression VisitNullConstant(NullConstantExpression expression, object? argument)
    {
        return NullConstant;
    }

    public override Expression VisitLiteralConstant(LiteralConstantExpression expression, object? argument)
    {
        Type type = expression.TypedValue.GetType();
        return expression.TypedValue.CreateTupleAccessExpressionForConstant(type);
    }
}
