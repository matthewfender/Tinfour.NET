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
///     Raster data storage using 16-bit signed integers with scale and offset.
/// </summary>
/// <remarks>
///     <para>
///         This implementation uses 2 bytes per cell (quarter of Float64),
///         applying a linear transformation: value = (rawValue * Scale) + Offset.
///     </para>
///     <para>
///         For bathymetric data with depths from -500m to +100m, using Scale=0.01
///         and Offset=0 allows storing values with 1cm precision in the range
///         approximately -327m to +327m.
///     </para>
///     <para>
///         The value short.MinValue (-32768) is reserved as the NaN sentinel.
///         Valid stored values range from -32767 to 32767.
///     </para>
/// </remarks>
public class Int16ScaledRasterData : IRasterData
{
    /// <summary>
    ///     The sentinel value used to represent NaN (no-data).
    /// </summary>
    public const short NaNSentinel = short.MinValue;

    private readonly short[,] _data;

    /// <summary>
    ///     Creates a new Int16ScaledRasterData with the specified dimensions and scaling.
    /// </summary>
    /// <param name="width">The width in cells.</param>
    /// <param name="height">The height in cells.</param>
    /// <param name="scale">
    ///     The scale factor. Stored value = (actual value - offset) / scale.
    ///     For 1cm resolution, use 0.01.
    /// </param>
    /// <param name="offset">
    ///     The offset value. Allows shifting the representable range.
    ///     For depth data centered around 0, use 0.
    /// </param>
    public Int16ScaledRasterData(int width, int height, double scale, double offset = 0.0)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive");
        if (scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be positive");

        Width = width;
        Height = height;
        Scale = scale;
        Offset = offset;
        _data = new short[width, height];

        // Initialize with NaN sentinel
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            _data[x, y] = NaNSentinel;
    }

    /// <summary>
    ///     Gets the scale factor used for conversion.
    ///     Actual value = (stored value * Scale) + Offset.
    /// </summary>
    public double Scale { get; }

    /// <summary>
    ///     Gets the offset used for conversion.
    ///     Actual value = (stored value * Scale) + Offset.
    /// </summary>
    public double Offset { get; }

    /// <summary>
    ///     Gets the minimum representable value (excluding NaN).
    /// </summary>
    public double MinRepresentableValue => (short.MinValue + 1) * Scale + Offset;

    /// <summary>
    ///     Gets the maximum representable value.
    /// </summary>
    public double MaxRepresentableValue => short.MaxValue * Scale + Offset;

    /// <inheritdoc />
    public int Width { get; }

    /// <inheritdoc />
    public int Height { get; }

    /// <inheritdoc />
    public RasterDataType DataType => RasterDataType.Int16Scaled;

    /// <inheritdoc />
    public long MemorySize => (long)Width * Height * sizeof(short);

    /// <inheritdoc />
    public double GetValue(int x, int y)
    {
        var raw = _data[x, y];
        if (raw == NaNSentinel)
            return double.NaN;

        return raw * Scale + Offset;
    }

    /// <inheritdoc />
    public void SetValue(int x, int y, double value)
    {
        if (double.IsNaN(value))
        {
            _data[x, y] = NaNSentinel;
            return;
        }

        // Convert to scaled integer
        var scaled = (value - Offset) / Scale;

        // Clamp to valid range (excluding NaN sentinel)
        if (scaled <= short.MinValue)
            _data[x, y] = (short)(short.MinValue + 1);
        else if (scaled >= short.MaxValue)
            _data[x, y] = short.MaxValue;
        else
            _data[x, y] = (short)Math.Round(scaled);
    }

    /// <inheritdoc />
    public Array GetBackingArray()
    {
        return _data;
    }

    /// <summary>
    ///     Gets direct access to the backing short array.
    /// </summary>
    /// <returns>The short[,] backing array.</returns>
    public short[,] GetInt16Array()
    {
        return _data;
    }
}
