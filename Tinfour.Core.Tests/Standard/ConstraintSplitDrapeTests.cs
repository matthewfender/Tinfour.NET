namespace Tinfour.Core.Tests.Standard;

using global::Tinfour.Core.Common;
using global::Tinfour.Core.Interpolation;
using global::Tinfour.Core.Standard;
using Xunit;

/// <summary>
///     Regression tests for constraint-edge split Z handling (feature 551).
///     A depth-bearing constraint (real Z, e.g. a shoreline) must split LINEARLY to preserve
///     its profile; a no-depth constraint (created NaN, filled from the surface during
///     pre-interpolation) must DRAPE its split points over the surface so a region/clip
///     boundary does not carve features into the terrain.
/// </summary>
public class ConstraintSplitDrapeTests
{
    // Tilted-plane "surface": z increases with x. At x, depth = x * Slope.
    private const double Slope = 0.1;

    private static IncrementalTin BuildPlaneTin()
    {
        var tin = new IncrementalTin(1.0);
        var verts = new List<IVertex>();
        for (var x = 0.0; x <= 100.0; x += 10.0)
        for (var y = 0.0; y <= 100.0; y += 10.0)
            verts.Add(new Vertex(x, y, x * Slope, verts.Count));
        tin.Add(verts);
        return tin;
    }

    // ---- Concern #1 + flag survival: NaN constraint vertices appear, get filled from the
    //      surface, get flagged, and survive AddConstraints; real-depth vertices do not. ----
    [Fact]
    public void PreInterpolation_FlagsOnlyTheNoDepthConstraintVertices_AndTheySurvive()
    {
        var tin = BuildPlaneTin();

        // Mixed square constraint (CCW): two real-depth "shoreline" vertices at z = 0,
        // two no-depth "boundary" vertices at z = NaN (to be filled from the surface).
        // Offset off the 10-unit grid so constraint vertices don't coincide with data points.
        var v_shore1 = new Vertex(25, 25, 0.0, 1001);          // real depth (defining)
        var v_bnd1 = new Vertex(85, 25, double.NaN, 1002);     // no depth -> fill ~ 85*0.1 = 8.5
        var v_bnd2 = new Vertex(85, 85, double.NaN, 1003);     // no depth -> fill ~ 8.5
        var v_shore2 = new Vertex(25, 85, 0.0, 1004);          // real depth (defining)
        var constraint = new PolygonConstraint(new List<IVertex> { v_shore1, v_bnd1, v_bnd2, v_shore2 });

        tin.AddConstraints(new List<IConstraint> { constraint }, restoreConformity: true,
            preInterpolateZ: true, InterpolationType.TriangularFacet);

        var boundaryVertex = FindVertex(tin, 85, 25);
        var shorelineVertex = FindVertex(tin, 25, 25);

        Assert.NotNull(boundaryVertex);
        Assert.NotNull(shorelineVertex);

        // No-depth boundary vertex: filled from the surface (~8.5) AND flagged.
        Assert.False(double.IsNaN(boundaryVertex!.GetZ()));
        Assert.Equal(8.5, boundaryVertex.GetZ(), 1); // within 0.1
        Assert.True(boundaryVertex.HasInterpolatedZ(), "no-depth boundary vertex must be flagged");

        // Real-depth shoreline vertex: keeps its defining 0 AND is NOT flagged.
        Assert.Equal(0.0, shorelineVertex!.GetZ(), 6);
        Assert.False(shorelineVertex.HasInterpolatedZ(), "depth-bearing vertex must NOT be flagged");
    }

