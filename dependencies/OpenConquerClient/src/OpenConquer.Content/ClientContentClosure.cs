using OpenConquer.Content.Ani;
using OpenConquer.Content.Configuration;
using OpenConquer.Content.Login;
using OpenConquer.Content.Map;
using OpenConquer.Content.Role;

namespace OpenConquer.Content;

/// <summary>
/// Resolves the content requirements required by the client at runtime.
/// </summary>
public static class ClientContentClosure
{
    private const string ControlAniPath = "ani/Control.ani";
    private const string LifeSectionName = "Progress40";
    private const string ManaSectionName = "Progress41";
    private const string SkillSectionName = "Progress42";
    private const string ProgressSectionName = "Progress45";
    private const string StaminaSectionName = "Progress46";
    private const string ExtendedStaminaSectionName = "Progress47";
    private const string DialogSectionName = "Dialog4";

    private static readonly int[] s_startupLogoVariantIndexes = [1, 2];

    private static readonly (string SectionName, int FrameCount)[] s_actionButtonSections =
    [
        ("Button40", 2),
        ("Button410", 2),
        ("Button42", 2),
        ("Button43", 2),
        ("Main3_MissionBtn", 3),
        ("Button45", 2),
        ("Button46", 2),
        ("Button47", 2),
        ("Button49", 2),
        ("Button48", 2),
        ("Button412", 2),
        ("Main3_OrganiseBtn", 4),
        ("Button41", 3),
    ];

    private static readonly (string SectionName, int FrameCount)[] s_checkControlSections =
    [
        ("Check40", 2),
        ("Check43", 2),
        ("Check46", 2),
        ("Button411", 2),
    ];

    public static IReadOnlyList<ClientContentRequirement> Resolve(IClientContentSource contentSource, uint? mapDataId = null,
        bool allMaps = false, IReadOnlySet<uint>? excludedMapDataIds = null)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        StartupLogoConfiguration startupLogo = StartupLogoConfiguration.LoadOrDefault(contentSource);

        List<ClientContentRequirement> requirements =
        [
            new(GameSetupConfiguration.RelativePath, ContentLookupMode.LooseOnly),
            new(GameFontConfiguration.RelativePath, ContentLookupMode.LooseOnly),
            new(StartupLogoConfiguration.RelativePath, ContentLookupMode.LooseOnly),
            new(ClientVersionLabel.RelativePath, ContentLookupMode.LooseOnly),
            new(SelectCharacterIni.RelativePath, ContentLookupMode.LooseOnly),
            new(GameMapCatalog.RelativePath, ContentLookupMode.LooseOnly),
            new(ControlAniPath, ContentLookupMode.LooseOnly),
        ];

        if (contentSource.TryOpenRead("ini/c3.wdb", ContentLookupMode.LooseOnly, out Stream? database))
        {
            database.Dispose();
            requirements.Add(new ClientContentRequirement("ini/c3.wdb", ContentLookupMode.LooseOnly));
            requirements.Add(new ClientContentRequirement("ini/package.ini", ContentLookupMode.LooseOnly));
            requirements.Add(new ClientContentRequirement("c3.wdf", ContentLookupMode.LooseOnly));

            foreach (string path in new[] { "ini/3DObj.dbc", "ini/3DTexture.dbc" })
            {
                if (contentSource.TryOpenRead(path, ContentLookupMode.LooseOnly, out Stream? index))
                {
                    index.Dispose();
                    requirements.Add(new ClientContentRequirement(path, ContentLookupMode.LooseOnly));
                }
            }
        }

        if (LoginLoadingContentPaths.IsPresent(contentSource))
        {
            requirements.AddRange(LoginLoadingContentPaths.Paths.Select(static path =>
                new ClientContentRequirement(path, ContentLookupMode.LooseOnly)));
        }

        foreach (int variantIndex in s_startupLogoVariantIndexes)
        {
            requirements.Add(new ClientContentRequirement(startupLogo.GetLogoPath(variantIndex), ContentLookupMode.LooseOnly));
        }

        AniIndexFile controlAni = AniIndexFile.Load(contentSource, ControlAniPath, ContentLookupMode.LooseOnly);

