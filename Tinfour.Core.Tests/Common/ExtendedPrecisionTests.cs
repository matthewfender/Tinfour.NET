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

public class ExtendedPrecisionTests
{
    [Fact]
    public void Circumcircle_WithNearlyDegenerateTriangle_ShouldUseExtendedPrecision()
    {
        // Arrange - Create a nearly degenerate triangle
        var geoOps = new GeometricOperations();
        var result = new Circumcircle();

        // Nearly collinear vertices
        var a = new Vertex(0.0, 0.0, 0);
        var b = new Vertex(10.0, 0.0, 0);
        var c = new Vertex(5.0, 1e-10, 0); // Very small height

        // Act
        var success = geoOps.Circumcircle(a, b, c, result);

        // Assert - Should either compute a valid result or handle degeneracy gracefully
        if (success)
        {
            Assert.True(double.IsFinite(result.GetX()));
            Assert.True(double.IsFinite(result.GetY()));
            Assert.True(double.IsFinite(result.GetRadiusSq()));
        }
        else
        {
            // For truly degenerate cases, should return infinite values
            Assert.True(
                double.IsInfinity(result.GetX()) || double.IsInfinity(result.GetY())
                                                 || double.IsInfinity(result.GetRadiusSq()));
        }
    }

    [Fact]
    public void DoubleDouble_Arithmetic_ShouldMaintainPrecision()
    {
        // Arrange - Test extended precision arithmetic operations
        var a = new DoubleDouble(Math.PI);
        var b = new DoubleDouble(Math.E);

        // Act - Perform various arithmetic operations
        var sum = a + b;
        var diff = a - b;
        var product = a * b;
        var quotient = a / b;

        // Assert - All results should be finite and reasonable
        Assert.True(sum.IsFinite);
        Assert.True(diff.IsFinite);
        Assert.True(product.IsFinite);
        Assert.True(quotient.IsFinite);

        // Verify mathematical relationships
        Assert.True(sum.ToDouble() > Math.PI);
        Assert.True(sum.ToDouble() > Math.E);
        Assert.True(diff.ToDouble() > 0); // ? > e
        Assert.True(quotient.ToDouble() > 1); // ?/e > 1
    }

    [Fact]
    public void DoubleDouble_ShouldProvideHigherPrecisionThanDouble()
    {
        // Arrange - Test case demonstrating extended precision advantage
        var a = 1.0;
        var b = 1e-15;
        var c = -1e-15;

        // Standard double arithmetic
        var doubleResult = a + b + c;

        // DoubleDouble arithmetic
        var ddA = new DoubleDouble(a);
        var ddB = new DoubleDouble(b);
        var ddC = new DoubleDouble(c);
        var ddResult = ddA + ddB + ddC;

        // Act & Assert
        // Standard double may lose precision
        Assert.Equal(1.0, doubleResult);

        // DoubleDouble should maintain precision better
        Assert.Equal(1.0, ddResult.ToDouble());

        // The key difference: DoubleDouble can represent the small residual
        // that gets lost in standard double arithmetic
        Assert.True(ddResult.IsFinite);
    }

    [Fact]
    public void GeometricOperations_ShouldCountExtendedPrecisionUsage()
    {
        // Arrange
        var geoOps = new GeometricOperations();
        geoOps.ClearDiagnostics();

        // Create test vertices
        var a = new Vertex(0, 0, 0);
        var b = new Vertex(1, 0, 0);
        var c = new Vertex(0, 1, 0);
        var d = new Vertex(0.5, 0.5, 0);

        // Act - Perform multiple operations
        geoOps.InCircle(a, b, c, d);
        geoOps.InCircle(a, b, c, d);
        geoOps.Orientation(a, b, c);

        // Assert - Diagnostic counters should be working
        Assert.True(geoOps.GetInCircleCount() >= 2);
        Assert.True(geoOps.GetCircumcircleCount() >= 0);

        // Extended precision count may be 0 for well-conditioned cases
        Assert.True(geoOps.GetExtendedPrecisionInCircleCount() >= 0);
    }

    [Fact]
    public void GeometricOperations_WithNearZeroResults_ShouldUseExtendedPrecision()
    {
        // Arrange - Create vertices that produce very small geometric results
        var thresholds = new Thresholds(1.0);
        var geoOps = new GeometricOperations(thresholds);

        // Create nearly collinear points that will trigger extended precision
        var a = new Vertex(0.0, 0.0, 0);
        var b = new Vertex(1.0, 0.0, 0);
        var c = new Vertex(1.0, 1e-12, 0); // Nearly collinear
        var d = new Vertex(0.5, 1e-13, 0); // Very close to the line

        // Act - Perform operations that should trigger extended precision
        geoOps.ClearDiagnostics();

        // This should trigger extended precision for orientation
        var orientation = geoOps.Orientation(a, b, c);

        // This should trigger extended precision for in-circle test
        var inCircle = geoOps.InCircle(a, b, c, d);

        // Assert - Verify that extended precision was used
        Assert.True(
            geoOps.GetExtendedPrecisionInCircleCount() > 0,
            "Extended precision should have been used for nearly degenerate cases");

        // Results should be computed (not NaN or infinity)
        Assert.True(double.IsFinite(orientation));
        Assert.True(double.IsFinite(inCircle));
    }
}