using Xunit;

namespace IsaacSimSharp.Tests;

/// <summary>
/// Shares a single <see cref="MockBridgeFixture"/> (one bound endpoint) across all test
/// classes in the collection, so parallel classes don't fight over the same port.
/// </summary>
[CollectionDefinition(Name)]
public sealed class MockBridgeCollection : ICollectionFixture<MockBridgeFixture>
{
    public const string Name = "mock-bridge";
}
