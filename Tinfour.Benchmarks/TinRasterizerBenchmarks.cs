/*
 * Copyright 2025 G.W.Lucas
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

namespace Tinfour.Benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Tinfour.Core.Interpolation;
using Tinfour.Core.Standard;

/// <summary>
///     Benchmarks for TinRasterizer performance with StochasticLawsonsWalk optimizations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, 1, 1, 3)]
public class TinRasterizerBenchmarks
{
    [Params(1000, 2500)]
    public int Bounds;

    [Params(
        InterpolationType.TriangularFacet,
        InterpolationType.NaturalNeighbor,
        InterpolationType.InverseDistanceWeighting)]
    public InterpolationType InterpolationType = InterpolationType.NaturalNeighbor;

    [Params(10_000, 100_000, 250_000)]
    public int NumVertices;

    private IInterpolatorOverTin _interpolator = null!;

    private TinRasterizer _rasterizer = null!;

    private IncrementalTin _tin = null!;

    [Benchmark]
    public RasterResult CreateRaster()
    {
        // always full bounds raster for benchmarking
        return this._rasterizer.CreateRaster(this.Bounds, this.Bounds);
    }

    [GlobalSetup]
    public void Setup()
    {
        // Create a test TIN with a reasonable number of points
        this._tin = InterpolationBenchmarks.GenerateTerrainTin(this.NumVertices, this.Bounds, this.Bounds);
        this._rasterizer = new TinRasterizer(this._tin, this.InterpolationType);
    }
}