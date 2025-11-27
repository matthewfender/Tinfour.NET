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

public class LinearUnitsTests
{
    [Fact]
    public void Convert_BetweenDifferentUnits_ShouldConvertCorrectly()
    {
        // Arrange
        var meters = 1000.0;

        // Act
        var kilometers = LinearUnits.Meters.Convert(meters, LinearUnits.Kilometers);
        var feet = LinearUnits.Meters.Convert(meters, LinearUnits.Feet);

        // Assert
        Assert.Equal(1.0, kilometers, 10);
        Assert.Equal(1000.0 / 0.3048, feet, 5);
    }

    [Fact]
    public void Convert_BetweenSameUnits_ShouldReturnSameValue()
    {
        // Arrange
        var value = 42.5;

        // Act & Assert
        Assert.Equal(value, LinearUnits.Meters.Convert(value, LinearUnits.Meters));
        Assert.Equal(value, LinearUnits.Feet.Convert(value, LinearUnits.Feet));
    }

    [Fact]
    public void FromMeters_ShouldConvertCorrectly()
    {
        // Arrange & Act & Assert
        Assert.Equal(1.0, LinearUnits.Meters.FromMeters(1.0));
        Assert.Equal(1.0 / 0.3048, LinearUnits.Feet.FromMeters(1.0), 10);
        Assert.Equal(0.001, LinearUnits.Kilometers.FromMeters(1.0), 10);
        Assert.Equal(1.0 / 1609.344, LinearUnits.Miles.FromMeters(1.0), 10);
    }

    [Fact]
    public void GetAbbreviation_ShouldReturnCorrectValues()
    {
        // Arrange & Act & Assert
        Assert.Equal("m", LinearUnits.Meters.GetAbbreviation());
        Assert.Equal("ft", LinearUnits.Feet.GetAbbreviation());
        Assert.Equal("km", LinearUnits.Kilometers.GetAbbreviation());
        Assert.Equal("mi", LinearUnits.Miles.GetAbbreviation());
        Assert.Equal("unknown", LinearUnits.Unknown.GetAbbreviation());
    }

    [Fact]
    public void GetAllUnits_ShouldReturnAllEnumValues()
    {
        // Arrange & Act
        var units = LinearUnitsExtensions.GetAllUnits().ToList();

        // Assert
        Assert.True(units.Count >= 10); // Should have at least the defined units
        Assert.Contains(LinearUnits.Meters, units);
        Assert.Contains(LinearUnits.Feet, units);
        Assert.Contains(LinearUnits.Unknown, units);
    }

    [Fact]
    public void GetConversionFactor_ShouldReturnCorrectFactors()
    {
        // Arrange & Act
        var meterToFeet = LinearUnits.Meters.GetConversionFactor(LinearUnits.Feet);
        var feetToMeter = LinearUnits.Feet.GetConversionFactor(LinearUnits.Meters);
        var sameUnit = LinearUnits.Meters.GetConversionFactor(LinearUnits.Meters);

        // Assert
        Assert.Equal(1.0 / 0.3048, meterToFeet, 10);
        Assert.Equal(0.3048, feetToMeter, 10);
        Assert.Equal(1.0, sameUnit);
    }

    [Fact]
    public void GetName_ShouldReturnCorrectValues()
    {
        // Arrange & Act & Assert
        Assert.Equal("Meters", LinearUnits.Meters.GetName());
        Assert.Equal("Feet", LinearUnits.Feet.GetName());
        Assert.Equal("Kilometers", LinearUnits.Kilometers.GetName());
        Assert.Equal("Miles", LinearUnits.Miles.GetName());
        Assert.Equal("Unknown", LinearUnits.Unknown.GetName());
    }

    [Fact]
    public void IsImperial_ShouldIdentifyImperialUnits()
    {
        // Arrange & Act & Assert
        Assert.True(LinearUnits.Feet.IsImperial());
        Assert.True(LinearUnits.Miles.IsImperial());
        Assert.True(LinearUnits.Inches.IsImperial());

        Assert.False(LinearUnits.Meters.IsImperial());
        Assert.False(LinearUnits.Kilometers.IsImperial());
        Assert.False(LinearUnits.Fathoms.IsImperial());
        Assert.False(LinearUnits.Unknown.IsImperial());
    }

    [Fact]
    public void IsMetric_ShouldIdentifyMetricUnits()
    {
        // Arrange & Act & Assert
        Assert.True(LinearUnits.Meters.IsMetric());
        Assert.True(LinearUnits.Kilometers.IsMetric());
        Assert.True(LinearUnits.Centimeters.IsMetric());
        Assert.True(LinearUnits.Millimeters.IsMetric());

        Assert.False(LinearUnits.Feet.IsMetric());
        Assert.False(LinearUnits.Miles.IsMetric());
        Assert.False(LinearUnits.Inches.IsMetric());
        Assert.False(LinearUnits.Unknown.IsMetric());
    }

    [Fact]
    public void RealWorldConversions_ShouldBeAccurate()
    {
        // Arrange - Test real-world conversions
        var oneMile = 1.0;
        var oneKilometer = 1.0;

        // Act
        var mileToKm = LinearUnits.Miles.Convert(oneMile, LinearUnits.Kilometers);
        var kmToMile = LinearUnits.Kilometers.Convert(oneKilometer, LinearUnits.Miles);

        // Assert - 1 mile ? 1.609 km, 1 km ? 0.621 miles
        Assert.Equal(1.609344, mileToKm, 5);
        Assert.Equal(0.621371, kmToMile, 5);
    }

    [Fact]
    public void ToMeters_ShouldConvertCorrectly()
    {
        // Arrange & Act & Assert
        Assert.Equal(1.0, LinearUnits.Meters.ToMeters(1.0));
        Assert.Equal(0.3048, LinearUnits.Feet.ToMeters(1.0), 10);
        Assert.Equal(1000.0, LinearUnits.Kilometers.ToMeters(1.0));
        Assert.Equal(1609.344, LinearUnits.Miles.ToMeters(1.0), 10);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("xyz")]
    public void TryParse_WithInvalidInput_ShouldReturnFalse(string input)
    {
        // Arrange & Act
        var success = LinearUnitsExtensions.TryParse(input, out var result);

        // Assert
        Assert.False(success);
        Assert.Equal(LinearUnits.Unknown, result);
    }

    [Fact]
    public void TryParse_WithNullInput_ShouldReturnFalse()
    {
        // Arrange & Act
        var success = LinearUnitsExtensions.TryParse(null, out var result);

        // Assert
        Assert.False(success);
        Assert.Equal(LinearUnits.Unknown, result);
    }

    [Theory]
    [InlineData("m", LinearUnits.Meters)]
    [InlineData("ft", LinearUnits.Feet)]
    [InlineData("km", LinearUnits.Kilometers)]
    [InlineData("mi", LinearUnits.Miles)]
    [InlineData("Meters", LinearUnits.Meters)]
    [InlineData("FEET", LinearUnits.Feet)]
    public void TryParse_WithValidInput_ShouldReturnCorrectUnit(string input, LinearUnits expected)
    {
        // Arrange & Act
        var success = LinearUnitsExtensions.TryParse(input, out var result);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, result);
    }
}