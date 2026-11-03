using System.Numerics;

namespace OpenConquer.Content.Mesh;

internal static class C3PhyNormals
{
    private const float SharedPositionTolerance = 0.01f;

    internal static void Generate(C3PhyVertex[] vertices, ushort[] indices)
    {
        Vector3[] normals = new Vector3[vertices.Length];
        Accumulate(normals, vertices, indices);
        Smooth(normals, vertices);

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = vertices[i] with
            {
                Normal = normals[i]
            };
        }
    }

    private static void Accumulate(Vector3[] normals, C3PhyVertex[] vertices, ushort[] indices)
    {
        for (int i = 0; i < indices.Length; i += 3)
        {
            int a = indices[i];
            int b = indices[i + 1];
            int c = indices[i + 2];
            Vector3 position = vertices[a].Position;
            Vector3 face = Vector3.Cross(vertices[c].Position - position, vertices[b].Position - position);
            face = Normalize(face);
            normals[a] += face;
            normals[b] += face;
            normals[c] += face;
        }

        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = Normalize(normals[i]);
        }
    }

    private static void Smooth(Vector3[] normals, C3PhyVertex[] vertices)
    {
        Dictionary<(long X, long Y, long Z), List<int>> cells = [];
        List<int> matches = [];
        List<int> group = [];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 current = vertices[i].Position;
            (long X, long Y, long Z) cell = Cell(current);
            matches.Clear();
            group.Clear();
            CollectEarlier(cells, cell, matches);
            matches.Sort();
            Vector3 combined = normals[i];

            foreach (int previous in matches)
            {
                Vector3 other = vertices[previous].Position;
                if (MathF.Abs(current.X - other.X) > SharedPositionTolerance ||
                    MathF.Abs(current.Y - other.Y) > SharedPositionTolerance ||
                    MathF.Abs(current.Z - other.Z) > SharedPositionTolerance)
                {
                    continue;
                }

                combined = Normalize(combined + normals[previous]);
                group.Add(previous);
            }

            normals[i] = combined;
            foreach (int previous in group)
            {
                normals[previous] = combined;
            }

            if (!cells.TryGetValue(cell, out List<int>? entries))
            {
                entries = [];
                cells.Add(cell, entries);
            }

            entries.Add(i);
        }
    }

    private static void CollectEarlier(
        Dictionary<(long X, long Y, long Z), List<int>> cells,
        (long X, long Y, long Z) cell, List<int> matches)
    {
        for (long dz = -1; dz <= 1; dz++)
        {
            for (long dy = -1; dy <= 1; dy++)
            {
                for (long dx = -1; dx <= 1; dx++)
                {
                    if (cells.TryGetValue((cell.X + dx, cell.Y + dy, cell.Z + dz), out List<int>? previous))
                    {
                        matches.AddRange(previous);
                    }
                }
            }
        }
    }

    private static Vector3 Normalize(Vector3 normal)
    {
        float lengthSquared = normal.LengthSquared();
        if (MathF.Abs(lengthSquared - 1f) <= 1.1920929e-7f)
        {
            return normal;
        }

        if (!float.IsFinite(lengthSquared) || lengthSquared <= 1.17549435e-38f)
        {
            return Vector3.Zero;
        }

        return normal * (1f / MathF.Sqrt(lengthSquared));
    }

    private static (long X, long Y, long Z) Cell(Vector3 position) =>
        ((long)Math.Floor(position.X / SharedPositionTolerance),
         (long)Math.Floor(position.Y / SharedPositionTolerance),
         (long)Math.Floor(position.Z / SharedPositionTolerance));
}
