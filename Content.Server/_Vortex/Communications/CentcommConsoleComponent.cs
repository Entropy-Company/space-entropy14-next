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

        // Cooldown tracking - when actions were last used
        [DataField]
        public TimeSpan LastFTLUse;

        [DataField]
        public TimeSpan LastFTLToggleUse;

        [DataField]
        public TimeSpan LastThreatCodeUse;

        // Cooldown durations - configurable
        [DataField]
        public TimeSpan FTLCooldownDuration = TimeSpan.FromSeconds(5);

        [DataField]
        public TimeSpan FTLToggleCooldownDuration = TimeSpan.FromSeconds(5);

        [DataField]
        public TimeSpan ThreatCodeCooldownDuration = TimeSpan.FromSeconds(5);

        // Tab enable/disable configuration
        [DataField]
        public bool CommunicationTabEnabled = true;

        [DataField]
        public bool EvacuationTabEnabled = true;

        [DataField]
        public bool FTLTabEnabled = true;
    }
}