using System.Linq;
using Content.Server.Access.Systems;
using Content.Server.Chat.Systems;
using Content.Server.CrewManifest;
using Content.Server.Popups;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Vortex.Communications;
using Content.Shared._Vortex.Station.Components;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CrewManifest;
using Content.Shared.Database;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Vortex.Communications
{
    public sealed class CentcommConsoleSystem : EntitySystem
    {
        [Dependency] private readonly AccessReaderSystem _accessReaderSystem = default!;
        [Dependency] private readonly ChatSystem _chatSystem = default!;
        [Dependency] private readonly CrewManifestSystem _crewManifest = default!;
        [Dependency] private readonly EmergencyShuttleSystem _emergency = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
        [Dependency] private readonly StationSystem _stationSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
        [Dependency] private readonly IGameTiming _timing = default!;

        private const float UIUpdateInterval = 5.0f;

        public override void Initialize()
        {
            SubscribeLocalEvent<CentcommConsoleComponent, CentcommConsoleCallShuttleMessage>(OnCallShuttleMessage);
            SubscribeLocalEvent<CentcommConsoleComponent, CentcommConsoleRecallShuttleMessage>(OnRecallShuttleMessage);
            SubscribeLocalEvent<CentcommConsoleComponent, CentcommConsoleSelectStationMessage>(OnSelectStationMessage);
            SubscribeLocalEvent<CentcommConsoleComponent, MapInitEvent>(OnMapInit);
        }

        public override void Update(float frameTime)
        {
            var query = EntityQueryEnumerator<CentcommConsoleComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                comp.UIUpdateAccumulator += frameTime;

                if (comp.UIUpdateAccumulator < UIUpdateInterval)
                    continue;

                comp.UIUpdateAccumulator -= UIUpdateInterval;

                if (_uiSystem.IsUiOpen(uid, CentcommConsoleUiKey.Key))
                    UpdateUI(uid, comp);
            }

            base.Update(frameTime);
        }




        private void OnMapInit(EntityUid uid, CentcommConsoleComponent component, MapInitEvent args)
        {
            UpdateUI(uid, component);
        }

        private void OnSelectStationMessage(EntityUid uid, CentcommConsoleComponent component, CentcommConsoleSelectStationMessage message)
        {
            component.SelectedStation = EntityManager.GetEntity(message.Station);
            UpdateUI(uid, component);
        }

        private void UpdateUI(EntityUid uid, CentcommConsoleComponent component)
        {
            var canCall = CanCallShuttle(uid);
            var canRecall = CanRecallShuttle(uid);
            var canViewManifest = true; // Manifest enabled

            var stationNamesList = _stationSystem.GetStationNames()
                .Where(s => CompOrNull<StationManifestVisibilityComponent>(EntityManager.GetEntity(s.Entity))?.VisibleInCentcommManifest ?? true)
                .ToList();
            var stationNames = stationNamesList.ToDictionary(x => x.Entity, x => x.Name);
            if (component.SelectedStation == null && stationNames.Count > 0)
            {
                component.SelectedStation = EntityManager.GetEntity(stationNames.Keys.First());
            }

            string selectedStationName = "";
            CrewManifestEntries? manifestEntries = null;
            if (component.SelectedStation != null)
            {
                var netEntity = EntityManager.GetNetEntity(component.SelectedStation.Value);
                if (stationNames.TryGetValue(netEntity, out var name))
                {
                    selectedStationName = name;
                }
                var (manifestName, entries) = _crewManifest.GetCrewManifest(component.SelectedStation.Value);
                manifestEntries = entries;
            }

            if (_uiSystem.HasUi(uid, CentcommConsoleUiKey.Key))
            {
                _uiSystem.SetUiState(uid, CentcommConsoleUiKey.Key, new CentcommConsoleInterfaceState(
                    canCall,
                    canRecall,
                    canViewManifest,
                    _roundEndSystem.ExpectedCountdownEnd,
                    stationNames,
                    component.SelectedStation != null ? EntityManager.GetNetEntity(component.SelectedStation.Value) : null,
                    selectedStationName,
                    manifestEntries
                ));
            }
        }

        private bool CanCallShuttle(EntityUid console)
        {
            return !_emergency.EmergencyShuttleArrived && _roundEndSystem.CanCallOrRecall() && _roundEndSystem.ExpectedCountdownEnd == null;
        }

        private bool CanRecallShuttle(EntityUid console)
        {
            // Can recall if shuttle is called and round end can still be cancelled
            return _roundEndSystem.CanCallOrRecall() && _roundEndSystem.ExpectedCountdownEnd != null;
        }

        private void OnCallShuttleMessage(EntityUid uid, CentcommConsoleComponent component, CentcommConsoleCallShuttleMessage message)
        {
            if (!CanCallShuttle(uid))
                return;

            var player = message.Actor;

            if (!CanUse(player, uid))
            {
                _popupSystem.PopupCursor(Loc.GetString("comms-console-permission-denied"), player, PopupType.Medium);
                return;
            }

            // Validate arrival time (5-30 minutes)
            var arrivalTime = TimeSpan.FromMinutes(message.ArrivalTimeMinutes);
            if (arrivalTime < TimeSpan.FromMinutes(5) || arrivalTime > TimeSpan.FromMinutes(30))
            {
                _popupSystem.PopupCursor("Arrival time must be between 5 and 30 minutes", player, PopupType.Medium);
                return;
            }

            // Temporarily reduce cooldown for CentComm console
            var originalCooldown = _roundEndSystem.DefaultCooldownDuration;
            _roundEndSystem.DefaultCooldownDuration = TimeSpan.FromSeconds(15);

            // Call shuttle with custom arrival time instead of default 10 minutes
            _roundEndSystem.RequestRoundEnd(arrivalTime, uid);

            // Restore original cooldown
            _roundEndSystem.DefaultCooldownDuration = originalCooldown;

            UpdateUI(uid, component);
        }

        private void OnRecallShuttleMessage(EntityUid uid, CentcommConsoleComponent component, CentcommConsoleRecallShuttleMessage message)
        {
            if (!CanRecallShuttle(uid))
                return;

            if (!CanUse(message.Actor, uid))
            {
                _popupSystem.PopupCursor(Loc.GetString("comms-console-permission-denied"), message.Actor, PopupType.Medium);
                return;
            }

            // Temporarily reduce cooldown for CentComm console
            var originalCooldown = _roundEndSystem.DefaultCooldownDuration;
            _roundEndSystem.DefaultCooldownDuration = TimeSpan.FromSeconds(15);

            _roundEndSystem.CancelRoundEndCountdown(uid);

            // Restore original cooldown
            _roundEndSystem.DefaultCooldownDuration = originalCooldown;

            UpdateUI(uid, component);
        }


        private bool CanUse(EntityUid user, EntityUid console)
        {
            if (TryComp<AccessReaderComponent>(console, out var accessReaderComponent))
            {
                return _accessReaderSystem.IsAllowed(user, console, accessReaderComponent);
            }
            return true;
        }
    }
}