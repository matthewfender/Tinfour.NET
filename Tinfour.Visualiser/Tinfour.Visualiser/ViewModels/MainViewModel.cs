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

using static Tinfour.Visualiser.Services.CoordinateConverter;
using static Tinfour.Visualiser.Services.VoronoiRenderingService;

namespace Tinfour.Visualiser.ViewModels;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Tinfour.Core.Common;
using Tinfour.Core.Refinement;
using Tinfour.Core.Standard;
using Tinfour.Visualiser.Services;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _canGenerateContours;

    [ObservableProperty]
    private bool _canGenerateInterpolation;

    [ObservableProperty]
    private bool _canGenerateVoronoi;

    [ObservableProperty]
    private bool _constrainedInterpolationOnly;

    [ObservableProperty]
    private int _contourLevels = 10;

    [ObservableProperty]
    private double _contourOpacity = 1.0;

    [ObservableProperty]
    private double _contourRegionOpacity = 0.3;

    [ObservableProperty]
    private ContourRenderingService.ContourResult? _contourResult;

    [ObservableProperty]
    private TransformationType _coordinateSystem = TransformationType.None;

    [ObservableProperty]
    private float _edgeWidth = 0.5f;

    [ObservableProperty]
    private double _interpolationOpacity = 0.7;

    [ObservableProperty]
    private InterpolationRasterService.RasterResult? _interpolationResult;

    [ObservableProperty]
    private bool _isGenerating;

    private int _pointCount = 5;

    private TransformationType _selectedTransformation = TransformationType.WebMercator;

    [ObservableProperty]
    private ComboBoxItem _selectedTransformationItem;

    [ObservableProperty]
    private bool _showConstraints = true;

    [ObservableProperty]
    private bool _showContourRegions;

    [ObservableProperty]
    private bool _showContours;

    [ObservableProperty]
    private bool _showEdges = true;

    [ObservableProperty]
    private bool _showTriangles = true;

    [ObservableProperty]
    private bool _showVertices = true;

    [ObservableProperty]
    private bool _showVoronoi;

    partial void OnShowVoronoiChanged(bool value)
    {
        if (value && this._voronoiResult == null)
        {
            // Auto-generate Voronoi when toggled on and not already generated
            _ = this.GenerateVoronoi();
        }
    }

    [ObservableProperty]
    private bool _showVoronoiPolygons;

    [ObservableProperty]
    private string _statistics = string.Empty;

    [ObservableProperty]
    private string _statusText = "Click 'Generate Terrain' to create a triangulation";

    [ObservableProperty]
    private double _terrainHeight = 800;

    [ObservableProperty]
    private double _terrainWidth = 1200;

    [ObservableProperty]
    private string _title = "Tinfour .NET Visualiser";

    [ObservableProperty]
    private IncrementalTin? _triangulation;

    [ObservableProperty]
    private TriangulationResult? _triangulationResult;

    [ObservableProperty]
    private bool _useTriangleCollector;

    [ObservableProperty]
    private float _vertexSize = 1.5f;

    [ObservableProperty]
    private VoronoiResult? _voronoiResult;

    [ObservableProperty]
    private double _ruppertMinAngle = 20.0;

    [ObservableProperty]
    private bool _canApplyRuppert;

    public MainViewModel()
    {
        // Initialize ComboBox item for transformation selection
        this._selectedTransformationItem = new ComboBoxItem { Content = "Web Mercator", Tag = "WebMercator" };
    }

    partial void OnSelectedTransformationItemChanged(ComboBoxItem value)
    {
        if (value?.Tag is not null)
        {
            var tag = value.Tag.ToString();
            if (Enum.TryParse<TransformationType>(tag, out var transformationType))
                this._selectedTransformation = transformationType;
        }
    }

    public int PointCount
    {
        get => this._pointCount;
        set =>

            // Just update the value, don't automatically regenerate
            this.SetProperty(ref this._pointCount, value);
    }

    [RelayCommand]
    private void ClearContours()
    {
        this.ContourResult = null;
        this.ShowContours = false;
        this.ShowContourRegions = false;
        this.StatusText = "Contours cleared.";
    }

    [RelayCommand]
    private void ClearInterpolation()
    {
        this.InterpolationResult = null;
        this.StatusText = "Interpolation raster cleared.";
    }

    [RelayCommand]
    private void ClearTriangulation()
    {
        this.Triangulation = null;
        this.StatusText = "Triangulation cleared. Click a generate button to create a new triangulation.";
    }

    [RelayCommand]
    private async Task GenerateConstrainedTestAsync()
    {
        if (this.IsGenerating) return;

        try
        {
            this.IsGenerating = true;
            this.StatusText = "Adding concentric circle constraints...";

            if (this.Triangulation == null || !this.Triangulation.IsBootstrapped())
            {
                // No existing triangulation, create a simple one first
                this.StatusText = "Creating base triangulation with concentric constraints...";
                var result = await Task.Run(() => TriangulationGenerator.GenerateConstrainedTest(
                                 Math.Min(this.PointCount, 1000),
                                 this.TerrainWidth,
                                 this.TerrainHeight));

                this.Triangulation = result.Tin;
                this.TriangulationResult = result;

                // Use a coordinate system suitable for synthetic data
                this.CoordinateSystem = TransformationType.WebMercator;

                // Request reset view to ensure proper display
                MessageBus.RequestResetView();

                this.StatusText = $"✅ Constrained Test Generated\n{result}";
            }
            else
            {
                // Add concentric circle constraints to existing triangulation
                var result = await Task.Run(() => TriangulationGenerator.AddConcentricCircleConstraints(
                                 this.Triangulation,
                                 this.TerrainWidth,
                                 this.TerrainHeight));

                // Update the triangulation result
                this.TriangulationResult = result;

                // Force UI update by setting to null then back to the TIN
                var tin = this.Triangulation;
                this.Triangulation = null;
                this.Triangulation = tin;

                // Request reset view to ensure proper display
                MessageBus.RequestResetView();

                this.StatusText = $"✅ Concentric Circle Constraints Added\n{result}";
            }
        }
        catch (Exception ex)
        {
            this.StatusText = $"❌ Error: {ex.Message}";
        }
        finally
        {
            this.IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task GenerateContours()
    {
        if (this.IsGenerating) return;

        if (this.Triangulation == null || !this.Triangulation.IsBootstrapped())
        {
            this.StatusText = "❌ Error: You must first create or load a triangulation before generating contours.";
            return;
        }

        try
        {
            this.IsGenerating = true;
            this.StatusText = "Generating contours...";

            var result = await Task.Run(() => ContourRenderingService.GenerateContours(
                             this.Triangulation,
                             this.ContourLevels,
                             this.ShowContourRegions));

            this.ContourResult = result;

            //// Update contour display via MessageBus 
            // MessageBus.RequestContourUpdate(
            // result.Contours,
            // result.Regions,
            // true, // Show contours
            // ShowContourRegions,
            // ContourOpacity,
            // ContourRegionOpacity);
            this.ShowContours = true;
            this.UpdateStatistics();
            this.StatusText = $"✅ Contours Generated\n{result}";
        }
        catch (Exception ex)
        {
            this.StatusText = $"❌ Error generating contours: {ex.Message}";
        }
        finally
        {
            this.IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task GenerateSimpleTestAsync()
    {
        try
        {
            this.IsGenerating = true;
            this.StatusText = "Generating simple test triangulation...";

            var result = await Task.Run(() => TriangulationGenerator.GenerateSimpleTest(
                             Math.Min(this.PointCount, 1000),
                             this.TerrainWidth,
                             this.TerrainHeight));

            this.Triangulation = result.Tin;
            this.TriangulationResult = result;

            // Use a coordinate system suitable for synthetic data
            this.CoordinateSystem = TransformationType.WebMercator;

            // Request reset view to ensure proper display
            MessageBus.RequestResetView();

            this.StatusText = $"✅ Simple Test Generated\n{result}";
        }
        catch (Exception ex)
        {
            this.StatusText = $"❌ Error: {ex.Message}";
        }
        finally
        {
            this.IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task GenerateTerrainAsync()
    {
        if (this.IsGenerating) return;

        try
        {
            this.IsGenerating = true;
            this.StatusText = "Generating terrain triangulation...";

            // Run generation on background thread
            var result = await Task.Run(() =>
                             TriangulationGenerator.GenerateTerrainData(
                                 this.PointCount,
                                 this.TerrainWidth,
                                 this.TerrainHeight));

            this.Triangulation = result.Tin;
            this.TriangulationResult = result;

            // Use a coordinate system suitable for synthetic data
            this.CoordinateSystem = TransformationType.WebMercator;

            // Request reset view to ensure proper display
            MessageBus.RequestResetView();

            this.StatusText = $"✅ Terrain Generated\n{result}";
        }
        catch (Exception ex)
        {
            this.StatusText = $"❌ Error: {ex.Message}";
        }
        finally
        {
            this.IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task GenerateVoronoi()
    {
        if (this.Triangulation == null || !this.Triangulation.IsBootstrapped())
            return;

        try
        {
            this.IsGenerating = true;
            this.StatusText = "Generating Voronoi diagram...";

            VoronoiResult? result = null;
            await Task.Run(() =>
                {
                    result = VoronoiRenderingService.GenerateVoronoi(this.Triangulation);
                });

            this.VoronoiResult = result;
            this.UpdateStatistics();

            this.StatusText = $"✅ Voronoi diagram generated with {this.VoronoiResult?.PolygonCount} polygons";
        }
        catch (Exception ex)
        {
            this.StatusText = $"❌ Voronoi generation failed: {ex.Message}";
        }
        finally
        {
            this.IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task ApplyRuppertRefinement()
    {
        if (this.Triangulation == null || !this.Triangulation.IsBootstrapped())
        {
            this.StatusText = "❌ Error: You must first create or load a triangulation before applying Ruppert refinement.";
            return;
        }

        if (this.Triangulation.GetConstraints().Count == 0)
        {
            this.StatusText = "❌ Error: Ruppert refinement requires constraints. Add constraints first.";
            return;
        }

        try
        {
            this.IsGenerating = true;
            var initialVertexCount = this.Triangulation.GetVertices().Count;
            var initialTriangleCount = this.Triangulation.CountTriangles().ValidTriangles;
            this.StatusText = $"Applying Ruppert refinement (min angle: {this.RuppertMinAngle}°)...";

            var sw = Stopwatch.StartNew();
            var verticesAdded = 0;
            var refinementComplete = false;

            await Task.Run(() =>
            {
                var options = new RuppertOptions
                {
                    MinimumAngleDegrees = this.RuppertMinAngle,
                    MaxIterations = 100_000,
                    InterpolateZ = true,  // Required for rasterization - interpolates Z values for new vertices
                    InterpolationType = this.SelectedInterpolationType
                };

                var refiner = new RuppertRefiner(this.Triangulation, options);
                refinementComplete = refiner.Refine();
                verticesAdded = this.Triangulation.GetVertices().Count - initialVertexCount;
            });

            sw.Stop();

            var finalTriangleCount = this.Triangulation.CountTriangles().ValidTriangles;

            // Force UI update by setting to null then back to the TIN
            var tin = this.Triangulation;
            this.Triangulation = null;
            this.Triangulation = tin;

            // Update triangulation result
            this.TriangulationResult = new TriangulationResult
            {
                Tin = tin,
                VertexCount = tin.GetVertices().Count,
                EdgeCount = tin.GetEdges().Count,
                TriangleCount = tin.CountTriangles(),
                GenerationTime = sw.Elapsed,
                Bounds = tin.GetBounds()
            };

            // Request reset view to ensure proper display
            MessageBus.RequestResetView();

            var statusMessage = refinementComplete ? "✅ Ruppert Refinement Complete" : "⚠️ Ruppert Refinement Hit Iteration Limit";
            this.StatusText = $"{statusMessage}\n" +
                             $"Vertices added: {verticesAdded:N0}\n" +
                             $"Triangles: {initialTriangleCount:N0} → {finalTriangleCount:N0}\n" +
                             $"Time: {sw.ElapsedMilliseconds:N0}ms";
        }
        catch (Exception ex)
        {
            this.StatusText = $"❌ Ruppert refinement failed: {ex.Message}";
        }
        finally
        {
            this.IsGenerating = false;
        }
    }

    // Helper method to get the top-level window
    private WindowBase? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;

        if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            return singleViewPlatform.MainView as WindowBase;
        return null;
    }

    [RelayCommand]
    private async Task LoadConstraintsAsync()
    {
        if (this.IsGenerating) return;

        if (this.Triangulation == null || !this.Triangulation.IsBootstrapped())
        {
            this.StatusText = "❌ Error: You must first create or load a triangulation before adding constraints.";
            return;
        }

        try
        {
            this.IsGenerating = true;
            this.StatusText = "Loading constraints from file...";

            // Get top-level window for file dialog
            var topLevel = this.GetTopLevel();
            if (topLevel == null)
            {
                this.StatusText = "❌ Error: Could not get application window for file dialog.";
                return;
            }

            // Open file picker
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(
                            new FilePickerOpenOptions
                                {
                                    Title = "Select Constraints File",
                                    AllowMultiple = false,
                                    FileTypeFilter = new List<FilePickerFileType>
                                                         {
                                                             new("CSV Files (*.csv)") { Patterns = new[] { "*.csv" } },
                                                             new("Text Files (*.txt)") { Patterns = new[] { "*.txt" } },
                                                             new("All Files (*.*)") { Patterns = new[] { "*.*" } }
                                                         }
                                });

            if (files == null || !files.Any())
            {
                this.StatusText = "Constraints file loading cancelled.";
                return;
            }

            var file = files.First();
            this.StatusText = $"Loading constraints from {file.Name}...";

            ConstraintFileLoader.LoadResult result;

            // Always use stream-based loading which works in both desktop and browser
            try
            {
                await using var stream = await file.OpenReadAsync();

                // Add debug output for browser diagnosis
                Debug.WriteLine(
                    $"Constraint file stream opened: CanRead={stream.CanRead}, Length={stream.Length}, Position={stream.Position}");

                // First check if stream is valid and has content
                if (!stream.CanRead || stream.Length == 0)
                {
                    this.StatusText = "❌ Error: Could not read constraint file or file is empty.";
                    return;
                }

                result = await StreamBasedLoader.LoadConstraintsFromStreamAsync(stream, this._selectedTransformation);

                // Debug information after loading
                Debug.WriteLine(
                    $"Constraint loading result: Count={result.ConstraintCount}, Points={result.TotalPointCount}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception loading constraints: {ex.GetType().Name}: {ex.Message}");
                this.StatusText = $"❌ Error loading file: {ex.Message}";
                return;
            }

            if (result.ConstraintCount == 0)
            {
                this.StatusText =
                    "❌ Error: No valid constraints found in the file. Ensure each constraint has at least 3 points.";
                return;
            }

            // Debug information before adding constraints
            Debug.WriteLine($"Adding {result.Constraints.Count} constraints to TIN");

            try
            {
                // Filter out constraints with empty vertex lists
                var validConstraints = result.Constraints.Where((IConstraint c) => c.GetVertices().Any()).ToList();

                // Add the constraints to the existing TIN
                var success = await Task.Run(() =>
                                  ConstraintFileLoader.AddConstraintsToTin(this.Triangulation, validConstraints));

                if (success)
                {
                    Debug.WriteLine("Successfully added constraints to TIN");

                    // Force UI update by setting to null then back to the TIN
                    var tin = this.Triangulation;
                    this.Triangulation = null;
                    this.Triangulation = tin;

                    // Request reset view to ensure proper display
                    MessageBus.RequestResetView();

                    this.StatusText =
                        $"✅ {result.ConstraintCount} constraint{(result.ConstraintCount > 1 ? "s" : string.Empty)} added from {file.Name}\n{result}";
                }
                else
                {
                    Debug.WriteLine("Failed to add constraints to TIN");
                    this.StatusText = "❌ Error: Failed to add constraints to the triangulation.";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception adding constraints to TIN: {ex.GetType().Name}: {ex.Message}");
                this.StatusText = $"❌ Error adding constraints: {ex.Message}";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Top-level exception in LoadConstraintsAsync: {ex.GetType().Name}: {ex.Message}");
            this.StatusText = $"❌ Error loading constraints: {ex.Message}";
        }
        finally
        {
            this.IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task LoadVerticesAsync()
    {
        if (this.IsGenerating) return;

        try
        {
            this.IsGenerating = true;
            this.StatusText = "Loading vertices from file...";

            // Get top-level window for file dialog
            var topLevel = this.GetTopLevel();
            if (topLevel == null)
            {
                this.StatusText = "❌ Error: Could not get application window for file dialog.";
                return;
            }

            // Open file picker
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(
                            new FilePickerOpenOptions
                                {
                                    Title = "Select Vertices File",
                                    AllowMultiple = false,
                                    FileTypeFilter = new List<FilePickerFileType>
                                                         {
                                                             new("CSV Files (*.csv)") { Patterns = new[] { "*.csv" } },
                                                             new("Text Files (*.txt)") { Patterns = new[] { "*.txt" } },
                                                             new("All Files (*.*)") { Patterns = new[] { "*.*" } }
                                                         }
                                });

            if (files == null || !files.Any())
            {
                this.StatusText = "Vertex file loading cancelled.";
                return;
            }

            var file = files.First();
            this.StatusText = $"Loading vertices from {file.Name}...";

            VertexFileLoader.LoadResult result;

            // Always use stream-based loading which works in both desktop and browser
            try
            {
                await using var stream = await file.OpenReadAsync();
                result = await StreamBasedLoader.LoadVerticesFromStreamAsync(stream, this._selectedTransformation);
            }
            catch (Exception ex)
            {
                this.StatusText = $"❌ Error loading file: {ex.Message}";
                return;
            }

            if (result.VertexCount == 0)
            {
                this.StatusText = "❌ Error: No valid vertices found in the file.";
                return;
            }

            // Set the triangulation
            this.Triangulation = result.Tin;

            // Create triangulation result
            this.TriangulationResult = new TriangulationResult
                                           {
                                               Tin = result.Tin,
                                               VertexCount = result.VertexCount,
                                               EdgeCount = result.Tin.GetEdges().Count,
                                               TriangleCount = result.Tin.CountTriangles(),
                                               GenerationTime = TimeSpan.Zero,
                                               Bounds = result.Tin.GetBounds()
                                           };

            // Update the coordinate system property to match the loaded data
            this.CoordinateSystem = this._selectedTransformation;

            // Request a reset view to ensure the vertices are visible
            MessageBus.RequestResetView();

            this.StatusText = $"✅ Vertices loaded from {file.Name}\n{result}";
        }
        catch (Exception ex)
        {
            this.StatusText = $"❌ Error loading vertices: {ex.Message}";
        }
        finally
        {
            this.IsGenerating = false;
        }
    }

    partial void OnTriangulationChanged(IncrementalTin? value)
    {
        if (value != null)

            // Create a basic triangulation result when TIN is set directly
            this.TriangulationResult = new TriangulationResult
                                           {
                                               Tin = value,
                                               VertexCount = value.GetVertices().Count,
                                               EdgeCount = value.GetEdges().Count,
                                               TriangleCount = value.CountTriangles(),
                                               GenerationTime = TimeSpan.Zero,
                                               Bounds = value.GetBounds()
                                           };
        else this.TriangulationResult = null;
    }

    partial void OnTriangulationResultChanged(TriangulationResult? value)
    {
        this.CanGenerateContours = value?.Tin != null && value.Tin.IsBootstrapped();
        this.CanGenerateInterpolation = value?.Tin != null && value.Tin.IsBootstrapped();
        this.CanGenerateVoronoi = value?.Tin != null && CanGenerateVoronoi(value.Tin);
        this.CanApplyRuppert = value?.Tin != null && value.Tin.IsBootstrapped() && value.Tin.GetConstraints().Count > 0;

        // Clear dependent results when triangulation changes
        // IMPORTANT: Use property setters (not backing fields) to trigger PropertyChanged
        // so the UI updates and clears the old visuals
        this.ContourResult = null;
        this.InterpolationResult = null;
        this.VoronoiResult = null;

        this.UpdateStatistics();
    }

    [RelayCommand]
    private void ResetView()
    {
        Debug.WriteLine("ResetView command called");

        // Use MessageBus to request a reset view - works in both desktop and browser
        MessageBus.RequestResetView();
    }

    [RelayCommand]
    private void ResetViewSettings()
    {
        this.ShowVertices = true;
        this.ShowTriangles = true;
        this.ShowEdges = true;
        this.VertexSize = 1.5f;
        this.EdgeWidth = 0.5f;
    }

    private void UpdateStatistics()
    {
        var stats = new List<string>();

        if (this.TriangulationResult != null)
        {
            stats.Add("=== TRIANGULATION ===");
            stats.Add($"Vertices: {this.TriangulationResult.VertexCount:N0}");
            stats.Add($"Triangles: {this.TriangulationResult.TriangleCount.ValidTriangles:N0}");
            stats.Add($"Edges: {this.TriangulationResult.EdgeCount:N0}");
            stats.Add($"Generation Time: {this.TriangulationResult.GenerationTime.TotalMilliseconds:F1}ms");

            if (this.TriangulationResult.Bounds.HasValue)
            {
                var bounds = this.TriangulationResult.Bounds.Value;
                stats.Add(
                    $"Bounds: ({bounds.Left:F1}, {bounds.Top:F1}) to ({bounds.Left + bounds.Width:F1}, {bounds.Top + bounds.Height:F1})");
            }
        }

        if (this.ContourResult != null)
        {
            stats.Add(string.Empty);
            stats.Add("=== CONTOURS ===");
            stats.Add($"Line Contours: {this.ContourResult.LineContourCount:N0}");
            stats.Add($"Region Contours: {this.ContourResult.RegionContourCount:N0}");
            stats.Add($"Generation Time: {this.ContourResult.GenerationTime.TotalMilliseconds:F1}ms");
        }

        if (this.InterpolationResult != null)
        {
            stats.Add(string.Empty);
            stats.Add("=== INTERPOLATION ===");
            stats.Add(
                $"Raster Size: {this.InterpolationResult.Bitmap.PixelSize.Width} x {this.InterpolationResult.Bitmap.PixelSize.Height}");
            stats.Add(
                $"Valid Pixels: {this.InterpolationResult.ValidPixels:N0} / {this.InterpolationResult.TotalPixels:N0}");
            stats.Add($"Z Range: {this.InterpolationResult.MinZ:F2} to {this.InterpolationResult.MaxZ:F2}");
        }

        if (this.VoronoiResult != null)
        {
            stats.Add(string.Empty);
            stats.Add("=== VORONOI DIAGRAM ===");
            stats.Add(GetStatistics(this.VoronoiResult));
        }

        this.Statistics = string.Join(Environment.NewLine, stats);
    }
}