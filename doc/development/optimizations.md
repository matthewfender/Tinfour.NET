# Tinfour.NET Performance Optimizations

**Purpose:** Guide for .NET-specific optimizations in the Tinfour port  
**Audience:** Performance-focused developers

## Memory Optimizations

### Value Types vs Reference Types

Java's memory model is primarily reference-based, while .NET offers both reference types (classes) and value types (structs). For small, frequently instantiated types like vertices and geometric primitives, using structs can provide significant performance benefits:

```csharp
// Using a struct for the Vertex class
public readonly struct Vertex : ISamplePoint
{
    public double X { get; }
    public double Y { get; }
    public float Z { get; }
    
    public Vertex(double x, double y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}
```

**Benefits**:
- No heap allocation overhead
- No garbage collection pressure
- Better CPU cache locality
- Reduced memory fragmentation

**Considerations**:
- Limited to 16 bytes or less for optimal performance
- Should be immutable to avoid defensive copying
- Pass by reference (`in`, `ref`, `out`) to avoid copying

### Span&lt;T&gt; and Memory&lt;T&gt;

For operations on arrays or collections that require slicing or subsets without copying:

```csharp
public void ProcessPoints(ReadOnlySpan<Vertex> vertices)
{
    // Operate on vertices without copying
}
```

**Benefits**:
- Zero-allocation slicing of arrays and other memory
- Works with managed and unmanaged memory
- High-performance alternative to array copying

### ArrayPool&lt;T&gt;

For temporary arrays that are frequently allocated and deallocated:

```csharp
private static readonly ArrayPool<double> s_coordPool = ArrayPool<double>.Shared;

public void SomeMethod()
{
    double[] tempArray = s_coordPool.Rent(100);
    try
    {
        // Use the temporary array
    }
    finally
    {
        s_coordPool.Return(tempArray);
    }
}
```

**Benefits**:
- Reduces garbage collection pressure
- Reuses array instances
- Improves memory locality

## Computation Optimizations

### SIMD (Single Instruction, Multiple Data)

For vertex and vector operations that can be parallelized:

```csharp
using System.Numerics;

public static void TransformVertices(Span<Vertex> vertices, Matrix4x4 transform)
{
    for (int i = 0; i < vertices.Length; i++)
    {
        Vector3 point = new Vector3((float)vertices[i].X, (float)vertices[i].Y, vertices[i].Z);
        point = Vector3.Transform(point, transform);
        vertices[i] = new Vertex(point.X, point.Y, point.Z);
    }
}
```

**Benefits**:
- Hardware-accelerated parallel computations
- Significant speedup for vector operations
- Automatic use of AVX/SSE instructions

### System.Numerics

For geometric calculations and operations:

```csharp
using System.Numerics;

public static float ComputeArea(Vertex a, Vertex b, Vertex c)
{
    Vector3 ab = new Vector3((float)(b.X - a.X), (float)(b.Y - a.Y), b.Z - a.Z);
    Vector3 ac = new Vector3((float)(c.X - a.X), (float)(c.Y - a.Y), c.Z - a.Z);
    Vector3 cross = Vector3.Cross(ab, ac);
    return Vector3.Dot(cross, cross) * 0.5f;
}
```

**Benefits**:
- Highly optimized numeric operations
- Potential for hardware acceleration
- Clear, readable code that maps to mathematical concepts

### Task Parallelism

For operations that can be parallelized across multiple cores:

```csharp
public void ProcessTriangles(IReadOnlyList<SimpleTriangle> triangles)
{
    Parallel.ForEach(triangles, triangle => 
    {
        // Process each triangle independently
    });
}
```

**Benefits**:
- Utilizes all available CPU cores
- Scales with hardware capabilities
- Potential for significant speedup on multi-core systems

### Unsafe Code for Critical Paths

For the most performance-critical operations, unsafe code can provide direct memory access:

```csharp
public unsafe void FastCopy(double* source, double* destination, int count)
{
    for (int i = 0; i < count; i++)
    {
        destination[i] = source[i];
    }
}
```

**Benefits**:
- Direct memory manipulation
- Eliminates bounds checking
- Can be faster for tight loops

**Considerations**:
- Increased complexity and potential for bugs
- Security implications
- Reduced safety guarantees
- Only use when profiling shows clear benefits

## Collection Optimizations

