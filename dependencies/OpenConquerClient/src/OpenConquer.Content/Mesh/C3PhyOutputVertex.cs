using System.Numerics;
using System.Runtime.InteropServices;

namespace OpenConquer.Content.Mesh;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct C3PhyOutputVertex(
    Vector3 Position, Vector3 Normal, uint DiffuseArgb, Vector2 TextureCoordinate);
