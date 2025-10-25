namespace Content.Server._Vortex.Elevator;

[RegisterComponent]
public sealed partial class ElevatorDoorComponent : Component
{
    [DataField]
    public string ElevatorId = "";

    [DataField]
    public string Floor = "";
}