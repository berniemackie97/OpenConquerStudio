using OpenConquer.Content.Ani;
using OpenConquer.Content.Magic;
using OpenConquer.Content.Text;

namespace OpenConquer.Content;

internal static class MainHudStatusHintContentRequirements
{
    public const string DialogSectionName = "Dialog21";
    public const string SkillRegionPath = "ini/ProgressXp.rgn";
    public const string ManaRegionPath = "ini/ProgressMp.rgn";
    public const string LifeRegionPath = "ini/ProgressHp.rgn";

    public static void Add(List<ClientContentRequirement> requirements, IClientContentSource contentSource, AniIndexFile controlAni)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentNullException.ThrowIfNull(controlAni);

        requirements.Add(new ClientContentRequirement(ClientStringResources.RelativePath, ContentLookupMode.LooseOnly));
        requirements.Add(new ClientContentRequirement(SkillRegionPath, ContentLookupMode.LooseOnly));
        requirements.Add(new ClientContentRequirement(ManaRegionPath, ContentLookupMode.LooseOnly));
        requirements.Add(new ClientContentRequirement(LifeRegionPath, ContentLookupMode.LooseOnly));

        requirements.Add(new ClientContentRequirement(MagicTypeFile.RelativePath, ContentLookupMode.LooseOnly));
        requirements.Add(new ClientContentRequirement(MagicEffectFile.RelativePath, ContentLookupMode.LooseOnly));
        requirements.Add(new ClientContentRequirement(SubProfessionInfoFile.RelativePath, ContentLookupMode.LooseOnly));

        string? localizationPath = ClientKeyedStringResources.ResolveConfiguredContentPath(contentSource);

        if (localizationPath is not null)
        {
            if (!string.Equals(localizationPath, ClientKeyedStringResources.RetailRelativePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Retail 5517 keyed localization path '{localizationPath}' does not match the verified '{ClientKeyedStringResources.RetailRelativePath}' dependency.");
            }

            requirements.Add(new ClientContentRequirement(localizationPath, ContentLookupMode.LooseOnly));
        }

        AniIndexSection section = controlAni.GetRequiredSection(DialogSectionName);

        if (section.FrameCount != 1)
        {
            throw new InvalidDataException($"ANI section [{DialogSectionName}] must contain exactly 1 frame; found {section.FrameCount}.");
        }

        requirements.Add(new ClientContentRequirement(section.FramePaths[0], ContentLookupMode.LooseThenPackage));
    }
}
