using Xunit;

namespace Grundet.Api.Tests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class ApiIntegrationCollection : ICollectionFixture<ApiIntegrationFixture>
{
    public const string Name = "api-integration";
}
