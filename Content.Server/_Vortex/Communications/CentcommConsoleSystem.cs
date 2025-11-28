using System.Linq;
using Content.Server.Access.Systems;
using Content.Server.Administration.Logs;
using Content.Server.AlertLevel;
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
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Map.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Localization;
using Robust.Shared.GameObjects;
using Content.Shared.UserInterface;

namespace Content.Server._Vortex.Communications
{
    public sealed class CentcommConsoleSystem : EntitySystem
    {
        [Dependency] private readonly AccessReaderSystem _accessReaderSystem = default!;
        [Dependency] private readonly AlertLevelSystem _alertLevelSystem = default!;
        [Dependency] private readonly ChatSystem _chatSystem = default!;
        [Dependency] private readonly CrewManifestSystem _crewManifest = default!;
        [Dependency] private readonly EmergencyShuttleSystem _emergency = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
        [Dependency] private readonly StationSystem _stationSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly IAdminLogManager _adminLogger = default!;
        [Dependency] private readonly ILocalizationManager _loc = default!;
        [Dependency] private readonly SharedMapSystem _mapSystem = default!;
        [Dependency] private readonly LabelSystem _labelSystem = default!;
        [Dependency] private readonly SharedStorageSystem _storageSystem = default!;

        private const float UIUpdateInterval = 5.0f;

        // Cache for visible stations to avoid expensive lookups
        private Dictionary<NetEntity, string>? _cachedVisibleStations;
        private TimeSpan _lastCacheUpdate;
        private const float StationCacheDuration = 10.0f; // Cache for 10 seconds

        private Dictionary<NetEntity, string> GetVisibleStations()
        {
            // Return cached result if still valid
            if (_cachedVisibleStations != null &&
                (_timing.CurTime - _lastCacheUpdate).TotalSeconds < StationCacheDuration)
            {
                return _cachedVisibleStations;
            }

            // Rebuild cache with optimized filtering
            return _cachedVisibleStations = FilterVisibleStations(_stationSystem.GetStationNames());
        }

        private Dictionary<NetEntity, string> FilterVisibleStations(List<(string Name, NetEntity Entity)> allStations)
        {
            var visibleStations = new Dictionary<NetEntity, string>();
            var visibleStationEntities = GetVisibleStationEntities();

            foreach (var (name, netEntity) in allStations)
            {
                var stationEntity = EntityManager.GetEntity(netEntity);
                if (IsStationVisible(stationEntity, visibleStationEntities))
                {
                    visibleStations[netEntity] = name;
                }
            }

            _lastCacheUpdate = _timing.CurTime;
            return visibleStations;
        }

        private HashSet<EntityUid> GetVisibleStationEntities()
        {
            var visibleEntities = new HashSet<EntityUid>();
            var query = EntityQueryEnumerator<StationManifestVisibilityComponent>();

            while (query.MoveNext(out var uid, out var visibilityComp))
            {
                if (visibilityComp.VisibleInCentcommManifest)
                    visibleEntities.Add(uid);
            }

            return visibleEntities;
        }

        private bool IsStationVisible(EntityUid stationEntity, HashSet<EntityUid> visibleEntities)
        {
            return visibleEntities.Contains(stationEntity) ||
                   !TryComp<StationManifestVisibilityComponent>(stationEntity, out _);
        }

