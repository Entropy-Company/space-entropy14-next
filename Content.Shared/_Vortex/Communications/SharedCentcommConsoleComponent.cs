using Content.Shared.CrewManifest;
using Robust.Shared.Serialization;

namespace Content.Shared._Vortex.Communications
{
    [Virtual]
    public partial class SharedCentcommConsoleComponent : Component
    {
    }

    [Serializable, NetSerializable]
    public sealed class CentcommConsoleInterfaceState : BoundUserInterfaceState
    {
        public readonly bool CanCallShuttle;
        public readonly bool CanRecallShuttle;
        public readonly bool CanViewManifest;
        public readonly bool CanCreateFTLDisk;
        public readonly bool CanToggleBSSCorridor;
        public readonly bool CanApplyThreatCode;
        public readonly bool BSSCorridorOpen;
        public readonly TimeSpan? ExpectedCountdownEnd;
        public readonly bool CountdownStarted;
        public readonly Dictionary<NetEntity, string> StationNames;
        public readonly NetEntity? SelectedStation;
        public readonly string SelectedStationName;
        public readonly CrewManifestEntries? ManifestEntries;
        public readonly Dictionary<string, string> ThreatCodes; // Display name -> Alert level ID

        public CentcommConsoleInterfaceState(bool canCallShuttle, bool canRecallShuttle, bool canViewManifest, bool canCreateFTLDisk, bool canToggleBSSCorridor, bool canApplyThreatCode, bool bssCorridorOpen = false, TimeSpan? expectedCountdownEnd = null, Dictionary<NetEntity, string>? stationNames = null, NetEntity? selectedStation = null, string? selectedStationName = null, CrewManifestEntries? manifestEntries = null, Dictionary<string, string>? threatCodes = null)
        {
            CanCallShuttle = canCallShuttle;
            CanRecallShuttle = canRecallShuttle;
            CanViewManifest = canViewManifest;
            CanCreateFTLDisk = canCreateFTLDisk;
            CanToggleBSSCorridor = canToggleBSSCorridor;
            CanApplyThreatCode = canApplyThreatCode;
            BSSCorridorOpen = bssCorridorOpen;
            ExpectedCountdownEnd = expectedCountdownEnd;
            CountdownStarted = expectedCountdownEnd != null;
            StationNames = stationNames ?? new();
            SelectedStation = selectedStation;
            SelectedStationName = selectedStationName ?? "";
            ManifestEntries = manifestEntries;
            ThreatCodes = threatCodes ?? new();
        }
    }

    [Serializable, NetSerializable]
    public sealed class CentcommConsoleCallShuttleMessage : BoundUserInterfaceMessage
    {
        public readonly float ArrivalTimeMinutes;

        public CentcommConsoleCallShuttleMessage(float arrivalTimeMinutes)
        {
            ArrivalTimeMinutes = arrivalTimeMinutes;
        }
    }

    [Serializable, NetSerializable]
    public sealed class CentcommConsoleRecallShuttleMessage : BoundUserInterfaceMessage
    {
    }

    [Serializable, NetSerializable]
    public sealed class CentcommConsoleViewManifestMessage : BoundUserInterfaceMessage
    {
    }

    [Serializable, NetSerializable]
    public sealed class CentcommConsoleSelectStationMessage : BoundUserInterfaceMessage
    {
        public readonly NetEntity Station;

        public CentcommConsoleSelectStationMessage(NetEntity station)
        {
            Station = station;
        }
    }

    [Serializable, NetSerializable]
    public sealed class CentcommConsoleCreateFTLDiskMessage : BoundUserInterfaceMessage
    {
    }

    [Serializable, NetSerializable]
    public sealed class CentcommConsoleToggleBSSCorridorMessage : BoundUserInterfaceMessage
    {
    }

    [Serializable, NetSerializable]
    public sealed class CentcommConsoleUpdateBSSButtonMessage : BoundUserInterfaceMessage
    {
        public bool IsOpen;
    }

    [Serializable, NetSerializable]
    public sealed class CentcommConsoleRequestBSSStateMessage : BoundUserInterfaceMessage
    {
    }

    [Serializable, NetSerializable]
    public sealed class CentcommConsoleApplyThreatCodeMessage : BoundUserInterfaceMessage
    {
        public readonly NetEntity Station;
        public readonly string ThreatCode;

        public CentcommConsoleApplyThreatCodeMessage(NetEntity station, string threatCode)
        {
            Station = station;
            ThreatCode = threatCode;
        }
    }

    [Serializable, NetSerializable]
    public enum CentcommConsoleUiKey
    {
        Key
    }
}