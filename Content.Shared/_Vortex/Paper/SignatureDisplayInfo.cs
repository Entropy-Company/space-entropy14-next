using System;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Vortex.Paper;

/// <summary>
/// Contains information about a signature for display purposes.
/// Includes font styling information for rendering signatures.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public partial struct SignatureDisplayInfo
{
    /// <summary>
    /// The name of the person who signed.
    /// </summary>
    [DataField("signedName")]
    public string SignedName;

    /// <summary>
    /// The font prototype ID to use for this signature.
    /// </summary>
    [DataField("fontId")]
    public string FontId;

    /// <summary>
    /// The font size for this signature.
    /// </summary>
    [DataField("fontSize")]
    public int FontSize;

    /// <summary>
    /// The color of this signature.
    /// </summary>
    [DataField("signColor")]
    public Color SignColor;
}