// SPDX-FileCopyrightText: 2021 moonheart08 <moonheart08@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Chris V <HoofedEar@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Kara <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2022 Kevin Zheng <kevinz5000@gmail.com>
// SPDX-FileCopyrightText: 2022 Veritius <veritiusgaming@gmail.com>
// SPDX-FileCopyrightText: 2022 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 metalgearsloth <comedian_vs_clown@hotmail.com>
// SPDX-FileCopyrightText: 2022 wrexbe <81056464+wrexbe@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._CorvaxGoob.TTS;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.EUI;
using Content.Server.Station.Systems; // Vortex-PlayableCentCom
using Content.Shared.Administration;
using Content.Shared.Chat;
using Content.Shared.Eui;
using Robust.Shared.Audio;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Server.Administration.UI
{
    public sealed class AdminAnnounceEui : BaseEui
    {
        [Dependency] private readonly IAdminManager _adminManager = default!;
        [Dependency] private readonly IChatManager _chatManager = default!;
        private StationSystem _stationSystem = default!; // Vortex-PlayableCentCom
        private readonly TTSSystem _tts; // CorvaxGoob-TTS
        private readonly ChatSystem _chatSystem;
        [Dependency] private readonly IResourceManager _resourceManager = default!;

        public AdminAnnounceEui()
        {
            IoCManager.InjectDependencies(this);
            _chatSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<ChatSystem>();
            _tts = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<TTSSystem>()!; // CorvaxGoob-TTS
            _stationSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<StationSystem>(); // Vortex-PlayableCentCom
        }

        public override void Opened()
        {
            StateDirty();
        }

        public override EuiStateBase GetNewState()
        {
            // Vortex-PlayableCentCom-Edit-Start
            var state = new AdminAnnounceEuiState();
            foreach (var (name, netEntity) in _stationSystem.GetStationNames())
            {
                state.Stations[netEntity] = name;
            }
            return state;
            // Vortex-PlayableCentCom-Edit-End
        }

        public override void HandleMessage(EuiMessageBase msg)
        {
            base.HandleMessage(msg);

            switch (msg)
            {
                case AdminAnnounceEuiMsg.DoAnnounce doAnnounce:
                    if (!_adminManager.HasAdminFlag(Player, AdminFlags.Admin))
                    {
                        Close();
                        break;
                    }

                    var color = Color.Gold;
                    if (!string.IsNullOrWhiteSpace(doAnnounce.ColorHex))
                    {
                        try { color = Color.FromHex(doAnnounce.ColorHex); } catch { color = Color.Gold; }
                    }
                    var sound = new SoundPathSpecifier("/Audio/_CorvaxGoob/Announcements/announce.ogg"); // Vortex-PlayableCentCom-Edit
                    if (!string.IsNullOrWhiteSpace(doAnnounce.SoundPath))
                        sound = new SoundPathSpecifier(doAnnounce.SoundPath.Trim());

                    switch (doAnnounce.AnnounceType)
                    {
                        case AdminAnnounceType.Server:
                            _chatManager.DispatchServerAnnouncement(doAnnounce.Announcement);
                            break;
                        // Vortex-PlayableCentCom-Start
                        case AdminAnnounceType.AllStations:
                            _chatSystem.DispatchGlobalAnnouncement(doAnnounce.Announcement, doAnnounce.Announcer, true, sound, color);
                            _tts.SendTTSAdminAnnouncement(doAnnounce.Announcement, doAnnounce.Voice); // CorvaxGoob-TTS
                            break;
                        case AdminAnnounceType.SpecificStation:
                            if (doAnnounce.SelectedStation.HasValue)
                            {
                                var entityManager = IoCManager.Resolve<IEntityManager>();
                                var stationUid = entityManager.GetEntity(doAnnounce.SelectedStation.Value);
                                _chatSystem.DispatchStationAnnouncement(stationUid, doAnnounce.Announcement, doAnnounce.Announcer, true, sound, color);
                                _tts.SendTTSAdminAnnouncement(doAnnounce.Announcement, doAnnounce.Voice); // CorvaxGoob-TTS
                            }
                            break;
                    }
                    // Vortex-PlayableCentCom-End

                    StateDirty();

                    if (doAnnounce.CloseAfter)
                        Close();

                    break;
            }
        }
    }
}