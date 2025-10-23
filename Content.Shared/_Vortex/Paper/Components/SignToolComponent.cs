using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Vortex.Paper;

/// <summary>
/// Component for tools that can be used for signing documents.
/// Defines the appearance and style of signatures created with this tool.
/// </summary>
[RegisterComponent]
public sealed partial class SignToolComponent : Component
{
    /// <summary>
    /// The font prototype ID to use for signatures.
    /// </summary>
    [DataField("font")]
    public string FontId = "Sign";

    /// <summary>
    /// The font size for signatures.
    /// </summary>
    [DataField("fontSize")]
    public int FontSize = 16;

    /// <summary>
    /// The color of the signature text.
    /// </summary>
    [DataField("signColor")]
    public Color SignColor = Color.Black;
}