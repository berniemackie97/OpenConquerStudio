using System.Text;

namespace OpenConquer.Content.Map;

public static class MapAssetId
{
    public static void Validate(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 260 || !value.IsNormalized(NormalizationForm.FormC))
        {
            throw new ArgumentException("Asset ID is not canonical.", nameof(value));
        }

        bool start = true;
        foreach (char c in value)
        {
            if (c == '/')
            {
                if (start)
                {
                    throw new ArgumentException("Asset ID contains an empty segment.", nameof(value));
                }
                start = true;
                continue;
            }

            if (c is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-' or '.'))
            {
                throw new ArgumentException("Asset IDs use lowercase ASCII characters.", nameof(value));
            }
            start = false;
        }

        if (start || value.Split('/').Any(segment => segment is "." or ".." || segment.EndsWith('.')))
        {
            throw new ArgumentException("Asset ID contains an invalid segment.", nameof(value));
        }
    }
}
