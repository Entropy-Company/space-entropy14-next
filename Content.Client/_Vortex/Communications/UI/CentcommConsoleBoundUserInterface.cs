using Content.Shared._Vortex.Communications;
using Content.Shared.CrewManifest;
using Robust.Client.UserInterface;

namespace Content.Client._Vortex.Communications.UI
{
    public sealed class CentcommConsoleBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private CentcommConsoleMenu? _menu;

        private Dictionary<NetEntity, string> _stationNames = new();
        private NetEntity? _selectedStation;
        private string _selectedStationName = "";
        private CrewManifestEntries? _manifestEntries;

        private CentcommManifestWindow? _manifestWindow;

        public CentcommConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            _menu = this.CreateWindow<CentcommConsoleMenu>();
            _menu.OnCallShuttle += CallShuttle;
            _menu.OnRecallShuttle += RecallShuttle;
            _menu.OnViewManifest += ViewManifest;
        }

        private void CallShuttle(float arrivalTimeMinutes)
        {
            SendMessage(new CentcommConsoleCallShuttleMessage(arrivalTimeMinutes));
        }

        private void RecallShuttle()
        {
            SendMessage(new CentcommConsoleRecallShuttleMessage());
        }

        private void ViewManifest()
        {
            _manifestWindow = new CentcommManifestWindow();
            _manifestWindow.OnStationSelected += station => SendMessage(new CentcommConsoleSelectStationMessage(station));
            _manifestWindow.Populate(_stationNames, _selectedStation, _selectedStationName, _manifestEntries);
            _manifestWindow.OpenCentered();
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            if (state is not CentcommConsoleInterfaceState centcommState)
                return;

            _stationNames = centcommState.StationNames;
            _selectedStation = centcommState.SelectedStation;
            _selectedStationName = centcommState.SelectedStationName;
            _manifestEntries = centcommState.ManifestEntries;

            if (_menu != null)
            {
                _menu.CanCallShuttle = centcommState.CanCallShuttle;
                _menu.CanRecallShuttle = centcommState.CanRecallShuttle;
                _menu.CanViewManifest = centcommState.CanViewManifest;
                _menu.CountdownStarted = centcommState.CountdownStarted;
                _menu.CountdownEnd = centcommState.ExpectedCountdownEnd;

                _menu.UpdateCountdown();
                _menu.CallShuttleButton.Disabled = !_menu.CanCallShuttle;
                _menu.RecallShuttleButton.Disabled = !_menu.CanRecallShuttle;
                _menu.ViewManifestButton.Disabled = !_menu.CanViewManifest;
            }

            if (_manifestWindow != null)
            {
                _manifestWindow.Populate(_stationNames, _selectedStation, _selectedStationName, _manifestEntries);
            }
        }
    }
}