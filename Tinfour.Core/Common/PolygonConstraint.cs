/*
 * Copyright 2015-2025 Gary W. Lucas.
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

/*
 * -----------------------------------------------------------------------
 *
 * Revision History:
 * Date     Name         Description
 * ------   ---------    -------------------------------------------------
 * 02/2013  G. Lucas     Initial implementation
 * 08/2025 M.Fender     Ported to C# for Tinfour.Core
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Common;

/// <summary>
///     Simple bounds structure to replace System.Drawing.RectangleF dependency.
/// </summary>
internal readonly struct BoundsRect
{
    public double MinX { get; }
    public double MinY { get; }
    public double MaxX { get; }
    public double MaxY { get; }

    public BoundsRect(double minX, double minY, double maxX, double maxY)
    {
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    public bool Contains(double x, double y)
    {
        return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
    }
}

/// <summary>
///     Represents a polygon constraint for a Triangulated Irregular Network.
/// </summary>
public class PolygonConstraint : Polyline, IPolygonConstraint
{
    private readonly object _boundsLock = new();

    private readonly bool _definesRegion;

    private readonly bool _isHole;

    private object? _applicationData;

    // For hit-test logic
    private BoundsRect? _bounds;

    private int _constraintIndex;

    private double? _defaultZ;

    private IQuadEdge? _linkingEdge;

    private IIncrementalTin? _managingTin;

    private double _signedArea = double.NaN;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PolygonConstraint" /> class.
    /// </summary>
    /// <param name="definesRegion">Indicates whether this polygon defines a constrained region.</param>
    public PolygonConstraint(bool definesRegion = true)
    {
        _definesRegion = definesRegion;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PolygonConstraint" /> class with the specified vertices.
    /// </summary>
    /// <param name="vertices">A collection of vertices defining the constraint.</param>
    /// <param name="definesRegion">Indicates whether this polygon defines a constrained region.</param>
    /// <param name="isHole">Indicates if the polygon represents a hole within a larger region.</param>
    public PolygonConstraint(IEnumerable<IVertex> vertices, bool definesRegion = true, bool isHole = false)
        : base(vertices)
    {
        _definesRegion = definesRegion;
        _isHole = isHole;
        Complete();
    }

    /// <inheritdoc />
    public bool DefinesConstrainedRegion()
    {
        return _definesRegion;
    }

    /// <inheritdoc />
    public object? GetApplicationData()
    {
        return _applicationData;
    }

    /// <summary>
    ///     Gets the absolute area of the polygon.
    /// </summary>
    public double GetArea()
    {
        return Math.Abs(GetSignedArea());
    }

    /// <inheritdoc />
    public int GetConstraintIndex()
    {
        return _constraintIndex;
    }

    /// <inheritdoc />
    public IQuadEdge? GetConstraintLinkingEdge()
    {
        return _linkingEdge;
    }

    /// <inheritdoc />
    public IConstraint GetConstraintWithNewGeometry(IList<IVertex> geometry)
    {
        var newConstraint = new PolygonConstraint(geometry, _definesRegion, _isHole)
                                {
                                    _applicationData = _applicationData
                                };
        return newConstraint;
    }

    /// <inheritdoc />
    public double? GetDefaultZ()
    {
        return _defaultZ;
    }

    /// <summary>
    ///     Gets the number of edges in the polygon.
    /// </summary>
    /// <returns>A non-negative integer.</returns>
    public int GetEdgeCount()
    {
        return Vertices.Count;
    }

    /// <inheritdoc />
    public IIncrementalTin? GetManagingTin()
    {
        return _managingTin;
    }

    /// <summary>
    ///     Gets the perimeter of the polygon.
    /// </summary>
    /// <returns>A positive floating-point value.</returns>
    public double GetPerimeter()
    {
        return GetLength();
    }

    /// <summary>
    // Gets the signed area of the polygon. Positive for counter-clockwise, negative for clockwise.
    /// </summary>
    public double GetSignedArea()
    {
        if (double.IsNaN(_signedArea)) _signedArea = ComputeSignedArea();
        return _signedArea;
    }

    /// <summary>
    ///     Indicates if the polygon vertices are wound in a counter-clockwise order.
    /// </summary>
    public bool IsCounterclockwise()
    {
        return GetSignedArea() > 0;
    }

    /// <summary>
    ///     Gets a value indicating whether this polygon is a hole.
    /// </summary>
    public bool IsHole()
    {
        return _isHole;
    }

    /// <inheritdoc />
    public bool IsPointInsideConstraint(double x, double y)
    {
        if (!_definesRegion || Vertices.Count < 3) return false;

        if (_bounds == null)
            lock (_boundsLock)
            {
                if (_bounds == null)
                {
                    var (left, top, width, height) = GetBounds();
                    _bounds = new BoundsRect(left, top, left + width, top + height);
                }
            }

        if (!_bounds.Value.Contains(x, y)) return false;

        // Use the ray-casting algorithm (even-odd rule)
        var n = Vertices.Count;
        var isInside = false;
        var p1 = Vertices[n - 1];
        for (var i = 0; i < n; i++)
        {
            var p2 = Vertices[i];
            if (p2.Y > y != p1.Y > y && x < (p1.X - p2.X) * (y - p2.Y) / (p1.Y - p2.Y) + p2.X) isInside = !isInside;
            p1 = p2;
        }

        return isInside;
    }

    /// <inheritdoc />
    public override bool IsPolygon()
    {
        return true;
    }

    /// <inheritdoc />
    public override IPolyline Refactor(IEnumerable<IVertex> geometry)
    {
        var newConstraint = new PolygonConstraint(geometry, _definesRegion, _isHole)
                                {
                                    _applicationData = _applicationData
                                };
        return newConstraint;
    }

    /// <inheritdoc />
    public void SetApplicationData(object? applicationData)
    {
        _applicationData = applicationData;
    }

    /// <inheritdoc />
    public void SetConstraintIndex(IIncrementalTin? tin, int index)
    {
        _managingTin = tin;
        _constraintIndex = index;
    }

    /// <inheritdoc />
    public void SetConstraintLinkingEdge(IQuadEdge linkingEdge)
    {
        _linkingEdge = linkingEdge;
    }

    /// <inheritdoc />
    public void SetDefaultZ(double? defaultZ)
    {
        _defaultZ = defaultZ;
    }

    private double ComputeSignedArea()
    {
        if (Vertices.Count < 3) return 0;
        double areaSum = 0;
        var p0 = Vertices[0];
        for (var i = 1; i < Vertices.Count - 1; i++)
        {
            var p1 = Vertices[i];
            var p2 = Vertices[i + 1];
            areaSum += (p1.X - p0.X) * (p2.Y - p0.Y) - (p2.X - p0.X) * (p1.Y - p0.Y);
        }

        return areaSum / 2.0;
    }
}