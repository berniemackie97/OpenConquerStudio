using System.Numerics;

namespace OpenConquer.Content.Mesh;

public sealed class C3Motion
{
    private readonly uint[] _frameIndices;
    private readonly Matrix4x4[] _boneMatrices;

    internal C3Motion(uint boneCount, uint frameCount, uint encodingTag, uint[] frameIndices,
        Matrix4x4[] boneMatrices)
    {
        BoneCount = boneCount;
        FrameCount = frameCount;
        EncodingTag = encodingTag;
        _frameIndices = frameIndices;
        _boneMatrices = boneMatrices;
    }

    public uint BoneCount
    {
        get;
    }

    public uint FrameCount
    {
        get;
    }

    public uint EncodingTag
    {
        get;
    }

    public int KeyframeCount => _frameIndices.Length;

    public uint GetKeyframeIndex(int keyframe) => _frameIndices[keyframe];

    public Matrix4x4 GetBoneMatrix(int keyframe, int bone) =>
        _boneMatrices[checked(keyframe * (int)BoneCount + bone)];
}
