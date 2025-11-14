using Robust.Shared.GameObjects;

namespace Content.Server._Vortex.Station.Components;

/// <summary>
/// Forces players to spawn in cryo capsules on this station, overriding their spawn preferences.
/// </summary>
[RegisterComponent]
public sealed partial class ForceCryoSpawnComponent : Component
{
    /// <summary>
    /// If true, late-joining players will spawn in cryo capsules regardless of their spawn priority preference.
    /// </summary>
    [DataField]
    public bool ForceLateJoinCryo = false;

    /// <summary>
    /// If true, round-start players will spawn in cryo capsules instead of their job spawn points.
    /// </summary>
    [DataField]
    public bool ForceRoundStartCryo = false;
}