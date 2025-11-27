/*
 * Copyright 2014 Gary W. Lucas.
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
 * 08/2014  G. Lucas     Created
 * 08/2015  G. Lucas     Migrated to current package
 * 08/2025 M.Fender     Ported to C#
 *
 * Notes:
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Interpolation;

using Tinfour.Core.Common;

/// <summary>
///     Defines an interface for interpolating data over a Triangulated
///     Irregular Network implementation.
/// </summary>
/// <remarks>
///     <para>
///         <strong>Thread Safety</strong>
///     </para>
///     <para>
///         Interpolator instances are NOT thread-safe. Each instance maintains internal
///         state (navigator position cache) that is modified during interpolation.
///         However, multiple interpolator instances can safely share the same TIN
///         provided the TIN is locked and not modified.
///     </para>
///     <para>
///         For parallel interpolation, create one interpolator per thread or use
///         <see cref="TinRasterizer"/> which handles this automatically.
///     </para>
/// </remarks>
public interface IInterpolatorOverTin : IProcessUsingTin
{
    /// <summary>
    ///     If true, interpolation is only performed within constrained regions.
    /// </summary>
    bool ConstrainedRegionsOnly { get; }

    /// <summary>
    ///     Gets or sets the maximum distance from a data point at which interpolation
    ///     will be performed. If the query point is further than this distance from the
    ///     nearest vertex of the enclosing triangle, NaN is returned.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A value of null (default) disables distance checking, allowing interpolation
    ///         at any distance from data points within the TIN.
    ///     </para>
    ///     <para>
    ///         The distance check is performed against the nearest vertex of the enclosing
    ///         triangle. If the nearest vertex is further than MaxInterpolationDistance
    ///         from the query point, NaN is returned.
    ///     </para>
    /// </remarks>
    double? MaxInterpolationDistance { get; set; }

    /// <summary>
    ///     Gets a string describing the interpolation method
    ///     that can be used for labeling graphs and printouts.
    ///     Because this string may be used as a column header in a table,
    ///     its length should be kept short.
    /// </summary>
    /// <returns>A valid string</returns>
    string GetMethod();

    /// <summary>
    ///     Computes the surface normal at the most recent interpolation point,
    ///     returning an array of three values giving the unit surface
    ///     normal as x, y, and z coordinates. If the recent interpolation was
    ///     unsuccessful (returned a Double.NaN), the results of this method
    ///     call are undefined. If the computation of surface normals is not
    ///     supported, the class may throw an UnsupportedOperationException.
    /// </summary>
    /// <returns>
    ///     If defined and successful, a valid array of dimension 3 giving
    ///     the x, y, and z components of the unit normal, respectively; otherwise,
    ///     a zero-sized array.
    /// </returns>
    double[] GetSurfaceNormal();

    /// <summary>
    ///     Perform interpolation using the specified valuator.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <strong>Important Synchronization Issue</strong>
    ///     </para>
    ///     <para>
    ///         To improve performance, classes that implement this interface
    ///         frequently maintain state data about the TIN that can be reused
    ///         for query to query. They also avoid run-time overhead by not
    ///         implementing any kind of synchronization or concurrent-modification
    ///         testing provided by collection classes. If an application modifies
    ///         the TIN, instances of this class will not be aware of the change.
    ///         In such cases, interpolation methods may fail by either throwing an
    ///         exception or, worse, returning an incorrect value. The onus is on the
    ///         calling application to manage the use of this class and to ensure that
    ///         no modifications are made to the TIN between interpolation operations.
    ///         If the TIN is modified, the internal state data for this class must
    ///         be reset using a call to ResetForChangeToTin() defined in the
    ///         IProcessUsingTin interface.
    ///     </para>
    /// </remarks>
    /// <param name="x">The x coordinate for the interpolation point</param>
    /// <param name="y">The y coordinate for the interpolation point</param>
    /// <param name="valuator">
    ///     A valid valuator for interpreting the z value of each
    ///     vertex or a null value to use the default.
    /// </param>
    /// <returns>
    ///     If the interpolation is successful, a valid floating point
    ///     value; otherwise, a NaN.
    /// </returns>
    double Interpolate(double x, double y, IVertexValuator? valuator);

    /// <summary>
    ///     Indicates whether the interpolation class supports the computation
    ///     of surface normals through the GetSurfaceNormal() method.
    /// </summary>
    /// <returns>
    ///     True if the class implements the ability to compute
    ///     surface normals; otherwise, false.
    /// </returns>
    bool IsSurfaceNormalSupported();
}