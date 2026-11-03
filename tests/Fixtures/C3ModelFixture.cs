using System.Collections.Immutable;
using System.Numerics;
using OpenConquer.Content.Mesh;

namespace OpenConquerStudio.Tests;

internal static class C3ModelFixture
{
    internal static C3ModelData Create()
    {
        byte[] body = File("body_surface", "v_armet");
        byte[] hair = File("hair_surface");
        C3ResolvedPart parent = new("body", "c3/body.c3", "", "ini/body-motion.dbc", 1, 5, 6, 7, 123, 456, 0);
        C3ResolvedPart child = new("armet", "c3/hair.c3", "", "ini/hair-motion.dbc", 2, 5, 6, 8, 124, 457, 0);
        return C3ModelData.Create(Guid.NewGuid(), [parent, child],
            [new C3ModelClip("Idle", "c3/body.c3", 66, true) { PartMotions = [new C3ModelMotionBinding(1, "c3/hair.c3")] }],
            ["v_armet"], new Dictionary<string, byte[]> { ["c3/body.c3"] = body, ["c3/hair.c3"] = hair },
            [new C3ModelPartBinding(0, -1, "", Matrix(Matrix4x4.CreateTranslation(10, 20, 30))),
                new C3ModelPartBinding(1, 0, "v_armet", Matrix(Matrix4x4.CreateScale(2, 3, 4)))]);
    }

    internal static ImmutableArray<float> Matrix(Matrix4x4 value) =>
        [.. System.Runtime.InteropServices.MemoryMarshal.Cast<Matrix4x4, float>(new ReadOnlySpan<Matrix4x4>(in value))];

    internal static byte[] File(params string[] submeshNames)
    {
        using MemoryStream output = new();
        output.Write(new byte[16]);
        using BinaryWriter writer = new(output, System.Text.Encoding.UTF8, leaveOpen: true);
        foreach (string name in submeshNames)
        {
            Section("PHY4", Phy(name));
            Section("MOTI", Motion());
        }
        Section("UNKN", [11, 22, 33, 44, 55]);
        return output.ToArray();

        void Section(string tag, byte[] bytes)
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes(tag));
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
    }

    private static byte[] Phy(string name)
    {
        using MemoryStream output = new();
        using BinaryWriter writer = new(output, System.Text.Encoding.UTF8, leaveOpen: true);
        byte[] nameBytes = System.Text.Encoding.ASCII.GetBytes(name);
        writer.Write(nameBytes.Length);
        writer.Write(nameBytes);
        writer.Write(2u);
        writer.Write(1u);
        writer.Write(2u);
        foreach (Vector3 position in new Vector3[] { new(-1, 0, -1), new(1, 0, -1), new(0, 0, 1) })
        {
            writer.Write(position.X);
            writer.Write(position.Y);
            writer.Write(position.Z);
            writer.Write(0.25f);
            writer.Write(0.5f);
            writer.Write(0f);
            writer.Write(0u);
            writer.Write(0u);
            writer.Write(1f);
            writer.Write(0f);
        }
        writer.Write(1u);
        writer.Write(0u);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)2);
        writer.Write(3);
        writer.Write(new byte[] { 0xAB, 0xCD, 0xEF });
        foreach (float value in new float[] { -1, -1, -1, 1, 1, 1 })
        {
            writer.Write(value);
        }
        foreach (float value in Matrix(Matrix4x4.Identity))
        {
            writer.Write(value);
        }
        writer.Write(2u);
        writer.Write(1u);
        writer.Write(new byte[16]);
        writer.Write(1u);
        writer.Write(new byte[16]);
        writer.Write(1u);
        writer.Write(new byte[16]);
        writer.Write("STEP"u8);
        writer.Write(0.5f);
        writer.Write(0.25f);
        writer.Write("2SIDBIL2"u8);
        return output.ToArray();
    }

    private static byte[] Motion()
    {
        using MemoryStream output = new();
        using BinaryWriter writer = new(output, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(1u);
        writer.Write(6u);
        writer.Write("KKEY"u8);
        writer.Write(2u);
        foreach (uint frame in new[] { 0u, 5u })
        {
            writer.Write(frame);
            Matrix4x4 matrix = Matrix4x4.CreateTranslation(frame * 2, frame, frame * 3);
            matrix.M12 = frame * 0.1f;
            foreach (float value in Matrix(matrix))
            {
                writer.Write(value);
            }
        }
        writer.Write(1);
        writer.Write(new byte[6 * 4]);
        return output.ToArray();
    }
}
