namespace OpenConquer.Rendering.World;

internal sealed class WorldSceneryDepthOrder
{
    private readonly List<Entry> _active = [];

    internal void Insert(WorldScenerySprite sprite, bool terrain = false) =>
        Insert(new Entry(sprite, terrain));

    internal void InsertRole(int tileX, int tileY) =>
        Insert(new Entry(tileX, tileY));

    private void Insert(Entry incoming)
    {
        if (!incoming.Terrain)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                Entry parent = _active[i];
                if (!parent.Terrain || !Contains(parent, incoming))
                {
                    continue;
                }

                InsertChild(parent.Children ??= [], incoming);
                _active[i] = parent;
                return;
            }
        }

        int cursor = -1;
        for (int i = 0; i < _active.Count; i++)
        {
            Entry existing = _active[i];
            if (Overlaps(incoming.TileX, incoming.Width, existing.TileX, existing.Width))
            {
                if (incoming.TileY < existing.TileY)
                {
                    InsertBefore(incoming, i, cursor);
                    return;
                }

                cursor = ScanForward(incoming, i);
            }
            else if (Overlaps(incoming.TileY, incoming.Height, existing.TileY, existing.Height))
            {
                if (incoming.TileX < existing.TileX)
                {
                    InsertBefore(incoming, i, cursor);
                    return;
                }

                cursor = ScanForward(incoming, i);
            }
        }

        if (cursor >= 0)
        {
            _active.Insert(cursor + 1, incoming);
            return;
        }

        long sum = Sum(incoming);
        int position = 0;
        while (position < _active.Count && sum >= Sum(_active[position]))
        {
            position++;
        }

        _active.Insert(position, incoming);
    }

    internal IReadOnlyList<WorldScenerySprite> ToDrawList() => ToDrawList(out _);

    internal IReadOnlyList<WorldScenerySprite> ToDrawList(out int roleIndex)
    {
        List<WorldScenerySprite> sprites = new(_active.Count);
        int resolvedRoleIndex = -1;
        foreach (Entry entry in _active)
        {
            Append(entry);
            if (entry.Children is { } children)
            {
                foreach (Entry child in children)
                {
                    Append(child);
                }
            }
        }

        roleIndex = resolvedRoleIndex;
        return sprites;

        void Append(Entry entry)
        {
            if (entry.IsRole)
            {
                resolvedRoleIndex = sprites.Count;
            }
            else
            {
                sprites.Add(entry.Sprite!.Value);
            }
        }
    }

    private int ScanForward(Entry incoming, int pivot)
    {
        int cursor = pivot;
        long pivotSum = Sum(_active[pivot]);
        long sum = Sum(incoming);
        for (int i = pivot + 1; i < _active.Count; i++)
        {
            long candidate = Sum(_active[i]);
            if (candidate != pivotSum && sum <= candidate)
            {
                break;
            }

            cursor = i;
        }

        return cursor;
    }

    private void InsertBefore(Entry incoming, int pivot, int cursor)
    {
        int position = pivot;
        long pivotSum = Sum(_active[pivot]);
        long sum = Sum(incoming);
        for (int i = pivot - 1; i >= 0; i--)
        {
            if (i <= cursor || (Sum(_active[i]) != pivotSum && sum >= Sum(_active[i])))
            {
                break;
            }

            position = i;
        }

        _active.Insert(position, incoming);
    }

    private static void InsertChild(List<Entry> children, Entry incoming)
    {
        for (int i = 0; i < children.Count; i++)
        {
            Entry existing = children[i];
            bool before = Overlaps(incoming.TileX, incoming.Width, existing.TileX, existing.Width)
                ? incoming.TileY < existing.TileY
                : Overlaps(incoming.TileY, incoming.Height, existing.TileY, existing.Height)
                    ? incoming.TileX < existing.TileX
                    : Sum(incoming) < Sum(existing);
            if (before)
            {
                children.Insert(i, incoming);
                return;
            }
        }

        children.Add(incoming);
    }

    private static bool Contains(Entry parent, Entry entry) =>
        (long)parent.TileX - parent.Width < entry.TileX && entry.TileX <= parent.TileX &&
        (long)parent.TileY - parent.Height < entry.TileY && entry.TileY <= parent.TileY;

    private static bool Overlaps(int a, int aWidth, int b, int bWidth) =>
        (long)a - aWidth < b && (long)b - bWidth < a;

    private static long Sum(Entry entry) => (long)entry.TileX + entry.TileY;

    private struct Entry
    {
        internal Entry(WorldScenerySprite sprite, bool terrain)
        {
            Sprite = sprite;
            TileX = sprite.TileX;
            TileY = sprite.TileY;
            Width = sprite.FootprintWidth;
            Height = sprite.FootprintHeight;
            Terrain = terrain;
            IsRole = false;
        }

        internal Entry(int tileX, int tileY)
        {
            TileX = tileX;
            TileY = tileY;
            Width = 1;
            Height = 1;
            IsRole = true;
        }

        internal readonly WorldScenerySprite? Sprite;
        internal readonly int TileX;
        internal readonly int TileY;
        internal readonly int Width;
        internal readonly int Height;
        internal readonly bool Terrain;
        internal readonly bool IsRole;
        internal List<Entry>? Children;
    }
}
