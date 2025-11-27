# Tinfour.NET Coding Standards and Conventions

**Purpose:** Guidelines for maintaining consistency in the Tinfour.NET codebase  
**Audience:** Contributors and developers

## General Principles

1. **Maintain Mathematical Correctness**: The mathematical logic and algorithms must remain identical to the Java implementation.
2. **Optimize for Performance**: Memory efficiency and processing speed are critical priorities.
3. **Write Clean, Maintainable Code**: Prefer clear, maintainable code over clever optimizations.
4. **Cross-Platform Compatibility**: Code must work on Windows, macOS, Linux, and WebAssembly.

## Language Usage

### C# Features

1. **Use C# 8.0+ features** where they improve code quality or performance.
2. **Always use `var`** when the type is obvious from the right side of the assignment.
3. **Use nullable reference types** with proper annotations (`?`, `!`).
4. **Use expression-bodied members** for simple methods and properties.
5. **Use pattern matching** where it enhances readability.
6. **Use tuple returns** for methods that need to return multiple values.

### Types and Memory Management

1. **Consider structs vs classes** carefully:
   - Use structs for small (≤16 bytes), immutable value types
   - Use classes for larger objects and when inheritance is needed
2. **Use `readonly struct`** for immutable value types.
3. **Use `Span<T>` and `Memory<T>`** for zero-allocation array operations.
4. **Consider object pooling** for frequently allocated/deallocated objects.
5. **Be mindful of boxing/unboxing** operations with value types.

### Asynchronous Programming

1. **Async all the way down** when appropriate.
2. **Use Task Parallel Library (TPL)** for computationally intensive operations.
3. **Consider `ValueTask`** for high-performance async operations.
4. **Provide synchronous alternatives** for performance-critical paths.

## Naming and Formatting

1. **Match .NET naming conventions**:
   - PascalCase for types, methods, properties, and public fields
   - camelCase for local variables and parameters
   - _camelCase for private fields
2. **Use descriptive names**; avoid abbreviations except for common ones (e.g., Id, Xml, Http).
3. **One statement per line**; no compound statements.
4. **Preserve original comments** and add new ones explaining porting decisions.
5. **Use XML documentation comments** for public APIs.

## Type Mappings (Java to C#)

| Java Type       | C# Equivalent     | Notes                                |
|----------------|-------------------|--------------------------------------|
| int            | int              |                                      |
| long           | long             |                                      |
| double         | double           |                                      |
| float          | float            |                                      |
| boolean        | bool             |                                      |
| String         | string           |                                      |
| Object         | object           |                                      |
| ArrayList&lt;T&gt;   | List&lt;T&gt;          |                                      |
| HashMap&lt;K,V&gt;   | Dictionary&lt;K,V&gt;  | Consider performance characteristics |
| Set&lt;T&gt;         | HashSet&lt;T&gt;       |                                      |
| Iterator&lt;T&gt;    | IEnumerator&lt;T&gt;   | Consider using `foreach` instead     |
| Iterable&lt;T&gt;    | IEnumerable&lt;T&gt;   |                                      |

## Collection Usage

1. **Prefer `IEnumerable<T>`** for method return types when the caller only needs to enumerate.
2. **Use concrete collection types** (List&lt;T&gt;, Dictionary&lt;K,V&gt;) for properties and fields.
3. **Consider immutable collections** for thread safety and to prevent modification.
4. **Use collection initializers** where appropriate.

## Error Handling

1. **Use exceptions for exceptional conditions**, not control flow.
2. **Prefer specific exception types** over general ones.
3. **Consider returning `Result<T>` or tuple** for operations that may fail in expected ways.
4. **Use proper null handling** with nullable reference types.

## Testing

1. **Write unit tests for all functionality** using xUnit.
2. **Match Java test data and test cases** where possible.
3. **Ensure tests validate mathematical correctness** against known values or Java results.
4. **Include performance benchmarks** for critical operations.

## Project Organization

1. **Maintain namespace hierarchy** matching Java package structure.
2. **Organize files by feature**, not by type.
3. **Keep interfaces and implementations** in separate files.
4. **Group related functionality** in the same namespace.

## Documentation

1. **Preserve algorithm descriptions** from Java comments.
2. **Document performance characteristics** and constraints.
3. **Include XML documentation** for public APIs.
4. **Document any deviations** from the Java implementation.

## Version Control

1. **Commit logical units** of work.
2. **Include descriptive commit messages** explaining the changes.
3. **Reference the original Java code** in comments when necessary.

## Performance Considerations

1. **Profile before optimizing** to identify actual bottlenecks.
2. **Consider SIMD operations** for vector math.
3. **Minimize memory allocations** in tight loops.
4. **Use value types appropriately** to reduce heap allocations.
5. **Consider unsafe code blocks** only when absolutely necessary for performance.

---

**Last Updated:** November 2025
