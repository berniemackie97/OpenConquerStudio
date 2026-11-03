using System.Numerics;
using OpenConquer.Content.Mesh;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Rendering.World;

/// <summary>Renders the existing C3 skinning/indexed-mesh implementation inside a caller-owned graphics context.</summary>
public sealed class C3ModelPreviewRenderer : IDisposable
{
    private readonly C3ModelPreview _preview;
    private readonly List<(WorldRoleMesh Mesh, OpenGLTexture2D Texture)> _meshes = [];
    private readonly List<OpenGLTexture2D> _textures = [];
    private readonly Vector3 _center;
    private readonly float _extent;

    public C3ModelPreviewRenderer(OpenGLGraphicsDevice device, C3ModelPreview preview)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(preview);
        _preview = preview;
        Vector3 minimum = new(float.PositiveInfinity);
        Vector3 maximum = new(float.NegativeInfinity);
        try
        {
            foreach (C3ModelPreviewPart part in preview.Parts)
            {
                OpenGLTexture2D texture = part.Texture is { } image
                    ? device.CreateTexture2D(image.Width, image.Height, image.Pixels.Span)
                    : device.CreateTexture2D(1, 1, new byte[] { 255, 255, 255, 255 });
                _textures.Add(texture);
                foreach (C3ModelTrack track in part.Tracks)
                {
                    if (track.IsAttachment)
                    {
                        continue;
                    }
                    _meshes.Add((new WorldRoleMesh(device, track.Mesh, track.Motion), texture));
                    Matrix4x4[] bones = new Matrix4x4[track.Motion?.BoneCount ?? 0];
                    C3PhyOutputVertex[] vertices = new C3PhyOutputVertex[track.Mesh.Vertices.Count];
                    C3PhySkinner.Calculate(track.Mesh, track.Motion, 0, null, 0, 0, bones, vertices);
                    foreach (C3PhyOutputVertex vertex in vertices)
                    {
                        minimum = Vector3.Min(minimum, vertex.Position);
                        maximum = Vector3.Max(maximum, vertex.Position);
                    }
                }
            }
            if (_meshes.Count == 0 || !float.IsFinite(minimum.X))
            {
                throw new InvalidDataException("Model has no drawable geometry.");
            }
            _center = (minimum + maximum) * 0.5f;
            _extent = Math.Max(1f, (maximum - minimum).Length());
        }
        catch { Dispose(); throw; }
    }

    public void Draw(OpenGLRenderer renderer, int frame, float zoom, float rotationDegrees, bool showBones)
    {
        Matrix4x4 clip = Matrix4x4.CreateTranslation(-_center) * Matrix4x4.CreateRotationZ(rotationDegrees * MathF.PI / 180f) *
            Matrix4x4.CreateRotationX(-MathF.PI / 2f) * Matrix4x4.CreateScale(1.8f * zoom / _extent) *
            Matrix4x4.CreateScale(1, 1, 0.25f) * Matrix4x4.CreateTranslation(0, 0, 0.5f);
        foreach ((WorldRoleMesh mesh, OpenGLTexture2D texture) in _meshes)
        {
            mesh.Draw(renderer, texture, clip, frame);
        }
        if (!showBones)
        {
            return;
        }
        foreach (C3ModelTrack track in _preview.Parts.SelectMany(p => p.Tracks))
        {
            if (track.Motion is not { } motion)
            {
                continue;
            }
            for (uint bone = 0; bone < motion.BoneCount; bone++)
            {
                if (C3MotionSampler.TrySampleBone(motion, bone, frame, out Matrix4x4 pose))
                {
                    Vector3 point = Vector3.Transform(new Vector3(pose.M41, pose.M42, pose.M43), clip);
                    renderer.DrawSolidRectangle((int)((point.X + 1) * 256) - 2, (int)((1 - point.Y) * 256) - 2, 4, 4, new SpriteColor(255, 80, 50, 255));
                }
            }
        }
    }

    public void Dispose()
    {
        foreach (var item in _meshes)
        {
            item.Mesh.Dispose();
        }
        foreach (OpenGLTexture2D texture in _textures)
        {
            texture.Dispose();
        }
        _meshes.Clear();
        _textures.Clear();
    }
}
