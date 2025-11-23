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
            _menu.OnCreateFTLDisk += CreateFTLDisk;
            _menu.OnToggleBSSCorridor += ToggleBSSCorridor;
            _menu.OnApplyThreatCode += ApplyThreatCode;

            // Request initial FTL state
            SendMessage(new CentcommConsoleRequestBSSStateMessage());
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
            // Close existing manifest window if it's open
            if (_manifestWindow != null)
            {
                _manifestWindow.Close();
                _manifestWindow = null;
            }

            // Create and open new manifest window
            _manifestWindow = new CentcommManifestWindow();
            _manifestWindow.OnStationSelected += station => SendMessage(new CentcommConsoleSelectStationMessage(station));
            _manifestWindow.OnClose += () => _manifestWindow = null; // Clear reference when window is closed
            _manifestWindow.Populate(_stationNames, _selectedStation, _selectedStationName, _manifestEntries);
            _manifestWindow.OpenCentered();
        }

        private void CreateFTLDisk()
        {
            SendMessage(new CentcommConsoleCreateFTLDiskMessage());
        }

        private void ToggleBSSCorridor()
        {
            SendMessage(new CentcommConsoleToggleBSSCorridorMessage());
        }

        private void ApplyThreatCode(NetEntity station, string threatCode)
        {
            SendMessage(new CentcommConsoleApplyThreatCodeMessage(station, threatCode));
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
                _menu.CountdownStarted = centcommState.CountdownStarted;
                _menu.CountdownEnd = centcommState.ExpectedCountdownEnd;

                _menu.UpdateCountdown();
                _menu.CallShuttleButton.Disabled = !centcommState.CanCallShuttle;
                _menu.RecallShuttleButton.Disabled = !centcommState.CanRecallShuttle;
                _menu.ViewManifestButton.Disabled = !centcommState.CanViewManifest;
                _menu.CreateFTLDiskButton.Disabled = !centcommState.CanCreateFTLDisk;
                _menu.ToggleBSSCorridorButton.Disabled = !centcommState.CanToggleBSSCorridor;
                _menu.ApplyThreatCodeButton.Disabled = !centcommState.CanApplyThreatCode;

                // Update BSS button with current state
                _menu.UpdateBSSButton(centcommState.BSSCorridorOpen);

                // Populate communication tab dropdowns
                _menu.PopulateStationSelector(centcommState.StationNames);
                _menu.PopulateThreatCodeSelector(centcommState.ThreatCodes);
            }

            if (_manifestWindow != null)
            {
                _manifestWindow.Populate(_stationNames, _selectedStation, _selectedStationName, _manifestEntries);
            }
        }

        protected override void ReceiveMessage(BoundUserInterfaceMessage message)
        {
            base.ReceiveMessage(message);

            if (message is CentcommConsoleUpdateBSSButtonMessage updateMsg)
            {
                _menu?.UpdateBSSButton(updateMsg.IsOpen);
            }
            else if (message is CentcommConsoleApplyThreatCodeMessage applyMsg)
            {
                _menu?.OnThreatCodeApplied(applyMsg.ThreatCode == "УСПЕШНО!");
            }
        }
    }
}