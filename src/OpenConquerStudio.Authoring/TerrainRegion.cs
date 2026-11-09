namespace OpenConquerStudio.Authoring;

public readonly record struct TerrainRegion(int StartX, int StartY, int EndX, int EndY)
{
    public static TerrainRegion Between(int x0, int y0, int x1, int y1) =>
        new(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1));

    public void Validate(int width, int height)
    {
        if (StartX < 0 || StartY < 0 || EndX < StartX || EndY < StartY ||
            EndX >= width || EndY >= height)
        {
            throw new InvalidDataException("Terrain selection is outside the authored grid.");
        }
    }
}
