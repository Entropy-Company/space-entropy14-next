namespace Content.Shared.Paper;

[RegisterComponent]
public sealed partial class TicketMachineComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly), DataField]
    public int Queue = 0;

    public string TicketProto = "PaperTicket";
}
