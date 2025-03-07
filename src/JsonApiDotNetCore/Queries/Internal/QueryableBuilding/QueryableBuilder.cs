using System.Linq.Expressions;
using JetBrains.Annotations;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore.Metadata;

namespace JsonApiDotNetCore.Queries.Internal.QueryableBuilding;

/// <summary>
/// Drives conversion from <see cref="QueryLayer" /> into system <see cref="Expression" /> trees.
/// </summary>
[PublicAPI]
public class QueryableBuilder
{
    private readonly Expression _source;
    private readonly Type _elementType;
    private readonly Type _extensionType;
    private readonly LambdaParameterNameFactory _nameFactory;
    private readonly IResourceFactory _resourceFactory;
    private readonly IModel _entityModel;
    private readonly LambdaScopeFactory _lambdaScopeFactory;

    public QueryableBuilder(Expression source, Type elementType, Type extensionType, LambdaParameterNameFactory nameFactory, IResourceFactory resourceFactory,
        IModel entityModel, LambdaScopeFactory? lambdaScopeFactory = null)
    {
        ArgumentGuard.NotNull(source);
        ArgumentGuard.NotNull(elementType);
        ArgumentGuard.NotNull(extensionType);
        ArgumentGuard.NotNull(nameFactory);
        ArgumentGuard.NotNull(resourceFactory);
        ArgumentGuard.NotNull(entityModel);

        _source = source;
        _elementType = elementType;
        _extensionType = extensionType;
        _nameFactory = nameFactory;
        _resourceFactory = resourceFactory;
        _entityModel = entityModel;
        _lambdaScopeFactory = lambdaScopeFactory ?? new LambdaScopeFactory(_nameFactory);
    }

    public virtual Expression ApplyQuery(QueryLayer layer)
    {
        ArgumentGuard.NotNull(layer);

        Expression expression = _source;

        if (layer.Include != null)
        {
            expression = ApplyInclude(expression, layer.Include, layer.ResourceType);
        }

        if (layer.Filter != null)
        {
            expression = ApplyFilter(expression, layer.Filter);
        }

        if (layer.Sort != null)
        {
            expression = ApplySort(expression, layer.Sort);
        }

        if (layer.Pagination != null)
        {
            expression = ApplyPagination(expression, layer.Pagination);
        }

        if (layer.Selection is { IsEmpty: false })
        {
            expression = ApplySelection(expression, layer.Selection, layer.ResourceType);
        }

        return expression;
    }

    protected virtual Expression ApplyInclude(Expression source, IncludeExpression include, ResourceType resourceType)
    {
        using LambdaScope lambdaScope = _lambdaScopeFactory.CreateScope(_elementType);

        var builder = new IncludeClauseBuilder(source, lambdaScope, resourceType);
        return builder.ApplyInclude(include);
    }

    protected virtual Expression ApplyFilter(Expression source, FilterExpression filter)
    {
        using LambdaScope lambdaScope = _lambdaScopeFactory.CreateScope(_elementType);

        var builder = new WhereClauseBuilder(source, lambdaScope, _extensionType, _nameFactory);
        return builder.ApplyWhere(filter);
    }

    protected virtual Expression ApplySort(Expression source, SortExpression sort)
    {
        using LambdaScope lambdaScope = _lambdaScopeFactory.CreateScope(_elementType);

        var builder = new OrderClauseBuilder(source, lambdaScope, _extensionType);
        return builder.ApplyOrderBy(sort);
    }

    protected virtual Expression ApplyPagination(Expression source, PaginationExpression pagination)
    {
        using LambdaScope lambdaScope = _lambdaScopeFactory.CreateScope(_elementType);

        var builder = new SkipTakeClauseBuilder(source, lambdaScope, _extensionType);
        return builder.ApplySkipTake(pagination);
    }

    protected virtual Expression ApplySelection(Expression source, FieldSelection selection, ResourceType resourceType)
    {
        using LambdaScope lambdaScope = _lambdaScopeFactory.CreateScope(_elementType);

        var builder = new SelectClauseBuilder(source, lambdaScope, _entityModel, _extensionType, _nameFactory, _resourceFactory);
        return builder.ApplySelect(selection, resourceType);
    }
}
