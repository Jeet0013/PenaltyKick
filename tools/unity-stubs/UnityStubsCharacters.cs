// UnityEngine surface needed by Assets/Scripts/Unity/Characters, beyond what
// UnityStubs.cs declares. Compile-check only, never shipped — see README.md.
//
// Kept in its own file so the character work and other concurrent work never
// edit the same stub file. The csproj globs UnityStubs*.cs.
#pragma warning disable CA1050, IDE0060, CS0067, CS0649

namespace UnityEngine
{
    /// <summary>
    /// <c>Mesh.SetColors(List&lt;Color&gt;)</c>, which the real API declares as an
    /// instance method.
    /// </summary>
    /// <remarks>
    /// <see cref="Mesh"/> is declared in UnityStubs.cs and is not partial, and
    /// that file belongs to someone else, so the method cannot be added to the
    /// class here. An extension method resolves identically at the call site.
    /// In Unity the instance method wins and this file does not exist.
    /// </remarks>
    public static class MeshColorStubExtensions
    {
        public static void SetColors(this Mesh mesh, System.Collections.Generic.List<Color> colors) { }
    }
}
