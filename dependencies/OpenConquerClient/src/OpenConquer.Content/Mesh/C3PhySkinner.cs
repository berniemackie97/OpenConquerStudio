using System.Numerics;

namespace OpenConquer.Content.Mesh;

public static class C3PhySkinner
{
    public static void Calculate(C3PhyMesh mesh, C3Motion? primary, int primaryFrame,
        C3Motion? secondary, int secondaryFrame, float secondaryAlpha,
        Span<Matrix4x4> boneMatrices, Span<C3PhyOutputVertex> output,
        uint diffuseArgb = uint.MaxValue, Vector2 textureOffset = default)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (output.Length < mesh.Vertices.Count)
        {
            throw new ArgumentException("The output buffer is smaller than the mesh vertex count.", nameof(output));
        }

        int paletteSize = primary is null ? 0 : checked((int)Math.Max(primary.BoneCount, secondary?.BoneCount ?? 0));
        if (boneMatrices.Length < paletteSize)
        {
            throw new ArgumentException("The bone matrix buffer is smaller than the motion's bone count.", nameof(boneMatrices));
        }

        SampleBones(mesh, primary, primaryFrame, secondary, secondaryFrame,
            secondaryAlpha, boneMatrices, paletteSize);

        for (int i = 0; i < mesh.Vertices.Count; i++)
        {
            C3PhyVertex vertex = mesh.Vertices[i];
            (Vector3 position, Vector3 normal) = Deform(vertex, primary, boneMatrices, paletteSize);
            output[i] = new C3PhyOutputVertex(position, normal, diffuseArgb,
                vertex.TextureCoordinate + textureOffset);
        }
    }

    private static void SampleBones(C3PhyMesh mesh, C3Motion? primary, int primaryFrame,
        C3Motion? secondary, int secondaryFrame, float secondaryAlpha,
        Span<Matrix4x4> boneMatrices, int paletteSize)
    {
        if (primary is null)
        {
            return;
        }

        foreach (uint boneIndex in mesh.UsedBoneIndices)
        {
            if (boneIndex >= paletteSize)
            {
                continue;
            }

            Matrix4x4 sampled = default;
            if (!C3MotionSampler.TrySampleBlended(primary, primaryFrame, secondary, secondaryFrame,
                secondaryAlpha, boneIndex, ref sampled))
            {
                throw new InvalidDataException("A required bone motion has no sampleable keyframes.");
            }

            boneMatrices[(int)boneIndex] = sampled;
        }
    }

    private static (Vector3 Position, Vector3 Normal) Deform(C3PhyVertex vertex,
        C3Motion? primary, ReadOnlySpan<Matrix4x4> boneMatrices, int paletteSize)
    {
        if (vertex.BoneWeight1 == 0f && vertex.BoneWeight2 == 0f)
        {
            return (vertex.Position, vertex.Normal);
        }

        Matrix4x4 first = GetMatrix(vertex.BoneIndex1, paletteSize, primary, boneMatrices);
        Matrix4x4 second = GetMatrix(vertex.BoneIndex2, paletteSize, primary, boneMatrices);
        Matrix4x4 blended = C3MatrixBlend.Combine(first, vertex.BoneWeight1,
            second, vertex.BoneWeight2);

        Vector4 position = Vector4.Transform(new Vector4(vertex.Position, 1f), blended);
        if (MathF.Abs(position.W - 1f) > 1.1920929e-7f)
        {
            position /= position.W;
        }

        Vector3 normal = Vector3.TransformNormal(vertex.Normal, blended);
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) ||
            !float.IsFinite(position.Z) || !float.IsFinite(normal.X) ||
            !float.IsFinite(normal.Y) || !float.IsFinite(normal.Z))
        {
            throw new InvalidDataException("Bone deformation produced invalid vertex coordinates.");
        }

        return (new Vector3(position.X, position.Y, position.Z), normal);
    }

    private static Matrix4x4 GetMatrix(uint index, int paletteSize,
        C3Motion? primary, ReadOnlySpan<Matrix4x4> boneMatrices) =>
        primary is not null && index < paletteSize
            ? boneMatrices[(int)index]
            : Matrix4x4.Identity;
}
