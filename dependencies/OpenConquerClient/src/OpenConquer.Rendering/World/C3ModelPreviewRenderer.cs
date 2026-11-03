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
    private readonly List<(WorldRoleMesh Mesh, OpenGLTexture2D Texture, int PartIndex)> _meshes = [];
    private readonly List<OpenGLTexture2D> _textures = [];
    private readonly Vector3 _center;
    private readonly float _extent;

    public C3ModelPreviewRenderer(OpenGLGraphicsDevice device, C3ModelPreview preview)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(preview);
        _preview = preview;
        _center = (preview.BoundsMin + preview.BoundsMax) * 0.5f;
        _extent = Math.Max(1f, (preview.BoundsMax - preview.BoundsMin).Length());
        try
        {
            for (int partIndex = 0; partIndex < preview.Parts.Length; partIndex++)
            {
                C3ModelPreviewPart part = preview.Parts[partIndex];
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
                    _meshes.Add((new WorldRoleMesh(device, track.Mesh, track.Motion), texture, partIndex));
                }
            }
            if (_meshes.Count == 0)
            {
                throw new InvalidDataException("Model has no drawable geometry.");
            }
        }
        catch { Dispose(); throw; }
    }

    public void Draw(OpenGLRenderer renderer, int frame, float zoom, float rotationDegrees, bool showBones)
    {
        Matrix4x4 clip = Matrix4x4.CreateTranslation(-_center) * Matrix4x4.CreateRotationZ(rotationDegrees * MathF.PI / 180f) *
            Matrix4x4.CreateRotationX(MathF.PI / 2f) * Matrix4x4.CreateScale(1.8f * zoom / _extent) *
            Matrix4x4.CreateScale(1, 1, 0.25f) * Matrix4x4.CreateTranslation(0, 0, 0.5f);
        foreach ((WorldRoleMesh mesh, OpenGLTexture2D texture, int partIndex) in _meshes)
        {
            mesh.Draw(renderer, texture, _preview.PartTransform(partIndex, frame) * clip, frame);
        }
        if (!showBones)
        {
            return;
        }
        for (int partIndex = 0; partIndex < _preview.Parts.Length; partIndex++)
        {
            Matrix4x4 transform = _preview.PartTransform(partIndex, frame) * clip;
            foreach (C3ModelTrack track in _preview.Parts[partIndex].Tracks)
            {
                if (track.Motion is not { } motion)
                {
                    continue;
                }
                for (uint bone = 0; bone < motion.BoneCount; bone++)
                {
                    if (C3MotionSampler.TrySampleBone(motion, bone, frame, out Matrix4x4 pose))
                    {
                        Vector3 point = Vector3.Transform(new Vector3(pose.M41, pose.M42, pose.M43), transform);
                        renderer.DrawSolidRectangle((int)((point.X + 1) * 256) - 2, (int)((1 - point.Y) * 256) - 2, 4, 4, new SpriteColor(255, 80, 50, 255));
                    }
                }
            }
        }
    }

    public void DrawInWorld(OpenGLRenderer renderer, int frame, Matrix4x4 main, Matrix4x4 shadow)
    {
        for (int partIndex = 0; partIndex < _preview.Parts.Length; partIndex++)
        {
            Matrix4x4 pose = _preview.PartTransform(partIndex, frame);
            foreach (var item in _meshes)
            {
                if (item.PartIndex == partIndex)
                {
                    item.Mesh.Draw(renderer, item.Texture, pose * main, frame);
                }
            }
            foreach (var item in _meshes)
            {
                if (item.PartIndex == partIndex)
                {
                    item.Mesh.DrawShadow(renderer, pose * shadow);
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
