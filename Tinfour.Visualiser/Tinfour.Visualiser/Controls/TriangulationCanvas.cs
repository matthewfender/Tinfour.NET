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

namespace Tinfour.Visualiser.Controls;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Timers;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;

using SkiaSharp;

using Tinfour.Core.Common;
using Tinfour.Core.Contour;
using Tinfour.Core.Standard;
using Tinfour.Core.Utils;
using Tinfour.Visualiser.Services;

using static Tinfour.Visualiser.Services.VoronoiRenderingService;

/// <summary>
///     A SkiaSharp-based canvas for rendering Delaunay triangulations with contour support.
/// </summary>
public class TriangulationCanvas : Control
{
    private bool _isPanning;

    // Pan and zoom properties
    private Point _lastMousePosition;

    private SKPoint _panOffset = new(0, 0);

    private float _zoomFactor = 1.0f;

    static TriangulationCanvas()
    {
        // Invalidate visual on property changes
        AffectsRender<TriangulationCanvas>(
            TriangulationProperty,
            InterpolationRasterProperty,
            ContoursProperty,
            ContourRegionsProperty,
            VoronoiResultProperty,
            ShowVerticesProperty,
            ShowTrianglesProperty,
            ShowEdgesProperty,
            VertexSizeProperty,
            EdgeWidthProperty,
            InterpolationOpacityProperty,
            ShowContoursProperty,
            ShowContourRegionsProperty,
            ContourOpacityProperty,
            ContourRegionOpacityProperty,
            ShowVoronoiProperty,
            ShowVoronoiPolygonsProperty,
            UseTriangleCollectorProperty);
    }

    public TriangulationCanvas()
    {
        // Register mouse events for pan and zoom
        this.PointerPressed += this.OnPointerPressed;
        this.PointerMoved += this.OnPointerMoved;
        this.PointerReleased += this.OnPointerReleased;
        this.PointerWheelChanged += this.OnPointerWheelChanged;
        this.PointerExited += this.OnPointerExited;

        // Register double-click for reset view
        this.DoubleTapped += this.OnDoubleTapped;

        // Set default cursor to indicate panning is available
        this.Cursor = new Cursor(StandardCursorType.Hand);

        // Add tooltip to inform users about available interactions
        ToolTip.SetTip(this, "Pan: Drag with mouse\nZoom: Mouse wheel\nReset View: Double-click");

        // Subscribe to MessageBus events for reset view, as it's a global action
        MessageBus.ResetViewRequested += (sender, args) => ResetView();
    }

    // Dependency Properties for data binding
    public static readonly StyledProperty<IncrementalTin?> TriangulationProperty =
        AvaloniaProperty.Register<TriangulationCanvas, IncrementalTin?>(nameof(Triangulation));

    public IncrementalTin? Triangulation
    {
        get => GetValue(TriangulationProperty);
        set => SetValue(TriangulationProperty, value);
    }

    public static readonly StyledProperty<WriteableBitmap?> InterpolationRasterProperty =
        AvaloniaProperty.Register<TriangulationCanvas, WriteableBitmap?>(nameof(InterpolationRaster));

    public WriteableBitmap? InterpolationRaster
    {
        get => GetValue(InterpolationRasterProperty);
        set => SetValue(InterpolationRasterProperty, value);
    }

    public static readonly StyledProperty<Rect?> RasterBoundsProperty =
        AvaloniaProperty.Register<TriangulationCanvas, Rect?>(nameof(RasterBounds));

    public Rect? RasterBounds
    {
        get => GetValue(RasterBoundsProperty);
        set => SetValue(RasterBoundsProperty, value);
    }

    public static readonly StyledProperty<double> InterpolationOpacityProperty =
        AvaloniaProperty.Register<TriangulationCanvas, double>(nameof(InterpolationOpacity), 0.7);

    public double InterpolationOpacity
    {
        get => GetValue(InterpolationOpacityProperty);
        set => SetValue(InterpolationOpacityProperty, value);
    }

    public static readonly StyledProperty<List<Contour>?> ContoursProperty =
        AvaloniaProperty.Register<TriangulationCanvas, List<Contour>?>(nameof(Contours));