### Dictionary&lt;TKey, TValue&gt; vs ConcurrentDictionary&lt;TKey, TValue&gt;

For thread-safe operations:

```csharp
private static readonly ConcurrentDictionary<int, Vertex> s_vertexCache = new();
```

**Benefits**:
- Thread-safe without explicit locking
- Better scaling in multi-threaded environments
- Lower contention than locked dictionaries

### Immutable Collections

For collections that don't change after creation:

```csharp
public ImmutableArray<Vertex> Vertices { get; }
```

**Benefits**:
- Thread-safe without locking
- Cannot be accidentally modified
- Performance guarantees for certain operations

### Custom Collections for Special Cases

For specialized collections with domain-specific access patterns:

```csharp
public class SpatialVertexCollection
{
    // Custom implementation optimized for spatial queries
}
```

**Benefits**:
- Optimized for specific access patterns
- Can eliminate unnecessary operations
- Potential for domain-specific optimizations

## Asynchronous Programming

### Async/Await for I/O Operations

```csharp
public async Task<IList<Vertex>> LoadVerticesAsync(string filename)
{
    using var fileStream = new FileStream(filename, FileMode.Open, 
        FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
    // Read asynchronously
    return vertices;
}
```

**Benefits**:
- Non-blocking I/O operations
- Better resource utilization
- Improved responsiveness

### ValueTask&lt;T&gt; for High-Performance Async

```csharp
public ValueTask<double> CalculateInterpolatedValueAsync(double x, double y)
{
    // Implementation
}
```

**Benefits**:
- Reduced allocations for synchronous completion
- Better performance for frequently called async methods
- Compatible with async/await pattern

## Memory Management

### Object Pooling

For frequently allocated/deallocated objects:

```csharp
private readonly ObjectPool<Edge> _edgePool = 
    new DefaultObjectPool<Edge>(new DefaultPooledObjectPolicy<Edge>());

public Edge GetEdge()
{
    return _edgePool.Get();
}

public void ReturnEdge(Edge edge)
{
    _edgePool.Return(edge);
}
```

**Benefits**:
- Reduces garbage collection pressure
- Reuses object instances
- Can improve performance in tight loops

### Struct-based Collections

For collections of small value types:

```csharp
public List<Vertex> Vertices { get; } = new List<Vertex>();
```

**Benefits**:
- Contiguous memory layout
- Better cache locality
- Reduced pointer chasing

## Platform-Specific Considerations

### Trimming and AOT Compilation

For WebAssembly and mobile platforms:

```xml
<PropertyGroup>
    <PublishTrimmed>true</PublishTrimmed>
    <PublishAot>true</PublishAot>
</PropertyGroup>
```

**Benefits**:
- Reduced binary size
- Faster startup time
- Better runtime performance on constrained devices

### Cross-Platform Math Precision

For ensuring consistent results across platforms:

```csharp
// Use explicit MathF.* methods for float operations
float result = MathF.Sqrt(value);

// Use Math.* methods for double operations
double result = Math.Sqrt(value);
```

**Benefits**:
- More predictable cross-platform behavior
- Explicit control over precision
- Consistent results across different hardware

## Current Performance Characteristics

**Observed (BenchmarkDotNet, .NET 8, Release; Hilbert sorted + preallocation):**

| Vertex Count | Time | Memory |
|--------------|------|--------|
| 1,000 | ~1.0 ms | ~1.15 MB |
| 10,000 | ~15.5 ms | ~11.7 MB |
| 100,000 | ~196 ms | ~112.6 MB |
| 1,000,000 | ~1.4 s | ~750 MB |

**Notes**:
- ~2.5× slower than Java baseline for large datasets
- Some allocations are from LINQ/materialization and diagnostic accessors

## Optimization Roadmap

### Applied Optimizations
- ✅ AddSorted (Hilbert) and PreAllocateForVertices
- ✅ AggressiveInlining on critical QuadEdge getters
- ✅ EdgePool memory management

### Quick Wins (Low Risk)
- Add AggressiveInlining to tiny hot helpers (InCircleWithGhosts, TestAndTransfer)
- Reduce diagnostic object allocations in hot paths

### Future Work (Medium Effort)
- Vectorize small arithmetic in predicates (System.Numerics) — validate carefully
- Micro-opt in iterators (avoid transient lists; use struct enumerators where feasible)

---

**Last Updated:** November 2025
