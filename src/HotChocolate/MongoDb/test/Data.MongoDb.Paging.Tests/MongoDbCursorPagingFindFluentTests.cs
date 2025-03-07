using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HotChocolate.Data.MongoDb.Filters;
using HotChocolate.Execution;
using HotChocolate.Resolvers;
using HotChocolate.Types;
using HotChocolate.Types.Pagination;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Snapshooter.Xunit;
using Squadron;
using Xunit;

namespace HotChocolate.Data.MongoDb.Paging;

public class MongoDbCursorPagingFindFluentTests : IClassFixture<MongoResource>
{
    private readonly List<Foo> foos = new List<Foo>
        {
            new Foo { Bar = "a" },
            new Foo { Bar = "b" },
            new Foo { Bar = "d" },
            new Foo { Bar = "e" },
            new Foo { Bar = "f" }
        };

    private readonly MongoResource _resource;

    public MongoDbCursorPagingFindFluentTests(MongoResource resource)
    {
        _resource = resource;
    }

    [Fact]
    public async Task Simple_StringList_Default_Items()
    {
        Snapshot.FullName();

        IRequestExecutor executor = await CreateSchemaAsync();

        IExecutionResult result = await executor
            .ExecuteAsync(@"
                    {
                        foos {
                            edges {
                                node {
                                    bar
                                }
                                cursor
                            }
                            nodes {
                                bar
                            }
                            pageInfo {
                                hasNextPage
                                hasPreviousPage
                                startCursor
                                endCursor
                            }
                        }
                    }");
        result.MatchDocumentSnapshot();
    }

    [Fact]
    public async Task Simple_StringList_First_2()
    {
        Snapshot.FullName();

        IRequestExecutor executor = await CreateSchemaAsync();

        IExecutionResult result = await executor
            .ExecuteAsync(@"
                    {
                        foos(first: 2) {
                            edges {
                                node {
                                    bar
                                }
                                cursor
                            }
                            nodes {
                                bar
                            }
                            pageInfo {
                                hasNextPage
                                hasPreviousPage
                                startCursor
                                endCursor
                            }
                        }
                    }");
        result.MatchDocumentSnapshot();
    }

    [Fact]
    public async Task Simple_StringList_First_2_After()
    {
        Snapshot.FullName();

        IRequestExecutor executor = await CreateSchemaAsync();

        IExecutionResult result = await executor
            .ExecuteAsync(@"
                    {
                        foos(first: 2 after: ""MQ=="") {
                            edges {
                                node {
                                    bar
                                }
                                cursor
                            }
                            nodes {
                                bar
                            }
                            pageInfo {
                                hasNextPage
                                hasPreviousPage
                                startCursor
                                endCursor
                            }
                        }
                    }");
        result.MatchDocumentSnapshot();
    }

    [Fact]
    public async Task Simple_StringList_Last_1_Before()
    {
        Snapshot.FullName();

        IRequestExecutor executor = await CreateSchemaAsync();

        IExecutionResult result = await executor
            .ExecuteAsync(@"
                    {
                        foos(last: 1 before: ""NA=="") {
                            edges {
                                node {
                                    bar
                                }
                                cursor
                            }
                            nodes {
                                bar
                            }
                            pageInfo {
                                hasNextPage
                                hasPreviousPage
                                startCursor
                                endCursor
                            }
                        }
                    }");
        result.MatchDocumentSnapshot();
    }

    [Fact]
    public async Task Simple_StringList_Global_DefaultItem_2()
    {
        Snapshot.FullName();

        IRequestExecutor executor = await CreateSchemaAsync();


        IExecutionResult result = await executor
            .ExecuteAsync(@"
                    {
                        foos {
                            edges {
                                node {
                                    bar
                                }
                                cursor
                            }
                            nodes {
                                bar
                            }
                            pageInfo {
                                hasNextPage
                                hasPreviousPage
                                startCursor
                                endCursor
                            }
                        }
                    }");
        result.MatchDocumentSnapshot();
    }

    [Fact]
    public async Task JustTotalCount()
    {
        Snapshot.FullName();

        IRequestExecutor executor = await CreateSchemaAsync();


        IExecutionResult result = await executor
            .ExecuteAsync(@"
                    {
                        foos {
                            totalCount
                        }
                    }");
        result.MatchDocumentSnapshot();
    }

    [Fact]
    public async Task TotalCount_AndFirst()
    {
        Snapshot.FullName();

        IRequestExecutor executor = await CreateSchemaAsync();

        IExecutionResult result = await executor
            .ExecuteAsync(@"
                    {
                        foos(first:1) {
                            totalCount
                        }
                    }");
        result.MatchDocumentSnapshot();
    }

    public class Foo
    {
        [BsonId]
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Bar { get; set; } = default!;
    }

    private Func<IResolverContext, MongoDbCollectionExecutable<TResult>> BuildResolver<TResult>(
        MongoResource mongoResource,
        IEnumerable<TResult> results)
        where TResult : class
    {
        IMongoCollection<TResult> collection =
            mongoResource.CreateCollection<TResult>("data_" + Guid.NewGuid().ToString("N"));

        collection.InsertMany(results);

        return ctx => collection.AsExecutable();
    }

    private ValueTask<IRequestExecutor> CreateSchemaAsync()
    {
        return new ServiceCollection()
            .AddGraphQL()
            .AddMongoDbPagingProviders()
            .AddFiltering(x => x.AddMongoDbDefaults())
            .AddQueryType(
                descriptor =>
                {
                    descriptor
                        .Field("foos")
                        .Resolve(BuildResolver(_resource, foos))
                        .Type<ListType<ObjectType<Foo>>>()
                        .Use(
                            next => async context =>
                            {
                                await next(context);
                                if (context.Result is IExecutable executable)
                                {
                                    context.ContextData["query"] = executable.Print();
                                }
                            })
                        .UsePaging<ObjectType<Foo>>(
                            options: new PagingOptions { IncludeTotalCount = true });
                })
            .UseRequest(
                next => async context =>
                {
                    await next(context);
                    if (context.ContextData.TryGetValue("query", out var queryString))
                    {
                        context.Result =
                            QueryResultBuilder
                                .FromResult(context.Result!.ExpectQueryResult())
                                .SetContextData("query", queryString)
                                .Create();
                    }
                })
            .ModifyRequestOptions(x => x.IncludeExceptionDetails = true)
            .UseDefaultPipeline()
            .Services
            .BuildServiceProvider()
            .GetRequiredService<IRequestExecutorResolver>()
            .GetRequestExecutorAsync();
    }
}
