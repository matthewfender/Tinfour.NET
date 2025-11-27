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
 * 08/2025  M. Fender     Created - Constraint processing diagnostics
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Diagnostics;

using System.Diagnostics;
using System.Text;

using Tinfour.Core.Common;
using Tinfour.Core.Edge;
using Tinfour.Core.Standard;

/// <summary>
///     Diagnostic utility for tracking and analyzing the constraint addition process.
/// </summary>
/// <remarks>
///     This class provides detailed diagnostics of the constraint insertion process, step by step,
///     to help identify and resolve null reference issues.
/// </remarks>
public static class ConstraintAdditionProcessDiagnostics
{
    private static readonly List<string> _log = new();

    private static int _indentLevel;

    /// <summary>
    ///     Returns whether diagnostics are currently enabled.
    /// </summary>
    public static bool IsEnabled { get; private set; }

    /// <summary>
    ///     Adds a separator line to the log for better readability.
    /// </summary>
    public static void AddSeparator()
    {
        if (!IsEnabled) return;
        Log("------------------------------------------------");
    }

    /// <summary>
    ///     Clears the diagnostic log.
    /// </summary>
    public static void ClearLog()
    {
        _log.Clear();
        _indentLevel = 0;
        Log("Log cleared");
    }

    /// <summary>
    ///     Disables diagnostics collection.
    /// </summary>
    public static void DisableDiagnostics()
    {
        if (IsEnabled)
        {
            Log("Diagnostics disabled");
            IsEnabled = false;
        }
    }

    /// <summary>
    ///     Enables diagnostics collection.
    /// </summary>
    public static void EnableDiagnostics()
    {
        IsEnabled = true;
        _log.Clear();
        _indentLevel = 0;
        Log("Diagnostics enabled");
    }

    /// <summary>
    ///     Logs entry into a method with its parameters.
    /// </summary>
    /// <param name="methodName">The name of the method being entered</param>
    /// <param name="parameters">Parameters passed to the method</param>
    public static void EnterMethod(string methodName, params string[] parameters)
    {
        if (!IsEnabled) return;

        var sb = new StringBuilder($"ENTER: {methodName}");
        if (parameters != null && parameters.Length > 0)
        {
            sb.Append(" (");
            sb.Append(string.Join(", ", parameters));
            sb.Append(')');
        }

        Log(sb.ToString());
        _indentLevel++;
    }

    /// <summary>
    ///     Logs exit from a method with optional return value.
    /// </summary>
    /// <param name="methodName">The name of the method being exited</param>
    /// <param name="returnValue">Optional return value</param>
    public static void ExitMethod(string methodName, string returnValue = "")
    {
        if (!IsEnabled) return;

        _indentLevel--;
        if (string.IsNullOrEmpty(returnValue)) Log($"EXIT: {methodName}");
        else Log($"EXIT: {methodName} => {returnValue}");
    }

    /// <summary>
    ///     Records the constraint addition process flow hierarchy.
    ///     This serves as documentation of the call sequence for constraint insertion.
    /// </summary>
    public static string GetConstraintProcessFlow()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Constraint Addition Process Flow:");
        sb.AppendLine("=================================");
        sb.AppendLine();
        sb.AppendLine("1. IncrementalTin.AddConstraints(IList<IConstraint> constraints, bool restoreConformity)");
        sb.AppendLine("   - Entry point for constraint addition");
        sb.AppendLine("   - Assigns unique constraint indices");
        sb.AppendLine("   - For each constraint in the input list:");
        sb.AppendLine();
        sb.AppendLine("     1.1. ConstraintProcessor.ProcessConstraint(constraint, edgesForConstraint, searchEdge)");
        sb.AppendLine("          - Main constraint processing method");
        sb.AppendLine("          - Processes each segment in the constraint");
        sb.AppendLine();
        sb.AppendLine("          1.1.1. StochasticLawsonsWalk.FindAnEdgeFromEnclosingTriangle(searchEdge, x0, y0)");
        sb.AppendLine("                - Locates triangle containing first vertex of constraint segment");
        sb.AppendLine("                - May use TestAndTransfer to walk through triangles");
        sb.AppendLine("                - May use FindAssociatedPerimeterEdge when point is outside TIN");
        sb.AppendLine();
        sb.AppendLine("          1.1.2. For each segment in constraint:");
        sb.AppendLine();
        sb.AppendLine(
            "                1.1.2.1. ConstraintProcessor.CheckForDirectEdge(e0, v0, v1, constraint, edgesForConstraint)");
        sb.AppendLine("                         - Checks if edge already exists between vertices");
        sb.AppendLine("                         - If found, marks as constrained and continues to next segment");
        sb.AppendLine();
        sb.AppendLine(
            "                1.1.2.2. If not found, ConstraintProcessor.ProcessConstraintSegmentWithIntersection(...)");
        sb.AppendLine("                         - Performs cavity digging to create constraint edge");
        sb.AppendLine(
            "                         - May call FindNearestVertex, ProcessDirectConnection, or CreateDirectConstraintEdge");
        sb.AppendLine(
            "                         - Calls FillCavity to triangulate holes created by constraint insertion");
        sb.AppendLine();
        sb.AppendLine("     1.2. If constraint defines region, ConstraintProcessor.FloodFillConstrainedRegion(...)");
        sb.AppendLine("          - Marks edges in constraint's interior");
        sb.AppendLine();
        sb.AppendLine("     1.3. If restoreConformity is true, iterates through edges to restore Delaunay property");
        sb.AppendLine("          - For each edge, calls ConstraintProcessor.RestoreConformity(edge, 0)");
        sb.AppendLine("          - May recursively subdivide constrained edges");
        sb.AppendLine();
        sb.AppendLine("2. Returns from IncrementalTin.AddConstraints");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    ///     Returns the full diagnostic log.
    /// </summary>
    public static string[] GetFullLog()
    {
        return _log.ToArray();
    }

