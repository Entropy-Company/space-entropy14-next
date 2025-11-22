using Content.Shared._Vortex.Communications;

namespace Content.Server._Vortex.Communications
{
    [RegisterComponent]
    public sealed partial class CentcommConsoleComponent : SharedCentcommConsoleComponent
    {
        [DataField]
        public float UIUpdateAccumulator;

        [DataField]
        public EntityUid? SelectedStation;

        [DataField]
        public TimeSpan FTLCooldownEndTime;

        [DataField]
        public TimeSpan BSSCooldownEndTime;

        [DataField]
        public TimeSpan ThreatCodeCooldownEndTime;

        [DataField]
        public TimeSpan ThreatCodeCooldownDuration = TimeSpan.FromSeconds(5);
    }
}