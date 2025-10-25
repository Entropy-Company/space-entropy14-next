using Robust.Shared.GameStates;

namespace Content.Server._Vortex.Elevator;

[RegisterComponent]
public sealed partial class ElevatorPointComponent : Component
{
    [DataField]
    public string FloorId = "";
}