/*
 * Copyright 2023 G.W. Lucas
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

namespace Tinfour.Core.Tests.Common;

using Tinfour.Core.Common;

using Xunit;

public class VertexTests
{
    [Fact]
    public void Constructor_ShouldSetCoordinates()
    {
        // Arrange & Act
        var vertex = new Vertex(1.0, 2.0, 3.0);

        // Assert
        Assert.Equal(1.0, vertex.GetX());
        Assert.Equal(2.0, vertex.GetY());
        Assert.Equal(3.0, vertex.GetZ());
    }

    [Fact]
    public void Constructor_WithIndex_ShouldSetCoordinatesAndIndex()
    {
        // Arrange & Act
        var vertex = new Vertex(1.0, 2.0, 3.0, 42);

        // Assert
        Assert.Equal(1.0, vertex.GetX());
        Assert.Equal(2.0, vertex.GetY());
        Assert.Equal(3.0, vertex.GetZ());
        Assert.Equal(42, vertex.GetIndex());
    }

    [Fact]
    public void GetDistance_BetweenVertices_ShouldCalculateCorrectly()
    {
        // Arrange
        var vertex1 = new Vertex(0, 0, 0);
        var vertex2 = new Vertex(3, 4, 0);

        // Act
        var distance = vertex1.GetDistance(vertex2);

        // Assert
        Assert.Equal(5, distance);
    }

    [Fact]
    public void GetDistance_ToPoint_ShouldCalculateCorrectly()
    {
        // Arrange
        var vertex = new Vertex(0, 0, 0);

        // Act
        var distance = vertex.GetDistance(3, 4);

        // Assert
        Assert.Equal(5, distance);
    }

    [Fact]
    public void GetDistanceSq_ShouldCalculateCorrectly()
    {
        // Arrange
        var vertex = new Vertex(0, 0, 0);

        // Act
        var distanceSq = vertex.GetDistanceSq(3, 4);

        // Assert
        Assert.Equal(25, distanceSq);
    }

    [Fact]
    public void GetLabel_ForNormalVertex_ShouldReturnIndex()
    {
        // Arrange
        var vertex = new Vertex(1.0, 2.0, 3.0, 42);

        // Act
        var label = vertex.GetLabel();

        // Assert
        Assert.Equal("42", label);
    }

    [Fact]
    public void GetLabel_ForSyntheticVertex_ShouldReturnPrefixedIndex()
    {
        // Arrange
        var vertex = new Vertex(1.0, 2.0, 3.0, 42).WithSynthetic(true);

        // Act
        var label = vertex.GetLabel();

        // Assert
        Assert.Equal("S42", label);
    }

    [Fact]
    public void Immutability_OperationsShouldReturnNewInstances()
    {
        // Arrange
        var original = new Vertex(1.0, 2.0, 3.0, 42);

        // Act
        var withNewIndex = original.WithIndex(99);
        var withSynthetic = original.WithSynthetic(true);

        // Assert
        Assert.Equal(42, original.GetIndex());
        Assert.Equal(99, withNewIndex.GetIndex());
        Assert.False(original.IsSynthetic());
        Assert.True(withSynthetic.IsSynthetic());
    }

    [Fact]
    public void IsNull_WithNaNZ_ShouldReturnTrue()
    {
        // Arrange
        var vertex = new Vertex(1.0, 2.0, double.NaN);

        // Act & Assert
        Assert.True(vertex.IsNull());
    }

    [Fact]
    public void IsNull_WithValidZ_ShouldReturnFalse()
    {
        // Arrange
        var vertex = new Vertex(1.0, 2.0, 3.0);

        // Act & Assert
        Assert.False(vertex.IsNull());
    }

    [Fact]
    public void WithAuxiliaryIndex_ShouldSetValidIndex()
    {
        // Arrange
        var vertex = new Vertex(1.0, 2.0, 3.0);

        // Act
        var newVertex = vertex.WithAuxiliaryIndex(42);

        // Assert
        Assert.Equal(42, newVertex.GetAuxiliaryIndex());
    }

    [Fact]
    public void WithAuxiliaryIndex_WithInvalidIndex_ShouldThrowException()
    {
        // Arrange
        var vertex = new Vertex(1.0, 2.0, 3.0);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => vertex.WithAuxiliaryIndex(256));
    }

    [Fact]
    public void WithConstraintMember_ShouldToggleConstraintFlag()
    {
        // Arrange
        var vertex = new Vertex(1.0, 2.0, 3.0);

        // Act
        var constraintVertex = vertex.WithConstraintMember(true);
        var nonConstraintVertex = constraintVertex.WithConstraintMember(false);

        // Assert
        Assert.True(constraintVertex.IsConstraintMember());
        Assert.False(nonConstraintVertex.IsConstraintMember());
    }

    [Fact]
    public void WithIndex_ShouldCreateNewVertexWithUpdatedIndex()
    {
        // Arrange
        var originalVertex = new Vertex(1.0, 2.0, 3.0, 42);

        // Act
        var newVertex = originalVertex.WithIndex(99);

        // Assert
        Assert.Equal(99, newVertex.GetIndex());
        Assert.Equal(1.0, newVertex.GetX());
        Assert.Equal(2.0, newVertex.GetY());
        Assert.Equal(3.0, newVertex.GetZ());
    }

    [Fact]
    public void WithStatus_ShouldSetAllStatusBits()
    {
        // Arrange
        var vertex = new Vertex(1.0, 2.0, 3.0);
        var status = Vertex.BitSynthetic | Vertex.BitConstraint;

        // Act
        var newVertex = vertex.WithStatus(status);

        // Assert
        Assert.Equal(status, newVertex.GetStatus());
        Assert.True(newVertex.IsSynthetic());
        Assert.True(newVertex.IsConstraintMember());
        Assert.False(newVertex.IsWithheld());
    }

    [Fact]
    public void WithSynthetic_ShouldToggleSyntheticFlag()
    {
        // Arrange
        var vertex = new Vertex(1.0, 2.0, 3.0);

        // Act
        var syntheticVertex = vertex.WithSynthetic(true);
        var nonSyntheticVertex = syntheticVertex.WithSynthetic(false);

        // Assert
        Assert.True(syntheticVertex.IsSynthetic());
        Assert.False(nonSyntheticVertex.IsSynthetic());
    }

    [Fact]
    public void WithWithheld_ShouldToggleWithheldFlag()
    {
        // Arrange
        var vertex = new Vertex(1.0, 2.0, 3.0);

        // Act
        var withheldVertex = vertex.WithWithheld(true);
        var nonWithheldVertex = withheldVertex.WithWithheld(false);

        // Assert
        Assert.True(withheldVertex.IsWithheld());
        Assert.False(nonWithheldVertex.IsWithheld());
    }
}