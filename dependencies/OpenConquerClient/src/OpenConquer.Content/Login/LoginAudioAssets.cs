using System.Text.Json;
using System.Text.Json.Serialization;
using OpenConquer.Content.Media;

namespace OpenConquer.Content.Login;

/// <summary>The sound events used by the startup and character-creation stages.</summary>
public enum LoginAudioCue
{
    Music,
    Glide,
    ServerSelect,
    Page,
    Dragon,
    ButtonClick,
    ButtonHover,
    Confirm,
    KeyOne,
    KeyTwo,
    KeyThree,
    Error,
    CharacterHover,
    CharacterSelect,
}

/// <summary>A decoded, signed 16-bit PCM clip.</summary>
public sealed class LoginAudioClip
{
    public LoginAudioClip(int sampleRate, int channelCount, byte[] pcm16)
    {
        ArgumentNullException.ThrowIfNull(pcm16);
        if (sampleRate is < 16000 or > 48000 || channelCount is not (1 or 2) || pcm16.Length == 0 || pcm16.Length % (2 * channelCount) != 0)
        {
            throw new ArgumentException("Invalid login audio format.");
        }

        SampleRate = sampleRate;
        ChannelCount = channelCount;
        Pcm16 = pcm16;
    }

    public int SampleRate
    {
        get;
    }

    public int ChannelCount
    {
        get;
    }

    public ReadOnlyMemory<byte> Pcm16
    {
        get;
    }
}

/// <summary>Startup and role sounds, decoded offline from the retail MP3 files to PCM.</summary>
public static class LoginAudioAssets
{
    private readonly record struct Clip(LoginAudioCue Cue, int SampleRate, int ChannelCount, int Length, string Sha256);

    private static readonly JsonSerializerOptions s_manifestOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter<LoginAudioCue>() },
    };
    private static readonly Clip[] s_clips = LoadManifest();

    public static LoginAudioClip Load(LoginAudioCue cue)
    {
        if (!Enum.IsDefined(cue))
        {
            throw new ArgumentOutOfRangeException(nameof(cue), cue, "Unknown login sound.");
        }

        Clip clip = s_clips[(int)cue];
        byte[] pcm = EmbeddedMedia.Read($"Login/Audio/{cue}.pcm.br", clip.Length, clip.Sha256, compressed: true);
        return new LoginAudioClip(clip.SampleRate, clip.ChannelCount, pcm);
    }

    private static Clip[] LoadManifest()
    {
        using Stream manifest = typeof(LoginAudioAssets).Assembly.GetManifestResourceStream("Assets/Login/Audio/manifest.json") ??
            throw new InvalidDataException("The startup sound manifest is missing.");

        Clip[] clips = JsonSerializer.Deserialize<Clip[]>(manifest, s_manifestOptions) ??
            throw new InvalidDataException("The startup sound manifest is empty.");
        if (clips.Length != Enum.GetValues<LoginAudioCue>().Length)
        {
            throw new InvalidDataException("The startup sound manifest does not enumerate every audio cue.");
        }

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i].Cue != (LoginAudioCue)i)
            {
                throw new InvalidDataException("The startup sound manifest cue order does not match the audio bank.");
            }
        }

        return clips;
    }
}
