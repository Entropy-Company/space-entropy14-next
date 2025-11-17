using Robust.Shared.GameObjects;

namespace Content.Shared._Vortex.Station.Components;

/// <summary>
/// Controls whether a station is visible in the Centcomm console manifest selector.
/// </summary>
[RegisterComponent]
public sealed partial class StationManifestVisibilityComponent : Component
{
    /// <summary>
    /// Whether this station should be visible in the Centcomm console manifest selector.
    /// </summary>
    [DataField]
    public bool VisibleInCentcommManifest { get; set; } = true;
}