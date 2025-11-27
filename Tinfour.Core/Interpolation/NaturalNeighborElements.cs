/*
 * Copyright 2022 Gary W. Lucas.
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
 * 08/2022  G. Lucas     Created
 * 09/2025  M. Fender    Ported to C#
 *
 * Notes:
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Interpolation;

using Tinfour.Core.Common;

/// <summary>
///     Provides a simple container for the component elements computed
///     during a natural neighbor interpolation. This class is intended to
///     support research and experimentation. This class is not used as part
///     of the internal operations in the NaturalNeighborInterpolator
///     implementation.
/// </summary>
/// <remarks>
///     <para>
///         <strong>Interpreting the results</strong>
///     </para>
///     <para>
///         If the query is successful, the result type element will be set to
///         SUCCESS. In that case, the interpolated value can be computed as shown
///         in the following example code. This logic is similar to what is
///         used internally by the interpolator.
///     </para>
///     <para>
///         <code>
///   NaturalNeighborInterpolator nni;  //  defined by application code
///   NaturalNeighborElements result = nni.GetNaturalNeighborElements(x, y);
///   if(result.GetResultType() == ResultType.SUCCESS){
///      // the weight and neighbor arrays will be of the same length
///      double[] weight   = result.GetSibsonCoordinates();
///      IVertex[] neighbor = result.GetNaturalNeighbors();
///      double zSum = 0;
///      for(int i=0; i&lt;weight.Length; i++){
///         zSum += weight[i]*neighbor[i].GetZ();
///      }
///      // zSum is the interpolated value
///   }
/// </code>
///     </para>
/// </remarks>
public class NaturalNeighborElements
{
    /// <summary>
    ///     The area of the polygon that would be constructed if the query coordinates
    ///     were integrated into Delaunay Triangulation or Voronoi Diagram.
    /// </summary>
    private readonly double _areaOfEmbeddedPolygon;

    /// <summary>
    ///     The Sibson coordinates (vertex weights) for the natural neighbors.
    ///     If defined, the sum of the lambdas will be 1.
    /// </summary>
    private readonly double[] _lambda;

    /// <summary>
    ///     The natural neighbors associated with the query position.
    /// </summary>
    private readonly IVertex[] _neighbors;

    /// <summary>
    ///     The result for the interpolation that produced this instance.
    /// </summary>
    private readonly ResultType _resultType;

    /// <summary>
    ///     Constructs a result indicating that the query point was either
    ///     exterior to, or on the boundary of, the Delaunay triangulation.
    /// </summary>
    /// <param name="x">The Cartesian coordinate of the query</param>
    /// <param name="y">The Cartesian coordinate of the query</param>
    public NaturalNeighborElements(double x, double y)
    {
        _resultType = ResultType.Exterior;
        X = x;
        Y = y;
        _areaOfEmbeddedPolygon = 0;
        _lambda = Array.Empty<double>();
        _neighbors = Array.Empty<IVertex>();
    }

    /// <summary>
    ///     Constructs a result indicating a successful query. The specified
    ///     coordinates were located in the interior of the Delaunay triangulation.
    /// </summary>
    /// <param name="x">The Cartesian coordinate of the query</param>
    /// <param name="y">The Cartesian coordinate of the query</param>
    /// <param name="lambda">The Sibson coordinates (weights) for the natural neighbors</param>
    /// <param name="neighbors">The natural neighbors</param>
    /// <param name="areaOfEmbeddedPolygon">
    ///     The area of the polygon that would be formed
    ///     if a point with the specified coordinates were inserted into the structure.
    /// </param>
    public NaturalNeighborElements(
        double x,
        double y,
        double[] lambda,
        IVertex[] neighbors,
        double areaOfEmbeddedPolygon)
    {
        _resultType = ResultType.Success;
        X = x;
        Y = y;
        _areaOfEmbeddedPolygon = areaOfEmbeddedPolygon;
        _lambda = lambda;
        _neighbors = neighbors;
    }

    /// <summary>
    ///     Constructs a result indicating that the query was co-located with
    ///     a vertex in the Delaunay triangulation.
    /// </summary>
    /// <param name="x">The Cartesian coordinate of the query</param>
    /// <param name="y">The Cartesian coordinate of the query</param>
    /// <param name="neighbor">The vertex co-located with the query coordinates</param>
    public NaturalNeighborElements(double x, double y, IVertex neighbor)
    {
        _resultType = ResultType.Colocation;
        X = x;
        Y = y;
        _areaOfEmbeddedPolygon = 0;
        _lambda = new[] { 1.0 };
        _neighbors = new[] { neighbor };
    }

    /// <summary>
    ///     Indicates the kind of results that are stored in this instance.
    /// </summary>
    public enum ResultType
    {
        /// <summary>
        ///     Indicates that the interpolation was successful and all member elements
        ///     are populated
        /// </summary>
        Success,

        /// <summary>
        ///     Indicates that the query point was co-located with one of
        ///     the defining vertices in the data set. Although this result represents
        ///     a successful query, there is no meaningful assignments to the set
        ///     of natural neighbors. Instead, the co-located vertex is stored in
        ///     the result. The array of natural neighbors
        ///     will contain exactly one element and the corresponding array of weights
        ///     will include one element with a value of 1.0
        /// </summary>
        Colocation,

        /// <summary>
        ///     Indicates that the query point was located on or outside the
        ///     boundary of the underlying Delaunay triangulation. The arrays of
        ///     natural neighbors and weights will be dimensioned to a size of zero.
        ///     This behavior may be subject to change in the future in order to
        ///     support extrapolation.
        /// </summary>
        Exterior
    }

    /// <summary>
    ///     The Cartesian x coordinate for the query that produced this instance.
    /// </summary>
    public double X { get; }

    /// <summary>
    ///     The Cartesian y coordinate for the query that produced this instance.
    /// </summary>
    public double Y { get; }

    /// <summary>
    ///     Gets the area of the embedded polygon that was calculated when the
    ///     natural neighbor elements were constructed.
    /// </summary>
    /// <returns>A valid floating-point number.</returns>
    public double GetAreaOfEmbeddedPolygon()
    {
        return _areaOfEmbeddedPolygon;
    }

    /// <summary>
    ///     Gets the area of the containing envelope from which natural
    ///     neighbor coordinates were derived. This value is the overall
    ///     area of the polygon defined by the set of natural neighbors.
    /// </summary>
    /// <returns>A positive, finite floating-point value.</returns>
    public double GetAreaOfEnvelope()
    {
        double areaSum = 0;
        if (_neighbors.Length > 2)
        {
            var a = _neighbors[^1];
            foreach (var b in _neighbors)
            {
                var aX = a.X - X;
                var aY = a.Y - Y;
                var bX = b.X - X;
                var bY = b.Y - Y;
                areaSum += aX * bY - aY * bX;
                a = b;
            }
        }

        return Math.Abs(areaSum / 2.0);
    }

    /// <summary>
    ///     Gets a count for the number of elements (neighbors, Sibson coordinates).
    /// </summary>
    /// <returns>A positive integer, zero if undefined.</returns>
    public int GetElementCount()
    {
        return _neighbors.Length;
    }

    /// <summary>
    ///     Gets the set of natural neighbors that were identified for the
    ///     interpolation that produced these results.
    ///     For performance reasons, this method returns a direct reference to
    ///     the member elements of this class, not a safe-copy of the array.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         If the query point was not inside the polygon,
    ///         this method will return an empty, zero-sized array. If the point is on the
    ///         perimeter of the polygon, this method will also return an empty array.
    ///     </para>
    ///     <para>
    ///         If the query point was co-located or nearly co-located with a
    ///         vertex in the underlying Delaunay triangulation, this method will return
    ///         an array of size 1.
    ///     </para>
    /// </remarks>
    /// <returns>
    ///     A valid array, potentially of length zero if interpolation
    ///     was unsuccessful.
    /// </returns>
    public IVertex[] GetNaturalNeighbors()
    {
        return _neighbors;
    }

    /// <summary>
    ///     Gets the type of result that produced this instance.
    /// </summary>
    /// <returns>A valid enumeration instance.</returns>
    public ResultType GetResultType()
    {
        return _resultType;
    }

    /// <summary>
    ///     Gets the weights that were computed for the neighboring vertices
    ///     during the interpolation operation that produced these results.
    ///     For performance reasons, this method returns a direct reference to
    ///     the member elements of this class, not a safe-copy of the array.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         If the query point was not inside the polygon,
    ///         this method will return an empty, zero-sized array. If the point is on the
    ///         perimeter of the polygon, this method will also return an empty array.
    ///     </para>
    ///     <para>
    ///         If the query point was co-located or nearly co-located with a
    ///         vertex in the underlying Delaunay triangulation, this method will return
    ///         an array of size 1.
    ///     </para>
    /// </remarks>
    /// <returns>
    ///     A valid array, potentially of length zero if interpolation
    ///     was unsuccessful.
    /// </returns>
    public double[] GetSibsonCoordinates()
    {
        return _lambda;
    }

    /// <summary>
    ///     Gets the Cartesian x coordinate that was specified for the interpolation.
    /// </summary>
    /// <returns>A floating-point value.</returns>
    public double GetX()
    {
        return X;
    }

    /// <summary>
    ///     Gets the Cartesian y coordinate that was specified for the interpolation.
    /// </summary>
    /// <returns>A floating-point value.</returns>
    public double GetY()
    {
        return Y;
    }
}