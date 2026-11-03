using OpenConquer.Content.Media;

namespace OpenConquer.Content.Flash;

/// <summary>
/// The retail 5517 create-role movies, baked offline (see <c>docs/startup/ROLE-001A.md</c>) and embedded under
/// <c>Assets/Role/</c>.
/// </summary>
public static class RoleMovies
{
    /// <summary><c>data/main/role.dat</c>: the create-role screen.</summary>
    public static FlashMovie LoadRole() =>
        Load("role", 354188, "890F8F74BEC7F794F71EE7AC89487F55346AF1C2EF9F0F4D5CC622396165B90E");

    /// <summary><c>data/main/flash/characterInAndOut.swf</c>: the class portraits role.dat loads into its stage.</summary>
    public static FlashMovie LoadCharacterInAndOut() =>
        Load("characterInAndOut", 273802, "8A8FF5020AA0AFE8C235D5F5CFCD4663706C58378C878E1400E3331FEC591F4B");

    /// <summary><c>data/main/flash/logo.swf</c>: the animated title role.dat loads into <c>logoMC</c>.</summary>
    public static FlashMovie LoadLogo() =>
        Load("logo", 3364, "3E3E20456681C9ED24D86FF510BEA5D5AAD8AD9AE0979A0F067A14ACB4CD01AB");

    private static FlashMovie Load(string name, int length, string sha256)
    {
        string directory = $"Role/{name}";
        byte[] json = EmbeddedMedia.Read($"{directory}/{name}.movie.json.br", length, sha256, compressed: true);
        return FlashMovieReader.Read(json, directory);
    }
}