    // ---- Concern #2: the split-Z decision is correct in both directions. ----
    [Fact]
    public void ComputeSplitZ_DepthBearing_IsLinear_NoDepth_Drapes()
    {
        var tin = BuildPlaneTin();
        var interpolator = InterpolatorFactory.Create(tin, InterpolationType.TriangularFacet);

        // Endpoints at x=40 and x=60, midpoint x=50 -> surface depth ~ 5.
        // Give them arbitrary Z (100/200) so linear (=150) is clearly distinguishable from drape (~5).
        var aDepth = new Vertex(40, 50, 100.0, 1);
        var bDepth = new Vertex(60, 50, 200.0, 2);

        // Depth-bearing (neither flagged) -> linear average, ignores the surface.
        Assert.False(ConstraintSplitInterpolation.ShouldDrape(aDepth, bDepth));
        var zLinear = ConstraintSplitInterpolation.ComputeSplitZ(aDepth, bDepth, 50, 50, interpolator);
        Assert.Equal(150.0, zLinear, 6);

        var aNoDepth = aDepth.WithInterpolatedZ(true);
        var bNoDepth = bDepth.WithInterpolatedZ(true);

        // Both endpoints flagged -> drapes onto the surface (~5), ignores endpoint Z.
        Assert.True(ConstraintSplitInterpolation.ShouldDrape(aNoDepth, bNoDepth));
        var zDrapeBoth = ConstraintSplitInterpolation.ComputeSplitZ(aNoDepth, bNoDepth, 50, 50, interpolator);
        Assert.Equal(5.0, zDrapeBoth, 1);
        Assert.NotEqual(150.0, zDrapeBoth, 0);

        // EITHER endpoint flagged (a no-depth boundary sub-edge whose midpoint is the seeded
        // NaN vertex; the other endpoint is a depth-bearing intersection) -> also drapes.
        Assert.True(ConstraintSplitInterpolation.ShouldDrape(aNoDepth, bDepth));
        Assert.Equal(5.0, ConstraintSplitInterpolation.ComputeSplitZ(aNoDepth, bDepth, 50, 50, interpolator), 1);

        // Null interpolator -> always linear, even when draping is requested.
        Assert.Equal(150.0, ConstraintSplitInterpolation.ComputeSplitZ(aNoDepth, bNoDepth, 50, 50, null), 6);
    }

    // ---- Flag propagation: a split midpoint on a no-depth constraint edge inherits the
    //      flag (via SplitEdge), so deeper refinement keeps draping. ----
    [Fact]
    public void SplitEdge_PropagatesInterpolatedZFlag_OnNoDepthConstraintEdge()
    {
        var tin = BuildPlaneTin();
        var v_shore1 = new Vertex(25, 25, 0.0, 1001);
        var v_bnd1 = new Vertex(85, 25, double.NaN, 1002);
        var v_bnd2 = new Vertex(85, 85, double.NaN, 1003);
        var v_shore2 = new Vertex(25, 85, 0.0, 1004);
        var constraint = new PolygonConstraint(new List<IVertex> { v_shore1, v_bnd1, v_bnd2, v_shore2 });
        tin.AddConstraints(new List<IConstraint> { constraint }, restoreConformity: true,
            preInterpolateZ: true, InterpolationType.TriangularFacet);

        // Find a constrained edge whose BOTH endpoints are flagged (the no-depth boundary,
        // x ≈ 85 on both ends) and split it.
        IQuadEdge? noDepthEdge = null;
        foreach (var e in tin.GetEdges())
        {
            var a = e.GetA();
            var b = e.GetB();
            if (a == null || b == null || a.IsNullVertex() || b.IsNullVertex()) continue;
            if (!e.IsConstrained()) continue;
            if (a is Vertex va && b is Vertex vb && va.HasInterpolatedZ() && vb.HasInterpolatedZ())
            {
                noDepthEdge = e;
                break;
            }
        }

        Assert.NotNull(noDepthEdge);
        var mid = tin.SplitEdge(noDepthEdge!, 0.5, 0.0);
        Assert.NotNull(mid);
        Assert.True(((Vertex)mid!).HasInterpolatedZ(),
            "split midpoint of a no-depth constraint edge must inherit the interpolated-Z flag");
    }

    private static Vertex? FindVertex(IncrementalTin tin, double x, double y)
    {
        foreach (var v in tin.GetVertices())
            if (v is Vertex vx && Math.Abs(vx.X - x) < 1e-6 && Math.Abs(vx.Y - y) < 1e-6)
                return vx;
        return null;
    }
}
