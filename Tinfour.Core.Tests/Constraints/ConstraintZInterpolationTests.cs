using System;
using System.Collections.Generic;
using System.Linq;
using Tinfour.Core.Common;
using Tinfour.Core.Standard;
using Xunit;

namespace Tinfour.Core.Tests.Constraints;

public class ConstraintZInterpolationTests
{
    [Fact]
    public void AddConstraints_WithPreInterpolateZ_ShouldPopulateNaNValues()
    {
        // Arrange
        var tin = new IncrementalTin();
        // Create a sloped plane: Z = X + Y
        tin.Add(new Vertex(0, 0, 0));
        tin.Add(new Vertex(10, 0, 10));
        tin.Add(new Vertex(0, 10, 10));
        tin.Add(new Vertex(10, 10, 20));

        // Create a constraint with NaN Z values in the middle
        // At (5, 5), Z should be 10 (5+5)
        var v1 = new Vertex(5, 5, double.NaN);
        var v2 = new Vertex(6, 6, double.NaN); // Z should be 12 (6+6)
        
        var constraint = new PolygonConstraint(new[] { v1, v2, new Vertex(5, 6, double.NaN) }); // Triangle

        // Act
        tin.AddConstraints(new List<IConstraint> { constraint }, true, preInterpolateZ: true);

        // Assert
        var vertices = tin.GetVertices().Where(v => v.X == 5 && v.Y == 5).ToList();
        Assert.Single(vertices);
        Assert.Equal(10.0, vertices[0].GetZ(), 1e-6);
        
        var v2Result = tin.GetVertices().First(v => v.X == 6 && v.Y == 6);
        Assert.Equal(12.0, v2Result.GetZ(), 1e-6);
    }

    [Fact]
    public void AddConstraints_WithPreInterpolateZ_OffHullVertices_ExtrapolateFromNearestVertex()
    {
        // Arrange: data hull is the unit-ish square below. Facet interpolation returns NaN OUTSIDE
        // this convex hull, so a constraint vertex placed beyond the (10,10) corner cannot be draped.
        var tin = new IncrementalTin();
        tin.Add(new Vertex(0, 0, 0));
        tin.Add(new Vertex(10, 0, 10));
        tin.Add(new Vertex(0, 10, 10));
        tin.Add(new Vertex(10, 10, 20)); // nearest data vertex to everything past the (10,10) corner

        // A small polygon constraint lying entirely off-hull, beyond the (10,10) corner.
        var offHull = new[]
        {
            new Vertex(15, 15, double.NaN),
            new Vertex(16, 15, double.NaN),
            new Vertex(15, 16, double.NaN),
        };
        var constraint = new PolygonConstraint(offHull);

        // Act
        tin.AddConstraints(new List<IConstraint> { constraint }, true, preInterpolateZ: true);

        // Assert: previously these stayed NaN (facet interpolation gives up off-hull), which silently
        // poisons contouring and meshing downstream. They must now carry the nearest data vertex's Z.
        foreach (var (x, y) in new[] { (15.0, 15.0), (16.0, 15.0), (15.0, 16.0) })
        {
            var result = tin.GetVertices().First(v => v.X == x && v.Y == y);
            Assert.False(double.IsNaN(result.GetZ()), $"off-hull vertex ({x},{y}) must not remain NaN");
            Assert.Equal(20.0, result.GetZ(), 1e-6); // nearest is (10,10,20)
        }
    }

    [Fact]
    public void AddConstraints_WithoutPreInterpolateZ_ShouldKeepNaNValues()
    {
        // Arrange
        var tin = new IncrementalTin();
        tin.Add(new Vertex(0, 0, 0));
        tin.Add(new Vertex(10, 0, 10));
        tin.Add(new Vertex(0, 10, 10));
        tin.Add(new Vertex(10, 10, 20));

        var v1 = new Vertex(5, 5, double.NaN);
        var constraint = new PolygonConstraint(new[] { v1, new Vertex(6, 6, double.NaN), new Vertex(5, 6, double.NaN) });

        // Act
        tin.AddConstraints(new List<IConstraint> { constraint }, true, preInterpolateZ: false);

        // Assert
        var vertices = tin.GetVertices().Where(v => v.X == 5 && v.Y == 5).ToList();
        Assert.Single(vertices);
        Assert.True(double.IsNaN(vertices[0].GetZ()));
    }
}
