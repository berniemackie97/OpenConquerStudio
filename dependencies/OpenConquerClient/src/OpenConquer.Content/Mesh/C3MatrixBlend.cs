using System.Numerics;

namespace OpenConquer.Content.Mesh;

internal static class C3MatrixBlend
{
    internal static Matrix4x4 Combine(Matrix4x4 left, float leftWeight,
        Matrix4x4 right, float rightWeight) => new(
        leftWeight * left.M11 + rightWeight * right.M11,
        leftWeight * left.M12 + rightWeight * right.M12,
        leftWeight * left.M13 + rightWeight * right.M13,
        leftWeight * left.M14 + rightWeight * right.M14,
        leftWeight * left.M21 + rightWeight * right.M21,
        leftWeight * left.M22 + rightWeight * right.M22,
        leftWeight * left.M23 + rightWeight * right.M23,
        leftWeight * left.M24 + rightWeight * right.M24,
        leftWeight * left.M31 + rightWeight * right.M31,
        leftWeight * left.M32 + rightWeight * right.M32,
        leftWeight * left.M33 + rightWeight * right.M33,
        leftWeight * left.M34 + rightWeight * right.M34,
        leftWeight * left.M41 + rightWeight * right.M41,
        leftWeight * left.M42 + rightWeight * right.M42,
        leftWeight * left.M43 + rightWeight * right.M43,
        leftWeight * left.M44 + rightWeight * right.M44);
}
