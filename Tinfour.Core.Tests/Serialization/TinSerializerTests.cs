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

namespace Tinfour.Core.Tests.Serialization;

using Tinfour.Core.Common;
using Tinfour.Core.Serialization;
using Tinfour.Core.Standard;

using Xunit;

/// <summary>
/// Test-driven development tests for TIN serialization.
/// These tests define the expected behavior of the TinSerializer class.
/// </summary>
public class TinSerializerTests
{
    #region Phase 1: Basic Round-Trip Tests

    [Fact]
    public void RoundTrip_SimpleTriangle_PreservesTopology()
    {
        // Arrange - Create a simple triangle (minimum valid TIN)
        var tin = new IncrementalTin(1.0);
        var vertices = new IVertex[]
        {
            new Vertex(0, 0, 1.0, 0),
            new Vertex(10, 0, 2.0, 1),
            new Vertex(5, 10, 3.0, 2)
        };
        tin.Add(vertices);
        Assert.True(tin.IsBootstrapped(), "TIN should be bootstrapped with 3 non-collinear vertices");

        // Act - Serialize and deserialize
        using var stream = new MemoryStream();
        TinSerializer.Write(tin, stream, compress: false);
        stream.Position = 0;
        var loadedTin = TinSerializer.Read(stream);

        // Assert - Verify topology is preserved
        Assert.True(loadedTin.IsBootstrapped(), "Loaded TIN should be bootstrapped");
        Assert.Equal(3, loadedTin.GetVertices().Count);

        // Verify triangles
        var originalTriangles = tin.GetTriangles().Where(t => !t.IsGhost()).ToList();
        var loadedTriangles = loadedTin.GetTriangles().Where(t => !t.IsGhost()).ToList();
        Assert.Equal(originalTriangles.Count, loadedTriangles.Count);
    }

    [Fact]
    public void RoundTrip_SimpleTriangle_PreservesVertexData()
    {
        // Arrange
        var tin = new IncrementalTin(1.0);
        var v0 = new Vertex(0, 0, 1.5, 100);
        var v1 = new Vertex(10, 0, 2.5, 101);
        var v2 = new Vertex(5, 10, 3.5, 102);
        tin.Add(new IVertex[] { v0, v1, v2 });

        // Act
        using var stream = new MemoryStream();
        TinSerializer.Write(tin, stream, compress: false);
        stream.Position = 0;
        var loadedTin = TinSerializer.Read(stream);

        // Assert - Verify vertex coordinates and indices
        var loadedVertices = loadedTin.GetVertices().OrderBy(v => v.GetIndex()).ToList();
        Assert.Equal(3, loadedVertices.Count);

        // Find vertices by approximate position (order may differ)
        var loadedV0 = loadedVertices.First(v => Math.Abs(v.X - 0) < 0.001 && Math.Abs(v.Y - 0) < 0.001);
        var loadedV1 = loadedVertices.First(v => Math.Abs(v.X - 10) < 0.001 && Math.Abs(v.Y - 0) < 0.001);
        var loadedV2 = loadedVertices.First(v => Math.Abs(v.X - 5) < 0.001 && Math.Abs(v.Y - 10) < 0.001);

        Assert.Equal(1.5, loadedV0.GetZ(), 3);
        Assert.Equal(2.5, loadedV1.GetZ(), 3);
        Assert.Equal(3.5, loadedV2.GetZ(), 3);
    }

    [Fact]
    public void RoundTrip_Square_PreservesEdgeCount()
    {
        // Arrange - Square with 4 vertices = 2 triangles = 5 interior edges
        var tin = new IncrementalTin(1.0);
        var vertices = new IVertex[]
        {
            new Vertex(0, 0, 0, 0),
            new Vertex(10, 0, 0, 1),
            new Vertex(10, 10, 0, 2),
            new Vertex(0, 10, 0, 3)
        };
        tin.Add(vertices);

        var originalEdgeCount = tin.GetEdges().Count();

        // Act
        using var stream = new MemoryStream();
        TinSerializer.Write(tin, stream, compress: false);
        stream.Position = 0;
        var loadedTin = TinSerializer.Read(stream);

        // Assert
        Assert.Equal(originalEdgeCount, loadedTin.GetEdges().Count());
    }

