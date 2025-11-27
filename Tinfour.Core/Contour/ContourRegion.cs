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
 *
 * Notes:
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Contour;

/// <summary>
///     Provides elements and access methods for a region created through a
///     contour-building process.
/// </summary>
public class ContourRegion
{
    private readonly double _absArea;

    private readonly double _area;

    private readonly List<ContourRegion> _children = new();

    private readonly ContourRegionType _contourRegionType;

    private readonly List<ContourRegionMember> _memberList = new();

    private readonly int _regionIndex;

    private readonly double _xTest;

    private readonly double _yTest;

    private ContourRegion? _parent;

    public ContourRegion(List<ContourRegionMember> memberList, int regionIndex)
    {
        if (memberList.Count == 0)
            throw new ArgumentException("An empty specification for a region geometry is not supported");

        _regionIndex = regionIndex;
        _memberList.AddRange(memberList);

        double a = 0;
        var rType = ContourRegionType.Interior;

        foreach (var member in memberList)
        {
            if (member.Contour.GetContourType() == Contour.ContourType.Boundary) rType = ContourRegionType.Perimeter;

            var s = CalculateAreaContribution(member.Contour);
            if (member.Forward) a += s;
            else a -= s;
        }

        _contourRegionType = rType;
        _area = a / 2.0;
        _absArea = Math.Abs(_area);

        var contour = memberList[0].Contour;
        var xy = contour.GetXY();
        _xTest = (xy[0] + xy[2]) / 2.0;
        _yTest = (xy[1] + xy[3]) / 2.0;
    }

    /// <summary>
    ///     Construct a region based on a single, closed-loop contour.
    /// </summary>
    /// <param name="contour">A valid instance describing a single, closed-loop contour.</param>
    public ContourRegion(Contour contour)
    {
        _contourRegionType = contour.GetContourType() == Contour.ContourType.Interior
                                      ? ContourRegionType.Interior
                                      : ContourRegionType.Perimeter;

        if (!contour.IsClosed()) throw new ArgumentException("Single contour constructor requires closed loop");

        _memberList.Add(new ContourRegionMember(contour, true));
        _area = CalculateAreaContribution(contour) / 2;
        _absArea = Math.Abs(_area);

        _regionIndex = _area < 0 ? contour.GetRightIndex() : contour.GetLeftIndex();

        var xy = contour.GetXY();
        _xTest = (xy[0] + xy[2]) / 2.0;
        _yTest = (xy[1] + xy[3]) / 2.0;
    }

    /// <summary>
    ///     An enumeration that indicates the type of a contour region.
    /// </summary>
    public enum ContourRegionType
    {
        /// <summary>
        ///     All contours lie entirely within the interior of the TIN and do not
        ///     intersect its perimeter.
        /// </summary>
        Interior,

        /// <summary>
        ///     At least one contour lies on the perimeter of the TIN. Note that
        ///     primary regions are never enclosed by another region.
        /// </summary>
        Perimeter
    }

    public int ApplicationIndex { get; set; }

    /// <summary>
    ///     Indicates whether the specified point is inside a closed polygon.
    /// </summary>
    /// <param name="xy">
    ///     An array giving the Cartesian coordinates of the closed, simple
    ///     polygon for the region to be tested.
    /// </param>
    /// <param name="x">The Cartesian coordinate for the point of interest</param>
    /// <param name="y">The Cartesian coordinate for the point of interest</param>
    /// <returns>True if the point is inside the contour; otherwise, false</returns>
    public static bool IsPointInsideRegion(double[] xy, double x, double y)
    {
        var rCross = 0;
        var lCross = 0;
        var n = xy.Length / 2;
        var x0 = xy[0];
        var y0 = xy[1];

        for (var i = 1; i < n; i++)
        {
            var x1 = xy[i * 2];
            var y1 = xy[i * 2 + 1];

            var yDelta = y0 - y1;
            if (y1 > y != y0 > y)
            {
                var xTest = (x1 * y0 - x0 * y1 + y * (x0 - x1)) / yDelta;
                if (xTest > x) rCross++;
            }

            if (y1 < y != y0 < y)
            {
                var xTest = (x1 * y0 - x0 * y1 + y * (x0 - x1)) / yDelta;
                if (xTest < x) lCross++;
            }

            x0 = x1;
            y0 = y1;
        }

        // (rCross%2) != (lCross%2)
        if (((rCross ^ lCross) & 0x01) == 1) return false; // on border

        if ((rCross & 0x01) == 1) return true; // unambiguously inside

        return false; // unambiguously outside
    }

