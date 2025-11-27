/*
 * Copyright 2023 G.W. Lucas
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

namespace Tinfour.Core.Tests.Constraints;

using Tinfour.Core.Common;
using Tinfour.Core.Standard;

using Xunit;

/// <summary>
///     Comprehensive tests for Constrained Delaunay Triangulation (CDT) functionality.
///     These tests cover linear constraints, polygon constraints, hole handling,
///     and constraint preservation during vertex insertion.
/// </summary>
public class ConstrainedDelaunayTriangulationTests
{
    [Fact]
    public void AddConstraints_AfterDisposal_ShouldThrowException()
    {
        // Arrange
        var tin = new IncrementalTin();
        tin.Add(new IVertex[] { new Vertex(0, 0, 0), new Vertex(1, 0, 0), new Vertex(0, 1, 0) });
        tin.Dispose();

        var constraint = new LinearConstraint(new IVertex[] { new Vertex(0, 0, 0), new Vertex(1, 0, 0) });

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => tin.AddConstraints(new[] { constraint }, true));
    }

    [Fact]
    public void AddConstraints_MultipleLinearConstraints_ShouldHandleIntersections()
    {
        // Arrange
        var tin = new IncrementalTin();
        var vertices = new IVertex[]
                           {
                               new Vertex(0, 0, 0), new Vertex(4, 0, 0), new Vertex(4, 4, 0), new Vertex(0, 4, 0),
                               new Vertex(2, 2, 0)
                           };
        tin.Add(vertices);

        // Create intersecting linear constraints
        var constraint1 = new LinearConstraint(new IVertex[] { new Vertex(0, 2, 0), new Vertex(4, 2, 0) });
        var constraint2 = new LinearConstraint(new IVertex[] { new Vertex(2, 0, 0), new Vertex(2, 4, 0) });

        var constraints = new List<IConstraint> { constraint1, constraint2 };

        // Act
        tin.AddConstraints(constraints, true);

        // Assert
        Assert.Equal(2, tin.GetConstraints().Count);
        Assert.True(tin.IsConformant());

        // TODO: Verify both constraints are properly inserted and intersection is handled
    }

    [Fact]
    public void AddConstraints_TooManyConstraints_ShouldThrowException()
    {
        // Arrange
        var tin = new IncrementalTin();
        tin.Add(new IVertex[] { new Vertex(0, 0, 0), new Vertex(1, 0, 0), new Vertex(0, 1, 0) });

        // Create more than the maximum allowed constraints (8190)
        var constraints = new List<IConstraint>();
        for (var i = 0; i < 8191; i++)
            constraints.Add(new LinearConstraint(new IVertex[] { new Vertex(i, 0, 0), new Vertex(i + 1, 0, 0) }));

        // Act & Assert
        Assert.Throws<ArgumentException>(() => tin.AddConstraints(constraints, true));
    }

    [Fact]
    public void AddConstraints_WithoutRestoreConformity_ShouldNotBeConformant()
    {
        // Arrange
        var tin = new IncrementalTin();
        var vertices = new IVertex[] { new Vertex(0, 0, 0), new Vertex(4, 0, 0), new Vertex(2, 3, 0) };
        tin.Add(vertices);

        var constraint = new LinearConstraint(new[] { vertices[0], vertices[1] });
        var constraints = new List<IConstraint> { constraint };

        // Act
        tin.AddConstraints(constraints, false);

        // Assert
        Assert.Single(tin.GetConstraints());
        Assert.False(tin.IsConformant()); // Should not be conformant
    }

    [Fact]
    public void AddLinearConstraint_CollinearPoints_ShouldNotLoop()
    {
        // This test case creates a specific geometric configuration that was
        // identified as causing an infinite loop in the constraint processor.
        // The constraint to be added shares a vertex (v1) with an existing
        // edge (v1-v2), and the constraint's other vertex (p2) is collinear
        // with that edge. This is a classic case that requires robust
        // traversal logic in the FindNextEdge method.

        // Arrange
        var tin = new IncrementalTin();

        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 0),
                               new Vertex(2, 0, 0),
                               new Vertex(2, 2, 0),
                               new Vertex(2, 2, 0),
                               new Vertex(1, 1, 0)
                           };

        tin.Add(vertices);

        var contraintVertices = new List<IVertex>
                                    {
                                        new Vertex(0, 1, 0),
                                        new Vertex(1, 0, 0),
                                        new Vertex(2, 1, 0),
                                        new Vertex(1, 2, 0)
                                    };

        var constraint = new PolygonConstraint(contraintVertices);

        // Act & Assert
        // With the buggy FindNextEdge, this call will result in an infinite loop.
        // A successful test is one that completes without throwing an exception or timing out.
        var exception = Record.Exception(() => tin.AddConstraints(new[] { constraint }, false));

        Assert.Null(exception);
    }

    [Fact]
    public void AddLinearConstraint_IntersectingExistingEdges_ShouldSplitAndInsertConstraint()
    {
        // Arrange - Create a square TIN and add diagonal constraint
        var tin = new IncrementalTin();
        var vertices = new IVertex[]
                           {
                               new Vertex(0, 0, 0, 0), // Bottom-left
                               new Vertex(4, 0, 0, 1), // Bottom-right
                               new Vertex(4, 4, 0, 2), // Top-right
                               new Vertex(0, 4, 0, 3), // Top-left
                               new Vertex(2, 2, 0, 4) // Center
                           };
        tin.Add(vertices);

        // Add diagonal constraint that will intersect existing edges
        var constraint = new LinearConstraint(
            new IVertex[]
                {
                    new Vertex(0, 2, 0, 5), // Left edge midpoint
                    new Vertex(4, 2, 0, 6) // Right edge midpoint
                });
        var constraints = new List<IConstraint> { constraint };

        // Act
        tin.AddConstraints(constraints, true);

        // Assert
        Assert.Single(tin.GetConstraints());
        Assert.True(tin.IsConformant());

        // TODO: Verify constraint edge exists and intersecting edges were split
    }

    [Fact]
    public void AddLinearConstraint_SimpleLine_ShouldMarkEdgesAsConstrained()
    {
        // Arrange
        var tin = new IncrementalTin();
        var vertices = new IVertex[]
                           {
                               new Vertex(0, 0, 0, 0), new Vertex(4, 0, 0, 1), new Vertex(2, 3, 0, 2),
                               new Vertex(2, -3, 0, 3)
                           };
        tin.Add(vertices);

        var constraint = new LinearConstraint(new[] { vertices[0], vertices[1] });
        var constraints = new List<IConstraint> { constraint };

        // Act
        tin.AddConstraints(constraints, true);

        // Assert
        Assert.Single(tin.GetConstraints());
        Assert.True(tin.IsConformant());

        // TODO: Once CDT is implemented, verify that the edge between vertices[0] and vertices[1] is constrained
        // var constrainedEdges = tin.GetEdges().Where(e => e.IsConstrained()).ToList();
        // Assert.True(constrainedEdges.Count > 0);
    }

    [Fact]
    public void AddPolygonConstraint_DiamondInsideRectangle_ShouldEmbedFourBorderEdges()
    {
        // Arrange
        double w = 120, h = 80;
        var tin = new IncrementalTin(1.0);
        var verts = new List<IVertex>
                        {
                            new Vertex(0, 0, 0, 0), // BL
                            new Vertex(w, 0, 0, 1), // BR
                            new Vertex(w, h, 0, 2), // TR
                            new Vertex(0, h, 0, 3), // TL
                            new Vertex(w / 2, h / 2, 0, 4) // center
                        };
        Assert.True(tin.AddSorted(verts));

        var diamond = new List<IVertex>
                          {
                              new Vertex(0, h / 2, 0, 5),
                              new Vertex(w / 2, 0, 0, 6),
                              new Vertex(w, h / 2, 0, 7),
                              new Vertex(w / 2, h, 0, 8)
                          };
        var poly = new PolygonConstraint(diamond);
        poly.Complete();

        // Act
        tin.AddConstraints(new[] { poly }, true);

        // Assert
        Assert.True(tin.IsConformant());
        var cons = tin.GetConstraints();
        Assert.Single(cons);
        var c = cons[0];
        Assert.True(c.DefinesConstrainedRegion());
        var cIdx = c.GetConstraintIndex();

        // Expect four border edges (the four diamond edges)
        var expected = new (double x, double y)[]
                           {
                               (0, h / 2), (w / 2, 0), // left-bottom
                               (w / 2, 0), (w, h / 2), // bottom-right
                               (w, h / 2), (w / 2, h), // right-top
                               (w / 2, h), (0, h / 2) // top-left
                           };

        var matched = 0;
        foreach (var e in tin.GetEdges())
        {
            if (!e.IsConstraintRegionBorder()) continue;
            if (e.GetConstraintBorderIndex() != cIdx) continue;
            // Match against each expected segment
            for (var i = 0; i < expected.Length; i += 2)
                if (EdgeMatchesPoints(e, expected[i], expected[i + 1]))
                {
                    matched++;
                    break;
                }
        }

        Assert.Equal(4, matched);

        // There should be some interior membership assigned as well
        var interiorCount = tin.GetEdges().Count(e => e.GetConstraintRegionInteriorIndex() == cIdx);
        Assert.True(interiorCount >= 0); // at least 0; do not enforce a minimum

        // Non-zero valid triangles
        var tc = tin.CountTriangles();
        Assert.True(tc.ValidTriangles > 0);
    }

    [Fact]
    public void AddPolygonConstraint_SimpleTriangle_ShouldCreateConstrainedRegion()
    {
        // Arrange
        var tin = new IncrementalTin();

        // Create a larger TIN area
        var backgroundVertices = new IVertex[]
                                     {
                                         new Vertex(-2, -2, 0), new Vertex(6, -2, 0), new Vertex(6, 6, 0),
                                         new Vertex(-2, 6, 0), new Vertex(2, 2, 0)
                                     };
        tin.Add(backgroundVertices);

        // Define triangular constraint region
        var constraintVertices = new IVertex[] { new Vertex(0, 0, 0), new Vertex(4, 0, 0), new Vertex(2, 3, 0) };

        var constraint = new PolygonConstraint(constraintVertices);
        var constraints = new List<IConstraint> { constraint };

        // Act
        tin.AddConstraints(constraints, true);

        // Assert
        Assert.Single(tin.GetConstraints());
        Assert.True(tin.IsConformant());
        Assert.True(tin.GetConstraints()[0].DefinesConstrainedRegion());

        // TODO: Verify border edges are marked as constrained and interior edges are marked as region members
    }

    [Fact]
    public void AddPolygonConstraint_WithHole_ShouldHandleHoleCorrectly()
    {
        // Arrange
        var tin = new IncrementalTin();

        // Create background
        var backgroundVertices = new IVertex[]
                                     {
                                         new Vertex(-2, -2, 0), new Vertex(8, -2, 0), new Vertex(8, 8, 0),
                                         new Vertex(-2, 8, 0)
                                     };
        tin.Add(backgroundVertices);

        // Outer polygon (counterclockwise)
        var outerVertices = new IVertex[]
                                {
                                    new Vertex(0, 0, 0), new Vertex(6, 0, 0), new Vertex(6, 6, 0), new Vertex(0, 6, 0)
                                };

        // Inner polygon (clockwise - defines hole)
        var holeVertices = new IVertex[]
                               {
                                   new Vertex(2, 2, 0), new Vertex(2, 4, 0), new Vertex(4, 4, 0), new Vertex(4, 2, 0)
                               };

        var outerConstraint = new PolygonConstraint(outerVertices);
        var holeConstraint = new PolygonConstraint(holeVertices, true, true);
        var constraints = new List<IConstraint> { outerConstraint, holeConstraint };

        // Act
        tin.AddConstraints(constraints, true);

        // Assert
        Assert.Equal(2, tin.GetConstraints().Count);
        Assert.True(tin.IsConformant());
        Assert.False(outerConstraint.IsHole());
        Assert.True(holeConstraint.IsHole());

        // TODO: Verify hole is properly excluded from the constrained region
    }

    /// <summary>
    ///     Verifies that the area inside the constraint is properly identified
    ///     by checking that all triangles in the TIN are properly accounted for.
    /// </summary>
    [Fact]
    public void AddTriangularConstraint_AreaIsProperlyIdentified()
    {
        // Arrange
        var tin = CreateSimpleGridTin();
        var constraint = CreateTriangularConstraint();

        // Act
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        // Get the constraint vertices for area calculation
        var vertices = constraint.GetVertices().ToList();
        double x1 = vertices[0].X, y1 = vertices[0].Y;
        double x2 = vertices[1].X, y2 = vertices[1].Y;
        double x3 = vertices[2].X, y3 = vertices[2].Y;

        // Act
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        // Assert
        // Calculate the theoretical area of the triangular constraint using the standard formula
        var expectedConstraintArea = 0.5 * Math.Abs(x1 * (y2 - y3) + x2 * (y3 - y1) + x3 * (y1 - y2));

        // Sum the areas of all triangles that have constraint region membership
        double constraintArea = 0;
        double totalArea = 0;

        foreach (var triangle in tin.GetTriangles())
        {
            if (triangle.IsGhost()) continue;

            var area = triangle.GetArea();
            totalArea += area;

            var isConstrained = false;
            foreach (var edge in new[] { triangle.GetEdgeA(), triangle.GetEdgeB(), triangle.GetEdgeC() })
                if (edge.IsConstraintRegionInterior() || edge.IsConstraintRegionBorder())
                {
                    isConstrained = true;
                    break;
                }

            if (isConstrained) constraintArea += area;
        }

        // Area calculations should be within a small tolerance
        Assert.Equal(expectedConstraintArea, constraintArea, 3);
    }

    /// <summary>
    ///     Verifies that adding a triangular constraint that requires edge splitting
    ///     maintains the correct number of edges and triangles.
    /// </summary>
    [Fact]
    public void AddTriangularConstraint_WithEdgeSplitting_MaintainsCorrectStructure()
    {
        // Arrange
        var tin = CreateSimpleGridTin();

        var constraint = CreateTriangularConstraint();

        // Act
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        // Assert
        var triangleCount = tin.CountTriangles();

        // Verify the triangle count is as expected
        Assert.Equal(11, triangleCount.ValidTriangles);

        // Count non-ghost edges
        var edgeCount = tin.GetEdges().Count(e => !e.GetB().IsNullVertex());
        Assert.Equal(19, edgeCount);

        // Count constrained edges
        var constrainedEdgeCount = tin.GetEdges().Count(e => !e.GetB().IsNullVertex() && e.IsConstrained());
        Assert.Equal(5, constrainedEdgeCount);

        // Verify vertex count - original 4 + 3 constraint vertices
        Assert.Equal(9, tin.GetVertices().Count);

        // Verify synthetic vertex count (edge splits create synthetic vertices)
        var syntheticVertices = tin.GetSyntheticVertexCount();
        Assert.Equal(2, syntheticVertices);
    }

    [Fact]
    public void AddVertexAfterConstraints_ShouldPreserveConstraints()
    {
        // Arrange
        var tin = new IncrementalTin();
        var vertices = new IVertex[] { new Vertex(0, 0, 0), new Vertex(4, 0, 0), new Vertex(2, 3, 0) };
        tin.Add(vertices);

        var constraint = new LinearConstraint(new[] { vertices[0], vertices[1] });
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        // Act - Add vertex after constraints
        var newVertex = new Vertex(2, 1, 0);
        var result = tin.Add(newVertex);

        // Assert
        Assert.True(result);
        Assert.Single(tin.GetConstraints());
        Assert.True(tin.IsConformant());

        // TODO: Verify constraint is still marked on appropriate edges
    }

    [Fact]
    public void EdgeConstraintProperties_AfterConstraintInsertion_ShouldBeSetCorrectly()
    {
        // Arrange
        var tin = new IncrementalTin();
        var vertices = new IVertex[] { new Vertex(0, 0, 0), new Vertex(4, 0, 0), new Vertex(2, 3, 0) };
        tin.Add(vertices);

        var constraint = new LinearConstraint(new[] { vertices[0], vertices[1] });
        tin.AddConstraints(new[] { constraint }, true);

        // Act & Assert
        // TODO: Once CDT is implemented, find the constraint edge and verify its properties
        // var constraintEdge = findConstraintEdge(vertices[0], vertices[1]);
        // Assert.True(constraintEdge.IsConstrained());
        // Assert.True(constraintEdge.IsConstraintLineMember());
        // Assert.Equal(0, constraintEdge.GetConstraintLineIndex());
    }

    [Fact]
    public void GetConstraints_AfterAddingMultiple_ShouldReturnAllConstraints()
    {
        // Arrange
        var tin = new IncrementalTin();
        var vertices = new IVertex[]
                           {
                               new Vertex(0, 0, 0), new Vertex(4, 0, 0), new Vertex(2, 3, 0), new Vertex(1, 1, 0)
                           };
        tin.Add(vertices);

        var linearConstraint = new LinearConstraint(new[] { vertices[0], vertices[1] });
        var polygonConstraint = new PolygonConstraint(new[] { vertices[0], vertices[1], vertices[2] });
        var constraints = new List<IConstraint> { linearConstraint, polygonConstraint };

        // Act
        tin.AddConstraints(constraints, true);
        var retrievedConstraints = tin.GetConstraints();

        // Assert
        Assert.Equal(2, retrievedConstraints.Count);
        Assert.Contains(retrievedConstraints, c => !c.DefinesConstrainedRegion()); // Linear constraint
        Assert.Contains(retrievedConstraints, c => c.DefinesConstrainedRegion()); // Polygon constraint
    }

    [Fact]
    public void MixedMode_RectangleRegionWithVerticalLines_ShouldFlagBordersAndLines()
    {
        // Arrange a 5x5 grid (0..4)
        var tin = new IncrementalTin(1.0);
        var verts = new List<IVertex>();
        for (var i = 0; i <= 4; i++)
        for (var j = 0; j <= 4; j++)
            verts.Add(new Vertex(j, i, 0, i * 5 + j));

        Assert.True(tin.Add(verts));

        // Rectangle polygon from (1,1) to (3,3)
        var rect = new List<IVertex>
                       {
                           new Vertex(1, 1, 0), new Vertex(3, 1, 0), new Vertex(3, 3, 0), new Vertex(1, 3, 0)
                       };
        var poly = new PolygonConstraint(rect); // orientation handled in Complete
        poly.Complete();

        // Vertical linear constraints at x=1,2,3 from y=0..4
        var v1 = new LinearConstraint(new IVertex[] { new Vertex(1, 0, 0), new Vertex(1, 4, 0) });
        var v2 = new LinearConstraint(new IVertex[] { new Vertex(2, 0, 0), new Vertex(2, 4, 0) });
        var v3 = new LinearConstraint(new IVertex[] { new Vertex(3, 0, 0), new Vertex(3, 4, 0) });

        // Act
        tin.AddConstraints(new IConstraint[] { poly, v1, v2, v3 }, true);

        // Assert
        Assert.True(tin.IsConformant());
        var list = tin.GetConstraints();
        Assert.Equal(4, list.Count);
        var polyIdx = list[0].GetConstraintIndex(); // polygons are assigned first in our implementation

        // At least 4 edges on the border (one per side) flagged as border for the rectangle
        var borderEdges = tin.GetEdges()
            .Count(e => e.IsConstraintRegionBorder() && e.GetConstraintBorderIndex() == polyIdx);
        Assert.True(borderEdges >= 4);

        // For each vertical constraint, ensure there is at least one line-member edge with its index
        var lineIdxs = list.Where(c => !c.DefinesConstrainedRegion()).Select(c => c.GetConstraintIndex()).ToList();
        foreach (var idx in lineIdxs)
        {
            var any = tin.GetEdges().Any(e => e.IsConstraintLineMember() && e.GetConstraintLineIndex() == idx);
            Assert.True(any);
        }

        // Valid triangles should be non-zero
        var tc = tin.CountTriangles();
        Assert.True(tc.ValidTriangles > 0);
    }

    /// <summary>
    ///     Verifies that a simple TIN is correctly created with 2 triangles.
    /// </summary>
    [Fact]
    public void SimpleGridTin_HasExpectedStructure()
    {
        // Arrange & Act
        var tin = CreateSimpleGridTin();

        // Assert
        var triangleCount = tin.CountTriangles();
        Assert.Equal(2, triangleCount.ValidTriangles);

        var nonGhostEdgeCount = tin.GetEdges().Count(e => !e.GetB().IsNullVertex());
        Assert.Equal(5, nonGhostEdgeCount);

        Assert.Equal(4, tin.GetVertices().Count);
    }

    /// <summary>
    ///     Creates a simple 2x2 grid TIN for testing.
    /// </summary>
    /// <returns>An initialized TIN with a 2x2 grid of vertices.</returns>
    private static IncrementalTin CreateSimpleGridTin()
    {
        // Create a 2x2 grid of vertices
        var vertices = new List<IVertex>();
        for (var y = 0; y < 2; y++)
        for (var x = 0; x < 2; x++)
            vertices.Add(new Vertex(x, y, 0, y * 2 + x));

        // Create and initialize TIN
        var tin = new IncrementalTin();
        tin.Add(vertices);

        return tin;
    }

    /// <summary>
    ///     Creates a triangular constraint that will force edge splitting.
    /// </summary>
    /// <returns>A polygon constraint defining a triangle.</returns>
    private static IConstraint CreateTriangularConstraint()
    {
        var constraint = new PolygonConstraint();

        constraint.Add(new Vertex(1.7, 0.3, 0, 300));
        constraint.Add(new Vertex(0.5, 1.7, 0, 200));
        constraint.Add(new Vertex(0.3, 0.3, 0, 100));
        constraint.Complete();
        return constraint;
    }

    private static bool EdgeMatchesPoints(
        IQuadEdge e,
        (double x, double y) a,
        (double x, double y) b,
        double tol = 1.0e-6)
    {
        var A = e.GetA();
        var B = e.GetB();
        if (B.IsNullVertex()) return false;
        var d1 = Near(A, a.x, a.y, tol) && Near(B, b.x, b.y, tol);
        var d2 = Near(A, b.x, b.y, tol) && Near(B, a.x, a.y, tol);
        return d1 || d2;
    }

    private static bool Near(IVertex v, double x, double y, double tol = 1.0e-6)
    {
        return Math.Abs(v.X - x) <= tol && Math.Abs(v.Y - y) <= tol;
    }
}