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

public class DoubleDoubleTests
{
    [Fact]
    public void Abs_ShouldReturnAbsoluteValue()
    {
        // Arrange
        var positive = new DoubleDouble(3.14);
        var negative = new DoubleDouble(-3.14);
        var zero = DoubleDouble.Zero;

        // Act
        var absPositive = DoubleDouble.Abs(positive);
        var absNegative = DoubleDouble.Abs(negative);
        var absZero = DoubleDouble.Abs(zero);

        // Assert
        Assert.Equal(3.14, absPositive.ToDouble(), 14);
        Assert.Equal(3.14, absNegative.ToDouble(), 14);
        Assert.True(absZero.IsZero);
    }

    [Fact]
    public void Addition_WithDouble_ShouldWork()
    {
        // Arrange
        var a = new DoubleDouble(1.0, 1e-16);

        // Act
        var result = a + 2.0;

        // Assert
        Assert.Equal(3.0 + 1e-16, result.ToDouble(), 15);
    }

    [Fact]
    public void Addition_WithDoubleDoubles_ShouldBeAccurate()
    {
        // Arrange
        var a = new DoubleDouble(1.0, 1e-16);
        var b = new DoubleDouble(2.0, 2e-16);

        // Act
        var result = a + b;

        // Assert
        Assert.Equal(3.0, result.Hi, 14);
        Assert.Equal(3e-16, result.Lo, 15);
        Assert.Equal(3.0 + 3e-16, result.ToDouble(), 15);
    }

    [Fact]
    public void CompareTo_ShouldReturnCorrectOrdering()
    {
        // Arrange
        var a = new DoubleDouble(1.0);
        var b = new DoubleDouble(2.0);
        var c = new DoubleDouble(1.0);

        // Act & Assert
        Assert.True(a.CompareTo(b) < 0);
        Assert.True(b.CompareTo(a) > 0);
        Assert.Equal(0, a.CompareTo(c));
    }

    [Fact]
    public void Constants_ShouldHaveCorrectValues()
    {
        // Assert
        Assert.True(DoubleDouble.Zero.IsZero);
        Assert.Equal(0.0, DoubleDouble.Zero.ToDouble());

        Assert.Equal(1.0, DoubleDouble.One.ToDouble());

        Assert.True(DoubleDouble.PositiveInfinity.IsInfinity);
        Assert.True(double.IsPositiveInfinity(DoubleDouble.PositiveInfinity.ToDouble()));

        Assert.True(DoubleDouble.NegativeInfinity.IsInfinity);
        Assert.True(double.IsNegativeInfinity(DoubleDouble.NegativeInfinity.ToDouble()));

        Assert.True(DoubleDouble.NaN.IsNaN);
    }

    [Fact]
    public void Constructor_WithHighAndLow_ShouldCreateCorrectValue()
    {
        // Arrange & Act
        var dd = new DoubleDouble(1.0, 1e-16);

        // Assert
        Assert.Equal(1.0, dd.Hi);
        Assert.Equal(1e-16, dd.Lo);
        Assert.Equal(1.0 + 1e-16, dd.ToDouble());
    }

    [Fact]
    public void Constructor_WithSingleDouble_ShouldCreateCorrectValue()
    {
        // Arrange & Act
        var dd = new DoubleDouble(3.14159);

        // Assert
        Assert.Equal(3.14159, dd.Hi);
        Assert.Equal(0.0, dd.Lo);
        Assert.Equal(3.14159, dd.ToDouble());
    }

    [Fact]
    public void Division_WithDouble_ShouldWork()
    {
        // Arrange
        var a = new DoubleDouble(10.0);

        // Act
        var result = a / 4.0;

        // Assert
        Assert.Equal(2.5, result.ToDouble(), 14);
    }

    [Fact]
    public void Division_WithDoubleDoubles_ShouldBeAccurate()
    {
        // Arrange
        var a = new DoubleDouble(6.0);
        var b = new DoubleDouble(2.0);

        // Act
        var result = a / b;

        // Assert
        Assert.Equal(3.0, result.ToDouble(), 14);
    }

