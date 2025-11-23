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
        public TimeSpan LastBSSUse;

        [DataField]
        public TimeSpan LastThreatCodeUse;

        // Cooldown durations - configurable
        [DataField]
        public TimeSpan FTLCooldownDuration = TimeSpan.FromSeconds(5);

        [DataField]
        public TimeSpan BSSCooldownDuration = TimeSpan.FromSeconds(5);

        [DataField]
        public TimeSpan ThreatCodeCooldownDuration = TimeSpan.FromSeconds(5);
    }
}