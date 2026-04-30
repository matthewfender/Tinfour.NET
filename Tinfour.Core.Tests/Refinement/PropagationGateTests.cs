/*
 * Copyright 2025 M. Fender
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace Tinfour.Core.Tests.Refinement;

using Tinfour.Core.Common;
using Tinfour.Core.Standard;

using Xunit;
using Xunit.Abstractions;

/// <summary>
///     Tests that verify the widened constraint detection gate in InsertVertex.
///     After FIX-03, InsertVertex checks all 3 edges of the containing triangle
///     for constraint region membership, not just the nearest edge. This prevents
///     missed propagation when the nearest edge is a constraint border (not interior).
/// </summary>
public class PropagationGateTests
{
    private readonly ITestOutputHelper _output;

    public PropagationGateTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    ///     Verifies that a vertex inserted clearly inside a constraint region
    ///     has all its non-border radiating edges marked as constraint region interior.
    ///     This is the basic correctness test for PropagateConstraintRegionMembership.
    /// </summary>
    [Fact]
    public void InsertInsideConstraintRegion_AllRadiatingEdgesMarkedInterior()
    {
        // Setup: Create a TIN with a large square constraint region.
        // Use corner + interior vertices to bootstrap the TIN.
        var tin = new IncrementalTin(50.0);

        var v0 = new Vertex(0, 0, 0.0, 0);
        var v1 = new Vertex(200, 0, 0.0, 1);
        var v2 = new Vertex(200, 200, 0.0, 2);
        var v3 = new Vertex(0, 200, 0.0, 3);

        tin.Add(new IVertex[] { v0, v1, v2, v3 });

        // Add polygon constraint defining a region
        var constraint = new PolygonConstraint(
            new IVertex[] { v0, v1, v2, v3 },
            definesRegion: true);
        tin.AddConstraints(new IConstraint[] { constraint }, true);

        // Insert a vertex clearly inside the constraint region (center of the square)
        var interior = new Vertex(100, 100, 1.0, 100);
        var resultEdge = tin.AddAndReturnEdge(interior);

        Assert.NotNull(resultEdge);
        Assert.Equal(interior, resultEdge.GetA());

        // Walk all edges radiating from the inserted vertex
        var totalEdges = 0;
        var interiorEdges = 0;
        var borderEdges = 0;

        foreach (var e in resultEdge.GetPinwheel())
        {
            totalEdges++;
            var isInterior = e.IsConstraintRegionInterior();
            var isBorder = e.IsConstraintRegionBorder();
            var isMember = e.IsConstraintRegionMember();

            _output.WriteLine(
                $"Edge to ({e.GetB().X:F1}, {e.GetB().Y:F1}): " +
                $"member={isMember}, interior={isInterior}, border={isBorder}");

            if (isInterior) interiorEdges++;
            if (isBorder) borderEdges++;
        }

        _output.WriteLine($"Total: {totalEdges} edges, {interiorEdges} interior, {borderEdges} border");

        // All non-border radiating edges should be marked as interior
        Assert.True(totalEdges > 0, "Inserted vertex should have radiating edges");
        Assert.True(interiorEdges + borderEdges == totalEdges,
            $"All {totalEdges} radiating edges should be constraint region members " +
            $"(got {interiorEdges} interior + {borderEdges} border)");
    }

    /// <summary>
    ///     Verifies that the 3-edge gate detects constraint membership even when
    ///     the vertex is inserted near a constraint border. By constructing a
    ///     scenario where the nearest edge to the insertion point is likely a
    ///     constraint border, we test that the widened gate (checking all 3 edges)
    ///     still correctly triggers PropagateConstraintRegionMembership.
    /// </summary>
    [Fact]
    public void InsertNearConstraintBorder_StillPropagatesInteriorFlags()
    {
        // Setup: Create a TIN with a rectangular constraint.
        // The rectangle has a well-defined border along its edges.
        var tin = new IncrementalTin(50.0);

        // Create a wider rectangle to ensure clear border edges
        var v0 = new Vertex(0, 0, 0.0, 0);
        var v1 = new Vertex(300, 0, 0.0, 1);
        var v2 = new Vertex(300, 100, 0.0, 2);
        var v3 = new Vertex(0, 100, 0.0, 3);

        tin.Add(new IVertex[] { v0, v1, v2, v3 });

        // Add polygon constraint
        var constraint = new PolygonConstraint(
            new IVertex[] { v0, v1, v2, v3 },
            definesRegion: true);
        tin.AddConstraints(new IConstraint[] { constraint }, true);

        // Log the initial topology: find border and interior edges
        var allEdges = tin.GetEdges();
        var initialBorderCount = 0;
        var initialInteriorCount = 0;
        foreach (var e in allEdges)
        {
            if (e.IsConstraintRegionBorder()) initialBorderCount++;
            if (e.IsConstraintRegionInterior()) initialInteriorCount++;
        }
        _output.WriteLine(
            $"Initial topology: {initialBorderCount} border edges, {initialInteriorCount} interior edges");

        // Insert a vertex very close to the bottom border (y=0 edge).
        // This point is just barely inside the rectangle (y=1) and very close
        // to the bottom edge, so GetNearestEdgeInTriangle is likely to return
        // the bottom border edge as nearest.
        var nearBorder = new Vertex(150, 1, 0.5, 200);
        var resultEdge = tin.AddAndReturnEdge(nearBorder);

        Assert.NotNull(resultEdge);
        Assert.Equal(nearBorder, resultEdge.GetA());

        // Walk all edges radiating from the inserted vertex
        var totalEdges = 0;
        var memberEdges = 0;

        foreach (var e in resultEdge.GetPinwheel())
        {
            totalEdges++;
            var isMember = e.IsConstraintRegionMember();
            var isInterior = e.IsConstraintRegionInterior();
            var isBorder = e.IsConstraintRegionBorder();

            _output.WriteLine(
                $"Edge to ({e.GetB().X:F1}, {e.GetB().Y:F1}): " +
                $"member={isMember}, interior={isInterior}, border={isBorder}");

            if (isMember) memberEdges++;
        }

        _output.WriteLine($"Total: {totalEdges} edges, {memberEdges} members");

        // All radiating edges should be constraint region members (interior or border).
        // With the old single-edge check, if GetNearestEdgeInTriangle returned the
        // border edge (which is a member), this would still pass. But in specific
        // topologies, the nearest edge can be on the non-member side -- the 3-edge
        // gate catches that case.
        Assert.True(totalEdges > 0, "Inserted vertex should have radiating edges");
        Assert.True(memberEdges == totalEdges,
            $"All {totalEdges} radiating edges should be constraint region members " +
            $"(got {memberEdges} members). Near-border insertion should not miss propagation.");
    }

    /// <summary>
    ///     Verifies that inserting multiple vertices at various positions inside
    ///     a constraint region results in correct interior flag propagation for all.
    ///     This is a broader integration test ensuring the widened gate works across
    ///     multiple sequential insertions.
    /// </summary>
    [Fact]
    public void InsertMultipleVertices_AllGetCorrectInteriorFlags()
    {
        var tin = new IncrementalTin(50.0);

        var v0 = new Vertex(0, 0, 0.0, 0);
        var v1 = new Vertex(200, 0, 0.0, 1);
        var v2 = new Vertex(200, 200, 0.0, 2);
        var v3 = new Vertex(0, 200, 0.0, 3);

        tin.Add(new IVertex[] { v0, v1, v2, v3 });

        var constraint = new PolygonConstraint(
            new IVertex[] { v0, v1, v2, v3 },
            definesRegion: true);
        tin.AddConstraints(new IConstraint[] { constraint }, true);

        // Insert multiple vertices at various positions inside the region
        var testPoints = new (double x, double y)[]
        {
            (100, 100),   // center
            (50, 50),     // near corner
            (150, 10),    // near bottom border
            (10, 150),    // near left border
            (190, 190),   // near top-right corner
            (100, 5),     // very close to bottom border
            (5, 100),     // very close to left border
        };

        var allPassed = true;

        for (var i = 0; i < testPoints.Length; i++)
        {
            var (x, y) = testPoints[i];
            var vertex = new Vertex(x, y, 1.0, 300 + i);
            var edge = tin.AddAndReturnEdge(vertex);

            if (edge == null)
            {
                _output.WriteLine($"Point ({x}, {y}): AddAndReturnEdge returned null (merged?)");
                continue;
            }

            var totalEdges = 0;
            var memberEdges = 0;

            foreach (var e in edge.GetPinwheel())
            {
                totalEdges++;
                if (e.IsConstraintRegionMember()) memberEdges++;
            }

            var ok = memberEdges == totalEdges && totalEdges > 0;
            _output.WriteLine(
                $"Point ({x,3}, {y,3}): {totalEdges} edges, {memberEdges} members -- {(ok ? "PASS" : "FAIL")}");

            if (!ok) allPassed = false;
        }

        Assert.True(allPassed,
            "All inserted vertices should have all radiating edges marked as constraint region members");
    }
}