        AddFrameRequirements(requirements, controlAni.GetRequiredSection(LifeSectionName), expectedFrameCount: 3);
        AddFrameRequirements(requirements, controlAni.GetRequiredSection(ManaSectionName), expectedFrameCount: 3);
        AddFrameRequirements(requirements, controlAni.GetRequiredSection(SkillSectionName), expectedFrameCount: 3);
        AddFrameRequirements(requirements, controlAni.GetRequiredSection(ProgressSectionName), expectedFrameCount: 1);
        AddFrameRequirements(requirements, controlAni.GetRequiredSection(StaminaSectionName), expectedFrameCount: 2);
        AddFrameRequirements(requirements, controlAni.GetRequiredSection(ExtendedStaminaSectionName), expectedFrameCount: 2);
        AddFrameRequirements(requirements, controlAni.GetRequiredSection(DialogSectionName), expectedFrameCount: 2);

        foreach ((string sectionName, int frameCount) in s_actionButtonSections)
        {
            AddFrameRequirements(requirements, controlAni.GetRequiredSection(sectionName), frameCount);
        }

        foreach ((string sectionName, int frameCount) in s_checkControlSections)
        {
            AddFrameRequirements(requirements, controlAni.GetRequiredSection(sectionName), frameCount);
        }

        MainHudQuickbarContentRequirements.Add(requirements, contentSource, controlAni);
        MainHudSelectedSkillContentRequirements.Add(requirements, contentSource, controlAni);
        MainHudStatusHintContentRequirements.Add(requirements, contentSource, controlAni);

        if (excludedMapDataIds is not null && !allMaps)
        {
            throw new ArgumentException("Map exclusions require a catalog-wide selection.");
        }

        if (allMaps && mapDataId is not null)
        {
            throw new ArgumentException("Select either a map-data ID or all maps.");
        }

        if (allMaps || mapDataId is not null)
        {
            GameMapCatalog catalog = GameMapCatalog.Load(contentSource);
            IEnumerable<uint> selectedMaps = allMaps ? catalog.MapDataIds.Order() : [mapDataId!.Value];
            foreach (uint selectedMap in selectedMaps)
            {
                if (excludedMapDataIds?.Contains(selectedMap) == true)
                {
                    continue;
                }

                try
                {
                    ClientWorldContentRequirements.Add(requirements, contentSource, catalog, selectedMap);
                }
                catch (Exception exception) when (allMaps &&
                    exception is InvalidDataException or IOException or UnauthorizedAccessException)
                {
                    throw new InvalidDataException($"Unable to resolve content for map data ID {selectedMap}: {exception.Message}", exception);
                }
            }
        }

        return Normalize(requirements);
    }

    public static IReadOnlyList<ClientContentRequirement> ResolveMap(
        IClientContentSource source, GameMapCatalog catalog, uint mapDataId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(catalog);

        List<ClientContentRequirement> requirements = [];
        ClientWorldContentRequirements.Add(requirements, source, catalog, mapDataId);
        return Normalize(requirements);
    }

    private static void AddFrameRequirements(List<ClientContentRequirement> requirements, AniIndexSection section, int expectedFrameCount)
    {
        if (section.FrameCount != expectedFrameCount)
        {
            throw new InvalidDataException($"ANI section [{section.Name}] must contain exactly {expectedFrameCount} frame(s); found {section.FrameCount}.");
        }

        foreach (string framePath in section.FramePaths)
        {
            requirements.Add(new ClientContentRequirement(framePath, ContentLookupMode.LooseThenPackage));
        }
    }

    private static ClientContentRequirement[] Normalize(IEnumerable<ClientContentRequirement> requirements)
    {
        Dictionary<string, ClientContentRequirement> requirementsByPath = new(StringComparer.OrdinalIgnoreCase);

        foreach (ClientContentRequirement requirement in requirements)
        {
            if (requirementsByPath.TryGetValue(requirement.ContentPath, out ClientContentRequirement? existing))
            {
                if (existing.LookupMode != requirement.LookupMode)
                {
                    throw new InvalidOperationException($"Client content path '{requirement.ContentPath}' is required with conflicting lookup modes {existing.LookupMode} and {requirement.LookupMode}.");
                }

                continue;
            }

            requirementsByPath.Add(requirement.ContentPath, requirement);
        }

        return requirementsByPath.Values.OrderBy(static requirement => requirement.ContentPath, StringComparer.Ordinal).ToArray();
    }
}