    /// <summary>
    ///     Adds instrumentation to a ConstraintProcessor instance.
    /// </summary>
    public static void InstrumentConstraintProcessor(ConstraintProcessor processor)
    {
        // This method would add instrumentation to processor methods if it were possible
        // without modifying the ConstraintProcessor class itself
        Log("ConstraintProcessor instrumentation not possible without direct class modification.");
    }

    /// <summary>
    ///     Logs a general diagnostic message.
    /// </summary>
    /// <param name="message">The message to log</param>
    public static void Log(string message)
    {
        if (!IsEnabled) return;

        var indentation = new string(' ', _indentLevel * 2);
        _log.Add($"{indentation}{message}");
        Debug.WriteLine($"[CONSTRAINT DIAG] {indentation}{message}");
    }

    /// <summary>
    ///     Logs details about a constraint.
    /// </summary>
    /// <param name="constraint">The constraint to inspect</param>
    /// <param name="label">Optional label for the constraint</param>
    public static void LogConstraint(IConstraint constraint, string label)
    {
        if (!IsEnabled) return;

        if (constraint == null)
        {
            Log($"{label} Constraint: NULL");
            return;
        }

        var vertices = constraint.GetVertices();
        Log(
            $"{label} Constraint: Index={constraint.GetConstraintIndex()}, " + $"Type={constraint.GetType().Name}, "
                                                                             + $"Vertices={vertices.Count}, "
                                                                             + $"IsPolygon={constraint.IsPolygon()}, "
                                                                             + $"DefinesRegion={constraint.DefinesConstrainedRegion()}");

        // Log first few vertices if available
        if (vertices.Count > 0)
        {
            var vertsToShow = Math.Min(3, vertices.Count);
            for (var i = 0; i < vertsToShow; i++)
            {
                var v = vertices[i];
                Log($"   Vertex[{i}]: {VertexToString(v)}");
            }

            if (vertices.Count > vertsToShow) Log($"   ... and {vertices.Count - vertsToShow} more vertices");
        }
    }

    /// <summary>
    ///     Logs details about a quad edge.
    /// </summary>
    /// <param name="edge">The edge to inspect</param>
    /// <param name="label">Optional label for the edge</param>
    public static void LogEdge(IQuadEdge edge, string label)
    {
        if (!IsEnabled) return;

        if (edge == null)
        {
            Log($"{label} Edge: NULL");
            return;
        }

        var a = edge.GetA();
        var b = edge.GetB();

        Log(
            $"{label} Edge: Index={edge.GetIndex()}, " + $"A={VertexToString(a)}, " + $"B={VertexToString(b)}, "
            + $"IsConstrained={(edge is QuadEdge qe ? qe.IsConstrained().ToString() : "Unknown")}");

        // Check navigation edges
        var forward = edge.GetForward();
        var reverse = edge.GetReverse();
        var dual = edge.GetDual();

        Log(
            $"   Navigation: Forward={forward?.GetIndex() ?? -1}, " + $"Reverse={reverse?.GetIndex() ?? -1}, "
                                                                    + $"Dual={dual?.GetIndex() ?? -1}");
    }

    /// <summary>
    ///     Logs details about a vertex.
    /// </summary>
    /// <param name="vertex">The vertex to inspect</param>
    /// <param name="label">Optional label for the vertex</param>
    public static void LogVertex(Vertex vertex, string label)
    {
        if (!IsEnabled) return;

        Log($"{label} Vertex: {VertexToString(vertex)}");
    }

    /// <summary>
    ///     Saves the diagnostic log to a file.
    /// </summary>
    /// <param name="filePath">Path to save the log</param>
    public static void SaveLogToFile(string filePath)
    {
        if (_log.Count == 0) return;

        try
        {
            File.WriteAllLines(filePath, _log);
            Debug.WriteLine($"[CONSTRAINT DIAG] Log saved to {filePath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CONSTRAINT DIAG] Failed to save log: {ex.Message}");
        }
    }

    /// <summary>
    ///     Returns a diagnostic-friendly string representation of a vertex.
    /// </summary>
    public static string VertexToString(IVertex vertex)
    {
        if (vertex.IsNullVertex()) return "NullVertex";

        return $"({vertex.X:F2}, {vertex.Y:F2}, Idx={vertex.GetIndex()})";
    }
}