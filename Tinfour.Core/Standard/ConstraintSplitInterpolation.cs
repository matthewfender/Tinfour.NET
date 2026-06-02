namespace Tinfour.Core.Standard;

using Tinfour.Core.Common;
using Tinfour.Core.Interpolation;

/// <summary>
///     Computes the Z (depth) value for a synthetic vertex inserted when a constraint
///     edge is split (during Delaunay-conformity restoration or Ruppert refinement).
/// </summary>
/// <remarks>
///     <para>
///         A constraint that defines its own depth profile — e.g. a shoreline at 0 m —
///         must keep that profile: split points are interpolated <b>linearly</b> between the
///         two constraint vertices. Sampling the surrounding surface there would pull deep
///         terrain depths onto the constraint and create spurious vertical steps along it.
///     </para>
///     <para>
///         A constraint created <b>without</b> a depth — e.g. a clipping/region boundary —
///         has its vertices filled from the surface during pre-interpolation and flagged
///         <see cref="Vertex.BitInterpolatedZ"/>. Its split points must <b>drape</b> over the
///         surface so the boundary follows the terrain and does not carve artificial features
///         into the depth surface.
///     </para>
/// </remarks>
internal static class ConstraintSplitInterpolation
{
    /// <summary>
    ///     Returns true if the split point on edge a-b should drape over the surface — i.e.
    ///     at least one endpoint's Z was interpolated from the surface (a no-depth vertex).
    /// </summary>
    /// <remarks>
    ///     No-depth boundary edges are seeded with a NaN vertex at their midpoint (filled
    ///     from the surface and flagged), while their endpoints may be depth-bearing
    ///     intersection points. The "either endpoint" rule therefore drapes the boundary
    ///     sub-edges that touch such a flagged midpoint, while genuine depth-bearing edges
    ///     (e.g. shoreline edges, where neither endpoint is flagged) stay linear.
    /// </remarks>
    public static bool ShouldDrape(IVertex a, IVertex b)
        => (a is Vertex va && va.HasInterpolatedZ()) || (b is Vertex vb && vb.HasInterpolatedZ());

    /// <summary>
    ///     Computes the Z for the midpoint of constraint edge a-b at (mx, my): linear between
    ///     the endpoints for a depth-bearing constraint; surface-draped (with linear fallback
    ///     when the interpolator returns NaN, or no interpolator is available) for a no-depth
    ///     constraint.
    /// </summary>
    public static double ComputeSplitZ(IVertex a, IVertex b, double mx, double my,
        IInterpolatorOverTin? interpolator)
    {
        var linear = (a.GetZ() + b.GetZ()) * 0.5;
        if (interpolator == null || !ShouldDrape(a, b))
            return linear;

        var z = interpolator.Interpolate(mx, my, null);
        return double.IsNaN(z) ? linear : z;
    }
}