    /// <summary>
    ///     Adds a child (nested) region to the internal list and sets
    ///     the child-region's parent link.
    /// </summary>
    /// <param name="region">A valid reference.</param>
    public void AddChild(ContourRegion region)
    {
        _children.Add(region);
        region._parent = this;
    }

    /// <summary>
    ///     Gets the absolute value of the area for this region.
    /// </summary>
    /// <returns>A positive value greater than zero</returns>
    public double GetAbsArea()
    {
        return _absArea;
    }

    /// <summary>
    ///     Gets the absolute value of the overall area of the region.
    ///     No adjustment is made for enclosed regions.
    /// </summary>
    /// <returns>A positive value.</returns>
    public double GetAbsoluteArea()
    {
        return _absArea;
    }

    /// <summary>
    ///     Get the area for the region excluding that of any enclosed
    ///     regions. The enclosed regions are not, strictly speaking,
    ///     part of this region and, so, are not included in the adjusted area.
    /// </summary>
    /// <returns>A positive value.</returns>
    public double GetAdjustedArea()
    {
        var sumArea = _absArea;
        foreach (var enclosedRegion in _children) sumArea -= enclosedRegion.GetAbsoluteArea();
        return sumArea;
    }

    /// <summary>
    ///     Gets the signed area of the region. If the points that specify the region
    ///     are given in a counter-clockwise order, the region will have a positive
    ///     area. If the points are given in a clockwise order, the region will have a
    ///     negative area.
    /// </summary>
    /// <returns>A signed real value.</returns>
    public double GetArea()
    {
        return _area;
    }

    /// <summary>
    ///     Gets an enumerated value indicating what kind of contour
    ///     region this instance represents.
    /// </summary>
    /// <returns>A valid enumeration.</returns>
    public ContourRegionType GetContourRegionType()
    {
        return _contourRegionType;
    }

    /// <summary>
    ///     Gets a list of regions that are enclosed by this region.
    ///     This list includes only those regions that are immediately
    ///     enclosed by this region, but does not include any that may
    ///     be "nested" within the enclosed regions.
    /// </summary>
    /// <returns>A valid, potentially empty, list.</returns>
    public List<ContourRegion> GetEnclosedRegions()
    {
        return new List<ContourRegion>(_children);
    }

    /// <summary>
    ///     Gets the parent region for this region, if any. Regions that are
    ///     enclosed in other regions will have a parent. Regions that are
    ///     not enclosed will not have a parent.
    /// </summary>
    /// <returns>A valid instance, or a null.</returns>
    public ContourRegion? GetParent()
    {
        return _parent;
    }

    /// <summary>
    ///     Gets the index of the region. The indexing scheme is based on the original
    ///     values of the zContour array used when the contour regions were built. The
    ///     minimum proper region index is zero.
    /// </summary>
    /// <remarks>
    ///     At this time, regions are not constructed for areas of null data. In future
    ///     implementations, null-data regions will be indicated by a region index of
    ///     -1.
    /// </remarks>
    /// <returns>A positive integer value, or -1 for null-data regions.</returns>
    public int GetRegionIndex()
    {
        return _regionIndex;
    }

    /// <summary>
    ///     Gets an enumerated value indicating what kind of contour
    ///     region this instance represents.
    /// </summary>
    /// <returns>A valid enumeration.</returns>
    public ContourRegionType GetRegionType()
    {
        return _contourRegionType;
    }

    /// <summary>
    ///     Gets a point lying on one of the segments in the region to support testing
    ///     for polygon enclosures. Note that the test point is never one of the
    ///     vertices of the segment.
    /// </summary>
    /// <returns>A tuple representing the test point (x, y)</returns>
    public (double X, double Y) GetTestPoint()
    {
        return (_xTest, _yTest);
    }