    public List<Contour>? Contours
    {
        get => GetValue(ContoursProperty);
        set => SetValue(ContoursProperty, value);
    }

    public static readonly StyledProperty<List<ContourRegion>?> ContourRegionsProperty =
        AvaloniaProperty.Register<TriangulationCanvas, List<ContourRegion>?>(nameof(ContourRegions));

    public List<ContourRegion>? ContourRegions
    {
        get => GetValue(ContourRegionsProperty);
        set => SetValue(ContourRegionsProperty, value);
    }

    public static readonly StyledProperty<bool> ShowContoursProperty =
        AvaloniaProperty.Register<TriangulationCanvas, bool>(nameof(ShowContours));

    public bool ShowContours
    {
        get => GetValue(ShowContoursProperty);
        set => SetValue(ShowContoursProperty, value);
    }

    public static readonly StyledProperty<bool> ShowContourRegionsProperty =
        AvaloniaProperty.Register<TriangulationCanvas, bool>(nameof(ShowContourRegions));

    public bool ShowContourRegions
    {
        get => GetValue(ShowContourRegionsProperty);
        set => SetValue(ShowContourRegionsProperty, value);
    }

    public static readonly StyledProperty<double> ContourOpacityProperty =
        AvaloniaProperty.Register<TriangulationCanvas, double>(nameof(ContourOpacity), 1.0);

    public double ContourOpacity
    {
        get => GetValue(ContourOpacityProperty);
        set => SetValue(ContourOpacityProperty, value);
    }

    public static readonly StyledProperty<double> ContourRegionOpacityProperty =
        AvaloniaProperty.Register<TriangulationCanvas, double>(nameof(ContourRegionOpacity), 0.3);

    public double ContourRegionOpacity
    {
        get => GetValue(ContourRegionOpacityProperty);
        set => SetValue(ContourRegionOpacityProperty, value);
    }

    public static readonly StyledProperty<VoronoiResult?> VoronoiResultProperty =
        AvaloniaProperty.Register<TriangulationCanvas, VoronoiResult?>(nameof(VoronoiResult));

    public VoronoiResult? VoronoiResult
    {
        get => GetValue(VoronoiResultProperty);
        set => SetValue(VoronoiResultProperty, value);
    }

    public static readonly StyledProperty<bool> ShowVoronoiProperty =
        AvaloniaProperty.Register<TriangulationCanvas, bool>(nameof(ShowVoronoi));

    public bool ShowVoronoi
    {
        get => GetValue(ShowVoronoiProperty);
        set => SetValue(ShowVoronoiProperty, value);
    }

    public static readonly StyledProperty<bool> ShowVoronoiPolygonsProperty =
        AvaloniaProperty.Register<TriangulationCanvas, bool>(nameof(ShowVoronoiPolygons));

    public bool ShowVoronoiPolygons
    {
        get => GetValue(ShowVoronoiPolygonsProperty);
        set => SetValue(ShowVoronoiPolygonsProperty, value);
    }

    public static readonly StyledProperty<bool> ShowVerticesProperty =
        AvaloniaProperty.Register<TriangulationCanvas, bool>(nameof(ShowVertices), true);

    public bool ShowVertices
    {
        get => GetValue(ShowVerticesProperty);
        set => SetValue(ShowVerticesProperty, value);
    }

    public static readonly StyledProperty<bool> ShowTrianglesProperty =
        AvaloniaProperty.Register<TriangulationCanvas, bool>(nameof(ShowTriangles), true);

    public bool ShowTriangles
    {
        get => GetValue(ShowTrianglesProperty);
        set => SetValue(ShowTrianglesProperty, value);
    }

    public static readonly StyledProperty<bool> ShowEdgesProperty =
        AvaloniaProperty.Register<TriangulationCanvas, bool>(nameof(ShowEdges), true);

    public bool ShowEdges
    {
        get => GetValue(ShowEdgesProperty);
        set => SetValue(ShowEdgesProperty, value);
    }

    public static readonly StyledProperty<float> VertexSizeProperty =
        AvaloniaProperty.Register<TriangulationCanvas, float>(nameof(VertexSize), 1.5f);

