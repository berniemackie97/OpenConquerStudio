using System.Numerics;
using System.Runtime.InteropServices;
using OpenConquer.Content.Mesh;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Meshes;

namespace OpenConquer.Rendering.World;

public sealed class WorldRoleMesh : IDisposable
{
    private readonly C3PhyMesh _source;
    private readonly C3Motion? _motion;
    private readonly OpenGLIndexedMesh _gpuMesh;
    private readonly Matrix4x4[] _bones;
    private readonly C3PhyOutputVertex[] _vertices;

    public WorldRoleMesh(OpenGLGraphicsDevice device, C3PhyMesh source, C3Motion? motion)
    {
        _source = source;
        _motion = motion;
        _bones = new Matrix4x4[motion is null ? 0 : checked((int)motion.BoneCount)];
        _vertices = new C3PhyOutputVertex[source.Vertices.Count];
        _gpuMesh = device.CreateIndexedMesh(source.Vertices.Count, [.. source.Indices]);
    }

    public void Draw(OpenGLRenderer renderer, OpenConquer.Rendering.OpenGL.Resources.OpenGLTexture2D texture,
        Matrix4x4 main, int frame)
    {
        C3PhySkinner.Calculate(_source, _motion, frame, null, 0, 0, _bones, _vertices);
        _gpuMesh.Upload(MemoryMarshal.AsBytes(_vertices.AsSpan()));
        renderer.DrawIndexedMesh(_gpuMesh, texture, main, MeshCullMode.Clockwise);
    }

    public void DrawShadow(OpenGLRenderer renderer, Matrix4x4 shadow) =>
        renderer.DrawIndexedMeshShadow(_gpuMesh, shadow, MeshCullMode.Clockwise);

    public void Dispose() => _gpuMesh.Dispose();
}