    [Fact]
    public void Equality_ShouldWorkCorrectly()
    {
        // Arrange
        var a = new DoubleDouble(1.0, 1e-16);
        var b = new DoubleDouble(1.0, 1e-16);
        var c = new DoubleDouble(1.0, 2e-16);

        // Act & Assert
        Assert.True(a == b);
        Assert.False(a == c);
        Assert.True(a != c);
        Assert.False(a != b);
    }

    [Fact]
    public void Equals_ShouldWorkWithObject()
    {
        // Arrange
        var a = new DoubleDouble(1.0, 1e-16);
        object b = new DoubleDouble(1.0, 1e-16);
        object c = new DoubleDouble(2.0, 1e-16);
        object d = "not a DoubleDouble";

        // Act & Assert
        Assert.True(a.Equals(b));
        Assert.False(a.Equals(c));
        Assert.False(a.Equals(d));
        Assert.False(a.Equals(null));
    }

    [Fact]
    public void ExplicitConversion_ToDouble_ShouldWork()
    {
        // Arrange
        var dd = new DoubleDouble(3.14159, 1e-16);

        // Act
        var result = (double)dd;

        // Assert
        Assert.Equal(3.14159 + 1e-16, result, 15);
    }

    [Fact]
    public void GetHashCode_ShouldBeConsistent()
    {
        // Arrange
        var a = new DoubleDouble(1.0, 1e-16);
        var b = new DoubleDouble(1.0, 1e-16);
        var c = new DoubleDouble(2.0, 1e-16);

        // Act & Assert
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a.GetHashCode(), c.GetHashCode());
    }

    [Theory]
    [InlineData(2.0, 1.0, true)]
    [InlineData(1.0, 2.0, false)]
    [InlineData(1.0, 1.0, false)]
    public void GreaterThan_ShouldReturnCorrectResult(double a, double b, bool expected)
    {
        // Arrange
        var ddA = new DoubleDouble(a);
        var ddB = new DoubleDouble(b);

        // Act
        var result = ddA > ddB;

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void HighPrecisionAddition_ShouldPreservePrecision()
    {
        // Arrange - Test case where regular double arithmetic loses precision
        var a = 1.0;
        var b = 1e-16;

        var ddA = new DoubleDouble(a);
        var ddB = new DoubleDouble(b);

        // Act
        var ddResult = ddA + ddB;

        // Assert - Double-double should preserve the small component
        // The key test is that DoubleDouble preserves precision better than standard double
        Assert.Equal(1.0, ddResult.Hi);
        Assert.NotEqual(0.0, ddResult.Lo); // DoubleDouble should preserve some precision

        // Verify the sum is mathematically correct
        var expectedSum = a + b;
        Assert.Equal(expectedSum, ddResult.ToDouble(), 15);
    }

    [Fact]
    public void ImplicitConversion_FromDouble_ShouldWork()
    {
        // Act
        DoubleDouble dd = 3.14159;

        // Assert
        Assert.Equal(3.14159, dd.Hi);
        Assert.Equal(0.0, dd.Lo);
    }

    [Fact]
    public void IsFinite_ShouldDetectFiniteNumbers()
    {
        // Arrange
        var finite = new DoubleDouble(1.0, 1e-16);
        var infinite = new DoubleDouble(double.PositiveInfinity);
        var nan = new DoubleDouble(double.NaN);

        // Act & Assert
        Assert.True(finite.IsFinite);
        Assert.False(infinite.IsFinite);
        Assert.False(nan.IsFinite);
    }

    [Fact]
    public void IsInfinity_ShouldDetectInfinity()
    {
        // Arrange
        var posInf = new DoubleDouble(double.PositiveInfinity);
        var negInf = new DoubleDouble(double.NegativeInfinity);
        var number = new DoubleDouble(1.0);

        // Act & Assert
        Assert.True(posInf.IsInfinity);
        Assert.True(negInf.IsInfinity);
        Assert.False(number.IsInfinity);
    }

    [Fact]
    public void IsNaN_ShouldDetectNaN()
    {
        // Arrange
        var nan = new DoubleDouble(double.NaN);
        var number = new DoubleDouble(1.0);

        // Act & Assert
        Assert.True(nan.IsNaN);
        Assert.False(number.IsNaN);
    }

    [Fact]
    public void IsZero_ShouldDetectZero()
    {
        // Arrange
        var zero = new DoubleDouble(0.0, 0.0);
        var nonZero = new DoubleDouble(1e-100);

        // Act & Assert
        Assert.True(zero.IsZero);
        Assert.False(nonZero.IsZero);
    }

    [Theory]
    [InlineData(1.0, 2.0, true)]
    [InlineData(2.0, 1.0, false)]
    [InlineData(1.0, 1.0, false)]
    public void LessThan_ShouldReturnCorrectResult(double a, double b, bool expected)
    {
        // Arrange
        var ddA = new DoubleDouble(a);
        var ddB = new DoubleDouble(b);

        // Act
        var result = ddA < ddB;

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Max_ShouldReturnLargerValue()
    {
        // Arrange
        var a = new DoubleDouble(1.0);
        var b = new DoubleDouble(2.0);

        // Act
        var max1 = DoubleDouble.Max(a, b);
        var max2 = DoubleDouble.Max(b, a);

        // Assert
        Assert.Equal(b, max1);
        Assert.Equal(b, max2);
    }

    [Fact]
    public void Min_ShouldReturnSmallerValue()
    {
        // Arrange
        var a = new DoubleDouble(1.0);
        var b = new DoubleDouble(2.0);

        // Act
        var min1 = DoubleDouble.Min(a, b);
        var min2 = DoubleDouble.Min(b, a);

        // Assert
        Assert.Equal(a, min1);
        Assert.Equal(a, min2);
    }

    [Fact]
    public void Multiplication_WithDouble_ShouldWork()
    {
        // Arrange
        var a = new DoubleDouble(2.5);

        // Act
        var result = a * 4.0;

        // Assert
        Assert.Equal(10.0, result.ToDouble(), 14);
    }

    [Fact]
    public void Multiplication_WithDoubleDoubles_ShouldBeAccurate()
    {
        // Arrange
        var a = new DoubleDouble(2.0);
        var b = new DoubleDouble(3.0);

        // Act
        var result = a * b;

        // Assert
        Assert.Equal(6.0, result.ToDouble(), 14);
    }

    [Fact]
    public void Negation_ShouldWork()
    {
        // Arrange
        var a = new DoubleDouble(3.14159, 1e-16);

        // Act
        var result = -a;

        // Assert
        Assert.Equal(-3.14159, result.Hi, 14);
        Assert.Equal(-1e-16, result.Lo, 15);
    }

    [Fact]
    public void Subtraction_ShouldWork()
    {
        // Arrange
        var a = new DoubleDouble(3.0, 3e-16);
        var b = new DoubleDouble(1.0, 1e-16);

        // Act
        var result = a - b;

        // Assert
        Assert.Equal(2.0, result.Hi, 14);
        Assert.Equal(2e-16, result.Lo, 15);
    }

    [Fact]
    public void ToString_ShouldFormatCorrectly()
    {
        // Arrange
        var number = new DoubleDouble(3.14159);
        var nan = DoubleDouble.NaN;
        var posInf = DoubleDouble.PositiveInfinity;
        var negInf = DoubleDouble.NegativeInfinity;

        // Act & Assert
        Assert.Contains("3.1415", number.ToString());
        Assert.Equal("NaN", nan.ToString());
        Assert.Equal("+Infinity", posInf.ToString());
        Assert.Equal("-Infinity", negInf.ToString());
    }
}