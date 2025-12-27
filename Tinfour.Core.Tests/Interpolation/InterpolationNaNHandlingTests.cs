using System;
using System.Collections.Generic;
using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Standard;
using Xunit;

namespace Tinfour.Core.Tests.Interpolation;

public class InterpolationNaNHandlingTests
{
    [Fact]
    public void NaturalNeighbor_ShouldIgnoreNaNVertices()
    {
        // Arrange
        var tin = new IncrementalTin();
        tin.Add(new Vertex(0, 0, 0));
        tin.Add(new Vertex(10, 0, 0));
        tin.Add(new Vertex(0, 10, 0));
        tin.Add(new Vertex(10, 10, 0));
        
        // Add a vertex with NaN Z in the middle
        tin.Add(new Vertex(5, 5, double.NaN));

        var interpolator = new NaturalNeighborInterpolator(tin);

        // Act
        // Interpolate near the NaN vertex. 
        // If it included the NaN vertex, the result would be NaN.
        // Since all other vertices are 0, the result should be 0.
        var result = interpolator.Interpolate(4, 4, null);

        // Assert
        Assert.Equal(0.0, result, 1e-6);
    }

    [Fact]
    public void IDW_ShouldIgnoreNaNVertices()
    {
        // Arrange
        var tin = new IncrementalTin();
        tin.Add(new Vertex(0, 0, 0));
        tin.Add(new Vertex(10, 0, 0));
        tin.Add(new Vertex(0, 10, 0));
        tin.Add(new Vertex(10, 10, 0));
        
        // Add a vertex with NaN Z in the middle
        tin.Add(new Vertex(5, 5, double.NaN));

        var interpolator = new InverseDistanceWeightingInterpolator(tin);

        // Act
        var result = interpolator.Interpolate(4, 4, null);

        // Assert
        Assert.Equal(0.0, result, 1e-6);
    }
}
