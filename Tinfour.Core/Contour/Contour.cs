/*
 * Copyright 2019 Gary W. Lucas.
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
 * 07/2019  G. Lucas     Created
 * 08/2025 M.Fender     Ported to C# for Tinfour.Core
 * 11/2025  M. Fender    Added Span<T> support for performance optimization
 *
 * Notes:
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Contour;

using System.Buffers;

using Tinfour.Core.Common;

/// <summary>
///     Provides methods and elements for constructing a contour. Tinfour defines
///     contours as specifying a boundary between two regions in a plane. The region
///     to the left of the contour is treated as including points with vertical
///     coordinates greater than or equal to the contour's vertical coordinate. The
///     values to the right are treated as including points with vertical coordinates
///     less than the contour's vertical coordinate. Thus, in an elevation set, a
///     hill would be represented with a set of closed-loop contours taken in
///     counterclockwise order. A valley would be represented as a set of closed-loop
///     contours taken in clockwise order.
/// </summary>
/// <remarks>
///     <para>
///         The Complete() method should always be called when a contour is fully
///         populated (e.g. it is complete). The Complete call trims the internal buffers
///         and performs any sanity checking required for contour management.
///     </para>
///     <para>
///         A closed-loop contour is expected to always include a "closure point" so that
///         the first point in the contour matches the last. This approach is taken to
///         simplify internal logic in the contour building routines. The Complete()
///         method ensures that a closure point is added to closed-loop contours if none
///         is provided by the application.
///     </para>
/// </remarks>
public class Contour
{
    private const int GrowthFactor = 256;

    private static int _serialIdSource;

    private readonly bool _closedLoop;

    private readonly int _contourId;

    private readonly int _leftIndex;

    private readonly int _rightIndex;

    private readonly double _z;

    private int _n;

    private double[] _xy = new double[GrowthFactor];

    /// <summary>
    ///     Constructs an instance of a contour
    /// </summary>
    /// <param name="leftIndex">
    ///     The contour-interval index of the area to the left of the
    ///     contour.
    /// </param>
    /// <param name="rightIndex">
    ///     The contour-interval index of the area to the right of
    ///     the contour.
    /// </param>
    /// <param name="z">The vertical coordinate for the contour</param>
    /// <param name="closedLoop">
    ///     Indicates if the contour is to be treated as a closed
    ///     loop.
    /// </param>
    public Contour(int leftIndex, int rightIndex, double z, bool closedLoop)
    {
        // The contour ID is just a debugging aid. It gives a way of detecting
        // when a problematic contour is constructed. Once the software is mature
        // it may not be necessary to preserve it.
        _contourId = Interlocked.Increment(ref _serialIdSource);
        _leftIndex = leftIndex;
        _rightIndex = rightIndex;
        _z = z;
        _closedLoop = closedLoop;
    }

    /// <summary>
    ///     An enumeration that indicates the type of a contour
    /// </summary>
    public enum ContourType
    {
        /// <summary>
        ///     Contour lies entirely in the interior of the TIN with the possible
        ///     exception of the two end points which may lie on perimeter edges. Both
        ///     the left and right index of the contour will be defined (zero or
        ///     greater).
        /// </summary>
        Interior,

        /// <summary>
        ///     Contour lies entirely on the boundary of the TIN.
        /// </summary>
        Boundary
    }

    internal TipLink? StartTip { get; set; }

    internal TipLink? TerminalTip { get; set; }

    internal bool TraversedBackward { get; set; }

    internal bool TraversedForward { get; set; }

    /// <summary>
    ///     Add a coordinate point to the contour.
    /// </summary>
    /// <param name="x">The Cartesian x-coordinate for the point</param>
    /// <param name="y">The Cartesian y-coordinate for the point</param>
    public void Add(double x, double y)
    {
        if (_n == _xy.Length) Array.Resize(ref _xy, _xy.Length + GrowthFactor);

        if (_n > 1)
            if (_xy[_n - 2] == x && _xy[_n - 1] == y)
                return;

        _xy[_n++] = x;
        _xy[_n++] = y;
    }

    /// <summary>
    ///     Called when the construction of a contour is complete to trim the memory
    ///     for the internal point collection. This method also ensures that a
    ///     closed-loop contour includes a closure point.
    /// </summary>
    /// <remarks>
    ///     References to edges and contour-building elements are not affected by this
    ///     call.
    /// </remarks>
    public void Complete()
    {
        if (_closedLoop && _n > 6)
        {
            // Ensure that there is a "closure" vertex included in the contour.
            // If the existing endpoints are numerically close, they are adjusted
            // slightly to ensure exact matches. Otherwise, an additional
            // vertex is added to the contour.
            var x0 = _xy[0];
            var y0 = _xy[1];
            var x1 = _xy[_n - 2];
            var y1 = _xy[_n - 1];

            if (x0 != x1 || y0 != y1)
            {
                if (NumericallySame(x0, x1) && NumericallySame(y0, y1))
                {
                    _xy[_n - 2] = x0;
                    _xy[_n - 1] = y0;
                }
                else
                {
                    Add(x0, y0);
                }
            }
        }

        if (_xy.Length > _n) Array.Resize(ref _xy, _n);
    }

    /// <summary>
    ///     Gets the bounding rectangle for the contour.
    /// </summary>
    /// <returns>A tuple representing the bounds (left, top, width, height)</returns>
    public (double Left, double Top, double Width, double Height) GetBounds()
    {
        if (_n < 2) return (0, 0, 0, 0);

        var minX = _xy[0];
        var maxX = _xy[0];
        var minY = _xy[1];
        var maxY = _xy[1];

        for (var i = 1; i < _n / 2; i++)
        {
            var x = _xy[i * 2];
            var y = _xy[i * 2 + 1];

            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        return (minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    ///     Gets the serialized identification code for the contour.
    ///     When used with the ContourBuilder, this
    ///     value gives a unique serial ID assigned when the contour is constructed.
    ///     This value should not be confused with the contour interval
    ///     or the left and right side index values.
    /// </summary>
    /// <returns>An integer value.</returns>
    public int GetContourId()
    {
        return _contourId;
    }

    /// <summary>
    ///     Gets the index for the value of the input contour array that
    ///     was used to build this contour, or a notional value if this
    ///     instance is a boundary contour.
    /// </summary>
    /// <remarks>
    ///     It is strongly recommended that application code check
    ///     to see if this instance is a boundary contour before using the contour index.
    /// </remarks>
    /// <returns>A value in the range 0 to the length of the input z contour array.</returns>
    public int GetContourIndex()
    {
        if (_rightIndex < 0)

            // This is a boundary contour. The contour index value
            // is not truly meaningful.
            return _leftIndex;
        return _leftIndex - 1;
    }

    /// <summary>
    ///     Indicates whether the contour is an interior or perimeter contour.
    ///     Note: future implementations may include additional types.
    /// </summary>
    /// <returns>A valid enumeration instance</returns>
    public ContourType GetContourType()
    {
        return _rightIndex == -1 ? ContourType.Boundary : ContourType.Interior;
    }

    /// <summary>
    ///     Get the index for the region lying to the left of the contour.
    /// </summary>
    /// <returns>
    ///     An integer in the range 0 to nContour, or -1 if the contour
    ///     borders a null-data area
    /// </returns>
    public int GetLeftIndex()
    {
        return _leftIndex;
    }

    /// <summary>
    ///     Get the index for the region lying to the right of the contour.
    /// </summary>
    /// <returns>
    ///     An integer in the range 0 to nContour, or -1 if the contour
    ///     borders a null-data area
    /// </returns>
    public int GetRightIndex()
    {
        return _rightIndex;
    }

    /// <summary>
    ///     Gets a safe copy of the coordinates for the contour. Coordinates
    ///     are stored in a one-dimensional array of doubles in the order:
    ///     { (x0,y0), (x1,y1), (x2,y2), etc. }.
    /// </summary>
    /// <returns>
    ///     A valid, potentially zero-length array giving x and y coordinates
    ///     for a series of points.
    /// </returns>
    public double[] GetXY()
    {
        var result = new double[_n];
        _xy.AsSpan(0, _n).CopyTo(result);
        return result;
    }

    /// <summary>
    ///     Gets a read-only span view of the coordinates for the contour.
    ///     This avoids allocation when only reading values.
    ///     Coordinates are stored in the order: { (x0,y0), (x1,y1), (x2,y2), etc. }.
    /// </summary>
    /// <returns>
    ///     A read-only span of the coordinate data.
    /// </returns>
    public ReadOnlySpan<double> GetXYSpan()
    {
        return _xy.AsSpan(0, _n);
    }

    /// <summary>
    ///     Gets the z value associated with the contour
    /// </summary>
    /// <returns>The z value used to construct the contour.</returns>
    public double GetZ()
    {
        return _z;
    }

    /// <summary>
    ///     Indicates whether the contour is a boundary contour.
    /// </summary>
    /// <returns>True if the contour is a boundary; otherwise, false.</returns>
    public bool IsBoundary()
    {
        return _rightIndex == -1;
    }

    /// <summary>
    ///     Indicates that the contour forms a closed loop
    /// </summary>
    /// <returns>True if the contour forms a closed loop; otherwise false</returns>
    public bool IsClosed()
    {
        return _closedLoop;
    }

    /// <summary>
    ///     Indicates whether the contour is empty.
    /// </summary>
    /// <returns>True if the contour has no geometry defined; otherwise false.</returns>
    public bool IsEmpty()
    {
        return _n < 4;

        // recall _n = nPoints*2. a single-point contour is empty.
    }

    /// <summary>
    ///     Indicates the number of points stored in the contour
    /// </summary>
    /// <returns>A positive integer value, potentially zero.</returns>
    public int Size()
    {
        return _n / 2;
    }

    public override string ToString()
    {
        var cString = string.Empty;
        if (_n >= 4)
        {
            var x0 = _xy[0];
            var y0 = _xy[1];
            var x1 = _xy[_n - 2];
            var y1 = _xy[_n - 1];
            cString = $"(x0,y0)=({x0:F6},{y0:F6})  (x1,y1)=({x1:F6},{y1:F6})";
        }

        return
            $"Contour {_contourId}: L={_leftIndex}, R={_rightIndex}, z={_z}, closed={_closedLoop}  {cString}";
    }

    /// <summary>
    ///     Used during construction of the contour from a Delaunay Triangulation to
    ///     create a through-edge transition point.
    /// </summary>
    /// <param name="e">The edge through which the contour passes.</param>
    /// <param name="zA">The value of the first vertex of the edge</param>
    /// <param name="zB">The value of the second vertex of the edge</param>
    internal void Add(IQuadEdge e, double zA, double zB)
    {
        if (_n == _xy.Length) Array.Resize(ref _xy, _xy.Length + GrowthFactor);

        // Interpolate out next point
        var A = e.GetA();
        var B = e.GetB();
        var zDelta = zB - zA;
        var x = ((_z - zA) * B.X + (zB - _z) * A.X) / zDelta;
        var y = ((_z - zA) * B.Y + (zB - _z) * A.Y) / zDelta;

        if (_n > 1)
            if (_xy[_n - 2] == x && _xy[_n - 1] == y)
                return;

        _xy[_n++] = x;
        _xy[_n++] = y;
    }

    /// <summary>
    ///     Used during construction of the contour from a Delaunay Triangulation to
    ///     indicate a through-vertex transition of the contour. The edge e is expected
    ///     to end in the vertex v and begin with a vertex that has a z-coordinate
    ///     greater than or equal to the contour z value. During construction, this
    ///     edge is used to indicate the area immediately to the left of the contour.
    /// </summary>
    /// <param name="v">A valid vertex through which the contour passes.</param>
    internal void Add(IVertex v)
    {
        if (_n == _xy.Length) Array.Resize(ref _xy, _xy.Length + GrowthFactor);

        var x = v.X;
        var y = v.Y;

        if (_n > 1)
            if (_xy[_n - 2] == x && _xy[_n - 1] == y)
                return;

        _xy[_n++] = x;
        _xy[_n++] = y;
    }

    /// <summary>
    ///     Null-out any resources that were required for building the contours
    ///     or regions, but are no longer needed.
    /// </summary>
    internal void CleanUp()
    {
        StartTip = null;
        TerminalTip = null;
    }

    private static bool NumericallySame(double a, double b)
    {
        if (double.IsNaN(a) || double.IsNaN(b)) return false;

        if (a == b)

            // This will take care of case where both are zero
            return true;

        var threshold = double.Epsilon * ((Math.Abs(a) + Math.Abs(b)) / 2.0) * 16;
        var absDelta = Math.Abs(a - b);
        return absDelta <= threshold;
    }
}