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

namespace Tinfour.Core.Tests.Common;

using Tinfour.Core.Common;

using Xunit;

public class GeometricOperationsTests
{
    private readonly GeometricOperations _geoOps;

    public GeometricOperationsTests()
    {
        this._geoOps = new GeometricOperations();
    }

    [Fact]
    public void Area_WithClockwiseTriangle_ShouldReturnNegativeArea()
    {
        // Arrange - Same triangle but clockwise order
        var a = new Vertex(0, 0, 0);
        var b = new Vertex(0, 1, 0);
        var c = new Vertex(1, 0, 0);

        // Act
        var area = this._geoOps.Area(a, b, c);

        // Assert
        Assert.True(area < 0);
        Assert.Equal(-0.5, area, 10);
    }

    [Fact]
    public void Area_WithCollinearPoints_ShouldReturnZero()
    {
        // Arrange
        var a = new Vertex(0, 0, 0);
        var b = new Vertex(1, 0, 0);
        var c = new Vertex(2, 0, 0);

        // Act
        var area = this._geoOps.Area(a, b, c);

        // Assert
        Assert.True(Math.Abs(area) < 1e-10);
    }

    [Fact]
    public void Area_WithCounterclockwiseTriangle_ShouldReturnPositiveArea()
    {
        // Arrange - Right triangle with area = 0.5
        var a = new Vertex(0, 0, 0);
        var b = new Vertex(1, 0, 0);
        var c = new Vertex(0, 1, 0);

        // Act
        var area = this._geoOps.Area(a, b, c);

        // Assert
        Assert.True(area > 0);
        Assert.Equal(0.5, area, 10);
    }

    [Fact]
    public void Circumcircle_WithCollinearPoints_ShouldReturnInfiniteCircle()
    {
        // Arrange
        var a = new Vertex(0, 0, 0);
        var b = new Vertex(1, 0, 0);
        var c = new Vertex(2, 0, 0);
        var result = new Circumcircle();

        // Act
        var success = this._geoOps.Circumcircle(a, b, c, result);

        // Assert
        Assert.False(success);
        Assert.True(double.IsInfinity(result.GetX()));
        Assert.True(double.IsInfinity(result.GetY()));
        Assert.True(double.IsInfinity(result.GetRadiusSq()));
    }

    [Fact]
    public void Circumcircle_WithValidTriangle_ShouldComputeCorrectCircle()
    {
        // Arrange - Right triangle
        var a = new Vertex(0, 0, 0);
        var b = new Vertex(3, 0, 0);
        var c = new Vertex(0, 4, 0);
        var result = new Circumcircle();

        // Act
        var success = this._geoOps.Circumcircle(a, b, c, result);

        // Assert
        Assert.True(success);
        Assert.Equal(1.5, result.GetX(), 2);
        Assert.Equal(2.0, result.GetY(), 2);
        Assert.Equal(2.5, result.GetRadius(), 2);
    }

    [Fact]
    public void ClearDiagnostics_ShouldResetAllCounters()
    {
        // Arrange
        var geoOps = new GeometricOperations();
        var a = new Vertex(0, 0, 0);
        var b = new Vertex(1, 0, 0);
        var c = new Vertex(0, 1, 0);

        geoOps.InCircle(a, b, c, new Vertex(0.5, 0.5, 0));
        geoOps.HalfPlane(a, b, c);

        // Act
        geoOps.ClearDiagnostics();

        // Assert
        Assert.Equal(0, geoOps.GetInCircleCount());
        Assert.Equal(0, geoOps.GetHalfPlaneCount());
        Assert.Equal(0, geoOps.GetCircumcircleCount());
        Assert.Equal(0, geoOps.GetExtendedPrecisionInCircleCount());
    }

    [Fact]
    public void Constructor_Default_ShouldCreateValidInstance()
    {
        // Act
        var geoOps = new GeometricOperations();

        // Assert
        Assert.NotNull(geoOps.GetThresholds());
        Assert.Equal(1.0, geoOps.GetThresholds().GetNominalPointSpacing());
    }

    [Fact]
    public void Constructor_WithThresholds_ShouldUseProvidedThresholds()
    {
        // Arrange
        var thresholds = new Thresholds(5.0);

        // Act
        var geoOps = new GeometricOperations(thresholds);

        // Assert
        Assert.Same(thresholds, geoOps.GetThresholds());
        Assert.Equal(5.0, geoOps.GetThresholds().GetNominalPointSpacing());
    }

    [Fact]
    public void DiagnosticCounters_ShouldTrackOperations()
    {
        // Arrange
        var geoOps = new GeometricOperations();
        var a = new Vertex(0, 0, 0);
        var b = new Vertex(1, 0, 0);
        var c = new Vertex(0, 1, 0);
        var d = new Vertex(0.5, 0.5, 0);

        // Act
        geoOps.InCircle(a, b, c, d);
        geoOps.InCircle(a, b, c, d);
        geoOps.HalfPlane(a, b, c);
        var result = new Circumcircle();
        geoOps.Circumcircle(a, b, c, result);

        // Assert
        Assert.Equal(2, geoOps.GetInCircleCount());
        Assert.Equal(1, geoOps.GetHalfPlaneCount());
        Assert.Equal(1, geoOps.GetCircumcircleCount());
    }