        public override void Initialize()
        {
            SubscribeLocalEvent<CentcommConsoleComponent, CentcommConsoleCallShuttleMessage>(OnCallShuttleMessage);
            SubscribeLocalEvent<CentcommConsoleComponent, CentcommConsoleRecallShuttleMessage>(OnRecallShuttleMessage);
            SubscribeLocalEvent<CentcommConsoleComponent, CentcommConsoleSelectStationMessage>(OnSelectStationMessage);
            SubscribeLocalEvent<CentcommConsoleComponent, CentcommConsoleCreateFTLDiskMessage>(OnCreateFTLDiskMessage);
            SubscribeLocalEvent<CentcommConsoleComponent, CentcommConsoleToggleFTLCorridorMessage>(OnToggleFTLCorridorMessage);
            SubscribeLocalEvent<CentcommConsoleComponent, CentcommConsoleRequestFTLStateMessage>(OnRequestFTLStateMessage);
            SubscribeLocalEvent<CentcommConsoleComponent, CentcommConsoleApplyThreatCodeMessage>(OnApplyThreatCodeMessage);
            SubscribeLocalEvent<CentcommConsoleComponent, BoundUserInterfaceCheckRangeEvent>(OnBoundUserInterfaceCheckRange);
            SubscribeLocalEvent<CentcommConsoleComponent, MapInitEvent>(OnMapInit);
            SubscribeLocalEvent<CentcommConsoleComponent, BoundUIOpenedEvent>(OnBoundUserInterfaceOpen);

            // Subscribe to station changes to invalidate cache
            SubscribeLocalEvent<StationInitializedEvent>(OnStationChanged);
            SubscribeLocalEvent<StationGridAddedEvent>(OnStationChanged);
            SubscribeLocalEvent<StationGridRemovedEvent>(OnStationChanged);
            SubscribeLocalEvent<StationRenamedEvent>(OnStationChanged);

            // Pre-cache stations on initialization to speed up first access
            GetVisibleStations();
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

        private void OnBoundUserInterfaceOpen(EntityUid uid, CentcommConsoleComponent component, BoundUIOpenedEvent args)
        {
            // Check if any tabs are enabled before allowing UI to open
            if (!component.CommunicationTabEnabled && !component.EvacuationTabEnabled && !component.FTLTabEnabled)
            {
                // Close the UI immediately if no tabs are enabled
                _uiSystem.CloseUi(uid, CentcommConsoleUiKey.Key, args.Actor);
                _popupSystem.PopupCursor(Loc.GetString("centcomm-console-disabled"), args.Actor, PopupType.Medium);
                return;
            }

            // Send immediate UI update when interface opens
            UpdateUI(uid, component);
        }

        private void OnStationChanged(EntityEventArgs args)
        {
            // Invalidate station cache when stations change
            _cachedVisibleStations = null;
        }

        private void OnSelectStationMessage(EntityUid uid, CentcommConsoleComponent component, CentcommConsoleSelectStationMessage message)
        {
            component.SelectedStation = EntityManager.GetEntity(message.Station);
            UpdateUI(uid, component);
        }

        private void OnBoundUserInterfaceCheckRange(EntityUid uid, CentcommConsoleComponent component, ref BoundUserInterfaceCheckRangeEvent args)
        {
            if (args.Result == BoundUserInterfaceRangeResult.Fail)
                return;

            // Check if user has access to the console
            if (!CanUse(args.Actor, uid))
            {
                args.Result = BoundUserInterfaceRangeResult.Fail;
                _popupSystem.PopupCursor(Loc.GetString("comms-console-permission-denied"), args.Actor, PopupType.Medium);
                return;
            }

            // Tab availability check is now handled in OnBoundUserInterfaceOpen to prevent UI from opening at all
        }

        private void UpdateUI(EntityUid uid, CentcommConsoleComponent component)
        {
            var canCall = CanCallShuttle(uid);
            var canRecall = CanRecallShuttle(uid);
            var canViewManifest = true; // Manifest enabled
            var canCreateFTLDisk = _timing.CurTime >= component.LastFTLUse + component.FTLCooldownDuration;
            var canToggleFTLCorridor = _timing.CurTime >= component.LastFTLToggleUse + component.FTLToggleCooldownDuration;
            var canApplyThreatCode = _timing.CurTime >= component.LastThreatCodeUse + component.ThreatCodeCooldownDuration;

            // Get current FTL corridor state
            var ftlCorridorOpen = GetFTLCorridorState();

            // Get cached visible stations
            var stationNames = GetVisibleStations();
            if (component.SelectedStation == null && stationNames.Count > 0)
            {
                component.SelectedStation = EntityManager.GetEntity(stationNames.Keys.First());
            }

            string selectedStationName = "";
            CrewManifestEntries? manifestEntries = null;
            Dictionary<string, string> threatCodes = new(); // Display name -> Alert level ID

            if (component.SelectedStation != null)
            {
                var netEntity = EntityManager.GetNetEntity(component.SelectedStation.Value);
                if (stationNames.TryGetValue(netEntity, out var name))
                {
                    selectedStationName = name;
                }
                var (manifestName, entries) = _crewManifest.GetCrewManifest(component.SelectedStation.Value);
                manifestEntries = entries;

                // Get available threat codes for this station
                if (TryComp<AlertLevelComponent>(component.SelectedStation.Value, out var alertComp) && alertComp.AlertLevels != null)
                {
                    foreach (var (id, detail) in alertComp.AlertLevels.Levels)
                    {
                        // Centcomm can only select alert levels that are marked as centcommSelectable
                        if (detail.CentcommSelectable)
                        {
                            // Get localized display name
                            if (_loc.TryGetString($"alert-level-{id}", out var displayName))
                            {
                                threatCodes[displayName] = id;
                            }
                        }
                    }
                }
            }

            if (_uiSystem.HasUi(uid, CentcommConsoleUiKey.Key))
            {
                _uiSystem.SetUiState(uid, CentcommConsoleUiKey.Key, new CentcommConsoleInterfaceState(
                    canCall,
                    canRecall,
                    canViewManifest,
                    canCreateFTLDisk,
                    canToggleFTLCorridor,
                    canApplyThreatCode,
                    ftlCorridorOpen,
                    _roundEndSystem.ExpectedCountdownEnd,
                    stationNames,
                    component.SelectedStation != null ? EntityManager.GetNetEntity(component.SelectedStation.Value) : null,
                    selectedStationName,
                    manifestEntries,
                    threatCodes,
                    component.CommunicationTabEnabled,
                    component.EvacuationTabEnabled,
                    component.FTLTabEnabled
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

        private EntityUid? FindCentComStation()
        {
            var stations = _stationSystem.GetStations();
            foreach (var station in stations)
            {
                if (TryComp<MetaDataComponent>(station, out var meta) &&
                    meta.EntityPrototype?.ID == "StandardGameCentcommStation")
                {
                    return station;
                }
            }
            return null;
        }

        private (EntityUid Map, StationDataComponent StationData)? GetCentComMapData()
        {
            var centComStation = FindCentComStation();
            if (centComStation == null) return null;

            var stationData = Comp<StationDataComponent>(centComStation.Value);
            if (stationData.Grids.Count == 0) return null;

            var mapId = Transform(stationData.Grids.First()).MapID;
            return _mapSystem.TryGetMap(mapId, out var mapUid)
                ? (mapUid.Value, stationData)
                : null;
        }

        private bool GetFTLCorridorState()
        {
            var mapData = GetCentComMapData();
            return mapData != null &&
                   TryComp<FTLDestinationComponent>(mapData.Value.Map, out var ftlComp) &&
                   !ftlComp.RequireCoordinateDisk;
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

        private void OnCreateFTLDiskMessage(EntityUid uid, CentcommConsoleComponent comp, CentcommConsoleCreateFTLDiskMessage message)
        {
            if (message.Actor is not { Valid: true } mob)
                return;

            if (!CanUse(mob, uid))
            {
                _popupSystem.PopupEntity(Loc.GetString("comms-console-permission-denied"), uid, message.Actor);
                return;
            }

            // Cooldown check
            if (_timing.CurTime < comp.LastFTLUse + comp.FTLCooldownDuration)
            {
                var remainingTime = (comp.LastFTLUse + comp.FTLCooldownDuration - _timing.CurTime).TotalSeconds;
                _popupSystem.PopupEntity($"Действие недоступно. Подождите {remainingTime:F1} секунд.", uid, message.Actor);
                return;
            }

            var mapData = GetCentComMapData();
            if (mapData == null)
            {
                _popupSystem.PopupEntity("CentCom station not found.", uid, message.Actor);
                return;
            }

            var dest = mapData.Value.Map;

            // Check if the map is initialized
            if (!TryComp<MapComponent>(dest, out var mapComp) || !mapComp.MapInitialized)
            {
                _popupSystem.PopupEntity("CentCom map not initialized.", uid, message.Actor);
                return;
            }

            // Ensure the destination has FTLDestinationComponent
            if (!TryComp<FTLDestinationComponent>(dest, out var ftlDestComp))
            {
                var ftlDest = EntityManager.AddComponent<FTLDestinationComponent>(dest);
                ftlDest.RequireCoordinateDisk = true;

                if (HasComp<MapGridComponent>(dest))
                {
                    ftlDest.BeaconsOnly = true;
                }
            }

            // Spawn the disk at the console
            var coords = Transform(uid).Coordinates;

            // create the FTL disk
            EntityUid cdUid = Spawn("CoordinatesDisk", coords);
            var cd = EntityManager.EnsureComponent<ShuttleDestinationCoordinatesComponent>(cdUid);
            cd.Destination = dest;
            Dirty(cdUid, cd);

            // create disk case
            EntityUid cdCaseUid = Spawn("DiskCase", coords);

            // apply labels
            if (TryComp<MetaDataComponent>(dest, out var destMeta) && destMeta != null && destMeta.EntityName != null)
            {
                _labelSystem.Label(cdUid, destMeta.EntityName);
                _labelSystem.Label(cdCaseUid, destMeta.EntityName);
            }

            // if the case has a storage, try to place the disk in there
            if (TryComp<StorageComponent>(cdCaseUid, out var storage) && _storageSystem.Insert(cdCaseUid, cdUid, out _, storageComp: storage, playSound: false))
            {
                // Disk inserted into case successfully
            }
            else
            {
                // Something went wrong, delete the case and just spawn the disk
                EntityManager.DeleteEntity(cdCaseUid);
            }

            _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{EntityManager.ToPrettyString(message.Actor):player} created an FTL disk to CentCom.");

            // Update cooldown
            comp.LastFTLUse = _timing.CurTime;

            UpdateUI(uid, comp);
        }

        private bool CanUse(EntityUid user, EntityUid console)
        {
            if (TryComp<AccessReaderComponent>(console, out var accessReaderComponent))
            {
                return _accessReaderSystem.IsAllowed(user, console, accessReaderComponent);
            }
            return true;
        }

        private void OnToggleFTLCorridorMessage(EntityUid uid, CentcommConsoleComponent comp, CentcommConsoleToggleFTLCorridorMessage message)
        {
            // Cooldown check
            if (_timing.CurTime < comp.LastFTLToggleUse + comp.FTLToggleCooldownDuration)
            {
                var remainingTime = (comp.LastFTLToggleUse + comp.FTLToggleCooldownDuration - _timing.CurTime).TotalSeconds;
                _popupSystem.PopupEntity($"Действие недоступно. Подождите {remainingTime:F1} секунд.", uid, message.Actor);
                return;
            }

            var mapData = GetCentComMapData();
            if (mapData == null)
            {
                _popupSystem.PopupEntity("CentCom station not found.", uid, message.Actor);
                return;
            }

            var mapUid = mapData.Value.Map;

            if (!TryComp<FTLDestinationComponent>(mapUid, out var ftlComp))
            {
                _popupSystem.PopupEntity("CentCom map has no FTL destination component.", uid, message.Actor);
                return;
            }

            // Toggle RequireCoordinateDisk
            ftlComp.RequireCoordinateDisk = !ftlComp.RequireCoordinateDisk;
            Dirty(mapUid, ftlComp);

            // Send update to client
            _uiSystem.ServerSendUiMessage(uid, CentcommConsoleUiKey.Key, new CentcommConsoleUpdateFTLButtonMessage { IsOpen = !ftlComp.RequireCoordinateDisk }, message.Actor);

            // Popup the status
            var status = ftlComp.RequireCoordinateDisk ? "Закрыт" : "Открыт";
            _popupSystem.PopupEntity($"БСС Коридор: {status}", uid, message.Actor);

            // Update cooldown
            comp.LastFTLToggleUse = _timing.CurTime;

            UpdateUI(uid, comp);
        }

        private void OnRequestFTLStateMessage(EntityUid uid, CentcommConsoleComponent comp, CentcommConsoleRequestFTLStateMessage message)
        {
            var mapData = GetCentComMapData();
            if (mapData == null) return;

            if (!TryComp<FTLDestinationComponent>(mapData.Value.Map, out var ftlComp)) return;

            _uiSystem.ServerSendUiMessage(uid, CentcommConsoleUiKey.Key,
                new CentcommConsoleUpdateFTLButtonMessage { IsOpen = !ftlComp.RequireCoordinateDisk }, message.Actor);
        }

        private void OnApplyThreatCodeMessage(EntityUid uid, CentcommConsoleComponent comp, CentcommConsoleApplyThreatCodeMessage message)
        {
            if (message.Actor is not { Valid: true } mob)
                return;

            if (!CanUse(mob, uid))
            {
                _popupSystem.PopupCursor(Loc.GetString("comms-console-permission-denied"), message.Actor, PopupType.Medium);
                return;
            }

            // Check Centcomm-specific cooldown
            if (_timing.CurTime < comp.LastThreatCodeUse + comp.ThreatCodeCooldownDuration)
            {
                var remainingTime = (comp.LastThreatCodeUse + comp.ThreatCodeCooldownDuration - _timing.CurTime).TotalSeconds;
                _popupSystem.PopupCursor($"Действие недоступно. Подождите {remainingTime:F1} секунд.", message.Actor, PopupType.Medium);
                return;
            }

            var targetStation = EntityManager.GetEntity(message.Station);

            // Get the alert level ID from the threat code name
            // The message.ThreatCode contains the localized display name (Russian)
            // We need to find the corresponding alert level ID
            string alertLevelId = GetAlertLevelIdFromDisplayName(targetStation, message.ThreatCode);

            if (string.IsNullOrEmpty(alertLevelId))
            {
                _popupSystem.PopupCursor("Неизвестный код угрозы", message.Actor, PopupType.Medium);
                return;
            }

            _alertLevelSystem.SetLevel(targetStation, alertLevelId, true, true, true); // force=true allows setting non-selectable levels

            _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{EntityManager.ToPrettyString(message.Actor):player} applied threat code '{message.ThreatCode}' to station {EntityManager.ToPrettyString(targetStation):station}.");

            // Set Centcomm-specific cooldown
            comp.LastThreatCodeUse = _timing.CurTime;

            // Immediately update UI to reflect the new alert level
            UpdateUI(uid, comp);

            // Send success message back to client
            _uiSystem.ServerSendUiMessage(uid, CentcommConsoleUiKey.Key, new CentcommConsoleApplyThreatCodeMessage(message.Station, "Успешно!"), message.Actor);
        }

        private string GetAlertLevelIdFromDisplayName(EntityUid station, string displayName)
        {
            if (!TryComp<AlertLevelComponent>(station, out var alertComp) || alertComp.AlertLevels == null)
                return string.Empty;

            // Find the alert level ID that has this display name
            foreach (var (id, _) in alertComp.AlertLevels.Levels)
            {
                // Get the localized name for this alert level
                if (_loc.TryGetString($"alert-level-{id}", out var localizedName) && localizedName == displayName)
                {
                    return id;
                }
            }

            return string.Empty;
        }
    }
}