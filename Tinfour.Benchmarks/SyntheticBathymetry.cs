namespace Tinfour.Benchmarks;

using Tinfour.Core.Common;

/// <summary>
///     Generates deterministic, bathymetry-like synthetic survey data for benchmarks:
///     boustrophedon (lawn-mower) sonar track lines over a smooth basin surface, plus an
///     island shoreline polygon constraint with NaN Z (the "drape" scenario used by
///     consumers that pre-interpolate constraint Z from the data surface).
/// </summary>
/// <remarks>
///     Real sonar data is dense along-track and sparse across-track, which is exactly the
///     distribution that punishes an unsorted incremental TIN build — uniform random points
///     understate the walk cost. The domain is a fixed 10 km × 10 km square in projected
///     (metre-like) coordinates; along-track spacing shrinks as the point count grows.
/// </remarks>
public static class SyntheticBathymetry
{
    /// <summary>Domain edge length in metres.</summary>
    public const double DomainSize = 10_000.0;

    /// <summary>
    ///     Generates <paramref name="count" /> survey points along jittered boustrophedon
    ///     track lines, with Z from a smooth synthetic basin. Deterministic for a given
    ///     (count, seed) pair.
    /// </summary>
    public static List<IVertex> GenerateSurveyPoints(int count, int seed = 12345)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        var rnd = new Random(seed);
        var vertices = new List<IVertex>(count);

        // Along-track spacing ~5x denser than the cross-track line spacing.
        var lineCount = Math.Max(2, (int)Math.Sqrt(count / 5.0));
        var pointsPerLine = (count + lineCount - 1) / lineCount;
        var lineSpacing = DomainSize / lineCount;
        var alongSpacing = DomainSize / pointsPerLine;

        var index = 0;
        for (var line = 0; line < lineCount && index < count; line++)
        {
            var y0 = (line + 0.5) * lineSpacing;
            var leftToRight = (line & 1) == 0;
            for (var i = 0; i < pointsPerLine && index < count; i++)
            {
                var t = (i + 0.5) * alongSpacing;
                var x = leftToRight ? t : DomainSize - t;

                // Jitter mimics GPS scatter and keeps points off an exact lattice.
                x += (rnd.NextDouble() - 0.5) * alongSpacing * 0.8;
                var y = y0 + (rnd.NextDouble() - 0.5) * lineSpacing * 0.4;

                vertices.Add(new Vertex(x, y, DepthAt(x, y), index));
                index++;
            }
        }

        return vertices;
    }

    /// <summary>
    ///     Creates an island shoreline polygon constraint: a wavy closed ring centred in
    ///     the domain, counter-clockwise (Tinfour region convention), with NaN Z on every
    ///     vertex so that consumers exercising <c>preInterpolateZ</c> take the
    ///     interpolation path for all constraint vertices.
    /// </summary>
    public static PolygonConstraint CreateShorelineConstraint(int vertexCount = 512)
    {
        return new PolygonConstraint(CreateShorelineRing(vertexCount), definesRegion: true);
    }

    /// <summary>
    ///     Creates the raw counter-clockwise NaN-Z shoreline ring used by
    ///     <see cref="CreateShorelineConstraint" />. Exposed so benchmark iterations can
    ///     build a fresh (stateful) constraint per invocation from one cached ring.
    /// </summary>
    public static List<IVertex> CreateShorelineRing(int vertexCount = 512)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(vertexCount, 3);

        var vertices = new List<IVertex>(vertexCount);
        var cx = DomainSize * 0.5;
        var cy = DomainSize * 0.5;
        var baseRadius = DomainSize * 0.18;

        for (var i = 0; i < vertexCount; i++)
        {
            // Counter-clockwise ring with mild lobes so constraint edges cross many triangles.
            var angle = 2.0 * Math.PI * i / vertexCount;
            var radius = baseRadius * (1.0 + 0.15 * Math.Sin(5.0 * angle));
            var x = cx + radius * Math.Cos(angle);
            var y = cy + radius * Math.Sin(angle);
            vertices.Add(new Vertex(x, y, double.NaN, i));
        }

        return vertices;
    }

    /// <summary>
    ///     Smooth synthetic basin depth (negative down), varied enough that facet
    ///     interpolation does real work.
    /// </summary>
    private static double DepthAt(double x, double y)
    {
        var dx = (x - DomainSize * 0.5) / (DomainSize * 0.35);
        var dy = (y - DomainSize * 0.5) / (DomainSize * 0.35);
        var basin = 18.0 * Math.Exp(-(dx * dx + dy * dy));
        var ripple = 3.0 * Math.Sin(x / 900.0) * Math.Cos(y / 700.0);
        return -(4.0 + basin + ripple);
    }
}