    /// <summary>
    ///     Get the XY coordinates for the contour region. Coordinates
    ///     are stored in a one-dimensional array of doubles in the order:
    ///     { (x0,y0), (x1,y1), (x2,y2), etc. }.
    /// </summary>
    /// <returns>A safe copy of the geometry of the contour region.</returns>
    public double[] GetXY()
    {
        var contour = _memberList[0].Contour;
        if (_memberList.Count == 1 && _memberList[0].Contour.IsClosed()) return contour.GetXY();

        var n = 0;
        foreach (var member in _memberList) n += member.Contour.Size() - 1;
        n++; // closure point

        var xy = new double[n * 2];
        var k = 0;

        foreach (var member in _memberList)
        {
            contour = member.Contour;
            var contourXy = contour.GetXY();
            n = contour.Size();

            if (member.Forward)
                for (var i = 0; i < n - 1; i++)
                {
                    xy[k++] = contourXy[i * 2];
                    xy[k++] = contourXy[i * 2 + 1];
                }
            else
                for (var i = n - 1; i > 0; i--)
                {
                    xy[k++] = contourXy[i * 2];
                    xy[k++] = contourXy[i * 2 + 1];
                }
        }

        xy[k++] = xy[0];
        xy[k++] = xy[1];

        // Remove any duplicate points
        n = 2;
        var px = xy[0];
        var py = xy[1];
        var removed = false;

        for (var i = 2; i < k; i += 2)
            if (xy[i] == px && xy[i + 1] == py)
            {
                removed = true; // it's a duplicate, remove it
            }
            else
            {
                // it does not match the previous point
                px = xy[i];
                py = xy[i + 1];
                if (removed)
                {
                    xy[n] = px;
                    xy[n + 1] = py;
                }

                n += 2;
            }

        if (removed)
        {
            // copy the coordinates to a downsized array
            var newXy = new double[n];
            Array.Copy(xy, newXy, n);
            return newXy;
        }

        return xy;
    }

    /// <summary>
    ///     Indicates whether this region has child regions.
    /// </summary>
    /// <returns>True if the region has children; otherwise, false.</returns>
    public bool HasChildren()
    {
        return _children.Count > 0;
    }

    /// <summary>
    ///     Indicates whether the specified point is inside the region
    /// </summary>
    /// <param name="x">The Cartesian coordinate for the point of interest</param>
    /// <param name="y">The Cartesian coordinate for the point of interest</param>
    /// <returns>True if the point is inside the contour; otherwise, false</returns>
    public bool IsPointInsideRegion(double x, double y)
    {
        var xy = GetXY();
        return IsPointInsideRegion(xy, x, y);
    }

    /// <summary>
    ///     Removes all child (nested) regions from the internal list and
    ///     nullifies any of the child-region parent links.
    /// </summary>
    public void RemoveChildren()
    {
        foreach (var c in _children) c.SetParent(null);

        _children.Clear();
    }

    /// <summary>
    ///     Sets the reference to the contour region that encloses the region
    ///     represented by this class. A null parent reference indicates that the
    ///     region is not enclosed by another.
    /// </summary>
    /// <param name="parent">A valid reference; or a null.</param>
    public void SetParent(ContourRegion? parent)
    {
        _parent = parent;
    }

    public override string ToString()
    {
        var areaString = _absArea > 0.1 ? $"{_area, 12:F3}" : $"{_area:F6}";

        return
            $"{_regionIndex, 4} {areaString}  {(_parent == null ? "root " : "child")} {_children.Count, 3}";
    }

    private static double CalculateAreaContribution(Contour contour)
    {
        var xy = contour.GetXY();
        var x0 = xy[0];
        var y0 = xy[1];
        var n = xy.Length / 2;
        double a = 0;

        for (var i = 1; i < n; i++)
        {
            var x1 = xy[i * 2];
            var y1 = xy[i * 2 + 1];
            a += x0 * y1 - x1 * y0;
            x0 = x1;
            y0 = y1;
        }

        return a;
    }
}