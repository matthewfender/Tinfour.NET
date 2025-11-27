namespace Tinfour.Core.Tests.Edge;

using Tinfour.Core.Common;
using Tinfour.Core.Edge;

using Xunit;
using Xunit.Abstractions;

public class EdgeConstraintDelegationDebugTest
{
    private readonly ITestOutputHelper _output;

    public EdgeConstraintDelegationDebugTest(ITestOutputHelper output)
    {
        this._output = output;
    }

    [Fact]
    public void TestEdgeConstraintDelegationDuringBasicTriangulation()
    {
        this._output.WriteLine("=== Testing Edge Constraint Delegation Impact ===");

        // Create edges like they would be created during basic triangulation
        var edgePool = new EdgePool();

        for (var i = 0; i < 10; i++)
        {
            var v1 = new Vertex(i, 0, 0);
            var v2 = new Vertex(i + 1, 0, 0);
            var edge = edgePool.AllocateEdge(v1, v2);

            this._output.WriteLine(
                $"Edge {i}: Index={edge.GetIndex()}, DualIndex={edge.GetDual().GetIndex()}, IsConstrained={edge.IsConstrained()}");

            // Check if any edge is incorrectly being marked as constrained
            if (edge.IsConstrained())
                this._output.WriteLine($"  *** WARNING: Edge {i} is incorrectly marked as constrained! ***");
        }

        edgePool.Dispose();
    }

    [Fact]
    public void TestQuadEdgeIndexInitialization()
    {
        this._output.WriteLine("=== Testing QuadEdge Index Initialization ===");

        //// Test default constructor
        // _output.WriteLine("--- Default Constructor ---");
        // var edge1 = new QuadEdge();
        // _output.WriteLine($"Base edge index: {edge1.GetIndex()}");
        // _output.WriteLine($"Dual edge index: {edge1.GetDual().GetIndex()}");
        // _output.WriteLine($"Base IsConstrained: {edge1.IsConstrained()}");
        // _output.WriteLine($"Dual IsConstrained: {edge1.GetDual().IsConstrained()}");

        // Test parameterized constructor
        this._output.WriteLine(string.Empty);
        this._output.WriteLine("--- Parameterized Constructor (index=100) ---");
        var edge2 = new QuadEdge(100);
        this._output.WriteLine($"Base edge index: {edge2.GetIndex()}");
        this._output.WriteLine($"Dual edge index: {edge2.GetDual().GetIndex()}");
        this._output.WriteLine($"Base IsConstrained: {edge2.IsConstrained()}");
        this._output.WriteLine($"Dual IsConstrained: {edge2.GetDual().IsConstrained()}");

        // Test edge pool allocation (this is what actually happens during triangulation)
        this._output.WriteLine(string.Empty);
        this._output.WriteLine("--- EdgePool Allocation ---");
        var edgePool = new EdgePool();
        var v1 = new Vertex(0, 0, 0);
        var v2 = new Vertex(1, 0, 0);
        var edge3 = edgePool.AllocateEdge(v1, v2);
        this._output.WriteLine($"EdgePool edge index: {edge3.GetIndex()}");
        this._output.WriteLine($"EdgePool dual index: {edge3.GetDual().GetIndex()}");
        this._output.WriteLine($"EdgePool IsConstrained: {edge3.IsConstrained()}");
        this._output.WriteLine($"EdgePool dual IsConstrained: {edge3.GetDual().IsConstrained()}");

        edgePool.Dispose();
    }
}