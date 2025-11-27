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

public class CoordinatePairTests
{
    [Fact]
    public void AdditionOperator_ShouldAddCoordinates()
    {
        // Arrange
        var pair1 = new CoordinatePair(1.5, 2.5);
        var pair2 = new CoordinatePair(3.0, 4.0);

        // Act
        var result = pair1 + pair2;

        // Assert
        Assert.Equal(4.5, result.X);
        Assert.Equal(6.5, result.Y);
    }

    [Fact]
    public void Constructor_Default_ShouldCreateZeroCoordinates()
    {
        // Arrange & Act
        var pair = new CoordinatePair();

        // Assert
        Assert.Equal(0.0, pair.X);
        Assert.Equal(0.0, pair.Y);
    }

    [Fact]
    public void Constructor_WithValues_ShouldSetCoordinates()
    {
        // Arrange & Act
        var pair = new CoordinatePair(3.14, 2.71);

        // Assert
        Assert.Equal(3.14, pair.X);
        Assert.Equal(2.71, pair.Y);
    }

    [Fact]
    public void CopyFrom_ShouldCopyCoordinates()
    {
        // Arrange
        var source = new CoordinatePair(7.5, 8.5);
        var target = new CoordinatePair();

        // Act
        target.CopyFrom(source);

        // Assert
        Assert.Equal(7.5, target.X);
        Assert.Equal(8.5, target.Y);
    }

    [Fact]
    public void DistanceCalculations_ShouldBeConsistent()
    {
        // Arrange
        var pair1 = new CoordinatePair(0, 0);
        var pair2 = new CoordinatePair(1, 1);

        // Act
        var distance1 = pair1.DistanceTo(pair2);
        var distance2 = pair1.DistanceTo(1.0, 1.0);
        var distanceSquared = pair1.DistanceSquaredTo(pair2);

        // Assert
        Assert.Equal(distance1, distance2, 10);
        Assert.Equal(distance1 * distance1, distanceSquared, 10);
        Assert.Equal(Math.Sqrt(2), distance1, 10);
    }

    [Fact]
    public void DistanceSquaredTo_ShouldBeMoreEfficient()
    {
        // Arrange
        var pair1 = new CoordinatePair(1, 1);
        var pair2 = new CoordinatePair(4, 5);

        // Act
        var distanceSquared = pair1.DistanceSquaredTo(pair2);
        var distance = pair1.DistanceTo(pair2);

        // Assert
        Assert.Equal(distance * distance, distanceSquared, 10);
        Assert.Equal(25.0, distanceSquared, 10); // sqrt((4-1)^2 + (5-1)^2) = sqrt(9+16) = 5, squared = 25
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(0, 0, 1, 0, 1)]
    [InlineData(0, 0, 0, 1, 1)]
    [InlineData(0, 0, 3, 4, 5)]
    [InlineData(-1, -1, 2, 3, 5)]
    public void DistanceTo_ShouldCalculateCorrectDistance(
        double x1,
        double y1,
        double x2,
        double y2,
        double expectedDistance)
    {
        // Arrange
        var pair1 = new CoordinatePair(x1, y1);
        var pair2 = new CoordinatePair(x2, y2);

        // Act
        var distance = pair1.DistanceTo(pair2);

        // Assert
        Assert.Equal(expectedDistance, distance, 10);
    }

    [Fact]
    public void DistanceTo_WithAnotherPair_ShouldCalculateCorrectly()
    {
        // Arrange
        var pair1 = new CoordinatePair(0, 0);
        var pair2 = new CoordinatePair(3, 4);

        // Act
        var distance = pair1.DistanceTo(pair2);

        // Assert
        Assert.Equal(5.0, distance, 10); // 3-4-5 right triangle
    }

    [Fact]
    public void DistanceTo_WithCoordinates_ShouldCalculateCorrectly()
    {
        // Arrange
        var pair = new CoordinatePair(2, 3);

        // Act
        var distance = pair.DistanceTo(5, 7);

        // Assert
        Assert.Equal(5.0, distance, 10); // sqrt((5-2)^2 + (7-3)^2) = sqrt(9+16) = 5
    }

    [Fact]
    public void DivisionOperator_ShouldDivideCoordinates()
    {
        // Arrange
        var pair = new CoordinatePair(6.0, 8.0);

        // Act
        var result = pair / 2.0;

        // Assert
        Assert.Equal(3.0, result.X);
        Assert.Equal(4.0, result.Y);
    }

