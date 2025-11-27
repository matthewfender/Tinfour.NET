# Tinfour.Net Benchmarks

This project contains performance benchmarks for the Tinfour.Net triangulated irregular network (TIN) library, focusing on TIN construction and interpolation operations.

## Running Benchmarks

### Command Line Options

```bash
# Run interpolation benchmarks only (default)
dotnet run --configuration Release

# Run specific benchmark types
dotnet run --configuration Release tin           # TIN construction benchmarks
dotnet run --configuration Release interpolation # All interpolation methods
dotnet run --configuration Release utilities     # TIN data extraction benchmarks
dotnet run --configuration Release all           # All benchmarks
```

### Benchmark Categories

#### 1. TIN Construction Benchmarks (`IncrementalTinBenchmarks`)
- Tests incremental TIN construction performance
- Parameters: Vertex count (1K, 10K, 100K)
- Measures time and memory usage for triangulation

#### 2. Interpolation Benchmarks (`InterpolationBenchmarks`)
- Tests interpolation performance directly against the TIN interpolator
- **Parameters:**
  - **TIN Bounds**: 1000×1000, 2000×2000, 5000×5000 (10000×10000 commented out)
  - **Vertex Count**: 10K, 100K (1M commented out for faster testing)
  - **Interpolation Type**: `TriangularFacet`, `NaturalNeighbor`, `InverseDistanceWeighting`
- **Benchmarks:**
  - `InterpolateFullGrid`: Systematic grid sampling across entire TIN bounds
  - `InterpolateRandomPoints`: Random point sampling within TIN bounds

#### 3. Inverse Distance Weighting Benchmarks (`InverseDistanceWeightingBenchmarks`)
- Specifically focuses on IDW interpolation with various parameters
- Compares IDW against other interpolation methods
- Tests different IDW variants (standard, Gaussian kernel, custom power)

#### 4. TIN Utilities Benchmarks (`TinUtilitiesBenchmarks`)
- Tests performance of data extraction from pre-generated TINs
- **Parameters:**
  - **TIN Bounds**: Fixed 5000×5000 for all tests
  - **Vertex Count**: 1K, 10K, 100K, 1M
- **Benchmarks:**
  - `ExtractAllEdges`: Performance of extracting all edges from TIN
  - `ExtractAllTriangles`: Performance of extracting all triangles from TIN
  - `ExtractAllVertices`: Performance of extracting all vertices from TIN
  - `ExtractPerimeter`: Performance of extracting TIN perimeter
  - `CountTriangles`: Performance of triangle counting operations

## Interpolation Test Strategy

### Grid-Based Testing (`InterpolateFullGrid`)
- Creates a **uniform grid** across the TIN bounds with appropriate stride
- **Systematic coverage** ensures every region of the TIN is tested
- **Predictable sampling** for consistent performance measurement

### Random Point Testing (`InterpolateRandomPoints`)  
- Tests **10,000 random points** within the TIN bounds
- Points are strictly within bounds (0 to TinBounds)
- Tests interpolation with **realistic query patterns**
- Includes **spatial locality** effects from random access

### Terrain Generation
The benchmarks use **sinusoidal terrain data** with realistic elevation patterns:

```csharp
double z = 
    50 * Math.Sin(x * 0.01) * Math.Cos(y * 0.01) +     // Large hills
    20 * Math.Sin(x * 0.03) * Math.Sin(y * 0.03) +     // Medium features  
    5 * Math.Sin(x * 0.1) * Math.Cos(y * 0.1) +       // Small features
    random.NextDouble() * 2;                           // Noise
```

This creates realistic terrain with:
- **Large hills**: Low-frequency variations (0.01 cycles per unit)
- **Medium features**: Mid-frequency terrain details (0.03 cycles per unit)
- **Small features**: High-frequency surface texture (0.1 cycles per unit)
- **Random noise**: Adds realistic irregularity

## Core Interpolation Testing

### What We're Measuring
- **Direct interpolator calls**: `interpolator.Interpolate(x, y, null)` for each point
- **No raster generation**: Pure interpolation performance without bitmap operations
- **No visualiser dependencies**: Benchmarks are completely separate from UI code
- **Systematic coverage**: Every part of the TIN bounds gets tested

### Performance Characteristics

The benchmarks measure:
- **Interpolation rate**: Points interpolated per second
- **Memory efficiency**: Allocations during interpolation
- **Scaling behavior**: Performance vs TIN size and vertex count
- **Point location cost**: Time to find containing triangle

### Optimization Features Used
- **Hilbert sorting** for better spatial locality during TIN construction
- **Pre-allocation** using `PreAllocateForVertices()` to reduce memory fragmentation  
- **Optimal nominal spacing** calculation for edge pool sizing

## Interpolation Methods Compared

### Triangular Facet Interpolation
- **Algorithm**: Linear interpolation within triangles using barycentric coordinates
- **Characteristics**: Fastest method, lowest memory usage, creates faceted surfaces
- **Best for**: Real-time applications, large datasets, performance-critical scenarios

### Natural Neighbor Interpolation
- **Algorithm**: Sibson's method using Voronoi diagrams and area-weighted interpolation
- **Characteristics**: Highest quality, most computationally intensive, smooth results
- **Best for**: High-quality visualizations, irregular data, final products

### Inverse Distance Weighting (IDW) Interpolation
- **Algorithm**: Classic distance-weighted interpolation with configurable parameters
- **Characteristics**: Balance of speed and quality, moderate memory usage, smooth results
- **Best for**: General-purpose use, moderate quality requirements, flexible configuration
- **Variants tested**:
  - Standard (Shepard's method with power=2)
  - Gaussian kernel
  - Custom power parameter

## Performance Expectations

### Typical Results
- **Triangular Facet**:
  - Small TINs (10K vertices): ~150K-165K interpolations/second
  - Large TINs (100K vertices): ~25K-27K interpolations/second
  - Minimal memory allocations

- **Natural Neighbor**:
  - Small TINs (10K vertices): ~20K-25K interpolations/second
  - Large TINs (100K vertices): ~5K-7K interpolations/second
  - Moderate memory allocations

- **IDW**:
  - Small TINs (10K vertices): ~50K-60K interpolations/second
  - Large TINs (100K vertices): ~10K-15K interpolations/second
  - Low-moderate memory allocations

### Factors Affecting Performance
1. **TIN vertex density**: More vertices = slower point location
2. **Spatial locality**: Grid sampling may be faster than random due to cache effects
3. **Triangle complexity**: Elongated triangles may affect performance
4. **Query pattern**: Sequential vs random access patterns
5. **Interpolation algorithm**: Different computational requirements per method

## Output Interpretation

### Benchmark Results
- **Mean**: Average execution time for all interpolations
- **Error**: Standard error of measurements  
- **StdDev**: Standard deviation across iterations
- **Allocated**: Memory allocated during benchmark

### Interpolation Results
Results include validation metrics:
- **ValidCount**: Successful interpolations (should be ~100% for interior points)
- **NanCount**: Points that returned NaN (typically none for interior points)
- **AverageValue**: Mean interpolated Z value from terrain function
- **Range**: [MinValue, MaxValue] of interpolated results

## Notes

- **Run in Release mode** for accurate performance measurements
- **Pure interpolation testing**: No visualization or raster generation overhead
- **Systematic coverage**: Grid ensures every TIN region is tested
- **Reproducible**: Fixed random seeds for consistent results across runs- **Reproducible**: Fixed random seeds for consistent results across runs