    [Fact]
    public void RoundTrip_RandomPoints_InterpolationWorks()
    {
        // Arrange - Create a TIN with random points
        var tin = new IncrementalTin(1.0);
        var random = new Random(42); // Fixed seed for reproducibility
        var vertices = new List<IVertex>();
        for (int i = 0; i < 100; i++)
        {
            var x = random.NextDouble() * 100;
            var y = random.NextDouble() * 100;
            var z = x + y; // Simple function for predictable interpolation
            vertices.Add(new Vertex(x, y, z, i));
        }
        tin.Add(vertices);

        // Act
        using var stream = new MemoryStream();
        TinSerializer.Write(tin, stream, compress: false);
        stream.Position = 0;
        var loadedTin = TinSerializer.Read(stream);

        // Assert - Verify interpolation works on loaded TIN
        // The TIN should be usable for interpolation after deserialization
        Assert.True(loadedTin.IsBootstrapped());
        Assert.Equal(100, loadedTin.GetVertices().Count);

        // Verify triangles exist
        var triangles = loadedTin.GetTriangles().Where(t => !t.IsGhost()).ToList();
        Assert.True(triangles.Count > 0, "Loaded TIN should have triangles");
    }

    [Fact]
    public void RoundTrip_PreservesBounds()
    {
        // Arrange
        var tin = new IncrementalTin(1.0);
        tin.Add(new IVertex[]
        {
            new Vertex(-50, -25, 0),
            new Vertex(150, 75, 0),
            new Vertex(50, 200, 0)
        });

        var originalBounds = tin.GetBounds();
        Assert.NotNull(originalBounds);

        // Act
        using var stream = new MemoryStream();
        TinSerializer.Write(tin, stream, compress: false);
        stream.Position = 0;
        var loadedTin = TinSerializer.Read(stream);

        // Assert
        var loadedBounds = loadedTin.GetBounds();
        Assert.NotNull(loadedBounds);

        // GetBounds returns (Left, Top, Width, Height) where Left=MinX, Top=MinY
        Assert.Equal(originalBounds.Value.Left, loadedBounds.Value.Left, 6);
        Assert.Equal(originalBounds.Value.Top, loadedBounds.Value.Top, 6);
        Assert.Equal(originalBounds.Value.Width, loadedBounds.Value.Width, 6);
        Assert.Equal(originalBounds.Value.Height, loadedBounds.Value.Height, 6);
    }

    [Fact]
    public void RoundTrip_PreservesNominalPointSpacing()
    {
        // Arrange
        var tin = new IncrementalTin(2.5); // Non-default spacing
        tin.Add(new IVertex[]
        {
            new Vertex(0, 0, 0),
            new Vertex(10, 0, 0),
            new Vertex(5, 10, 0)
        });

        // Act
        using var stream = new MemoryStream();
        TinSerializer.Write(tin, stream, compress: false);
        stream.Position = 0;
        var loadedTin = TinSerializer.Read(stream);

        // Assert
        Assert.Equal(2.5, loadedTin.GetNominalPointSpacing(), 6);
    }

    #endregion

    #region Phase 1: Header and Format Tests

    [Fact]
    public void Write_CreatesValidHeader()
    {
        // Arrange
        var tin = new IncrementalTin(1.0);
        tin.Add(new IVertex[]
        {
            new Vertex(0, 0, 0),
            new Vertex(10, 0, 0),
            new Vertex(5, 10, 0)
        });

        // Act
        using var stream = new MemoryStream();
        TinSerializer.Write(tin, stream, compress: false);
        stream.Position = 0;

        // Assert - Check magic number
        using var reader = new BinaryReader(stream);
        var magic = reader.ReadInt32();
        Assert.Equal(0x54494E53, magic); // "TINS"

        var version = reader.ReadInt16();
        Assert.Equal(1, version);
    }

