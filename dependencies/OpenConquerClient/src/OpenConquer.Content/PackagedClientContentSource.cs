using System.Diagnostics.CodeAnalysis;
using System.Text;
using OpenConquer.Content.Wdf;

namespace OpenConquer.Content;

/// <summary>
/// Composes loose files with the WDF packages declared by <c>ini/package.ini</c>.
/// </summary>
public sealed class PackagedClientContentSource : IClientContentSource
{
    public const string PackageConfigurationPath = "ini/package.ini";

    private const int MaximumPackageConfigurationLength = 64 * 1024;
    private const int MaximumVirtualPathLength = 255;

    private readonly ClientContentRoot _looseFiles;
    private readonly Dictionary<uint, WdfArchive> _packagesByPrefixHash;

    private PackagedClientContentSource(ClientContentRoot looseFiles, Dictionary<uint, WdfArchive> packagesByPrefixHash, List<WdfPackageRegistration> packageRegistrations)
    {
        _looseFiles = looseFiles;
        _packagesByPrefixHash = packagesByPrefixHash;
        PackageRegistrations = Array.AsReadOnly(packageRegistrations.ToArray());
    }

    /// <summary>
    /// Every <c>ini/package.ini</c> declaration in file order with its resolved outcome.
    /// </summary>
    public IReadOnlyList<WdfPackageRegistration> PackageRegistrations
    {
        get;
    }

    public static PackagedClientContentSource Open(string rootPath)
    {
        ClientContentRoot looseFiles = new(rootPath);

        HashSet<uint> registeredPrefixHashes = [];
        Dictionary<uint, WdfArchive> packagesByPrefixHash = [];
        List<WdfPackageRegistration> registrations = [];

        string[] declaredPackageNames;

        // Package declarations are optional, unreadable or malformed configuration falls back to loose file lookup.
        try
        {
            if (!looseFiles.TryOpenRead(PackageConfigurationPath, ContentLookupMode.LooseOnly, out Stream? declarationStream))
            {
                return new PackagedClientContentSource(looseFiles, packagesByPrefixHash, registrations);
            }

            using (declarationStream)
            {
                declaredPackageNames = ReadDeclaredPackageNames(declarationStream);
            }
        }
        catch (InvalidDataException)
        {
            return new PackagedClientContentSource(looseFiles, packagesByPrefixHash, registrations);
        }
        catch (IOException)
        {
            return new PackagedClientContentSource(looseFiles, packagesByPrefixHash, registrations);
        }
        catch (UnauthorizedAccessException)
        {
            return new PackagedClientContentSource(looseFiles, packagesByPrefixHash, registrations);
        }

        foreach (string declaredName in declaredPackageNames)
        {
            registrations.Add(RegisterPackage(looseFiles, registeredPrefixHashes, packagesByPrefixHash, declaredName));
        }

        return new PackagedClientContentSource(looseFiles, packagesByPrefixHash, registrations);
    }

    /// <inheritdoc />
    public bool TryOpenRead(string contentPath, ContentLookupMode mode, [NotNullWhen(true)] out Stream? stream)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown content lookup mode.");
        }

        if (mode != ContentLookupMode.PackageOnly && _looseFiles.TryOpenRead(contentPath, ContentLookupMode.LooseOnly, out stream))
        {
            return true;
        }

        if (mode == ContentLookupMode.LooseOnly)
        {
            stream = null;
            return false;
        }

        string normalizedPath = ClientContentPath.NormalizeVirtualPath(contentPath, nameof(contentPath), MaximumVirtualPathLength);
        string prefix = WdfPackagePrefix.FromVirtualPath(normalizedPath);
        uint prefixHash = WdfPathHash.Compute(prefix);

        if (_packagesByPrefixHash.TryGetValue(prefixHash, out WdfArchive? package))
        {
            return package.TryOpenRead(normalizedPath, out stream);
        }

        stream = null;
        return false;
    }

    /// <inheritdoc />
    public Stream OpenRequiredRead(string contentPath, ContentLookupMode mode)
    {
        if (TryOpenRead(contentPath, mode, out Stream? stream))
        {
            return stream;
        }

        throw new FileNotFoundException($"Client content file '{contentPath}' was not found in the packaged content source using {mode} lookup.");
    }

    private static string[] ReadDeclaredPackageNames(Stream declarationStream)
    {
        byte[] bytes = ContentReader.ReadBytes(declarationStream, PackageConfigurationPath, MaximumPackageConfigurationLength);

        return Encoding.Latin1.GetString(bytes).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static WdfPackageRegistration RegisterPackage(ClientContentRoot looseFiles, HashSet<uint> registeredPrefixHashes, Dictionary<uint, WdfArchive> packagesByPrefixHash, string declaredName)
    {
        string prefix = WdfPackagePrefix.FromDeclaredPackageName(declaredName);
        uint prefixHash = WdfPathHash.Compute(prefix);

        if (!registeredPrefixHashes.Add(prefixHash))
        {
            return new WdfPackageRegistration(declaredName, prefix, WdfPackageRegistrationOutcome.DuplicatePrefix);
        }

        WdfArchive archive;

        // An unavailable package is recorded without preventing the remaining content sources from loading.
        try
        {
            if (!looseFiles.TryResolveFile(declaredName, out string? packagePath))
            {
                return new WdfPackageRegistration(declaredName, prefix, WdfPackageRegistrationOutcome.FileNotFound);
            }

            archive = WdfArchive.Open(packagePath);
        }
        catch (InvalidDataException)
        {
            return new WdfPackageRegistration(declaredName, prefix, WdfPackageRegistrationOutcome.ArchiveUnavailable);
        }
        catch (IOException)
        {
            return new WdfPackageRegistration(declaredName, prefix, WdfPackageRegistrationOutcome.ArchiveUnavailable);
        }
        catch (UnauthorizedAccessException)
        {
            return new WdfPackageRegistration(declaredName, prefix, WdfPackageRegistrationOutcome.ArchiveUnavailable);
        }

        packagesByPrefixHash.Add(prefixHash, archive);

        return new WdfPackageRegistration(declaredName, prefix, WdfPackageRegistrationOutcome.Registered);
    }
}
