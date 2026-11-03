using System.Numerics;

namespace OpenConquer.Content.Mesh;

public static class C3MotionSampler
{
    public static bool TrySampleBone(C3Motion? motion, uint boneIndex, int framePosition, out Matrix4x4 matrix)
    {
        if (motion is null || boneIndex >= motion.BoneCount)
        {
            matrix = Matrix4x4.Identity;
            return true;
        }

        int count = motion.KeyframeCount;
        if (count == 0)
        {
            matrix = default;
            return false;
        }

        int bone = (int)boneIndex;
        int firstFrame = unchecked((int)motion.GetKeyframeIndex(0));
        if (framePosition <= firstFrame)
        {
            matrix = motion.GetBoneMatrix(0, bone);
            return true;
        }

        int lastFrame = unchecked((int)motion.GetKeyframeIndex(count - 1));
        if (framePosition >= lastFrame)
        {
            matrix = motion.GetBoneMatrix(count - 1, bone);
            return true;
        }

        int previous = 0;
        int next = count - 1;
        while (next - previous > 1)
        {
            int middle = previous + (next - previous) / 2;
            if (framePosition < unchecked((int)motion.GetKeyframeIndex(middle)))
            {
                next = middle;
            }
            else
            {
                previous = middle;
            }
        }

        int previousFrame = unchecked((int)motion.GetKeyframeIndex(previous));
        int nextFrame = unchecked((int)motion.GetKeyframeIndex(next));
        float weight = (float)((long)framePosition - previousFrame) / ((long)nextFrame - previousFrame);
        matrix = C3MatrixBlend.Combine(motion.GetBoneMatrix(previous, bone), 1f - weight,
            motion.GetBoneMatrix(next, bone), weight);
        return true;
    }

    public static bool TrySampleBlended(C3Motion? primary, int primaryFrame, C3Motion? secondary,
        int secondaryFrame, float secondaryAlpha, uint boneIndex, ref Matrix4x4 matrix)
    {
        if (primary is null || !TrySampleBone(primary, boneIndex, primaryFrame, out Matrix4x4 primaryMatrix))
        {
            return false;
        }

        if (secondary is null)
        {
            matrix = primaryMatrix;
            return true;
        }

        if (!TrySampleBone(secondary, boneIndex, secondaryFrame, out Matrix4x4 secondaryMatrix))
        {
            return false;
        }

        matrix = C3MatrixBlend.Combine(primaryMatrix, 1f - secondaryAlpha,
            secondaryMatrix, secondaryAlpha);
        return true;
    }
}
