/*
 * Copyright 2017-2025 Gary W. Lucas.
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

/*
 * -----------------------------------------------------------------------
 *
 * Revision History:
 * Date     Name         Description
 * ------   ---------    -------------------------------------------------
 * 08/2025 M.Fender     Created
 *
 * Notes:
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Tests.Utils;

using Tinfour.Core.Common;
using Tinfour.Core.Standard;
using Tinfour.Core.Utils;

using Xunit;

public class TriangleCollectorTests
{
    [Fact]
    public void VisitSimpleTriangles_PopulatedTin_CallsConsumerForEachTriangle()
    {
        // Arrange
        var tin = new IncrementalTin();
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 0), new Vertex(10, 0, 1), new Vertex(10, 10, 2), new Vertex(0, 10, 3)
                           };
        tin.Add(vertices);

        var triangles = new List<SimpleTriangle>();

        // Act
        TriangleCollector.VisitSimpleTriangles(tin, triangles.Add);

        // Assert
        Assert.Equal(2, triangles.Count); // A square with 4 vertices should form 2 triangles
    }

    [Fact]
    public void VisitTriangles_EmptyTin_DoesNotCallConsumer()
    {
        // Arrange
        var tin = new IncrementalTin();
        var callCount = 0;

        // Act
        TriangleCollector.VisitTriangles(tin, (IVertex[] vertices) => callCount++);

        // Assert
        Assert.Equal(0, callCount);
    }

    [Fact]
    public void VisitTriangles_PopulatedTin_CallsConsumerForEachTriangle()
    {
        // Arrange
        var tin = new IncrementalTin();
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 0), new Vertex(10, 0, 1), new Vertex(10, 10, 2), new Vertex(0, 10, 3)
                           };
        tin.Add(vertices);

        var triangles = new List<IVertex[]>();

        // Act
        TriangleCollector.VisitTriangles(tin, triangles.Add);

        // Assert
        Assert.Equal(2, triangles.Count); // A square with 4 vertices should form 2 triangles

        // Each triangle should have exactly 3 vertices
        foreach (var triangle in triangles) Assert.Equal(3, triangle.Length);
    }

    [Fact]
    public void VisitTrianglesConstrained_TinWithConstraints_CallsConsumerForConstrainedTriangles()
    {
        // Arrange
        var tin = new IncrementalTin();
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 0),
                               new Vertex(10, 0, 1),
                               new Vertex(10, 10, 2),
                               new Vertex(0, 10, 3),
                               new Vertex(5, 5, 4) // Interior point to create more triangles
                           };
        tin.Add(vertices);

        var constraintVertices = new List<IVertex>
                                     {
                                         new Vertex(1, 1, 0),
                                         new Vertex(9, 1, 0),
                                         new Vertex(9, 9, 0),
                                         new Vertex(1, 9, 0),
                                         new Vertex(1, 1, 0) // Close the loop
                                     };

        var constraint = new PolygonConstraint(constraintVertices);
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        var triangles = new List<IVertex[]>();

        // Act
        TriangleCollector.VisitTrianglesConstrained(tin, triangles.Add);

        // Assert
        Assert.True(triangles.Count > 0, "Should have found constrained triangles");

        // Each triangle should have exactly 3 vertices
        foreach (var triangle in triangles)
        {
            Assert.Equal(3, triangle.Length);

            // All vertices should be non-null
            foreach (var vertex in triangle) Assert.False(vertex.IsNullVertex());
        }
    }

    [Fact]
    public void VisitTrianglesForConstrainedRegion_InvalidConstraint_ThrowsArgumentException()
    {
        // Arrange
        var constraint = new PolygonConstraint(
            new List<IVertex>
                {
                    new Vertex(0, 0, 0),
                    new Vertex(10, 0, 0),
                    new Vertex(10, 10, 0),
                    new Vertex(0, 10, 0),
                    new Vertex(0, 0, 0)
                });

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            TriangleCollector.VisitTrianglesForConstrainedRegion(constraint, (IVertex[] _) => { }));
    }

    [Fact]
    public void VisitTrianglesForConstrainedRegion_ValidConstraint_CallsConsumerForTrianglesInRegion()
    {
        // Arrange
        var tin = new IncrementalTin();
        var vertices = new List<IVertex>
                           {
                               new Vertex(0, 0, 0),
                               new Vertex(10, 0, 1),
                               new Vertex(10, 10, 2),
                               new Vertex(0, 10, 3),
                               new Vertex(5, 5, 4) // Interior point to create more triangles
                           };
        tin.Add(vertices);

        var constraintVertices = new List<IVertex>
                                     {
                                         new Vertex(1, 1, 0),
                                         new Vertex(9, 1, 0),
                                         new Vertex(9, 9, 0),
                                         new Vertex(1, 9, 0),
                                         new Vertex(1, 1, 0) // Close the loop
                                     };

        var constraint = new PolygonConstraint(constraintVertices);
        tin.AddConstraints(new List<IConstraint> { constraint }, true);

        var triangles = new List<IVertex[]>();

        // Act
        TriangleCollector.VisitTrianglesForConstrainedRegion(constraint, triangles.Add);

        // Assert
        Assert.True(triangles.Count > 0, "Should have found triangles in the constrained region");

        // Each triangle should have exactly 3 vertices
        foreach (var triangle in triangles)
        {
            Assert.Equal(3, triangle.Length);

            // All vertices should be non-null
            foreach (var vertex in triangle) Assert.False(vertex.IsNullVertex());
        }
    }
}