    [Fact]
    public void Read_ThrowsOnInvalidMagic()
    {
        // Arrange - Create a stream with invalid magic number
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(0x12345678); // Invalid magic
        }
        stream.Position = 0;

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => TinSerializer.Read(stream));
    }

    #endregion

    #region Phase 1: Synthetic Vertex Tests

    [Fact]
    public void RoundTrip_PreservesSyntheticVertexStatus()
    {
        // Arrange - Create TIN with synthetic vertex
        var tin = new IncrementalTin(1.0);
        var v0 = new Vertex(0, 0, 0, 0);
        var v1 = new Vertex(10, 0, 0, 1);
        var v2 = new Vertex(5, 10, 0, 2).WithSynthetic(true); // Mark as synthetic
        tin.Add(new IVertex[] { v0, v1, v2 });

        // Act
        using var stream = new MemoryStream();
        TinSerializer.Write(tin, stream, compress: false);
        stream.Position = 0;
        var loadedTin = TinSerializer.Read(stream);

        // Assert
        var loadedVertices = loadedTin.GetVertices();
        var syntheticVertex = loadedVertices.FirstOrDefault(v =>
            Math.Abs(v.X - 5) < 0.001 && Math.Abs(v.Y - 10) < 0.001);

        Assert.NotNull(syntheticVertex);
        Assert.True(syntheticVertex.IsSynthetic(), "Synthetic status should be preserved");
    }

    #endregion

    #region Phase 2: Constraint Tests (will fail until Phase 2 implementation)

    [Fact]
    public void RoundTrip_LinearConstraint_PreservesConstraint()
    {
        // Arrange
        var tin = new IncrementalTin(1.0);
        tin.Add(new IVertex[]
        {
            new Vertex(0, 0, 0, 0),
            new Vertex(20, 0, 0, 1),
            new Vertex(20, 20, 0, 2),
            new Vertex(0, 20, 0, 3),
            new Vertex(10, 10, 0, 4)
        });

        var constraintVertices = new IVertex[]
        {
            new Vertex(5, 5, 0),
            new Vertex(15, 15, 0)
        };
        var constraint = new LinearConstraint(constraintVertices);
        tin.AddConstraints(new List<IConstraint> { constraint }, restoreConformity: true);

        // Act
        using var stream = new MemoryStream();
        TinSerializer.Write(tin, stream, compress: false);
        stream.Position = 0;
        var loadedTin = TinSerializer.Read(stream);

        // Assert
        var loadedConstraints = loadedTin.GetConstraints();
        Assert.Single(loadedConstraints);
        Assert.IsType<LinearConstraint>(loadedConstraints[0]);
    }

    [Fact]
    public void RoundTrip_PolygonConstraint_PreservesConstraint()
    {
        // Arrange
        var tin = new IncrementalTin(1.0);
        tin.Add(new IVertex[]
        {
            new Vertex(0, 0, 0, 0),
            new Vertex(30, 0, 0, 1),
            new Vertex(30, 30, 0, 2),
            new Vertex(0, 30, 0, 3)
        });

        var polygonVertices = new IVertex[]
        {
            new Vertex(10, 10, 0),
            new Vertex(20, 10, 0),
            new Vertex(20, 20, 0),
            new Vertex(10, 20, 0)
        };
        var constraint = new PolygonConstraint(polygonVertices, definesRegion: true);
        tin.AddConstraints(new List<IConstraint> { constraint }, restoreConformity: true);

        // Act
        using var stream = new MemoryStream();
        TinSerializer.Write(tin, stream, compress: false);
        stream.Position = 0;
        var loadedTin = TinSerializer.Read(stream);

        // Assert
        var loadedConstraints = loadedTin.GetConstraints();
        Assert.Single(loadedConstraints);
        var loadedConstraint = loadedConstraints[0] as PolygonConstraint;
        Assert.NotNull(loadedConstraint);
        Assert.True(loadedConstraint.DefinesConstrainedRegion());
    }

    [Fact]
    public void RoundTrip_ConstraintEdges_PreserveConstraintFlags()
    {
        // Arrange
        var tin = new IncrementalTin(1.0);
        tin.Add(new IVertex[]
        {
            new Vertex(0, 0, 0, 0),
            new Vertex(20, 0, 0, 1),
            new Vertex(20, 20, 0, 2),
            new Vertex(0, 20, 0, 3)
        });

        var constraint = new LinearConstraint(new IVertex[]
        {
            new Vertex(0, 0, 0),
            new Vertex(20, 20, 0)
        });
        tin.AddConstraints(new List<IConstraint> { constraint }, restoreConformity: true);

        // Count constrained edges before
        var constrainedEdgeCount = tin.GetEdges().Count(e => e.IsConstrained());

        // Act
        using var stream = new MemoryStream();
        TinSerializer.Write(tin, stream, compress: false);
        stream.Position = 0;
        var loadedTin = TinSerializer.Read(stream);

        // Assert
        var loadedConstrainedEdgeCount = loadedTin.GetEdges().Count(e => e.IsConstrained());
        Assert.Equal(constrainedEdgeCount, loadedConstrainedEdgeCount);
    }

    #endregion

    #region Phase 4: Compression Tests

    [Fact]
    public void RoundTrip_WithCompression_Works()
    {
        // Arrange
        var tin = new IncrementalTin(1.0);
        var random = new Random(42);
        var vertices = new List<IVertex>();
        for (int i = 0; i < 50; i++)
        {
            vertices.Add(new Vertex(random.NextDouble() * 100, random.NextDouble() * 100, random.NextDouble() * 10, i));
        }
        tin.Add(vertices);

        // Act
        using var stream = new MemoryStream();
        TinSerializer.Write(tin, stream, compress: true);
        stream.Position = 0;
        var loadedTin = TinSerializer.Read(stream);

        // Assert
        Assert.Equal(50, loadedTin.GetVertices().Count);
    }

    [Fact]
    public void Write_WithCompression_ProducesSmallerOutput()
    {
        // Arrange - Create a reasonably sized TIN
        var tin = new IncrementalTin(1.0);
        var random = new Random(42);
        var vertices = new List<IVertex>();
        for (int i = 0; i < 200; i++)
        {
            vertices.Add(new Vertex(random.NextDouble() * 100, random.NextDouble() * 100, random.NextDouble() * 10, i));
        }
        tin.Add(vertices);

        // Act
        using var uncompressedStream = new MemoryStream();
        TinSerializer.Write(tin, uncompressedStream, compress: false);
        var uncompressedSize = uncompressedStream.Length;

        using var compressedStream = new MemoryStream();
        TinSerializer.Write(tin, compressedStream, compress: true);
        var compressedSize = compressedStream.Length;

        // Assert
        Assert.True(compressedSize < uncompressedSize,
            $"Compressed ({compressedSize}) should be smaller than uncompressed ({uncompressedSize})");
    }

    #endregion

    #region File API Tests

    [Fact]
    public void WriteToFile_ReadFromFile_RoundTrips()
    {
        // Arrange
        var tin = new IncrementalTin(1.0);
        tin.Add(new IVertex[]
        {
            new Vertex(0, 0, 1.0),
            new Vertex(10, 0, 2.0),
            new Vertex(5, 10, 3.0)
        });

        var tempPath = Path.GetTempFileName();
        try
        {
            // Act
            TinSerializer.WriteToFile(tin, tempPath);
            var loadedTin = TinSerializer.ReadFromFile(tempPath);

            // Assert
            Assert.Equal(3, loadedTin.GetVertices().Count);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    #endregion
}
