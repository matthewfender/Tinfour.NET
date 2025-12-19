/*
 * Copyright 2025 M. Fender
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
 * 12/2025  M. Fender    Created for TIN serialization support
 *
 * -----------------------------------------------------------------------
 */

namespace Tinfour.Core.Serialization;

using System.IO.Compression;

using Tinfour.Core.Common;
using Tinfour.Core.Edge;
using Tinfour.Core.Standard;

/// <summary>
/// Provides methods for serializing and deserializing IncrementalTin instances
/// to and from binary streams.
/// </summary>
/// <remarks>
/// The binary format preserves complete TIN topology including:
/// - All vertices with their coordinates, indices, and status flags
/// - Complete quad-edge structure with forward/reverse links
/// - Constraint flags and indices on edges
/// - Constraint definitions (polygon and linear)
/// - TIN state (bounds, nominal point spacing, lock flags, etc.)
///
/// This allows refined meshes (e.g., after Ruppert refinement) to be saved
/// and restored without loss of topological information.
/// </remarks>
public static class TinSerializer
{
    /// <summary>
    /// Magic number for TIN files: "TINS" in little-endian.
    /// </summary>
    public const int MagicNumber = 0x54494E53;

    /// <summary>
    /// Current format version.
    /// </summary>
    public const short FormatVersion = 1;

    /// <summary>
    /// Flag indicating the payload is GZip compressed.
    /// </summary>
    public const short FlagCompressed = 0x0001;

    // Vertex kind markers
    private const byte VertexKindNull = 0;
    private const byte VertexKindRegular = 1;
    private const byte VertexKindMergerGroup = 2;

    // Constraint type markers
    private const byte ConstraintTypeLinear = 0;
    private const byte ConstraintTypePolygon = 1;

    /// <summary>
    /// Writes a complete TIN to a binary stream.
    /// </summary>
    /// <param name="tin">The TIN to serialize.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="compress">If true, compress the payload with GZip.</param>
    /// <exception cref="ArgumentNullException">If tin or stream is null.</exception>
    /// <exception cref="InvalidOperationException">If the TIN is not bootstrapped.</exception>
    public static void Write(IIncrementalTin tin, Stream stream, bool compress = true)
    {
        ArgumentNullException.ThrowIfNull(tin);
        ArgumentNullException.ThrowIfNull(stream);

        if (!tin.IsBootstrapped())
        {
            throw new InvalidOperationException("Cannot serialize a TIN that is not bootstrapped.");
        }

        // Cast to IncrementalTin to access internal methods
        if (tin is not IncrementalTin incrementalTin)
        {
            throw new ArgumentException("Only IncrementalTin instances can be serialized.", nameof(tin));
        }

        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        // Write header (uncompressed)
        writer.Write(MagicNumber);
        writer.Write(FormatVersion);
        writer.Write(compress ? FlagCompressed : (short)0);

        // Get the payload stream (compressed or not)
        Stream payloadStream;
        GZipStream? gzipStream = null;

        if (compress)
        {
            gzipStream = new GZipStream(stream, CompressionLevel.Optimal, leaveOpen: true);
            payloadStream = gzipStream;
        }
        else
        {
            payloadStream = stream;
        }

        try
        {
            using var payloadWriter = new BinaryWriter(payloadStream, System.Text.Encoding.UTF8, leaveOpen: true);
            WritePayload(incrementalTin, payloadWriter);
        }
        finally
        {
            gzipStream?.Dispose();
        }
    }

    /// <summary>
    /// Reads a TIN from a binary stream.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>A fully reconstructed IncrementalTin instance.</returns>
    /// <exception cref="ArgumentNullException">If stream is null.</exception>
    /// <exception cref="InvalidDataException">If the stream does not contain valid TIN data.</exception>
    public static IIncrementalTin Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        // Read and validate header
        var magic = reader.ReadInt32();
        if (magic != MagicNumber)
        {
            throw new InvalidDataException(
                $"Invalid TIN file format. Expected magic number 0x{MagicNumber:X8}, got 0x{magic:X8}.");
        }

        var version = reader.ReadInt16();
        if (version != FormatVersion)
        {
            throw new InvalidDataException(
                $"Unsupported TIN format version {version}. Expected version {FormatVersion}.");
        }

        var flags = reader.ReadInt16();
        var isCompressed = (flags & FlagCompressed) != 0;

        // Get the payload stream
        Stream payloadStream;
        GZipStream? gzipStream = null;

        if (isCompressed)
        {
            gzipStream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
            payloadStream = gzipStream;
        }
        else
        {
            payloadStream = stream;
        }

