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

namespace Tinfour.Core.Interpolation;

/// <summary>
///     Raster data storage using 32-bit floating point values.
/// </summary>
/// <remarks>
///     <para>
///         This implementation uses 4 bytes per cell (half of Float64),
///         providing approximately 7 significant digits of precision.
///         This is sufficient for most bathymetric and terrain applications.
///     </para>
///     <para>
///         NaN values are stored natively as float.NaN.
///     </para>
/// </remarks>
public class Float32RasterData : IRasterData
{
    private readonly float[,] _data;

    /// <summary>
    ///     Creates a new Float32RasterData with the specified dimensions.
    /// </summary>
    /// <param name="width">The width in cells.</param>
    /// <param name="height">The height in cells.</param>
    public Float32RasterData(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive");

        Width = width;
        Height = height;
        _data = new float[width, height];

        // Initialize with NaN (no-data)
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            _data[x, y] = float.NaN;
    }

    /// <inheritdoc />
    public int Width { get; }

    /// <inheritdoc />
    public int Height { get; }

    /// <inheritdoc />
    public RasterDataType DataType => RasterDataType.Float32;

    /// <inheritdoc />
    public long MemorySize => (long)Width * Height * sizeof(float);

    /// <inheritdoc />
    public double GetValue(int x, int y)
    {
        return _data[x, y];
    }

    /// <inheritdoc />
    public void SetValue(int x, int y, double value)
    {
        _data[x, y] = (float)value;
    }

    /// <inheritdoc />
    public Array GetBackingArray()
    {
        return _data;
    }

    /// <summary>
    ///     Gets direct access to the backing float array.
    /// </summary>
    /// <returns>The float[,] backing array.</returns>
    public float[,] GetFloatArray()
    {
        return _data;
    }
}
