namespace Tinfour.Core.Tests.Edge;

using Tinfour.Core.Edge;

using Xunit;

/// <summary>
///     Simple test to debug edge dual delegation without complex constraint processing
/// </summary>
public class EdgeDualDelegationTest
{
    [Fact]
    public void TestBasicEdgeDualCreation()
    {
        // Create a simple edge pair
        var edge = new QuadEdge(42);
        var dual = edge.GetDual();

        // Verify basic properties
        Assert.Equal(42, edge.GetIndex());
        Assert.Equal(43, dual.GetIndex());

        // Verify dual relationship
        Assert.Same(edge, dual.GetDual());
    }

    [Fact]
    public void TestConstraintDelegationBasics()
    {
        // Create edge pair
        var edge = new QuadEdge(100);

        // Test initial constraint state (should be false)
        Assert.False(edge.IsConstrained());
        Assert.False(edge.IsConstraintLineMember());

        // The edge index should not change from constraint queries
        Assert.Equal(100, edge.GetIndex());
    }
}