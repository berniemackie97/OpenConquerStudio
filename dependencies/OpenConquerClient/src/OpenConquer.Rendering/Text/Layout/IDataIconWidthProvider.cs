namespace OpenConquer.Rendering.Text.Layout;

/// <summary>
/// Resolves the measured width of one #NN data icon.
/// </summary>
internal interface IDataIconWidthProvider
{
    bool TryGetWidth(byte dataIconIndex, out int widthPixels);
}
