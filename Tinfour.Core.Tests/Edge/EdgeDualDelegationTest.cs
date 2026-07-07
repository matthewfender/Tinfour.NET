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
        // Create a simple edge pair (indices are pool-assigned, #832:
        // first allocation gets base index 0, dual base+1)
        var pool = new EdgePool();
        var edge = pool.AllocateUndefinedEdge();
        var dual = edge.GetDual();

        // Verify basic properties
        Assert.Equal(0, edge.GetIndex());
        Assert.Equal(edge.GetIndex() + 1, dual.GetIndex());

        // Verify dual relationship
        Assert.Same(edge, dual.GetDual());
    }

    [Fact]
    public void TestConstraintDelegationBasics()
    {
        // Create edge pair
        var pool = new EdgePool();
        var edge = pool.AllocateUndefinedEdge();
        var indexBefore = edge.GetIndex();

        // Test initial constraint state (should be false)
        Assert.False(edge.IsConstrained());
        Assert.False(edge.IsConstraintLineMember());

        // The edge index should not change from constraint queries
        Assert.Equal(indexBefore, edge.GetIndex());
    }
}