        try
        {
            using var payloadReader = new BinaryReader(payloadStream, System.Text.Encoding.UTF8, leaveOpen: true);
            return ReadPayload(payloadReader);
        }
        finally
        {
            gzipStream?.Dispose();
        }
    }

    /// <summary>
    /// Writes a TIN to a file.
    /// </summary>
    /// <param name="tin">The TIN to serialize.</param>
    /// <param name="path">The destination file path.</param>
    /// <param name="compress">If true, compress the payload with GZip.</param>
    public static void WriteToFile(IIncrementalTin tin, string path, bool compress = true)
    {
        ArgumentNullException.ThrowIfNull(tin);
        ArgumentException.ThrowIfNullOrEmpty(path);

        using var stream = File.Create(path);
        Write(tin, stream, compress);
    }

    /// <summary>
    /// Reads a TIN from a file.
    /// </summary>
    /// <param name="path">The source file path.</param>
    /// <returns>A fully reconstructed IncrementalTin instance.</returns>
    public static IIncrementalTin ReadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    #region Write Implementation

    private static void WritePayload(IncrementalTin tin, BinaryWriter writer)
    {
        var edgePool = tin.GetEdgePoolInternal();
        var state = tin.GetSerializationState();

        // Build vertex table: collect all unique vertices from edges
        var vertexToId = new Dictionary<IVertex, int>(ReferenceEqualityComparer.Instance);
        var vertices = new List<IVertex>();

        foreach (var edge in edgePool.GetAllocatedEdgesInOrder())
        {
            var a = edge.GetA();
            var b = edge.GetB();

            if (!a.IsNullVertex() && !vertexToId.ContainsKey(a))
            {
                vertexToId[a] = vertices.Count;
                vertices.Add(a);
            }

            if (!b.IsNullVertex() && !vertexToId.ContainsKey(b))
            {
                vertexToId[b] = vertices.Count;
                vertices.Add(b);
            }
        }

        // Write TIN state
        WriteTinState(writer, state);

        // Write vertex count and vertices
        writer.Write(vertices.Count);
        foreach (var vertex in vertices)
        {
            WriteVertex(writer, vertex);
        }

        // Write edge count and edges
        var edgeCount = edgePool.GetAllocatedCount();
        writer.Write(edgeCount);
        foreach (var edge in edgePool.GetAllocatedEdgesInOrder())
        {
            WriteEdge(writer, edge, vertexToId);
        }

        // Write constraints
        var constraints = tin.GetConstraintListInternal();
        writer.Write(constraints.Count);
        foreach (var constraint in constraints)
        {
            WriteConstraint(writer, constraint, vertexToId);
        }
    }

    private static void WriteTinState(BinaryWriter writer, TinSerializationState state)
    {
        writer.Write(state.BoundsMinX);
        writer.Write(state.BoundsMaxX);
        writer.Write(state.BoundsMinY);
        writer.Write(state.BoundsMaxY);
        writer.Write(state.NominalPointSpacing);
        writer.Write(state.NSyntheticVertices);
        writer.Write(state.SearchEdgeBaseIndex);
        writer.Write(state.MaxLengthOfQueueInFloodFill);

        // Pack flags into a single byte
        byte flags = 0;
        if (state.IsLocked) flags |= 0x01;
        if (state.LockedDueToConstraints) flags |= 0x02;
        if (state.IsConformant) flags |= 0x04;
        writer.Write(flags);

        // Reserved bytes for future use (align to nice boundary)
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
    }

    private static void WriteVertex(BinaryWriter writer, IVertex vertex)
    {
        if (vertex is VertexMergerGroup mergerGroup)
        {
            writer.Write(VertexKindMergerGroup);
            writer.Write(mergerGroup.X);
            writer.Write(mergerGroup.Y);
            writer.Write(mergerGroup.GetIndex());

            // Write member vertices
            var members = mergerGroup.GetVertices();
            writer.Write(members.Length);
            foreach (var v in members)
            {
                writer.Write(v.X);
                writer.Write(v.Y);
                writer.Write(v.GetZAsFloat());
                writer.Write(v.GetIndex());
                writer.Write((byte)(v.IsSynthetic() ? 0x01 : 0));
                writer.Write((byte)v.GetAuxiliaryIndex());
            }

            // Write resolution rule
            writer.Write((byte)mergerGroup.GetResolutionRule());
        }
        else if (vertex is Vertex v)
        {
            writer.Write(VertexKindRegular);
            writer.Write(v.X);
            writer.Write(v.Y);
            writer.Write(v.GetZAsFloat());
            writer.Write(v.GetIndex());

            // Pack status flags
            byte status = 0;
            if (v.IsSynthetic()) status |= 0x01;
            if (v.IsConstraintMember()) status |= 0x02;
            writer.Write(status);
            writer.Write((byte)v.GetAuxiliaryIndex());
        }
        else
        {
            throw new InvalidOperationException($"Unsupported vertex type: {vertex.GetType().Name}");
        }
    }

    private static void WriteEdge(BinaryWriter writer, QuadEdge edge, Dictionary<IVertex, int> vertexToId)
    {
        // Base index
        writer.Write(edge.GetBaseIndex());

        // Vertex A and B (as vertex IDs, -1 for null)
        var a = edge.GetA();
        var b = edge.GetB();
        writer.Write(a.IsNullVertex() ? -1 : vertexToId[a]);
        writer.Write(b.IsNullVertex() ? -1 : vertexToId[b]);

        // Forward and reverse links for side-0 (base)
        var f0 = edge.GetForwardInternal();
        var r0 = edge.GetReverseInternal();
        writer.Write(f0?.GetIndex() ?? -1);
        writer.Write(r0?.GetIndex() ?? -1);

        // Forward and reverse links for side-1 (partner/dual)
        var dual = edge.GetDualInternal();
        var f1 = dual.GetForwardInternal();
        var r1 = dual.GetReverseInternal();
        writer.Write(f1?.GetIndex() ?? -1);
        writer.Write(r1?.GetIndex() ?? -1);

        // Partner constraint bits (the packed constraint data)
        writer.Write(edge.GetPartnerConstraintBits());
    }

    private static void WriteConstraint(BinaryWriter writer, IConstraint constraint, Dictionary<IVertex, int> vertexToId)
    {
        if (constraint is PolygonConstraint polygon)
        {
            writer.Write(ConstraintTypePolygon);
            writer.Write(polygon.DefinesConstrainedRegion());
            writer.Write(polygon.IsHole());
            writer.Write(polygon.GetConstraintIndex());

            var linkingEdge = polygon.GetConstraintLinkingEdge();
            writer.Write(linkingEdge?.GetBaseIndex() ?? -1);

            var defaultZ = polygon.GetDefaultZ();
            writer.Write(defaultZ.HasValue);
            if (defaultZ.HasValue) writer.Write(defaultZ.Value);

            // Write vertices
            var vertices = polygon.GetVertices();
            writer.Write(vertices.Count);
            foreach (var v in vertices)
            {
                // Constraint vertices may be new (not in TIN edges), so write them inline
                writer.Write(v.X);
                writer.Write(v.Y);
                writer.Write((float)v.GetZ());
            }
        }
        else if (constraint is LinearConstraint linear)
        {
            writer.Write(ConstraintTypeLinear);
            writer.Write(linear.GetConstraintIndex());

            var linkingEdge = linear.GetConstraintLinkingEdge();
            writer.Write(linkingEdge?.GetBaseIndex() ?? -1);

            var defaultZ = linear.GetDefaultZ();
            writer.Write(defaultZ.HasValue);
            if (defaultZ.HasValue) writer.Write(defaultZ.Value);

            // Write vertices
            var vertices = linear.GetVertices();
            writer.Write(vertices.Count);
            foreach (var v in vertices)
            {
                writer.Write(v.X);
                writer.Write(v.Y);
                writer.Write((float)v.GetZ());
            }
        }
        else
        {
            throw new InvalidOperationException($"Unsupported constraint type: {constraint.GetType().Name}");
        }
    }

    #endregion

    #region Read Implementation

    private static IncrementalTin ReadPayload(BinaryReader reader)
    {
        // Read TIN state
        var state = ReadTinState(reader);

        // Create the TIN with the correct nominal point spacing
        var tin = IncrementalTin.CreateForDeserialization(state.NominalPointSpacing);
        var edgePool = tin.GetEdgePoolInternal();

        // Read vertices
        var vertexCount = reader.ReadInt32();
        var vertices = new IVertex[vertexCount];
        for (var i = 0; i < vertexCount; i++)
        {
            vertices[i] = ReadVertex(reader);
        }

        // Read edges
        var edgeCount = reader.ReadInt32();
        var edges = edgePool.AllocateEdgesForDeserialization(edgeCount);

        // Temporary storage for link indices (to be resolved after all edges are created)
        var edgeLinkData = new (int f0, int r0, int f1, int r1, int constraintBits)[edgeCount];

        for (var i = 0; i < edgeCount; i++)
        {
            var baseIndex = reader.ReadInt32();
            var aId = reader.ReadInt32();
            var bId = reader.ReadInt32();
            var f0Index = reader.ReadInt32();
            var r0Index = reader.ReadInt32();
            var f1Index = reader.ReadInt32();
            var r1Index = reader.ReadInt32();
            var constraintBits = reader.ReadInt32();

            var edge = edges[i];

            // Verify index matches expected
            if (edge.GetBaseIndex() != baseIndex)
            {
                throw new InvalidDataException(
                    $"Edge index mismatch: expected {i * 2}, file has {baseIndex}, pool allocated {edge.GetBaseIndex()}");
            }

            // Set vertices
            var a = aId >= 0 ? vertices[aId] : Vertex.Null;
            var b = bId >= 0 ? vertices[bId] : Vertex.Null;
            edge.SetA(a);
            edge.SetBDirect(b);

            // Store link data for second pass
            edgeLinkData[i] = (f0Index, r0Index, f1Index, r1Index, constraintBits);
        }

        // Second pass: resolve links
        for (var i = 0; i < edgeCount; i++)
        {
            var edge = edges[i];
            var (f0Index, r0Index, f1Index, r1Index, constraintBits) = edgeLinkData[i];

            // Resolve side-0 links
            edge.SetForwardDirect(ResolveEdgeLink(edgePool, f0Index));
            edge.SetReverseDirect(ResolveEdgeLink(edgePool, r0Index));

            // Resolve side-1 (partner/dual) links
            var dual = edge.GetDualInternal();
            dual.SetForwardDirect(ResolveEdgeLink(edgePool, f1Index));
            dual.SetReverseDirect(ResolveEdgeLink(edgePool, r1Index));

            // Set constraint bits on partner
            edge.SetPartnerConstraintBits(constraintBits);
        }

        // Read constraints
        var constraintCount = reader.ReadInt32();
        for (var i = 0; i < constraintCount; i++)
        {
            var constraint = ReadConstraint(reader, edgePool, tin);
            tin.AddConstraintForDeserialization(constraint);
        }

        // Rebuild linear constraint map
        RebuildLinearConstraintMap(tin, edgePool);

        // Restore TIN state
        IQuadEdge? searchEdge = null;
        if (state.SearchEdgeBaseIndex >= 0)
        {
            searchEdge = edgePool.GetEdgeByBaseIndex(state.SearchEdgeBaseIndex);
        }
        searchEdge ??= edgePool.GetStartingEdge();

        tin.RestoreSerializationState(state, searchEdge);

        return tin;
    }

    private static TinSerializationState ReadTinState(BinaryReader reader)
    {
        var state = new TinSerializationState
        {
            BoundsMinX = reader.ReadDouble(),
            BoundsMaxX = reader.ReadDouble(),
            BoundsMinY = reader.ReadDouble(),
            BoundsMaxY = reader.ReadDouble(),
            NominalPointSpacing = reader.ReadDouble(),
            NSyntheticVertices = reader.ReadInt32(),
            SearchEdgeBaseIndex = reader.ReadInt32(),
            MaxLengthOfQueueInFloodFill = reader.ReadInt32()
        };

        var flags = reader.ReadByte();
        state.IsLocked = (flags & 0x01) != 0;
        state.LockedDueToConstraints = (flags & 0x02) != 0;
        state.IsConformant = (flags & 0x04) != 0;

        // Read reserved bytes
        reader.ReadByte();
        reader.ReadByte();
        reader.ReadByte();

        return state;
    }

    private static IVertex ReadVertex(BinaryReader reader)
    {
        var kind = reader.ReadByte();

        switch (kind)
        {
            case VertexKindNull:
                return Vertex.Null;

            case VertexKindRegular:
            {
                var x = reader.ReadDouble();
                var y = reader.ReadDouble();
                var z = reader.ReadSingle();
                var index = reader.ReadInt32();
                var status = reader.ReadByte();
                var auxiliary = reader.ReadByte();
                return Vertex.CreateForDeserialization(x, y, z, index, status, auxiliary);
            }

            case VertexKindMergerGroup:
            {
                var x = reader.ReadDouble();
                var y = reader.ReadDouble();
                var groupIndex = reader.ReadInt32();

                var memberCount = reader.ReadInt32();
                var members = new List<Vertex>(memberCount);
                for (var i = 0; i < memberCount; i++)
                {
                    var mx = reader.ReadDouble();
                    var my = reader.ReadDouble();
                    var mz = reader.ReadSingle();
                    var mIndex = reader.ReadInt32();
                    var mStatus = reader.ReadByte();
                    var mAux = reader.ReadByte();
                    members.Add(Vertex.CreateForDeserialization(mx, my, mz, mIndex, mStatus, mAux));
                }

                var ruleValue = reader.ReadByte();
                var rule = (VertexMergerGroup.ResolutionRule)ruleValue;

                // Create the merger group
                var group = new VertexMergerGroup(members[0]);
                for (var i = 1; i < members.Count; i++)
                {
                    group.AddVertex(members[i]);
                }
                group.SetResolutionRule(rule);

                return group;
            }

            default:
                throw new InvalidDataException($"Unknown vertex kind: {kind}");
        }
    }

    private static QuadEdge? ResolveEdgeLink(EdgePool edgePool, int edgeIndex)
    {
        if (edgeIndex < 0) return null;

        var baseIndex = edgeIndex & ~1; // Clear low bit to get base index
        var side = edgeIndex & 1; // 0 = base, 1 = partner

        var baseEdge = edgePool.GetEdgeByBaseIndex(baseIndex);
        if (baseEdge == null) return null;

        return side == 0 ? baseEdge : baseEdge.GetDualInternal();
    }

    private static IConstraint ReadConstraint(BinaryReader reader, EdgePool edgePool, IncrementalTin tin)
    {
        var constraintType = reader.ReadByte();

        if (constraintType == ConstraintTypePolygon)
        {
            var definesRegion = reader.ReadBoolean();
            var isHole = reader.ReadBoolean();
            var constraintIndex = reader.ReadInt32();
            var linkingEdgeBaseIndex = reader.ReadInt32();

            var hasDefaultZ = reader.ReadBoolean();
            double? defaultZ = hasDefaultZ ? reader.ReadDouble() : null;

            var vertexCount = reader.ReadInt32();
            var vertices = new List<IVertex>(vertexCount);
            for (var i = 0; i < vertexCount; i++)
            {
                var x = reader.ReadDouble();
                var y = reader.ReadDouble();
                var z = reader.ReadSingle();
                vertices.Add(new Vertex(x, y, z));
            }

            var polygon = new PolygonConstraint(vertices, definesRegion, isHole);
            polygon.SetConstraintIndex(tin, constraintIndex);

            if (linkingEdgeBaseIndex >= 0)
            {
                var linkingEdge = edgePool.GetEdgeByBaseIndex(linkingEdgeBaseIndex);
                if (linkingEdge != null)
                {
                    polygon.SetConstraintLinkingEdge(linkingEdge);
                }
            }

            return polygon;
        }
        else if (constraintType == ConstraintTypeLinear)
        {
            var constraintIndex = reader.ReadInt32();
            var linkingEdgeBaseIndex = reader.ReadInt32();

            var hasDefaultZ = reader.ReadBoolean();
            double? defaultZ = hasDefaultZ ? reader.ReadDouble() : null;

            var vertexCount = reader.ReadInt32();
            var vertices = new List<IVertex>(vertexCount);
            for (var i = 0; i < vertexCount; i++)
            {
                var x = reader.ReadDouble();
                var y = reader.ReadDouble();
                var z = reader.ReadSingle();
                vertices.Add(new Vertex(x, y, z));
            }

            var linear = new LinearConstraint(vertices);
            linear.SetConstraintIndex(tin, constraintIndex);

            if (linkingEdgeBaseIndex >= 0)
            {
                var linkingEdge = edgePool.GetEdgeByBaseIndex(linkingEdgeBaseIndex);
                if (linkingEdge != null)
                {
                    linear.SetConstraintLinkingEdge(linkingEdge);
                }
            }

            return linear;
        }
        else
        {
            throw new InvalidDataException($"Unknown constraint type: {constraintType}");
        }
    }

    private static void RebuildLinearConstraintMap(IncrementalTin tin, EdgePool edgePool)
    {
        var constraints = tin.GetConstraintListInternal();

        foreach (var edge in edgePool.GetAllocatedEdgesInOrder())
        {
            if (edge.IsConstraintLineMember())
            {
                var lineIndex = edge.GetConstraintLineIndex();
                if (lineIndex >= 0 && lineIndex < constraints.Count)
                {
                    var constraint = constraints[lineIndex];
                    if (constraint is LinearConstraint)
                    {
                        edgePool.AddLinearConstraintMapping(edge.GetIndex(), constraint);
                    }
                }
            }
        }
    }

    #endregion
}
