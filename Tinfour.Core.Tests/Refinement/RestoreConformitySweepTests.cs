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
using Tinfour.Core.Utils;

using Xunit;
using Xunit.Abstractions;

/// <summary>
///     Tests that verify FIX-02: RestoreConformity correctly infers constraint
///     region interior index from triangle adjacency after flipping an edge,
///     even when surrounding edges have mixed constraint states. This prevents
///     SweepForConstraintAssignments from bailing on unmarked flipped edges.
/// </summary>
public class RestoreConformitySweepTests
{
    private readonly ITestOutputHelper _output;

    public RestoreConformitySweepTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    ///     Verifies that after adding a polygon constraint that forces RestoreConformity
    ///     flips, all edges inside the constraint region carry correct interior flags.
    ///     Uses a non-trivial polygon with interior points to maximize the chance of
    ///     triggering flips during constraint processing.
    /// </summary>
    [Fact]
    public void FlipNearConstraintBoundary_InfersCorrectInteriorIndex()
    {
        // Setup: Create a TIN with enough vertices to force non-Delaunay edges
        // that RestoreConformity will flip during constraint addition.
        var tin = new IncrementalTin(10.0);

        // Create outer vertices forming a large area
        var outerVertices = new List<IVertex>
        {
            new Vertex(0, 0, 0.0, 0),
            new Vertex(100, 0, 0.0, 1),
            new Vertex(100, 100, 0.0, 2),
            new Vertex(0, 100, 0.0, 3)
        };

        // Add interior points that create triangulation edges crossing the
        // constraint boundary -- RestoreConformity will flip these
        var interiorVertices = new List<IVertex>
        {
            new Vertex(30, 30, 1.0, 10),
            new Vertex(70, 30, 1.0, 11),
            new Vertex(70, 70, 1.0, 12),
            new Vertex(30, 70, 1.0, 13),
            new Vertex(50, 50, 1.0, 14),
            new Vertex(20, 50, 1.0, 15),
            new Vertex(80, 50, 1.0, 16),
            new Vertex(50, 20, 1.0, 17),
            new Vertex(50, 80, 1.0, 18)
        };

        var allVertices = new List<IVertex>();
        allVertices.AddRange(outerVertices);
        allVertices.AddRange(interiorVertices);
        tin.Add(allVertices);

        // Add a polygon constraint using the outer vertices
        var constraint = new PolygonConstraint(outerVertices, definesRegion: true);
        tin.AddConstraints(new IConstraint[] { constraint }, true);

        // Walk all edges and verify interior edges have consistent indices
        var interiorEdges = 0;
        var unmarkedInteriorEdges = 0;
        var constraintIndex = constraint.GetConstraintIndex();

        foreach (var edge in tin.GetEdges())
        {
            if (edge.IsConstraintRegionInterior())
            {
                interiorEdges++;
                var idx = edge.GetConstraintRegionInteriorIndex();
                if (idx < 0)
                {
                    unmarkedInteriorEdges++;
                    _output.WriteLine(
                        $"Edge ({edge.GetA().X:F1},{edge.GetA().Y:F1})->({edge.GetB().X:F1},{edge.GetB().Y:F1}): " +
                        $"interior=true but index={idx}");
                }
            }
        }

        _output.WriteLine($"Interior edges: {interiorEdges}, unmarked: {unmarkedInteriorEdges}");
        _output.WriteLine($"Constraint index: {constraintIndex}");

        // No interior edge should have an invalid index
        Assert.Equal(0, unmarkedInteriorEdges);
        Assert.True(interiorEdges > 0, "Expected at least some interior edges after constraint addition");

        // Run leak detector on this simple geometry -- should have zero leaks
        var report = ConstraintLeakDetector.Detect(tin, constraint);
        _output.WriteLine($"Leaked count: {report.LeakedCount}");
        _output.WriteLine($"Divergences: {report.Divergences.Count}");
        Assert.Equal(0, report.LeakedCount);
    }

    /// <summary>
    ///     Verifies constraint region integrity when two non-overlapping polygon
    ///     constraints are added. Edges in each region should carry the correct
    ///     (and different) interior indices. No edge should be marked interior
    ///     with an unknown index.
    /// </summary>
    [Fact]
    public void ConstraintRegionIntegrity_AfterMultipleConstraintAdditions()
    {
        var tin = new IncrementalTin(10.0);

        // Two non-overlapping rectangles side by side with a gap between them
        var leftVertices = new IVertex[]
        {
            new Vertex(0, 0, 0.0, 0),
            new Vertex(40, 0, 0.0, 1),
            new Vertex(40, 40, 0.0, 2),
            new Vertex(0, 40, 0.0, 3)
        };

        var rightVertices = new IVertex[]
        {
            new Vertex(60, 0, 0.0, 4),
            new Vertex(100, 0, 0.0, 5),
            new Vertex(100, 40, 0.0, 6),
            new Vertex(60, 40, 0.0, 7)
        };

        // Add some interior points to each region to ensure rich triangulation
        var extraVertices = new IVertex[]
        {
            new Vertex(20, 20, 1.0, 10),   // inside left
            new Vertex(80, 20, 1.0, 11),   // inside right
            new Vertex(50, 20, 0.5, 12)    // in the gap between
        };

        var allVertices = new List<IVertex>();
        allVertices.AddRange(leftVertices);
        allVertices.AddRange(rightVertices);
        allVertices.AddRange(extraVertices);
        tin.Add(allVertices);

        var leftConstraint = new PolygonConstraint(leftVertices, definesRegion: true);
        var rightConstraint = new PolygonConstraint(rightVertices, definesRegion: true);
        tin.AddConstraints(new IConstraint[] { leftConstraint, rightConstraint }, true);

        var leftIndex = leftConstraint.GetConstraintIndex();
        var rightIndex = rightConstraint.GetConstraintIndex();

        _output.WriteLine($"Left constraint index: {leftIndex}");
        _output.WriteLine($"Right constraint index: {rightIndex}");

        Assert.NotEqual(leftIndex, rightIndex);

        // Walk edges and verify consistency: every interior edge should belong
        // to one of the two known constraint indices, not an unknown one
        var leftInterior = 0;
        var rightInterior = 0;
        var unknownInterior = 0;

        foreach (var edge in tin.GetEdges())
        {
            if (!edge.IsConstraintRegionInterior()) continue;

            var idx = edge.GetConstraintRegionInteriorIndex();
            if (idx == leftIndex) leftInterior++;
            else if (idx == rightIndex) rightInterior++;
            else
            {
                unknownInterior++;
                _output.WriteLine(
                    $"Unknown interior edge ({edge.GetA().X:F1},{edge.GetA().Y:F1})->" +
                    $"({edge.GetB().X:F1},{edge.GetB().Y:F1}): index={idx}");
            }
        }

        _output.WriteLine(
            $"Left interior edges: {leftInterior}, Right interior edges: {rightInterior}, " +
            $"Unknown: {unknownInterior}");

        Assert.True(leftInterior > 0, "Left region should have interior edges");
        Assert.True(rightInterior > 0, "Right region should have interior edges");
        Assert.Equal(0, unknownInterior);
    }
}
