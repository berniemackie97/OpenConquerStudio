using OpenConquer.Content.Ani;

namespace OpenConquer.Content;

internal static class MainHudSelectedSkillContentRequirements
{
    private const string MagicAniPath = "ani/Magic.ani";
    private const string InitialSectionName = "Magic0";
    private const string CoverSectionName = "Image0";
    private const int ExpectedFrameCount = 1;

    public static void Add(List<ClientContentRequirement> requirements, IClientContentSource contentSource, AniIndexFile controlAni)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentNullException.ThrowIfNull(controlAni);

        AddRequiredFrame(requirements, controlAni.GetRequiredSection(CoverSectionName));

        requirements.Add(new ClientContentRequirement(MagicAniPath, ContentLookupMode.LooseThenPackage));

        AniIndexFile magicAni = AniIndexFile.Load(contentSource, MagicAniPath, ContentLookupMode.LooseThenPackage);
        AddRequiredFrame(requirements, magicAni.GetRequiredSection(InitialSectionName));
    }

    private static void AddRequiredFrame(List<ClientContentRequirement> requirements, AniIndexSection section)
    {
        if (section.FrameCount != ExpectedFrameCount)
        {
            throw new InvalidDataException($"ANI section [{section.Name}] must contain exactly {ExpectedFrameCount} frame; found {section.FrameCount}.");
        }

        requirements.Add(new ClientContentRequirement(section.FramePaths[0], ContentLookupMode.LooseThenPackage));
    }
}
