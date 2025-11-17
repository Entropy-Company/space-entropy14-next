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
        public readonly TimeSpan? ExpectedCountdownEnd;
        public readonly bool CountdownStarted;
        public readonly Dictionary<NetEntity, string> StationNames;
        public readonly NetEntity? SelectedStation;
        public readonly string SelectedStationName;
        public readonly CrewManifestEntries? ManifestEntries;

        public CentcommConsoleInterfaceState(bool canCallShuttle, bool canRecallShuttle, bool canViewManifest, TimeSpan? expectedCountdownEnd = null, Dictionary<NetEntity, string>? stationNames = null, NetEntity? selectedStation = null, string? selectedStationName = null, CrewManifestEntries? manifestEntries = null)
        {
            CanCallShuttle = canCallShuttle;
            CanRecallShuttle = canRecallShuttle;
            CanViewManifest = canViewManifest;
            ExpectedCountdownEnd = expectedCountdownEnd;
            CountdownStarted = expectedCountdownEnd != null;
            StationNames = stationNames ?? new();
            SelectedStation = selectedStation;
            SelectedStationName = selectedStationName ?? "";
            ManifestEntries = manifestEntries;
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
    public enum CentcommConsoleUiKey
    {
        Key
    }
}