    public float VertexSize
    {
        get => GetValue(VertexSizeProperty);
        set => SetValue(VertexSizeProperty, value);
    }

    public static readonly StyledProperty<float> EdgeWidthProperty =
        AvaloniaProperty.Register<TriangulationCanvas, float>(nameof(EdgeWidth), 0.5f);

    public float EdgeWidth
    {
        get => GetValue(EdgeWidthProperty);
        set => SetValue(EdgeWidthProperty, value);
    }

    public static readonly StyledProperty<bool> UseTriangleCollectorProperty =
        AvaloniaProperty.Register<TriangulationCanvas, bool>(nameof(UseTriangleCollector));

    public bool UseTriangleCollector
    {
        get => GetValue(UseTriangleCollectorProperty);
        set => SetValue(UseTriangleCollectorProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (this.Triangulation == null || !this.Triangulation.IsBootstrapped())
        {
            var canvasSize = this.Bounds.Size;
            if (canvasSize.Width > 0 && canvasSize.Height > 0)
            {
                var formattedText = new FormattedText(
                    "No triangulation available",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    16,
                    Brushes.Gray);

                context.DrawText(formattedText, new Point(10, 10));
            }

            return;
        }

        var bounds = this.Triangulation?.GetBounds();
        if (!bounds.HasValue)
            return;

        // Calculate transform from world coordinates to canvas coordinates
        var canvasWidth = this.Bounds.Width;
        var canvasHeight = this.Bounds.Height;

        if (canvasWidth <= 0 || canvasHeight <= 0)
            return;

        // if (_fitNextRefresh)
        // {
        // Debug.WriteLine("Auto-fitting triangulation to view on first render");
        // FitToView(renderSize);
        // _fitNextRefresh = false;
        // }

        // Create custom draw operation with contour support
        var drawOp = new TriangulationDrawOperation(
            new Rect(0, 0, canvasWidth, canvasHeight),
            this.Triangulation,
            this._zoomFactor,
            this._panOffset,
            this.ShowVertices,
            this.ShowTriangles,
            this.ShowEdges,
            this.VertexSize,
            this.EdgeWidth,
            this.InterpolationRaster,
            this.RasterBounds,
            this.InterpolationOpacity,
            this.Contours,
            this.ContourRegions,
            this.ShowContours,
            this.ShowContourRegions,
            this.ContourOpacity,
            this.ContourRegionOpacity,
            this.ShowVoronoi,
            this.ShowVoronoiPolygons,
            this.VoronoiResult,
            this.UseTriangleCollector);

        context.Custom(drawOp);
    }

    public void ResetView()
    {
        Debug.WriteLine("ResetView called on TriangulationCanvas");
        if (this.Triangulation == null || !this.Triangulation.IsBootstrapped())
        {
            Debug.WriteLine("ResetView aborted: No valid triangulation");
            return;
        }

        this.InvalidateMeasure();
        this.InvalidateArrange();

        var size = this.Bounds.Size;
        Debug.WriteLine($"ResetView: Canvas bounds = {size.Width}x{size.Height}");

        if (size.Width <= 0 || size.Height <= 0)
        {
            Debug.WriteLine("ResetView aborted: Invalid canvas size");
            return;
        }

        this.FitToView(size);
        this.InvalidateVisual();
    }

    private void FitToView(Size viewportSize)
    {
        if (this.Triangulation == null || !this.Triangulation.IsBootstrapped())
            return;

        var bounds = this.Triangulation.GetBounds();
        if (!bounds.HasValue)
            return;

        Debug.WriteLine(
            $"FitToView: Canvas size = {viewportSize.Width}x{viewportSize.Height}, TIN bounds = {bounds.Value.Left},{bounds.Value.Top},{bounds.Value.Width},{bounds.Value.Height}");

        const double padding = 0.1;
        var paddedWidth = bounds.Value.Width * (1 + padding);
        var paddedHeight = bounds.Value.Height * (1 + padding);

        var scaleX = viewportSize.Width / paddedWidth;
        var scaleY = viewportSize.Height / paddedHeight;
        this._zoomFactor = (float)Math.Min(scaleX, scaleY);

        this._panOffset = new SKPoint(
            (float)((viewportSize.Width - bounds.Value.Width * this._zoomFactor) / 2
                    - bounds.Value.Left * this._zoomFactor),
            (float)((viewportSize.Height - bounds.Value.Height * this._zoomFactor) / 2
                    - bounds.Value.Top * this._zoomFactor));

        Debug.WriteLine(
            $"FitToView: Applied zoom factor = {this._zoomFactor}, pan offset = {this._panOffset.X},{this._panOffset.Y}");
        this.InvalidateVisual();
    }

    // Mouse and interaction event handlers remain the same...
    private void OnDoubleTapped(object? sender, RoutedEventArgs e)
    {
        Debug.WriteLine("Double-click detected, resetting view");
        var originalCursor = this.Cursor;
        this.Cursor = new Cursor(StandardCursorType.Wait);
        this.ResetView();

        var timer = new Timer(300);
        timer.Elapsed += new ElapsedEventHandler((_, _) =>
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        this.Cursor = originalCursor;
                        timer.Dispose();
                    });
            });
        timer.AutoReset = false;
        timer.Start();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (this._isPanning)
        {
            this._isPanning = false;
            this.Cursor = new Cursor(StandardCursorType.Hand);
            e.Pointer.Capture(null);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (this._isPanning)
        {
            var currentPosition = e.GetPosition(this);
            var delta = currentPosition - this._lastMousePosition;

            this._panOffset = new SKPoint(this._panOffset.X + (float)delta.X, this._panOffset.Y + (float)delta.Y);

            this._lastMousePosition = currentPosition;
            this.InvalidateVisual();
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this._lastMousePosition = e.GetPosition(this);
            this._isPanning = true;
            this.Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Pointer.Capture(this);
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (this._isPanning)
        {
            this._isPanning = false;
            this.Cursor = new Cursor(StandardCursorType.Hand);
            e.Pointer.Capture(null);
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var position = e.GetPosition(this);
        var zoomDelta = (float)(e.Delta.Y * 0.1);
        var newZoom = Math.Clamp(this._zoomFactor + zoomDelta, 0.1f, 10.0f);

        var mouseDataX = ((float)position.X - this._panOffset.X) / this._zoomFactor;
        var mouseDataY = ((float)position.Y - this._panOffset.Y) / this._zoomFactor;

        this._zoomFactor = newZoom;

        this._panOffset = new SKPoint(
            (float)position.X - mouseDataX * this._zoomFactor,
            (float)position.Y - mouseDataY * this._zoomFactor);

        this.InvalidateVisual();
    }

    // Custom draw operation with contour support
    private class TriangulationDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;

        private readonly SKColor[] _constraintRegionColors = new[]
                                                                 {
                                                                     new SKColor(255, 182, 193, 180), // Light pink
                                                                     new SKColor(255, 218, 185, 180), // Peach
                                                                     new SKColor(221, 160, 221, 180), // Plum
                                                                     new SKColor(176, 224, 230, 180), // Powder blue
                                                                     new SKColor(240, 230, 140, 180), // Khaki
                                                                     new SKColor(255, 250, 205, 180), // Lemon chiffon
                                                                     new SKColor(144, 238, 144, 180) // Light green
                                                                 };

        private readonly double _contourOpacity;

        private readonly double _contourRegionOpacity;

        private readonly List<ContourRegion>? _contourRegions;

        private readonly List<Contour>? _contours;

        private readonly float _edgeWidth;

        private readonly WriteableBitmap? _interpolationRaster;

        private readonly SKPoint _offset;

        private readonly Rect? _rasterBounds;

        private readonly double _rasterOpacity;

        private readonly Dictionary<int, SKColor> _regionColors = new();

        private readonly float _scale;

        private readonly bool _showContourRegions;

        private readonly bool _showContours;

        private readonly bool _showEdges;

        private readonly bool _showTriangles;

        private readonly bool _showVertices;

        private readonly bool _showVoronoi;

        private readonly bool _showVoronoiPolygons;

        private readonly IncrementalTin? _triangulation;

        private readonly bool _useTriangleCollector;

        private readonly float _vertexSize;

        private readonly VoronoiResult? _voronoiResult;

        public TriangulationDrawOperation(
            Rect bounds,
            IncrementalTin? triangulation,
            float scale,
            SKPoint offset,
            bool showVertices,
            bool showTriangles,
            bool showEdges,
            float vertexSize,
            float edgeWidth,
            WriteableBitmap? interpolationRaster,
            Rect? rasterBounds,
            double rasterOpacity,
            List<Contour>? contours,
            List<ContourRegion>? contourRegions,
            bool showContours,
            bool showContourRegions,
            double contourOpacity,
            double contourRegionOpacity,
            bool showVoronoi,
            bool showVoronoiPolygons,
            VoronoiResult? voronoiResult,
            bool useTriangleCollector)
        {
            this._bounds = bounds;
            this._triangulation = triangulation;
            this._scale = scale;
            this._offset = offset;
            this._showVertices = showVertices;
            this._showTriangles = showTriangles;
            this._showEdges = showEdges;
            this._vertexSize = vertexSize;
            this._edgeWidth = edgeWidth;
            this._interpolationRaster = interpolationRaster;
            this._rasterBounds = rasterBounds;
            this._rasterOpacity = rasterOpacity;
            this._contours = contours;
            this._contourRegions = contourRegions;
            this._showContours = showContours;
            this._showContourRegions = showContourRegions;
            this._contourOpacity = contourOpacity;
            this._contourRegionOpacity = contourRegionOpacity;
            this._showVoronoi = showVoronoi;
            this._showVoronoiPolygons = showVoronoiPolygons;
            this._voronoiResult = voronoiResult;
            this._useTriangleCollector = useTriangleCollector;
        }

        public Rect Bounds => this._bounds;

        public void Dispose()
        {
        }

        public bool Equals(ICustomDrawOperation? other)
        {
            return ReferenceEquals(this, other);
        }

        public bool HitTest(Point p)
        {
            return this._bounds.Contains(p);
        }

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null)
                return;

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;

            canvas.Clear(SKColors.White);
            canvas.Save();
            
            // Apply transform: translate, scale, then flip Y-axis for geographic coordinates
            canvas.Translate(this._offset.X, this._offset.Y);
            canvas.Scale(this._scale, this._scale);
            
            // Flip Y-axis to correct geographic orientation (north=up)
            // This handles the mismatch between Avalonia's top-down Y and geographic bottom-up Y
            var bounds = this._triangulation?.GetBounds();
            if (bounds.HasValue)
            {
                var centerY = bounds.Value.Top + bounds.Value.Height / 2;
                canvas.Scale(1, -1);
                canvas.Translate(0, (float)(-2 * centerY));
            }

            // Draw triangulation components
            this.DrawTriangulation(canvas);

            // Draw interpolation raster if available
            if (this._interpolationRaster != null && this._rasterOpacity > 0) this.DrawInterpolationRaster(canvas);

            // Draw contours if enabled
            if (this._showContours && this._contours?.Count > 0) this.DrawContours(canvas);

            // Draw contour regions if enabled
            if (this._showContourRegions && this._contourRegions?.Count > 0) this.DrawContourRegions(canvas);

            // Draw Voronoi diagram if enabled
            if (this._showVoronoi) this.DrawVoronoi(canvas);

            canvas.Restore();
        }

        private void DrawContourRegions(SKCanvas canvas)
        {
            using var regionPaint = new SKPaint
                                        {
                                            Color = SKColors.LightGreen.WithAlpha(
                                                (byte)(255 * this._contourRegionOpacity)),
                                            Style = SKPaintStyle.Fill,
                                            IsAntialias = true
                                        };

            foreach (var region in this._contourRegions)
            {
                var coordinates = region.GetXY();
                if (coordinates.Length < 6) continue; // Need at least 3 points

                using var path = new SKPath();
                path.MoveTo((float)coordinates[0], (float)coordinates[1]);

                for (var i = 2; i < coordinates.Length; i += 2)
                    path.LineTo((float)coordinates[i], (float)coordinates[i + 1]);

                path.Close();
                canvas.DrawPath(path, regionPaint);
            }
        }

        private void DrawContours(SKCanvas canvas)
        {
            using var contourPaint = new SKPaint
                                         {
                                             Color = SKColors.DarkBlue.WithAlpha((byte)(255 * this._contourOpacity)),
                                             Style = SKPaintStyle.Stroke,
                                             StrokeWidth = 2.0f / this._scale,
                                             IsAntialias = true
                                         };

            foreach (var contour in this._contours)
            {
                if (contour.IsEmpty()) continue;

                var coordinates = contour.GetXY();
                if (coordinates.Length < 4) continue; // Need at least 2 points

                using var path = new SKPath();
                path.MoveTo((float)coordinates[0], (float)coordinates[1]);

                for (var i = 2; i < coordinates.Length; i += 2)
                    path.LineTo((float)coordinates[i], (float)coordinates[i + 1]);

                canvas.DrawPath(path, contourPaint);
            }
        }

        private void DrawInterpolationRaster(SKCanvas canvas)
        {
            if (this._interpolationRaster == null || this._rasterOpacity <= 0 || !this._rasterBounds.HasValue)
                return;

            try
            {
                var bounds = this._rasterBounds.Value;

                using var lockedBitmap = this._interpolationRaster.Lock();
                var pixelSize = this._interpolationRaster.PixelSize;

                var skBitmap = new SKBitmap();
                var info = new SKImageInfo(pixelSize.Width, pixelSize.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

                skBitmap.InstallPixels(info, lockedBitmap.Address, lockedBitmap.RowBytes);

                using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.Medium };

                var layerRect = new SKRect(
                    (float)bounds.X,
                    (float)bounds.Y,
                    (float)(bounds.X + bounds.Width),
                    (float)(bounds.Y + bounds.Height));

                using var layerPaint = new SKPaint
                                           {
                                               Color = SKColors.White.WithAlpha((byte)(255 * this._rasterOpacity))
                                           };
                canvas.SaveLayer(layerRect, layerPaint);

                var srcRect = new SKRect(0, 0, pixelSize.Width, pixelSize.Height);
                var destRect = new SKRect(
                    (float)bounds.X,
                    (float)bounds.Y,
                    (float)(bounds.X + bounds.Width),
                    (float)(bounds.Y + bounds.Height));

                canvas.DrawBitmap(skBitmap, srcRect, destRect, paint);
                canvas.Restore();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error drawing interpolation raster: {ex.Message}");
            }
        }

        private void DrawTriangulation(SKCanvas canvas)
        {
            // Standard triangulation drawing code - triangles, edges, vertices
            using var defaultTrianglePaint = new SKPaint
                                                 {
                                                     Color = SKColors.LightBlue.WithAlpha(128),
                                                     Style = SKPaintStyle.Fill,
                                                     IsAntialias = true
                                                 };

            using var edgePaint = new SKPaint
                                      {
                                          Color = SKColors.DarkBlue,
                                          Style = SKPaintStyle.Stroke,
                                          StrokeWidth = this._edgeWidth / this._scale,
                                          IsAntialias = true
                                      };
            using var perimeterEdgePaint = new SKPaint
                                               {
                                                   Color = SKColors.GreenYellow,
                                                   Style = SKPaintStyle.Stroke,
                                                   StrokeWidth = this._edgeWidth * 5 / this._scale,
                                                   IsAntialias = true
                                               };
            using var constraintEdgePaint = new SKPaint
                                                {
                                                    Color = SKColors.Red,
                                                    Style = SKPaintStyle.Stroke,
                                                    StrokeWidth = this._edgeWidth * 3 / this._scale,
                                                    IsAntialias = true
                                                };

            using var vertexPaint = new SKPaint
                                        {
                                            Color = SKColors.DarkRed, Style = SKPaintStyle.Fill, IsAntialias = true
                                        };

            var regionPaints = new Dictionary<int, SKPaint>();

            if (this._useTriangleCollector)
            {
                // Use TriangleCollector to render constrained regions
                Debug.WriteLine("Using TriangleCollector for visualization");

                // we won't draw non-constrained triangles when exercising the constrained triangle collector

                // Then use TriangleCollector to draw constrained triangles with region-specific colors
                TriangleCollector.VisitTrianglesConstrained(
                    this._triangulation,
                    (IVertex[] vertices) =>
                        {
                            var a = new SKPoint((float)vertices[0].X, (float)vertices[0].Y);
                            var b = new SKPoint((float)vertices[1].X, (float)vertices[1].Y);
                            var c = new SKPoint((float)vertices[2].X, (float)vertices[2].Y);

                            using var path = new SKPath();
                            path.MoveTo(a);
                            path.LineTo(b);
                            path.LineTo(c);
                            path.Close();

                            // Try to determine the region index
                            var regionIndex = -1;

                            // Look for edges that connect vertices of this triangle
                            // foreach (var edge in _triangulation.GetEdges())
                            // {
                            // bool connectsVertices = 
                            // (edge.GetA() == vertices[0] && edge.GetB() == vertices[1]) ||
                            // (edge.GetA() == vertices[1] && edge.GetB() == vertices[0]) ||
                            // (edge.GetA() == vertices[1] && edge.GetB() == vertices[2]) ||
                            // (edge.GetA() == vertices[2] && edge.GetB() == vertices[1]) ||
                            // (edge.GetA() == vertices[0] && edge.GetB() == vertices[2]) ||
                            // (edge.GetA() == vertices[2] && edge.GetB() == vertices[0]);

                            // if (connectsVertices && edge.IsConstraintRegionInterior())
                            // {
                            // regionIndex = edge.GetConstraintRegionInteriorIndex();
                            // break;
                            // }
                            // }

                            // Get or create a paint for this region
                            SKPaint? paintToUse;
                            {
                                // if (regionIndex >= 0)
                                if (!regionPaints.TryGetValue(regionIndex, out paintToUse))
                                {
                                    var color = this.GetRegionColor(regionIndex);
                                    paintToUse = new SKPaint
                                                     {
                                                         Color = color, Style = SKPaintStyle.Fill, IsAntialias = true
                                                     };
                                    regionPaints[regionIndex] = paintToUse;
                                }

                                canvas.DrawPath(path, paintToUse);
                            }

                            // else
                            // {
                            // // Use a default color for constrained triangles without a specific region
                            // paintToUse = new SKPaint
                            // {
                            // Color = SKColors.Orange.WithAlpha(128),
                            // Style = SKPaintStyle.Fill,
                            // IsAntialias = true
                            // };
                            // }
                        });
            }
            else if (this._showTriangles)
            {
                foreach (var triangle in this._triangulation.GetTriangles())
                {
                    if (triangle.IsGhost()) continue;

                    var a = new SKPoint((float)triangle.GetVertexA().X, (float)triangle.GetVertexA().Y);
                    var b = new SKPoint((float)triangle.GetVertexB().X, (float)triangle.GetVertexB().Y);
                    var c = new SKPoint((float)triangle.GetVertexC().X, (float)triangle.GetVertexC().Y);

                    using var path = new SKPath();
                    path.MoveTo(a);
                    path.LineTo(b);
                    path.LineTo(c);
                    path.Close();

                    var paintToUse = defaultTrianglePaint;
                    var isConstrained = false;
                    var regionIndex = -1;

                    // Check each edge for constraint region membership
                    foreach (var edge in new[] { triangle.GetEdgeA(), triangle.GetEdgeB(), triangle.GetEdgeC() })
                        if (edge.IsConstraintRegionInterior())
                        {
                            regionIndex = edge.GetConstraintRegionInteriorIndex();
                            isConstrained = true;
                            break;
                        }

                    if (isConstrained)
                    {
                        // Get or create a paint for this region from cache
                        if (!regionPaints.TryGetValue(regionIndex, out var regionPaint))
                        {
                            var color = this.GetRegionColor(regionIndex);
                            regionPaint = new SKPaint { Color = color, Style = SKPaintStyle.Fill, IsAntialias = true };
                            regionPaints[regionIndex] = regionPaint;
                        }

                        paintToUse = regionPaints[regionIndex];
                    }

                    canvas.DrawPath(path, paintToUse);
                }
            }

            if (this._showEdges)
            {
                // if (_useTriangleCollector)
                // {
                // foreach (var triangle in TriangleCollector.VisitTrianglesConstrained(_triangulation))
                // {
                // foreach (var edge in new IQuadEdge[]
                // { triangle.GetEdgeA(), triangle.GetEdgeB(), triangle.GetEdgeC() })
                // {
                // var vertexA = edge.GetA();
                // var vertexB = edge.GetB();
                // if (vertexB.IsNullVertex()) continue;

                // var pointA = new SKPoint((float)vertexA.X, (float)vertexA.Y);
                // var pointB = new SKPoint((float)vertexB.X, (float)vertexB.Y);

                // bool isConstrained = edge.IsConstrained();
                // canvas.DrawLine(pointA, pointB, isConstrained ? constraintEdgePaint : edgePaint);
                // }
                // }
                // }
                // else
                foreach (var edge in this._triangulation.GetEdges())
                {
                    var vertexA = edge.GetA();
                    var vertexB = edge.GetB();
                    if (vertexB.IsNullVertex()) continue;

                    var pointA = new SKPoint((float)vertexA.X, (float)vertexA.Y);
                    var pointB = new SKPoint((float)vertexB.X, (float)vertexB.Y);

                    var isConstrained = edge.IsConstrained();

                    canvas.DrawLine(pointA, pointB, isConstrained ? constraintEdgePaint : edgePaint);
                }

                var perimeterEdges = this._triangulation.GetPerimeter().ToList();

                // always show perimeter edges in a different paint 
                foreach (var edge in this._triangulation.GetPerimeter())
                {
                    var vertexA = edge.GetA();
                    var vertexB = edge.GetB();
                    if (vertexB.IsNullVertex()) continue;

                    var pointA = new SKPoint((float)vertexA.X, (float)vertexA.Y);
                    var pointB = new SKPoint((float)vertexB.X, (float)vertexB.Y);

                    canvas.DrawLine(pointA, pointB, perimeterEdgePaint);
                }
            }

            if (this._showVertices)
            {
                var adjustedSize = this._vertexSize / this._scale;
                foreach (var vertex in this._triangulation.GetVertices())
                    canvas.DrawCircle((float)vertex.X, (float)vertex.Y, adjustedSize, vertexPaint);
            }
        }

        private void DrawVoronoi(SKCanvas canvas)
        {
            if (this._voronoiResult == null) return;

            using var voronoiEdgePaint = new SKPaint
                                             {
                                                 Color = SKColors.DeepPink,
                                                 Style = SKPaintStyle.Stroke,
                                                 StrokeWidth = this._edgeWidth / this._scale,
                                                 IsAntialias = true
                                             };

            // Draw Voronoi edges
            if (this._showVoronoi)
                foreach (var edge in this._voronoiResult.Edges)
                {
                    var vertexA = edge.GetA();
                    var vertexB = edge.GetB();
                    if (vertexA != null && vertexB != null && !vertexA.IsNullVertex() && !vertexB.IsNullVertex())
                    {
                        var pointA = new SKPoint((float)vertexA.X, (float)vertexA.Y);
                        var pointB = new SKPoint((float)vertexB.X, (float)vertexB.Y);

                        canvas.DrawLine(pointA, pointB, voronoiEdgePaint);
                    }
                }
        }

        private SKColor GetRegionColor(int regionIndex)
        {
            if (regionIndex < 0) return SKColors.LightBlue.WithAlpha(128);

            if (!this._regionColors.TryGetValue(regionIndex, out var color))
            {
                // Assign a color from predefined palette or generate one
                if (regionIndex < this._constraintRegionColors.Length)
                {
                    color = this._constraintRegionColors[regionIndex];
                }
                else
                {
                    // Generate a color based on the region index
                    var hue = regionIndex * 137 % 360; // Golden angle to distribute colors
                    color = SKColor.FromHsl(hue, 70, 70, 180);
                }

                this._regionColors[regionIndex] = color;
            }

            return color;
        }
    }
}