namespace OpenConquerStudio.Authoring;

public sealed record WorldAsset(Guid Id, string Name, uint MapId, Guid TerrainId, ulong Flags);
