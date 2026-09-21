using Xunit;

namespace Collaboration.Tests.Integration;

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
