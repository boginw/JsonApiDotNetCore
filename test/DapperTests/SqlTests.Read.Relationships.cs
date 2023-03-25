using System.Net;
using DapperExample.Models;
using DapperExample.Repositories;
using FluentAssertions;
using JsonApiDotNetCore.Serialization.Objects;
using Microsoft.Extensions.DependencyInjection;
using TestBuildingBlocks;
using Xunit;

namespace DapperTests;

public sealed partial class SqlTests
{
    [Fact]
    public async Task Can_get_ToOne_relationship()
    {
        // Arrange
        var store = _factory.Services.GetRequiredService<SqlCaptureStore>();
        store.Clear();

        TodoItem todoItem = _fakers.TodoItem.Generate();
        todoItem.Owner = _fakers.Person.Generate();

        await RunOnDatabaseAsync(async dbContext =>
        {
            await ClearAllTablesAsync(dbContext);
            dbContext.TodoItems.Add(todoItem);
            await dbContext.SaveChangesAsync();
        });

        string route = $"/todoItems/{todoItem.StringId}/relationships/owner";

        // Act
        (HttpResponseMessage httpResponse, Document responseDocument) = await ExecuteGetAsync<Document>(route);

        // Assert
        httpResponse.ShouldHaveStatusCode(HttpStatusCode.OK);

        responseDocument.Data.SingleValue.ShouldNotBeNull();
        responseDocument.Data.SingleValue.Type.Should().Be("people");
        responseDocument.Data.SingleValue.Id.Should().Be(todoItem.Owner.StringId);

        responseDocument.Meta.Should().BeNull();

        store.SqlCommands.ShouldHaveCount(1);

        store.SqlCommands[0].With(command =>
        {
            command.Statement.Should().Be(_adapter.Adapt(@"SELECT t1.""Id"", t2.""Id""
FROM ""TodoItems"" AS t1
INNER JOIN ""People"" AS t2 ON t1.""OwnerId"" = t2.""Id""
WHERE t1.""Id"" = @p1
ORDER BY t2.""Id""
LIMIT @p2"));

            command.Parameters.ShouldHaveCount(2);
            command.Parameters.Should().Contain("@p1", todoItem.Id);
            command.Parameters.Should().Contain("@p2", 10);
        });
    }

    [Fact]
    public async Task Can_get_empty_ToOne_relationship()
    {
        // Arrange
        var store = _factory.Services.GetRequiredService<SqlCaptureStore>();
        store.Clear();

        TodoItem todoItem = _fakers.TodoItem.Generate();
        todoItem.Owner = _fakers.Person.Generate();

        await RunOnDatabaseAsync(async dbContext =>
        {
            await ClearAllTablesAsync(dbContext);
            dbContext.TodoItems.Add(todoItem);
            await dbContext.SaveChangesAsync();
        });

        string route = $"/todoItems/{todoItem.StringId}/relationships/assignee";

        // Act
        (HttpResponseMessage httpResponse, Document responseDocument) = await ExecuteGetAsync<Document>(route);

        // Assert
        httpResponse.ShouldHaveStatusCode(HttpStatusCode.OK);

        responseDocument.Data.Value.Should().BeNull();

        responseDocument.Meta.Should().BeNull();

        store.SqlCommands.ShouldHaveCount(1);

        store.SqlCommands[0].With(command =>
        {
            command.Statement.Should().Be(_adapter.Adapt(@"SELECT t1.""Id"", t2.""Id""
FROM ""TodoItems"" AS t1
LEFT JOIN ""People"" AS t2 ON t1.""AssigneeId"" = t2.""Id""
WHERE t1.""Id"" = @p1
ORDER BY t2.""Id""
LIMIT @p2"));

            command.Parameters.ShouldHaveCount(2);
            command.Parameters.Should().Contain("@p1", todoItem.Id);
            command.Parameters.Should().Contain("@p2", 10);
        });
    }

    [Fact]
    public async Task Can_get_ToMany_relationship()
    {
        // Arrange
        var store = _factory.Services.GetRequiredService<SqlCaptureStore>();
        store.Clear();

        TodoItem todoItem = _fakers.TodoItem.Generate();
        todoItem.Owner = _fakers.Person.Generate();
        todoItem.Tags = _fakers.Tag.Generate(2).ToHashSet();

        await RunOnDatabaseAsync(async dbContext =>
        {
            await ClearAllTablesAsync(dbContext);
            dbContext.TodoItems.Add(todoItem);
            await dbContext.SaveChangesAsync();
        });

        string route = $"/todoItems/{todoItem.StringId}/relationships/tags";

        // Act
        (HttpResponseMessage httpResponse, Document responseDocument) = await ExecuteGetAsync<Document>(route);

        // Assert
        httpResponse.ShouldHaveStatusCode(HttpStatusCode.OK);

        responseDocument.Data.ManyValue.ShouldHaveCount(2);

        responseDocument.Data.ManyValue[0].ShouldNotBeNull();
        responseDocument.Data.ManyValue[0].Type.Should().Be("tags");
        responseDocument.Data.ManyValue[0].Id.Should().Be(todoItem.Tags.ElementAt(0).StringId);

        responseDocument.Data.ManyValue[1].ShouldNotBeNull();
        responseDocument.Data.ManyValue[1].Type.Should().Be("tags");
        responseDocument.Data.ManyValue[1].Id.Should().Be(todoItem.Tags.ElementAt(1).StringId);

        responseDocument.Meta.Should().ContainTotal(2);

        store.SqlCommands.ShouldHaveCount(2);

        store.SqlCommands[0].With(command =>
        {
            command.Statement.Should().Be(_adapter.Adapt(@"SELECT COUNT(*)
FROM ""Tags"" AS t1
LEFT JOIN ""TodoItems"" AS t2 ON t1.""TodoItemId"" = t2.""Id""
WHERE t2.""Id"" = @p1"));

            command.Parameters.ShouldHaveCount(1);
            command.Parameters.Should().Contain("@p1", todoItem.Id);
        });

        store.SqlCommands[1].With(command =>
        {
            command.Statement.Should().Be(_adapter.Adapt(@"SELECT t1.""Id"", t2.""Id""
FROM ""TodoItems"" AS t1
LEFT JOIN ""Tags"" AS t2 ON t1.""Id"" = t2.""TodoItemId""
WHERE t1.""Id"" = @p1
ORDER BY t2.""Id""
LIMIT @p2"));

            command.Parameters.ShouldHaveCount(2);
            command.Parameters.Should().Contain("@p1", todoItem.Id);
            command.Parameters.Should().Contain("@p2", 10);
        });
    }

    [Fact]
    public async Task Cannot_get_relationship_for_unknown_primary_ID()
    {
        const long unknownTodoItemId = Unknown.TypedId.Int64;

        string route = $"/todoItems/{unknownTodoItemId}/relationships/owner";

        // Act
        (HttpResponseMessage httpResponse, Document responseDocument) = await ExecuteGetAsync<Document>(route);

        // Assert
        httpResponse.ShouldHaveStatusCode(HttpStatusCode.NotFound);

        responseDocument.Errors.ShouldHaveCount(1);

        ErrorObject error = responseDocument.Errors[0];
        error.StatusCode.Should().Be(HttpStatusCode.NotFound);
        error.Title.Should().Be("The requested resource does not exist.");
        error.Detail.Should().Be($"Resource of type 'todoItems' with ID '{unknownTodoItemId}' does not exist.");
    }
}