    [Fact]
    public void GeometricOperations_WithEquilateralTriangle_ShouldHaveCorrectProperties()
    {
        // Arrange - Equilateral triangle
        var a = new Vertex(0, 0, 0);
        var b = new Vertex(1, 0, 0);
        var c = new Vertex(0.5, Math.Sqrt(3) / 2, 0);

        // Act
        var area = this._geoOps.Area(a, b, c);
        var circumcircle = new Circumcircle();
        this._geoOps.Circumcircle(a, b, c, circumcircle);

        // Assert
        var expectedArea = Math.Sqrt(3) / 4; // Area of unit equilateral triangle
        Assert.Equal(expectedArea, area, 3);

        // Circumcenter should be at centroid
        var expectedCenterX = (a.X + b.X + c.X) / 3;
        var expectedCenterY = (a.Y + b.Y + c.Y) / 3;
        Assert.Equal(expectedCenterX, circumcircle.GetX(), 2);
        Assert.Equal(expectedCenterY, circumcircle.GetY(), 2);
    }

    [Theory]
    [InlineData(0, 0, 1, 0, 0.5, 0.5, 1)] // Point above line (left side)
    [InlineData(0, 0, 1, 0, 0.5, -0.5, -1)] // Point below line (right side)
    [InlineData(0, 0, 1, 0, 0.5, 0, 0)] // Point on line
    public void HalfPlane_ShouldReturnCorrectSide(
        double ax,
        double ay,
        double bx,
        double by,
        double cx,
        double cy,
        int expectedSign)
    {
        // Act
        var result = this._geoOps.HalfPlane(ax, ay, bx, by, cx, cy);

        // Assert
        if (expectedSign > 0)
            Assert.True(result > 0, $"Expected positive, got {result}");
        else if (expectedSign < 0)
            Assert.True(result < 0, $"Expected negative, got {result}");
        else
            Assert.True(Math.Abs(result) < 1e-10, $"Expected near zero, got {result}");
    }

    [Fact]
    public void InCircle_WithPointInsideCircle_ShouldReturnPositive()
    {
        // Arrange - Unit circle centered at origin, test point at center
        var a = new Vertex(-1, 0, 0);
        var b = new Vertex(1, 0, 0);
        var c = new Vertex(0, 1, 0);
        var d = new Vertex(0, 0, 0); // Center point, should be inside

        // Act
        var result = this._geoOps.InCircle(a, b, c, d);

        // Assert
        Assert.True(result > 0, $"Point should be inside circle, got {result}");
    }

    [Fact]
    public void InCircle_WithPointOutsideCircle_ShouldReturnNegative()
    {
        // Arrange - Triangle with circumcircle, test point far outside
        var a = new Vertex(0, 0, 0);
        var b = new Vertex(1, 0, 0);
        var c = new Vertex(0, 1, 0);
        var d = new Vertex(10, 10, 0); // Far outside

        // Act
        var result = this._geoOps.InCircle(a, b, c, d);

        // Assert
        Assert.True(result < 0, $"Point should be outside circle, got {result}");
    }

    [Fact]
    public void InCircleTest_ShouldReturnCorrectIntegerResult()
    {
        // Arrange
        var a = new Vertex(0, 0, 0);
        var b = new Vertex(2, 0, 0);
        var c = new Vertex(1, 1, 0);
        var inside = new Vertex(1, 0.2, 0);
        var outside = new Vertex(1, -2, 0); // Far outside

        // Act & Assert
        Assert.Equal(1, this._geoOps.InCircleTest(a, b, c, inside));
        Assert.Equal(-1, this._geoOps.InCircleTest(a, b, c, outside));
    }

    [Theory]
    [InlineData(0, 0, 1, 0, 0, 1, 1)] // Counterclockwise
    [InlineData(0, 0, 0, 1, 1, 0, -1)] // Clockwise
    [InlineData(0, 0, 1, 1, 2, 2, 0)] // Collinear
    public void Orientation_ShouldReturnCorrectSign(
        double ax,
        double ay,
        double bx,
        double by,
        double cx,
        double cy,
        int expectedSign)
    {
        // Act
        var result = this._geoOps.Orientation(ax, ay, bx, by, cx, cy);

        // Assert
        if (expectedSign > 0)
            Assert.True(result > 0, $"Expected positive, got {result}");
        else if (expectedSign < 0)
            Assert.True(result < 0, $"Expected negative, got {result}");
        else
            Assert.True(Math.Abs(result) < 1e-10, $"Expected near zero, got {result}");
    }

    [Fact]
    public void Orientation_WithVertices_ShouldMatchCoordinateVersion()
    {
        // Arrange
        var a = new Vertex(0, 0, 0);
        var b = new Vertex(1, 0, 0);
        var c = new Vertex(0, 1, 0);

        // Act
        var result1 = this._geoOps.Orientation(a, b, c);
        var result2 = this._geoOps.Orientation(0, 0, 1, 0, 0, 1);

        // Assert
        Assert.Equal(result2, result1, 10);
    }

    [Fact]
    public void OrientationTest_ShouldReturnCorrectIntegerResult()
    {
        // Arrange
        var ccw = new Vertex(0, 0, 0);
        var ccw2 = new Vertex(1, 0, 0);
        var ccw3 = new Vertex(0, 1, 0);

        var cw = new Vertex(0, 0, 0);
        var cw2 = new Vertex(0, 1, 0);
        var cw3 = new Vertex(1, 0, 0);

        // Act & Assert
        Assert.Equal(1, this._geoOps.OrientationTest(ccw, ccw2, ccw3)); // Counterclockwise
        Assert.Equal(-1, this._geoOps.OrientationTest(cw, cw2, cw3)); // Clockwise
    }
}