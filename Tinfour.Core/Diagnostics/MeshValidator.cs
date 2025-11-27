/*
 * MeshValidator: lightweight integrity checks for TIN topology
 */

namespace Tinfour.Core.Diagnostics;

using System.Runtime.CompilerServices;
using System.Text;

using Tinfour.Core.Common;

public static class MeshValidator
{
    public static Result Validate(IIncrementalTin tin, int maxIssues = 5)
    {
        var r = new Result();
        if (!tin.IsBootstrapped())
        {
            r.IsValid = false;
            r.Message = "TIN not bootstrapped";
            return r;
        }

        var sb = new StringBuilder();
        var issues = 0;
        var eCount = 0;
        var tri = 0;

        // Edge reciprocity checks
        foreach (var e in tin.GetEdgeIterator())
        {
            eCount++;
            var f = e.GetForward();
            var rvs = e.GetReverse();
            var d = e.GetDual();

            if (f == null)
            {
                if (++issues <= maxIssues) sb.AppendLine($"Edge {e.GetIndex()} has null forward");
                continue;
            }

            if (rvs == null)
            {
                if (++issues <= maxIssues) sb.AppendLine($"Edge {e.GetIndex()} has null reverse");
                continue;
            }

            if (!ReferenceEquals(f.GetReverse(), e))
                if (++issues <= maxIssues)
                    sb.AppendLine($"Forward/Reverse not reciprocal: {e.GetIndex()}");
            if (!ReferenceEquals(rvs.GetForward(), e))
                if (++issues <= maxIssues)
                    sb.AppendLine($"Reverse/Forward not reciprocal: {e.GetIndex()}");
            if (!ReferenceEquals(d.GetDual(), e))
                if (++issues <= maxIssues)
                    sb.AppendLine($"Dual reciprocity failed: {e.GetIndex()}");
        }

        // Triangle 3-cycle enumeration
        var visited = new HashSet<IQuadEdge>(ReferenceEqualityComparer<IQuadEdge>.Default);
        foreach (var e in tin.GetEdgeIterator())
        {
            if (visited.Contains(e)) continue;
            var ef = e.GetForward();
            if (ef == null) continue;
            var er = ef.GetForward();
            if (er == null) continue;
            if (!ReferenceEquals(er.GetForward(), e)) continue;

            visited.Add(e);
            visited.Add(ef);
            visited.Add(er);
            tri++;
        }

        r.IsValid = issues == 0 && tri > 0;
        r.EdgeCount = eCount;
        r.TriangleCount = tri;
        r.Message = sb.ToString();
        return r;
    }

    public sealed class Result
    {
        public int EdgeCount { get; internal set; }

        public bool IsValid { get; internal set; }

        public string Message { get; internal set; } = string.Empty;

        public int TriangleCount { get; internal set; }

        public override string ToString()
        {
            return $"Valid={IsValid}, E={EdgeCount}, T={TriangleCount}: {Message}";
        }
    }

    private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public static readonly ReferenceEqualityComparer<T> Default = new();

        public bool Equals(T? x, T? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(T obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}