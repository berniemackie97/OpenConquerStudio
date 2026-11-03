using OpenConquer.Content.Configuration;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Startup;

/// <summary>
/// The one shot startup logo selected for this launch.
/// </summary>
public sealed class StartupLogo
{
    private const int MaximumEncodedLength = 16 * 1024 * 1024;

    private StartupLogo(int variantIndex, string? contentPath, RgbaImage? image, string? unavailableReason)
    {
        VariantIndex = variantIndex;
        ContentPath = contentPath;
        Image = image;
        UnavailableReason = unavailableReason;
    }

    public int VariantIndex
    {
        get;
    }

    public string? ContentPath
    {
        get;
    }

    public RgbaImage? Image
    {
        get;
    }

    public string? UnavailableReason
    {
        get;
    }

    /// <summary>
    /// Resolves and loads the optional startup logo for the supplied monotonic tick.
    /// </summary>
    public static StartupLogo Load(IClientContentSource contentSource, long monotonicTickMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        int variantIndex = (int)((monotonicTickMilliseconds & 1L) + 1L);

        string contentPath;

        try
        {
            StartupLogoConfiguration configuration = StartupLogoConfiguration.LoadOrDefault(contentSource);

            contentPath = configuration.GetLogoPath(variantIndex);
        }
        catch (Exception exception) when (IsNonFatalLogoFailure(exception))
        {
            return new StartupLogo(variantIndex, contentPath: null, image: null, unavailableReason: $"Startup logo configuration could not be used: {exception.Message}");
        }

        try
        {
            if (!contentSource.TryOpenRead(contentPath, ContentLookupMode.LooseOnly, out Stream? stream))
            {
                return new StartupLogo(variantIndex, contentPath, image: null, unavailableReason: $"'{contentPath}' was not found as a loose file.");
            }

            byte[] encodedImage;

            using (stream)
            {
                encodedImage = ContentReader.ReadBytes(stream, contentPath, MaximumEncodedLength);
            }

            return new StartupLogo(variantIndex, contentPath, WindowsBitmapReader.Decode24Bit(encodedImage), unavailableReason: null);
        }
        catch (Exception exception) when (IsNonFatalLogoFailure(exception))
        {
            return new StartupLogo(variantIndex, contentPath, image: null, unavailableReason: $"'{contentPath}' could not be loaded: {exception.Message}");
        }
    }

    private static bool IsNonFatalLogoFailure(Exception exception)
    {
        return exception is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException;
    }
}
