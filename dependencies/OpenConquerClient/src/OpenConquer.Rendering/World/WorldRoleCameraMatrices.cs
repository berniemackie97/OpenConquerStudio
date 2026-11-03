using System.Numerics;
using OpenConquer.Content.Map;

namespace OpenConquer.Rendering.World;

internal static class WorldRoleCameraMatrices
{
    internal static (Matrix4x4 Main, Matrix4x4 Shadow) Create(
        GameMapContent map, GameMapPuzzleResources puzzle, WorldPuzzleProjection projection,
        int roleX, int roleY, float rotationDegrees, float scale)
    {
        int width = projection.ScreenWidth;
        int height = projection.ScreenHeight;
        long originX = (long)map.CellGrid.Width * 32 - (long)puzzle.GroundColumns * 128;
        long originY = 16 + (long)map.CellGrid.Height * 16 -
            (long)puzzle.GroundRows * 128 - ((map.CellGrid.Height + 1L) % 2) * 16;

        float anchorX = roleX - originX;
        float anchorY = roleY - originY;
        float cameraX = width * 0.5f + (projection.CenterX - originX);
        float cameraY = height * 0.5f + (projection.CenterY - originY);

        Matrix4x4 baseTransform = Matrix4x4.CreateScale(scale) *
            Matrix4x4.CreateRotationZ(rotationDegrees * (MathF.PI / 180f));
        Matrix4x4 sceneTransform = Matrix4x4.CreateRotationX(-MathF.PI / 6f) *
            Matrix4x4.CreateTranslation(anchorX, 0f, anchorY);
        Matrix4x4 view = new(
            1, 0, 0, 0,
            0, 0, 1, 0,
            0, -1, 0, 0,
            -cameraX, cameraY, 1000, 1);

        const float near = 1f;
        const float far = 10000f;
        Matrix4x4 projectionMatrix = new(
            2f / width, 0, 0, 0,
            0, 2f / height, 0, 0,
            0, 0, 1f / (far - near), 0,
            0, 0, -near / (far - near), 1);

        Matrix4x4 shadowMatrix = new(
            1000, 0, 0, 0,
            0, 1000, 0, 0,
            0, 0, 0, -1,
            0, 0, 0, 1000);
        Matrix4x4 flatten = Matrix4x4.Identity;
        flatten.M33 = 0;
        flatten.M43 = view.M43 * 1.5f;

        return (baseTransform * sceneTransform * view * projectionMatrix,
            baseTransform * shadowMatrix * sceneTransform * view * flatten * projectionMatrix);
    }
}
