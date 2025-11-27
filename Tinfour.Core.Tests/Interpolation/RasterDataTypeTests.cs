/*
 * Copyright 2025 Gary W. Lucas / ReefMaster Software Ltd.
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

namespace Tinfour.Core.Tests.Interpolation;

using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;
using Tinfour.Core.Standard;

using Xunit;

/// <summary>
///     Tests for flexible raster data types (Float32, Float64, Int16Scaled).
/// </summary>
public class RasterDataTypeTests
{
    private static IIncrementalTin CreateTestTin()
    {
        var tin = new IncrementalTin();
        var vertices = new List<IVertex>
        {
            new Vertex(0, 0, 0),
            new Vertex(100, 0, 50),
            new Vertex(100, 100, 100),
            new Vertex(0, 100, 50),
            new Vertex(50, 50, 50)
        };
        tin.Add(vertices);
        return tin;
    }

    #region Float64RasterData Tests

    [Fact]
    public void Float64RasterData_Constructor_CreatesCorrectSize()
    {
        // Arrange & Act
        var raster = new Float64RasterData(100, 50);

        // Assert
        Assert.Equal(100, raster.Width);
        Assert.Equal(50, raster.Height);
        Assert.Equal(RasterDataType.Float64, raster.DataType);
    }

    [Fact]
    public void Float64RasterData_InitializesWithNaN()
    {
        // Arrange & Act
        var raster = new Float64RasterData(10, 10);

        // Assert - all values should be NaN
        for (var y = 0; y < 10; y++)
        for (var x = 0; x < 10; x++)
            Assert.True(double.IsNaN(raster.GetValue(x, y)));
    }

    [Fact]
    public void Float64RasterData_SetAndGetValue_RoundTrips()
    {
        // Arrange
        var raster = new Float64RasterData(10, 10);
        var testValue = 123.456789012345;

        // Act
        raster.SetValue(5, 5, testValue);
        var result = raster.GetValue(5, 5);

        // Assert - full double precision preserved
        Assert.Equal(testValue, result);
    }

    [Fact]
    public void Float64RasterData_MemorySize_CalculatedCorrectly()
    {
        // Arrange
        var raster = new Float64RasterData(100, 100);

        // Assert - 100*100*8 bytes = 80,000 bytes
        Assert.Equal(80000L, raster.MemorySize);
    }

    [Fact]
    public void Float64RasterData_GetDoubleArray_ReturnsBackingArray()
    {
        // Arrange
        var raster = new Float64RasterData(10, 10);
        raster.SetValue(3, 4, 42.0);

        // Act
        var array = raster.GetDoubleArray();

        // Assert
        Assert.Equal(42.0, array[3, 4]);
    }

    [Fact]
    public void Float64RasterData_FromExistingArray_WrapsCorrectly()
    {
        // Arrange
        var data = new double[20, 15];
        data[5, 10] = 99.0;

        // Act
        var raster = new Float64RasterData(data);

        // Assert
        Assert.Equal(20, raster.Width);
        Assert.Equal(15, raster.Height);
        Assert.Equal(99.0, raster.GetValue(5, 10));
    }

    #endregion

    #region Float32RasterData Tests

    [Fact]
    public void Float32RasterData_Constructor_CreatesCorrectSize()
    {
        // Arrange & Act
        var raster = new Float32RasterData(100, 50);

        // Assert
        Assert.Equal(100, raster.Width);
        Assert.Equal(50, raster.Height);
        Assert.Equal(RasterDataType.Float32, raster.DataType);
    }

    [Fact]
    public void Float32RasterData_InitializesWithNaN()
    {
        // Arrange & Act
        var raster = new Float32RasterData(10, 10);

        // Assert
        for (var y = 0; y < 10; y++)
        for (var x = 0; x < 10; x++)
            Assert.True(double.IsNaN(raster.GetValue(x, y)));
    }

    [Fact]
    public void Float32RasterData_SetAndGetValue_RoundTripsWithReducedPrecision()
    {
        // Arrange
        var raster = new Float32RasterData(10, 10);
        var testValue = 123.456789f; // Single precision

        // Act
        raster.SetValue(5, 5, testValue);
        var result = raster.GetValue(5, 5);

        // Assert - float precision (~7 decimal digits)
        Assert.Equal(testValue, result, 5);
    }

    [Fact]
    public void Float32RasterData_MemorySize_HalfOfFloat64()
    {
        // Arrange
        var raster = new Float32RasterData(100, 100);

        // Assert - 100*100*4 bytes = 40,000 bytes (half of Float64)
        Assert.Equal(40000L, raster.MemorySize);
    }

    [Fact]
    public void Float32RasterData_GetFloatArray_ReturnsBackingArray()
    {
        // Arrange
        var raster = new Float32RasterData(10, 10);
        raster.SetValue(3, 4, 42.0);

        // Act
        var array = raster.GetFloatArray();

        // Assert
        Assert.Equal(42.0f, array[3, 4]);
    }

    [Fact]
    public void Float32RasterData_HandlesNaN()
    {
        // Arrange
        var raster = new Float32RasterData(10, 10);

        // Act
        raster.SetValue(5, 5, double.NaN);

        // Assert
        Assert.True(double.IsNaN(raster.GetValue(5, 5)));
    }

    #endregion

    #region Int16ScaledRasterData Tests

    [Fact]
    public void Int16ScaledRasterData_Constructor_CreatesCorrectSize()
    {
        // Arrange & Act
        var raster = new Int16ScaledRasterData(100, 50, scale: 0.01);

        // Assert
        Assert.Equal(100, raster.Width);
        Assert.Equal(50, raster.Height);
        Assert.Equal(RasterDataType.Int16Scaled, raster.DataType);
    }

    [Fact]
    public void Int16ScaledRasterData_InitializesWithNaN()
    {
        // Arrange & Act
        var raster = new Int16ScaledRasterData(10, 10, scale: 0.01);

        // Assert
        for (var y = 0; y < 10; y++)
        for (var x = 0; x < 10; x++)
            Assert.True(double.IsNaN(raster.GetValue(x, y)));
    }

    [Fact]
    public void Int16ScaledRasterData_SetAndGetValue_RoundTripsWithScale()
    {
        // Arrange
        var raster = new Int16ScaledRasterData(10, 10, scale: 0.01);
        var testValue = 123.45; // This should round to 123.45 with 0.01 scale

        // Act
        raster.SetValue(5, 5, testValue);
        var result = raster.GetValue(5, 5);

        // Assert - should be within scale resolution
        Assert.Equal(testValue, result, 2); // 2 decimal places with 0.01 scale
    }

    [Fact]
    public void Int16ScaledRasterData_WithOffset_CalculatesCorrectly()
    {
        // Arrange
        var raster = new Int16ScaledRasterData(10, 10, scale: 0.01, offset: -50.0);
        var testValue = -45.67;

        // Act
        raster.SetValue(5, 5, testValue);
        var result = raster.GetValue(5, 5);

        // Assert
        Assert.Equal(testValue, result, 2);
    }

    [Fact]
    public void Int16ScaledRasterData_MemorySize_QuarterOfFloat64()
    {
        // Arrange
        var raster = new Int16ScaledRasterData(100, 100, scale: 0.01);

        // Assert - 100*100*2 bytes = 20,000 bytes (quarter of Float64)
        Assert.Equal(20000L, raster.MemorySize);
    }

    [Fact]
    public void Int16ScaledRasterData_Scale_ReportsCorrectly()
    {
        // Arrange
        var raster = new Int16ScaledRasterData(10, 10, scale: 0.05, offset: 10.0);

        // Assert
        Assert.Equal(0.05, raster.Scale);
        Assert.Equal(10.0, raster.Offset);
    }

    [Fact]
    public void Int16ScaledRasterData_MinMaxRepresentable_CalculatedCorrectly()
    {
        // Arrange - scale=1.0, offset=0 gives range [-32767, 32767]
        var raster = new Int16ScaledRasterData(10, 10, scale: 1.0, offset: 0);

        // Assert
        Assert.Equal(-32767, raster.MinRepresentableValue);
        Assert.Equal(32767, raster.MaxRepresentableValue);

        // With scale=0.01, offset=0: range is [-327.67, 327.67]
        var raster2 = new Int16ScaledRasterData(10, 10, scale: 0.01, offset: 0);
        Assert.Equal(-327.67, raster2.MinRepresentableValue, 2);
        Assert.Equal(327.67, raster2.MaxRepresentableValue, 2);
    }

    [Fact]
    public void Int16ScaledRasterData_ClampsOutOfRangeValues()
    {
        // Arrange
        var raster = new Int16ScaledRasterData(10, 10, scale: 0.01);

        // Act - try to store value outside representable range
        raster.SetValue(5, 5, 500.0); // Max is ~327.67

        // Assert - should be clamped to max
        Assert.Equal(327.67, raster.GetValue(5, 5), 2);
    }

    [Fact]
    public void Int16ScaledRasterData_HandlesNaN()
    {
        // Arrange
        var raster = new Int16ScaledRasterData(10, 10, scale: 0.01);
        raster.SetValue(3, 3, 50.0); // Set a valid value first

        // Act
        raster.SetValue(3, 3, double.NaN);

        // Assert
        Assert.True(double.IsNaN(raster.GetValue(3, 3)));
    }

    [Fact]
    public void Int16ScaledRasterData_GetInt16Array_ReturnsBackingArray()
    {
        // Arrange
        var raster = new Int16ScaledRasterData(10, 10, scale: 0.01);
        raster.SetValue(3, 4, 42.0);

        // Act
        var array = raster.GetInt16Array();

        // Assert - stored value should be 4200 (42.0 / 0.01)
        Assert.Equal(4200, array[3, 4]);
    }

    [Fact]
    public void Int16ScaledRasterData_ForBathymetry_HandlesTypicalRange()
    {
        // Arrange - typical bathymetric data range: -500m to +50m
        // Using scale=0.01 and offset=0 gives range ~[-327.67, 327.67]
        // For deeper water, use scale=0.1 for range ~[-3276.7, 3276.7]
        var raster = new Int16ScaledRasterData(10, 10, scale: 0.1, offset: 0);

        // Act & Assert - test typical depth values
        raster.SetValue(0, 0, -250.5);
        Assert.Equal(-250.5, raster.GetValue(0, 0), 1);

        raster.SetValue(1, 0, 15.3);
        Assert.Equal(15.3, raster.GetValue(1, 0), 1);

        raster.SetValue(2, 0, -1000.0);
        Assert.Equal(-1000.0, raster.GetValue(2, 0), 1);
    }

    #endregion

    #region TinRasterizer with Different Data Types Tests

    [Fact]
    public void TinRasterizer_Float64_ProducesCorrectResults()
    {
        // Arrange
        using var tin = CreateTestTin();
        var rasterizer = new TinRasterizer(tin, InterpolationType.TriangularFacet);

        // Act
        var result = rasterizer.CreateRaster(10, 10, RasterDataType.Float64);

        // Assert
        Assert.NotNull(result.Data); // Float64 exposes Data property
        Assert.IsType<Float64RasterData>(result.RasterData);
        Assert.Equal(RasterDataType.Float64, result.RasterData.DataType);
    }

    [Fact]
    public void TinRasterizer_Float32_ProducesCorrectResults()
    {
        // Arrange
        using var tin = CreateTestTin();
        var rasterizer = new TinRasterizer(tin, InterpolationType.TriangularFacet);

        // Act
        var result = rasterizer.CreateRaster(10, 10, RasterDataType.Float32);

        // Assert
        Assert.Null(result.Data); // Float32 does not expose Data as double[,]
        Assert.IsType<Float32RasterData>(result.RasterData);
        Assert.Equal(RasterDataType.Float32, result.RasterData.DataType);
    }

    [Fact]
    public void TinRasterizer_Int16Scaled_ProducesCorrectResults()
    {
        // Arrange
        using var tin = CreateTestTin();
        var rasterizer = new TinRasterizer(tin, InterpolationType.TriangularFacet);

        // Act
        var result = rasterizer.CreateRaster(10, 10, RasterDataType.Int16Scaled,
            int16Scale: 0.01, int16Offset: 0);

        // Assert
        Assert.Null(result.Data);
        Assert.IsType<Int16ScaledRasterData>(result.RasterData);
        Assert.Equal(RasterDataType.Int16Scaled, result.RasterData.DataType);

        var int16Data = (Int16ScaledRasterData)result.RasterData;
        Assert.Equal(0.01, int16Data.Scale);
        Assert.Equal(0.0, int16Data.Offset);
    }

    [Fact]
    public void TinRasterizer_DifferentTypes_ProduceSimilarValues()
    {
        // Arrange
        using var tin = CreateTestTin();
        var rasterizer = new TinRasterizer(tin, InterpolationType.TriangularFacet);

        // Act
        var float64Result = rasterizer.CreateRaster(10, 10, RasterDataType.Float64);
        var float32Result = rasterizer.CreateRaster(10, 10, RasterDataType.Float32);
        var int16Result = rasterizer.CreateRaster(10, 10, RasterDataType.Int16Scaled, int16Scale: 0.01);

        // Assert - compare values at various points
        for (var y = 0; y < 10; y++)
        for (var x = 0; x < 10; x++)
        {
            var f64 = float64Result.RasterData.GetValue(x, y);
            var f32 = float32Result.RasterData.GetValue(x, y);
            var i16 = int16Result.RasterData.GetValue(x, y);

            if (!double.IsNaN(f64))
            {
                // Float32 should be within single-precision tolerance
                Assert.Equal(f64, f32, 4);

                // Int16Scaled should be within scale tolerance (0.01)
                Assert.Equal(f64, i16, 2);
            }
        }
    }

    [Fact]
    public void TinRasterizer_MemoryUsage_VariesByType()
    {
        // Arrange
        using var tin = CreateTestTin();
        var rasterizer = new TinRasterizer(tin, InterpolationType.TriangularFacet);

        // Act
        var float64Result = rasterizer.CreateRaster(100, 100, RasterDataType.Float64);
        var float32Result = rasterizer.CreateRaster(100, 100, RasterDataType.Float32);
        var int16Result = rasterizer.CreateRaster(100, 100, RasterDataType.Int16Scaled, int16Scale: 0.01);

        // Assert
        Assert.Equal(80000L, float64Result.RasterData.MemorySize);  // 100*100*8
        Assert.Equal(40000L, float32Result.RasterData.MemorySize);  // 100*100*4
        Assert.Equal(20000L, int16Result.RasterData.MemorySize);    // 100*100*2
    }

    #endregion

    #region RasterResult Tests

    [Fact]
    public void RasterResult_WithFloat64_ExposesDataProperty()
    {
        // Arrange
        var data = new Float64RasterData(10, 10);
        data.SetValue(5, 5, 42.0);

        var bounds = (Left: 0.0, Top: 0.0, Width: 10.0, Height: 10.0);

        // Act
        var result = new RasterResult(data, bounds, 1.0, 1.0, 0);

        // Assert
        Assert.NotNull(result.Data);
        Assert.Equal(42.0, result.Data![5, 5]);
    }

    [Fact]
    public void RasterResult_WithFloat32_DataPropertyIsNull()
    {
        // Arrange
        var data = new Float32RasterData(10, 10);
        var bounds = (Left: 0.0, Top: 0.0, Width: 10.0, Height: 10.0);

        // Act
        var result = new RasterResult(data, bounds, 1.0, 1.0, 0);

        // Assert
        Assert.Null(result.Data);
        Assert.NotNull(result.RasterData);
    }

    [Fact]
    public void RasterResult_GetStatistics_WorksWithAllTypes()
    {
        // Arrange
        using var tin = CreateTestTin();
        var rasterizer = new TinRasterizer(tin, InterpolationType.TriangularFacet);

        // Act & Assert for each type
        foreach (var dataType in new[] { RasterDataType.Float64, RasterDataType.Float32, RasterDataType.Int16Scaled })
        {
            var result = rasterizer.CreateRaster(10, 10, dataType, int16Scale: 0.01);
            var stats = result.GetStatistics();

            Assert.False(double.IsNaN(stats.Min));
            Assert.False(double.IsNaN(stats.Max));
            Assert.False(double.IsNaN(stats.Mean));
            Assert.True(stats.StdDev >= 0);
        }
    }

    [Fact]
    public void RasterResult_LegacyConstructor_StillWorks()
    {
        // Arrange
        var data = new double[10, 10];
        data[5, 5] = 42.0;
        var bounds = (Left: 0.0, Top: 0.0, Width: 10.0, Height: 10.0);

        // Act - use legacy constructor
        var result = new RasterResult(data, bounds, 10, 10, 1.0, 1.0, 0);

        // Assert
        Assert.NotNull(result.Data);
        Assert.Equal(42.0, result.Data![5, 5]);
        Assert.IsType<Float64RasterData>(result.RasterData);
    }

    #endregion

    #region Edge Cases and Validation

    [Fact]
    public void Float64RasterData_ThrowsOnInvalidDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Float64RasterData(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Float64RasterData(10, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Float64RasterData(-1, 10));
    }

    [Fact]
    public void Float32RasterData_ThrowsOnInvalidDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Float32RasterData(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Float32RasterData(10, 0));
    }

    [Fact]
    public void Int16ScaledRasterData_ThrowsOnInvalidParameters()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Int16ScaledRasterData(0, 10, 0.01));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Int16ScaledRasterData(10, 0, 0.01));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Int16ScaledRasterData(10, 10, 0)); // scale must be positive
        Assert.Throws<ArgumentOutOfRangeException>(() => new Int16ScaledRasterData(10, 10, -0.01)); // scale must be positive
    }

    [Fact]
    public void Float64RasterData_ThrowsOnNullArray()
    {
        Assert.Throws<ArgumentNullException>(() => new Float64RasterData(null!));
    }

    #endregion
}
