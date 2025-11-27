/*
 * Copyright 2025 G.W. Lucas
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

using Tinfour.Core.Common;

/// <summary>
///     Provides functionality for generating raster data from a Triangulated Irregular Network (TIN)
///     using a specified interpolation method.
/// </summary>
/// <remarks>
///     <para>
///         The TinRasterizer converts a TIN into a regular grid of values, where each grid cell contains
///         a value interpolated from the TIN at that location. This is useful for visualization,
///         analysis, and conversion to raster-based formats.
///     </para>
///     <para>
///         The rasterizer supports various interpolation methods through the IInterpolatorOverTin interface,
///         including triangular facet, natural neighbor, and inverse distance weighting interpolation.
///     </para>
///     <para>
///         Example usage:
///         <code>
/// // Create TIN and add vertices
/// var tin = new IncrementalTin();
/// tin.Add(vertices);
/// 
/// // Create interpolator
/// var interpolator = new TriangularFacetInterpolator(tin);
/// 
/// // Create rasterizer and generate 100x100 raster
/// var rasterizer = new TinRasterizer(tin, interpolator);
/// var result = rasterizer.CreateRaster(100, 100);
/// 
/// // Use the resulting grid data
/// double value = result.Data[50, 50];
/// </code>
///     </para>
/// </remarks>
public class TinRasterizer
{
    private readonly bool _constrainedRegionsOnly;

    private readonly InterpolationType _interpolationType;

    private readonly InterpolatorOptions? _interpolatorOptions;

    private readonly IIncrementalTin _tin;

    private readonly IVertexValuator? _valuator;

    /// <summary>
    ///     Creates a new instance of the TinRasterizer.
    /// </summary>
    /// <param name="tin">The TIN to rasterize.</param>
    /// <param name="interpolationType">The interpolation method to use.</param>
    /// <param name="constrainedRegionsOnly">If true, only points within constrained regions will be interpolated.</param>
    /// <param name="valuator">Optional valuator for interpreting vertex Z values.</param>
    public TinRasterizer(
        IIncrementalTin tin,
        InterpolationType interpolationType,
        bool constrainedRegionsOnly = false,
        IVertexValuator? valuator = null)
    {
        _tin = tin ?? throw new ArgumentNullException(nameof(tin));
        _interpolationType = interpolationType;
        _constrainedRegionsOnly = constrainedRegionsOnly;
        _valuator = valuator;
    }

    /// <summary>
    ///     Creates a new instance of the TinRasterizer with full interpolator options.
    /// </summary>
    /// <param name="tin">The TIN to rasterize.</param>
    /// <param name="interpolationType">The interpolation method to use.</param>
    /// <param name="options">Interpolator options including MaxInterpolationDistance.</param>
    /// <param name="valuator">Optional valuator for interpreting vertex Z values.</param>
    public TinRasterizer(
        IIncrementalTin tin,
        InterpolationType interpolationType,
        InterpolatorOptions options,
        IVertexValuator? valuator = null)
    {
        _tin = tin ?? throw new ArgumentNullException(nameof(tin));
        _interpolationType = interpolationType;
        _interpolatorOptions = options ?? throw new ArgumentNullException(nameof(options));
        _constrainedRegionsOnly = options.ConstrainedRegionsOnly;
        _valuator = valuator;
    }

    /// <summary>
    ///     Creates a raster grid covering the specified bounds at the given resolution,
    ///     maintaining the aspect ratio of the bounds.
    /// </summary>
    /// <param name="width">The requested width of the raster in pixels. If greater than 0, height parameter is ignored.</param>
    /// <param name="height">The requested height of the raster in pixels. Only used if width is less than or equal to 0.</param>
    /// <param name="bounds">The bounds of the area to rasterize. If null, the TIN's bounds will be used.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A RasterResult containing the interpolated values and associated metadata.</returns>
    /// <exception cref="ArgumentException">Thrown if both width and height are not positive.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the TIN is not bootstrapped or has no bounds.</exception>
    public RasterResult CreateRaster(
        int width,
        int height,
        (double Left, double Top, double Width, double Height)? bounds = null,
        CancellationToken cancellationToken = default)
    {
        if (width <= 0 && height <= 0)
            throw new ArgumentException("At least one of width or height must be positive");

        if (!_tin.IsBootstrapped()) throw new InvalidOperationException("TIN is not bootstrapped");

        // Use TIN bounds if not specified
        var areaBounds = bounds ?? _tin.GetBounds() ?? throw new InvalidOperationException("TIN has no bounds");

        // Calculate dimensions while preserving aspect ratio
        int calculatedWidth, calculatedHeight;
        var aspectRatio = areaBounds.Width / areaBounds.Height;

        if (width > 0)
        {
            calculatedWidth = width;
            calculatedHeight = (int)Math.Round(width / aspectRatio);
        }
        else
        {
            calculatedHeight = height;
            calculatedWidth = (int)Math.Round(height * aspectRatio);
        }

        // Ensure minimum size
        calculatedWidth = Math.Max(calculatedWidth, 2);
        calculatedHeight = Math.Max(calculatedHeight, 2);

        // Create result array
        var result = new double[calculatedWidth, calculatedHeight];

        // Calculate cell size
        var cellWidth = areaBounds.Width / calculatedWidth;
        var cellHeight = areaBounds.Height / calculatedHeight;

        // If constrained regions only, prepare for constraint checking
        var hasConstrainedRegions = false;
        if (_constrainedRegionsOnly)
            hasConstrainedRegions = _tin.GetConstraints().Any((IConstraint c) => c.DefinesConstrainedRegion());

        // Determine thread count (one per processor core)
        var processorCount = Environment.ProcessorCount;

        // Use ThreadLocal for interpolator instances
        var threadLocalInterpolator = new ThreadLocal<IInterpolatorOverTin>(() =>
            _interpolatorOptions != null
                ? InterpolatorFactory.Create(_tin, _interpolationType, _interpolatorOptions)
                : InterpolatorFactory.Create(_tin, _interpolationType, _constrainedRegionsOnly));

        // Thread-local no-data counters
        var threadNoDataCounts = new int[processorCount];

        // Calculate rows per thread
        var rowsPerThread = calculatedHeight / processorCount;

        // Process the grid in parallel, with each thread handling contiguous rows
        Parallel.For(
            0,
            processorCount,
            (int threadIndex) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    var startRow = threadIndex * rowsPerThread;
                    var endRow = threadIndex == processorCount - 1 ? calculatedHeight : startRow + rowsPerThread;

                    var interpolator = threadLocalInterpolator.Value!;
                    var noDataCount = 0;

                    for (var y = startRow; y < endRow; y++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        for (var x = 0; x < calculatedWidth; x++)
                        {
                            var worldX = areaBounds.Left + (x + 0.5) * cellWidth;
                            var worldY = areaBounds.Top + (y + 0.5) * cellHeight;

                            var value = interpolator.Interpolate(worldX, worldY, _valuator);
                            result[x, y] = value;

                            if (double.IsNaN(value)) noDataCount++;
                        }
                    }

                    threadNoDataCounts[threadIndex] = noDataCount;
                });

        // Sum up the no-data counts from all threads
        var totalNoDataCount = 0;
        for (var i = 0; i < processorCount; i++) totalNoDataCount += threadNoDataCounts[i];

        // If cancelled, throw cancellation exception
        cancellationToken.ThrowIfCancellationRequested();

        return new RasterResult(
            result,
            areaBounds,
            calculatedWidth,
            calculatedHeight,
            cellWidth,
            cellHeight,
            totalNoDataCount);
    }

    /// <summary>
    ///     Creates a raster grid with a specified data type for memory-efficient storage.
    /// </summary>
    /// <param name="width">The requested width of the raster in pixels.</param>
    /// <param name="height">The requested height of the raster in pixels.</param>
    /// <param name="dataType">The data type for raster storage (Float64, Float32, or Int16Scaled).</param>
    /// <param name="bounds">The bounds of the area to rasterize. If null, the TIN's bounds will be used.</param>
    /// <param name="int16Scale">Scale factor for Int16Scaled data type. Ignored for other types.</param>
    /// <param name="int16Offset">Offset for Int16Scaled data type. Ignored for other types.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A RasterResult containing the interpolated values and associated metadata.</returns>
    /// <remarks>
    ///     <para>
    ///         For large rasters, using Float32 or Int16Scaled can significantly reduce memory usage:
    ///         - Float64: 8 bytes per cell (12.8 GB for 1.6B cells)
    ///         - Float32: 4 bytes per cell (6.4 GB for 1.6B cells)
    ///         - Int16Scaled: 2 bytes per cell (3.2 GB for 1.6B cells)
    ///     </para>
    ///     <para>
    ///         For Int16Scaled, values are stored as: storedValue = (actualValue - offset) / scale.
    ///         For bathymetric data with depths -500m to +100m, use scale=0.01 for 1cm precision.
    ///     </para>
    /// </remarks>
    public RasterResult CreateRaster(
        int width,
        int height,
        RasterDataType dataType,
        (double Left, double Top, double Width, double Height)? bounds = null,
        double int16Scale = 0.01,
        double int16Offset = 0.0,
        CancellationToken cancellationToken = default)
    {
        if (width <= 0 && height <= 0)
            throw new ArgumentException("At least one of width or height must be positive");

        if (!_tin.IsBootstrapped()) throw new InvalidOperationException("TIN is not bootstrapped");

        // Use TIN bounds if not specified
        var areaBounds = bounds ?? _tin.GetBounds() ?? throw new InvalidOperationException("TIN has no bounds");

        // Calculate dimensions while preserving aspect ratio
        int calculatedWidth, calculatedHeight;
        var aspectRatio = areaBounds.Width / areaBounds.Height;

        if (width > 0)
        {
            calculatedWidth = width;
            calculatedHeight = (int)Math.Round(width / aspectRatio);
        }
        else
        {
            calculatedHeight = height;
            calculatedWidth = (int)Math.Round(height * aspectRatio);
        }

        // Ensure minimum size
        calculatedWidth = Math.Max(calculatedWidth, 2);
        calculatedHeight = Math.Max(calculatedHeight, 2);

        // Create appropriate raster data storage
        IRasterData rasterData = dataType switch
        {
            RasterDataType.Float64 => new Float64RasterData(calculatedWidth, calculatedHeight),
            RasterDataType.Float32 => new Float32RasterData(calculatedWidth, calculatedHeight),
            RasterDataType.Int16Scaled => new Int16ScaledRasterData(calculatedWidth, calculatedHeight, int16Scale, int16Offset),
            _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "Unknown raster data type")
        };

        // Calculate cell size
        var cellWidth = areaBounds.Width / calculatedWidth;
        var cellHeight = areaBounds.Height / calculatedHeight;

        // If constrained regions only, prepare for constraint checking
        var hasConstrainedRegions = false;
        if (_constrainedRegionsOnly)
            hasConstrainedRegions = _tin.GetConstraints().Any((IConstraint c) => c.DefinesConstrainedRegion());

        // Determine thread count (one per processor core)
        var processorCount = Environment.ProcessorCount;

        // Use ThreadLocal for interpolator instances
        var threadLocalInterpolator = new ThreadLocal<IInterpolatorOverTin>(() =>
            _interpolatorOptions != null
                ? InterpolatorFactory.Create(_tin, _interpolationType, _interpolatorOptions)
                : InterpolatorFactory.Create(_tin, _interpolationType, _constrainedRegionsOnly));

        // Thread-local no-data counters
        var threadNoDataCounts = new int[processorCount];

        // Calculate rows per thread
        var rowsPerThread = calculatedHeight / processorCount;

        // Process the grid in parallel, with each thread handling contiguous rows
        Parallel.For(
            0,
            processorCount,
            (int threadIndex) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    var startRow = threadIndex * rowsPerThread;
                    var endRow = threadIndex == processorCount - 1 ? calculatedHeight : startRow + rowsPerThread;

                    var interpolator = threadLocalInterpolator.Value!;
                    var noDataCount = 0;

                    for (var y = startRow; y < endRow; y++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        for (var x = 0; x < calculatedWidth; x++)
                        {
                            var worldX = areaBounds.Left + (x + 0.5) * cellWidth;
                            var worldY = areaBounds.Top + (y + 0.5) * cellHeight;

                            var value = interpolator.Interpolate(worldX, worldY, _valuator);
                            rasterData.SetValue(x, y, value);

                            if (double.IsNaN(value)) noDataCount++;
                        }
                    }

                    threadNoDataCounts[threadIndex] = noDataCount;
                });

        // Sum up the no-data counts from all threads
        var totalNoDataCount = 0;
        for (var i = 0; i < processorCount; i++) totalNoDataCount += threadNoDataCounts[i];

        // If cancelled, throw cancellation exception
        cancellationToken.ThrowIfCancellationRequested();

        return new RasterResult(
            rasterData,
            areaBounds,
            cellWidth,
            cellHeight,
            totalNoDataCount);
    }

    /// <summary>
    ///     Creates a raster with automatically calculated dimensions based on the TIN's bounds
    ///     and the specified cell size.
    /// </summary>
    /// <param name="cellSize">The size of each cell in world units.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A RasterResult containing the interpolated values and associated metadata.</returns>
    /// <exception cref="ArgumentException">Thrown if cellSize is not positive.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the TIN has no bounds.</exception>
    public RasterResult CreateRaster(double cellSize, CancellationToken cancellationToken = default)
    {
        if (cellSize <= 0) throw new ArgumentException("Cell size must be positive", nameof(cellSize));

        var bounds = _tin.GetBounds() ?? throw new InvalidOperationException("TIN has no bounds");

        // Calculate width and height based on cell size
        var width = (int)Math.Ceiling(bounds.Width / cellSize);
        var height = (int)Math.Ceiling(bounds.Height / cellSize);

        // Adjust bounds to match the calculated width and height
        var adjustedBounds = (bounds.Left, bounds.Top, width * cellSize, height * cellSize);

        return CreateRaster(width, height, adjustedBounds, cancellationToken);
    }

    /// <summary>
    ///     Creates a raster with automatically calculated dimensions based on the TIN's bounds
    ///     and a cell size derived from the specified target number of cells.
    /// </summary>
    /// <param name="targetCellCount">Approximate number of cells in the resulting raster.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A RasterResult containing the interpolated values and associated metadata.</returns>
    /// <exception cref="ArgumentException">Thrown if targetCellCount is not positive.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the TIN has no bounds.</exception>
    public RasterResult CreateRaster(int targetCellCount, CancellationToken cancellationToken = default)
    {
        if (targetCellCount <= 0)
            throw new ArgumentException("Target cell count must be positive", nameof(targetCellCount));

        var bounds = _tin.GetBounds() ?? throw new InvalidOperationException("TIN has no bounds");

        // Calculate appropriate cell size based on target cell count
        var aspect = bounds.Width / bounds.Height;
        var height = (int)Math.Sqrt(targetCellCount / aspect);
        var width = (int)(height * aspect);

        // Ensure minimum size
        width = Math.Max(width, 2);
        height = Math.Max(height, 2);

        return CreateRaster(width, height, bounds, cancellationToken);
    }

    /// <summary>
    ///     Creates a raster based on a specific height dimension, automatically calculating
    ///     the width to maintain the proper aspect ratio of the bounds.
    /// </summary>
    /// <param name="height">The height of the raster in pixels.</param>
    /// <param name="bounds">The bounds of the area to rasterize. If null, the TIN's bounds will be used.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A RasterResult containing the interpolated values and associated metadata.</returns>
    public RasterResult CreateRasterWithHeight(
        int height,
        (double Left, double Top, double Width, double Height)? bounds = null,
        CancellationToken cancellationToken = default)
    {
        if (height <= 0) throw new ArgumentException("Height must be positive", nameof(height));
        return CreateRaster(0, height, bounds, cancellationToken);
    }

    /// <summary>
    ///     Creates a raster with a specified scale factor (cells per world unit).
    /// </summary>
    /// <param name="scale">The scale factor in cells per world unit. Higher values create higher-resolution rasters.</param>
    /// <param name="bounds">The bounds of the area to rasterize. If null, the TIN's bounds will be used.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A RasterResult containing the interpolated values and associated metadata.</returns>
    /// <exception cref="ArgumentException">Thrown if scale is not positive.</exception>
    public RasterResult CreateRasterWithScale(
        double scale,
        (double Left, double Top, double Width, double Height)? bounds = null,
        CancellationToken cancellationToken = default)
    {
        if (scale <= 0) throw new ArgumentException("Scale must be positive", nameof(scale));

        var areaBounds = bounds ?? _tin.GetBounds() ?? throw new InvalidOperationException("TIN has no bounds");

        // Calculate width and height based on scale
        var width = (int)Math.Ceiling(areaBounds.Width * scale);
        var height = (int)Math.Ceiling(areaBounds.Height * scale);

        return CreateRaster(width, height, areaBounds, cancellationToken);
    }

    /// <summary>
    ///     Creates a raster based on a specific width dimension, automatically calculating
    ///     the height to maintain the proper aspect ratio of the bounds.
    /// </summary>
    /// <param name="width">The width of the raster in pixels.</param>
    /// <param name="bounds">The bounds of the area to rasterize. If null, the TIN's bounds will be used.</param>
    /// <param name="cancellationToken">Optional token to cancel the operation.</param>
    /// <returns>A RasterResult containing the interpolated values and associated metadata.</returns>
    public RasterResult CreateRasterWithWidth(
        int width,
        (double Left, double Top, double Width, double Height)? bounds = null,
        CancellationToken cancellationToken = default)
    {
        if (width <= 0) throw new ArgumentException("Width must be positive", nameof(width));
        return CreateRaster(width, 0, bounds, cancellationToken);
    }
}