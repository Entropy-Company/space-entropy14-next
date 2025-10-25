using Robust.Shared.Serialization;

namespace Content.Shared._Vortex.Elevator;

[Serializable, NetSerializable]
public enum ElevatorButtonVisuals : byte
{
    ButtonState
}

[Serializable, NetSerializable]
public enum ElevatorButtonState : byte
{
    ElevatorHere,
    ElevatorMoving,
    ElevatorElsewhere
}

public enum ElevatorButtonLayers : byte
{
    Base
}