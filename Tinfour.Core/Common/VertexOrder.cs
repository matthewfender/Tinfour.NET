namespace Tinfour.Core.Common;

/// <summary>
///     Optional ordering hint for bulk vertex insertion.
/// </summary>
public enum VertexOrder
{
    /// <summary>
    ///     Insert in the order provided by the caller.
    /// </summary>
    AsIs = 0,

    /// <summary>
    ///     Insert after applying a Hilbert space-filling curve sort to improve spatial locality.
    /// </summary>
    Hilbert = 1
}