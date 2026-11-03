using Xunit;

namespace Grundet.AdminCli.Tests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class AdminCliCollection : ICollectionFixture<AdminCliFixture>
{
    public const string Name = "admincli-integration";
}