    [Fact]
    public void Equals_WithDifferentCoordinates_ShouldReturnFalse()
    {
        // Arrange
        var pair1 = new CoordinatePair(1.5, 2.5);
        var pair2 = new CoordinatePair(1.5, 2.6);

        // Act & Assert
        Assert.False(pair1.Equals(pair2));
        Assert.False(pair1 == pair2);
        Assert.True(pair1 != pair2);
    }

    [Fact]
    public void Equals_WithObject_ShouldWorkCorrectly()
    {
        // Arrange
        var pair = new CoordinatePair(1.0, 2.0);
        object samePair = new CoordinatePair(1.0, 2.0);
        object differentPair = new CoordinatePair(1.0, 3.0);
        object notAPair = "not a coordinate pair";

        // Act & Assert
        Assert.True(pair.Equals(samePair));
        Assert.False(pair.Equals(differentPair));
        Assert.False(pair.Equals(notAPair));
        Assert.False(pair.Equals(null));
    }

    [Fact]
    public void Equals_WithSameCoordinates_ShouldReturnTrue()
    {
        // Arrange
        var pair1 = new CoordinatePair(1.5, 2.5);
        var pair2 = new CoordinatePair(1.5, 2.5);

        // Act & Assert
        Assert.True(pair1.Equals(pair2));
        Assert.True(pair1 == pair2);
        Assert.False(pair1 != pair2);
    }

    [Fact]
    public void GetHashCode_ShouldBeConsistent()
    {
        // Arrange
        var pair1 = new CoordinatePair(3.14, 2.71);
        var pair2 = new CoordinatePair(3.14, 2.71);
        var pair3 = new CoordinatePair(3.14, 2.72);

        // Act & Assert
        Assert.Equal(pair1.GetHashCode(), pair2.GetHashCode());
        Assert.NotEqual(pair1.GetHashCode(), pair3.GetHashCode());
    }

    [Fact]
    public void ImplicitConversion_FromTuple_ShouldWork()
    {
        // Act
        CoordinatePair pair = (3.14, 2.71);

        // Assert
        Assert.Equal(3.14, pair.X);
        Assert.Equal(2.71, pair.Y);
    }

    [Fact]
    public void ImplicitConversion_ToTuple_ShouldWork()
    {
        // Arrange
        var pair = new CoordinatePair(1.41, 1.73);

        // Act
        var (x, y) = pair;

        // Assert
        Assert.Equal(1.41, x);
        Assert.Equal(1.73, y);
    }

    [Fact]
    public void MultiplicationOperator_WithScalar_ShouldScaleCoordinates()
    {
        // Arrange
        var pair = new CoordinatePair(3.0, 4.0);

        // Act
        var result1 = pair * 2.0;
        var result2 = 2.0 * pair;

        // Assert
        Assert.Equal(6.0, result1.X);
        Assert.Equal(8.0, result1.Y);
        Assert.Equal(6.0, result2.X);
        Assert.Equal(8.0, result2.Y);
    }

    [Fact]
    public void Set_ShouldUpdateBothCoordinates()
    {
        // Arrange
        var pair = new CoordinatePair(1, 1);

        // Act
        pair.Set(10, 20);

        // Assert
        Assert.Equal(10.0, pair.X);
        Assert.Equal(20.0, pair.Y);
    }

    [Fact]
    public void SubtractionOperator_ShouldSubtractCoordinates()
    {
        // Arrange
        var pair1 = new CoordinatePair(5.0, 7.0);
        var pair2 = new CoordinatePair(2.0, 3.0);

        // Act
        var result = pair1 - pair2;

        // Assert
        Assert.Equal(3.0, result.X);
        Assert.Equal(4.0, result.Y);
    }

    [Fact]
    public void ToString_ShouldFormatCorrectly()
    {
        // Arrange
        var pair = new CoordinatePair(1.23, 4.56);

        // Act
        var result = pair.ToString();

        // Assert
        Assert.Contains("1.23", result);
        Assert.Contains("4.56", result);
        Assert.Contains("(", result);
        Assert.Contains(")", result);
        Assert.Contains(",", result);
    }

    [Fact]
    public void ToString_WithFormat_ShouldFormatCorrectly()
    {
        // Arrange
        var pair = new CoordinatePair(1.23456, 7.89012);

        // Act
        var result = pair.ToString("F2");

        // Assert
        Assert.Contains("1.23", result);
        Assert.Contains("7.89", result);
    }
}