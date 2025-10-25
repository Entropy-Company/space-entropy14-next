namespace Content.Server._Vortex.Elevator;

[RegisterComponent]
public sealed partial class ElevatorButtonComponent : Component
{
    [DataField]
    public string ElevatorId = string.Empty;

    [DataField]
    public ElevatorButtonType ButtonType = ElevatorButtonType.CallButton;

    [DataField]
    public string Floor = string.Empty;

}

public enum ElevatorButtonType
{
    CallButton,
    SendElevatorDown,
    SendElevatorUp,
}
