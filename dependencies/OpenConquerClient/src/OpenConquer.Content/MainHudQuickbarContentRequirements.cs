using OpenConquer.Content.Ani;

namespace OpenConquer.Content;

internal static class MainHudQuickbarContentRequirements
{
    private const string MagicAniPath = "ani/Magic.ani";
    private const string ItemMinIconAniPath = "ani/ItemMinIcon.Ani";
    private const string EffectAniPath = "ani/effect.ani";
    private const string VerifiedMissingRetailFramePath = "data/main3/skill38.dds";

    private static readonly string[] s_fixedGlowSections =
    [
        "FireLight",
        "RedLight",
        "BlueLight",
        "RoyalBlueLight",
        "YellowLight",
    ];

    public static void Add(List<ClientContentRequirement> requirements, IClientContentSource contentSource, AniIndexFile controlAni)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentNullException.ThrowIfNull(controlAni);

        AddRequiredControlSection(requirements, controlAni, "Compose_CoverPic");
        AddRequiredControlSection(requirements, controlAni, "Swapuse_UsemainbBtn");
        AddRequiredControlSection(requirements, controlAni, "Swapuse_SwapmainbBtn");
        AddRequiredControlSection(requirements, controlAni, "Equip_AddPic");

        for (int digit = 0; digit <= 9; digit++)
        {
            AddRequiredControlSection(requirements, controlAni, $"Main3_Num{digit}Pic");
            AddRequiredControlSection(requirements, controlAni, $"Equip_Num{digit}");
        }

        foreach (AniIndexSection section in controlAni.Sections)
        {
            if (HasUnsignedDecimalPayload(section.Name, "ButtonA", string.Empty) ||
                HasUnsignedDecimalPayload(section.Name, "Action_Dance", "Btn"))
            {
                AddFrameRequirements(requirements, section);
            }
        }

        requirements.Add(new ClientContentRequirement(MagicAniPath, ContentLookupMode.LooseThenPackage));
        requirements.Add(new ClientContentRequirement(ItemMinIconAniPath, ContentLookupMode.LooseThenPackage));
        requirements.Add(new ClientContentRequirement(EffectAniPath, ContentLookupMode.LooseThenPackage));

        AniIndexFile magicAni = AniIndexFile.Load(contentSource, MagicAniPath, ContentLookupMode.LooseThenPackage);
        AniIndexFile itemMinIconAni = AniIndexFile.Load(contentSource, ItemMinIconAniPath, ContentLookupMode.LooseThenPackage);
        AniIndexFile effectAni = AniIndexFile.Load(contentSource, EffectAniPath, ContentLookupMode.LooseThenPackage);

        foreach (AniIndexSection section in magicAni.Sections)
        {
            if (HasUnsignedDecimalPayload(section.Name, "MagicSkillType", string.Empty) ||
                HasUnsignedDecimalPayload(section.Name, "XpSkillType", string.Empty))
            {
                AddFrameRequirements(requirements, section);
            }
        }

        foreach (AniIndexSection section in itemMinIconAni.Sections)
        {
            if (string.Equals(section.Name, "ItemDefault", StringComparison.Ordinal) ||
                HasUnsignedDecimalPayload(section.Name, "Item", string.Empty))
            {
                AddFrameRequirements(requirements, section);
            }
        }

        foreach (string sectionName in s_fixedGlowSections)
        {
            AddFrameRequirements(requirements, effectAni.GetRequiredSection(sectionName));
        }
    }

    private static void AddRequiredControlSection(List<ClientContentRequirement> requirements, AniIndexFile controlAni, string sectionName) =>
        AddFrameRequirements(requirements, controlAni.GetRequiredSection(sectionName));

    private static void AddFrameRequirements(List<ClientContentRequirement> requirements, AniIndexSection section)
    {
        foreach (string framePath in section.FramePaths)
        {
            if (!string.Equals(framePath, VerifiedMissingRetailFramePath, StringComparison.OrdinalIgnoreCase))
            {
                requirements.Add(new ClientContentRequirement(framePath, ContentLookupMode.LooseThenPackage));
            }
        }
    }

    private static bool HasUnsignedDecimalPayload(string value, string prefix, string suffix)
    {
        if (!value.StartsWith(prefix, StringComparison.Ordinal) || !value.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        int start = prefix.Length;
        int length = value.Length - prefix.Length - suffix.Length;

        if (length <= 0)
        {
            return false;
        }

        for (int index = start; index < start + length; index++)
        {
            if (value[index] is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }
}
