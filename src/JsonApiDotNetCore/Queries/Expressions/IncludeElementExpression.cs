using System.Collections.Immutable;
using System.Text;
using JetBrains.Annotations;
using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCore.Queries.Expressions;

/// <summary>
/// Represents an element in <see cref="IncludeExpression" />.
/// </summary>
[PublicAPI]
public class IncludeElementExpression : QueryExpression
{
    public RelationshipAttribute Relationship { get; }
    public IImmutableSet<IncludeElementExpression> Children { get; }

    public IncludeElementExpression(RelationshipAttribute relationship)
        : this(relationship, ImmutableHashSet<IncludeElementExpression>.Empty)
    {
    }

    public IncludeElementExpression(RelationshipAttribute relationship, IImmutableSet<IncludeElementExpression> children)
    {
        ArgumentGuard.NotNull(relationship);
        ArgumentGuard.NotNull(children);

        Relationship = relationship;
        Children = children;
    }

    public override TResult Accept<TArgument, TResult>(QueryExpressionVisitor<TArgument, TResult> visitor, TArgument argument)
    {
        return visitor.VisitIncludeElement(this, argument);
    }

    public override string ToString()
    {
        return InnerToString(false);
    }

    public override string ToFullString()
    {
        return InnerToString(true);
    }

    private string InnerToString(bool toFullString)
    {
        var builder = new StringBuilder();
        builder.Append(toFullString ? $"{Relationship.LeftType.PublicName}:{Relationship.PublicName}" : Relationship.PublicName);

        if (Children.Any())
        {
            builder.Append('{');
            builder.Append(string.Join(",", Children.Select(child => toFullString ? child.ToFullString() : child.ToString()).OrderBy(name => name)));
            builder.Append('}');
        }

        return builder.ToString();
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj is null || GetType() != obj.GetType())
        {
            return false;
        }

        var other = (IncludeElementExpression)obj;

        return Relationship.Equals(other.Relationship) && Children.SetEquals(other.Children);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Relationship);

        foreach (IncludeElementExpression child in Children)
        {
            hashCode.Add(child);
        }

        return hashCode.ToHashCode();
    }
}
