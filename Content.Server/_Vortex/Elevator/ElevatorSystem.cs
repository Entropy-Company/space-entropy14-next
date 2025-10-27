using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared.Timing;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared._Vortex.Elevator;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Robust.Shared.Physics.Components;
using Content.Shared.Ghost;
using Robust.Shared.Physics;
using Robust.Server.Audio;
using Robust.Shared.Map;
using Robust.Shared.Audio;
using Robust.Shared.Map.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Content.Server.Doors.Systems;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;

namespace Content.Server._Vortex.Elevator;

public sealed class ElevatorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DoorSystem _doorSystem = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ElevatorButtonComponent, InteractHandEvent>(OnButtonInteract);
        SubscribeLocalEvent<ElevatorButtonComponent, ActivateInWorldEvent>(OnButtonActivate);
    }

    private void SpawnCheckedTimer(Entity<ElevatorComponent> ent, TimeSpan delay, Action action)
    {
        Timer.Spawn(delay, () =>
        {
            if (!Exists(ent) || !ent.Comp.IsMoving)
                return;
            action();
        });
    }

    private void FailMovement(Entity<ElevatorComponent> ent)
    {
        _audio.PlayPvs(ent.Comp.AlarmSound, ent);
        ent.Comp.IsMoving = false;
        UpdateButtonLights(ent);
    }

    private void StopMovement(Entity<ElevatorComponent> ent)
    {
        ent.Comp.IsMoving = false;
        UpdateButtonLights(ent);
    }

    private GasMixture CreateReplacementMixture(bool isIntermediate)
    {
        var mixture = new GasMixture(Atmospherics.CellVolume);
        if (isIntermediate)
        {
            // Clear only harmful/dangerous gases, keep nitrogen and oxygen
            var gasesToRemove = new[] {
                Gas.Plasma, Gas.Tritium, Gas.Frezon, Gas.BZ,
                Gas.Healium, Gas.Nitrium, Gas.NitrousOxide, Gas.CarbonDioxide
            };
            foreach (var gas in gasesToRemove)
            {
                mixture.SetMoles(gas, 0);
            }
            // Keep a minimal breathable atmosphere
            mixture.SetMoles(Gas.Nitrogen, 82);
            mixture.SetMoles(Gas.Oxygen, 22);
        }
        else
        {
            // For non-intermediate floors, just clear everything except minimal amounts
            mixture.SetMoles(Gas.Oxygen, 1);
            mixture.SetMoles(Gas.Nitrogen, 1);
        }
        mixture.Temperature = 293;
        return mixture;
    }

    private TimeSpan GetButtonUseDelay(Entity<ElevatorComponent> elevator, ElevatorButtonComponent button)
    {
        return elevator.Comp.SendDelay + elevator.Comp.IntermediateDelay + TimeSpan.FromSeconds(1);
    }

    private void SetButtonDelay(EntityUid button, Entity<ElevatorComponent> elevator)
    {
        if (TryComp<UseDelayComponent>(button, out var useDelay))
        {
            _useDelay.SetLength((button, useDelay), GetButtonUseDelay(elevator, Comp<ElevatorButtonComponent>(button)));
        }
    }

    private void StartMovement(Entity<ElevatorComponent> ent, string targetFloor)
    {
        MoveToFloorImmediate(ent, ent.Comp.IntermediateFloorId);
        ent.Comp.CurrentFloor = ent.Comp.IntermediateFloorId;

        _audio.PlayPvs(ent.Comp.TravelSound, ent);
        PerformIntermediateMovementCheck(ent, targetFloor);
    }

    private void PerformIntermediateMovementCheck(Entity<ElevatorComponent> ent, string targetFloor)
    {
        Timer.Spawn(ent.Comp.IntermediateDelay, () =>
        {
            if (!Exists(ent))
                return;

            if (!CanMoveWithEntities(ent))
            {
                FailMovement(ent);
                return;
            }

            MoveToFloorImmediate(ent, targetFloor);
            ent.Comp.CurrentFloor = targetFloor;
            OpenDoorsForFloor(ent.Comp.ElevatorId, targetFloor);

            _audio.PlayPvs(ent.Comp.ArrivalSound, ent);

            StopMovement(ent);
        });
    }

    private void MoveToFloorImmediate(Entity<ElevatorComponent> ent, string floorId)
    {
        if (ent.Comp.KillEntitiesInTargetArea)
            KillEntitiesInTargetArea(ent, floorId);
        TeleportToFloor(ent, floorId);
    }

    private List<EntityUid> GetEntitiesInElevator(EntityUid elevatorUid)
    {
        var elevatorTransform = Transform(elevatorUid);
        var aabb = _lookup.GetWorldAABB(elevatorUid, elevatorTransform);
        var intersectingEntities = _lookup.GetEntitiesIntersecting(elevatorTransform.MapID, aabb, LookupFlags.Uncontained);

        var entitiesInElevator = new List<EntityUid>();
        foreach (var entUid in intersectingEntities)
        {
            var entTransform = Transform(entUid);
            if (IsEntityValidForTeleport(entUid, elevatorUid, entTransform))
                entitiesInElevator.Add(entUid);
        }

        // Include buckled entities (entities that are anchored to entities in the elevator)
        var buckledEntities = new List<EntityUid>();
        foreach (var entUid in entitiesInElevator)
        {
            if (TryComp<BuckleComponent>(entUid, out var buckleComp) && buckleComp.BuckledTo is not null)
            {
                var buckledEntity = buckleComp.BuckledTo.Value;
                if (!entitiesInElevator.Contains(buckledEntity) && IsEntityValidForTeleport(buckledEntity, elevatorUid, null, true))
                {
                    buckledEntities.Add(buckledEntity);
                }
            }
        }
        entitiesInElevator.AddRange(buckledEntities);

        return entitiesInElevator;
    }

    private bool IsEntityValidForTeleport(EntityUid entUid, EntityUid elevatorUid, TransformComponent? entTransform = null, bool allowAnchored = false)
    {
        if (entUid == elevatorUid || HasComp<ElevatorDoorComponent>(entUid))
            return false;

        entTransform ??= Transform(entUid);
        if (entTransform.Anchored && !allowAnchored)
            return false;

        if (TryComp<PhysicsComponent>(entUid, out var physics) && physics.BodyType == BodyType.Static)
            return false;

        return true;
    }

    private bool CanMoveWithEntities(Entity<ElevatorComponent> ent)
    {
        return GetEntitiesInElevator(ent.Owner).Count(e => !HasComp<GhostComponent>(e)) <= ent.Comp.MaxEntitiesToTeleport;
    }

    private void TeleportToFloor(EntityUid uid, string floorId)
    {
        var query = EntityQueryEnumerator<ElevatorPointComponent>();
        while (query.MoveNext(out var pointUid, out var pointComp))
        {
            if (pointComp.FloorId != floorId)
                continue;

            var pointTransform = Transform(pointUid);
            var elevatorTransform = Transform(uid);

            var entitiesToTeleport = GetEntitiesInElevator(uid);

            // Unbuckle all buckled entities before teleporting
            foreach (var entUid in entitiesToTeleport)
            {
                if (TryComp<BuckleComponent>(entUid, out var buckleComp) && buckleComp.Buckled)
                {
                    _buckle.Unbuckle(entUid, null);
                }
            }

            var entitiesWithPositions = new List<(EntityUid, Vector2)>();
            foreach (var entUid in entitiesToTeleport)
            {
                var entTransform = Transform(entUid);
                var relativePos = entTransform.LocalPosition - elevatorTransform.LocalPosition;
                entitiesWithPositions.Add((entUid, relativePos));
            }

            if (TryComp<ElevatorComponent>(uid, out var elevatorComp) && (elevatorComp.TransferGases || elevatorComp.ClearGases))
            {
                var oldElevatorTransform = Transform(uid);
                var oldPos = oldElevatorTransform.LocalPosition;
                var offset = pointTransform.LocalPosition - oldPos;

                var gridUid = oldElevatorTransform.GridUid;
                if (gridUid != null && TryComp<GridAtmosphereComponent>(gridUid.Value, out var gridAtmos) && TryComp<GasTileOverlayComponent>(gridUid.Value, out var gasOverlay))
                {
                    var gridEntity = new Entity<GridAtmosphereComponent?, GasTileOverlayComponent?>(gridUid.Value, gridAtmos, gasOverlay);
                    var aabb = _lookup.GetWorldAABB(uid, oldElevatorTransform);
                    var minX = (int)Math.Floor(aabb.Left);
                    var maxX = (int)Math.Ceiling(aabb.Right);
                    var minY = (int)Math.Floor(aabb.Bottom);
                    var maxY = (int)Math.Ceiling(aabb.Top);

                    for (var x = minX; x < maxX; x++)
                    {
                        for (var y = minY; y < maxY; y++)
                        {
                            var sourcePos = new Vector2i(x, y);
                            var targetPos = new Vector2i((int)(x + offset.X), (int)(y + offset.Y));
                            var mixture = _atmosphere.GetTileMixture(gridEntity, null, sourcePos);
                            if (mixture != null)
                            {
                                if (elevatorComp.TransferGases)
                                    _atmosphere.SetTileMixture(gridEntity, null, targetPos, mixture.Clone());
                                if (elevatorComp.ClearGases || elevatorComp.ForceStandardAtmosphere)
                                {
                                    var sourceMixture = _atmosphere.GetTileMixture(gridEntity, null, sourcePos);
                                    if (sourceMixture != null)
                                    {
                                        GasMixture replacementMixture;

                                        if (elevatorComp.ForceStandardAtmosphere)
                                        {
                                            // Always set standard atmosphere (82 N2 + 22 O2)
                                            replacementMixture = new GasMixture(Atmospherics.CellVolume);
                                            replacementMixture.SetMoles(Gas.Nitrogen, 82);
                                            replacementMixture.SetMoles(Gas.Oxygen, 22);
                                            replacementMixture.Temperature = 293;
                                        }
                                        else
                                        {
                                            // Only clear gases if the source tile has dangerous gases
                                            var hasDangerousGases = sourceMixture.GetMoles(Gas.Plasma) > 0 ||
                                                                    sourceMixture.GetMoles(Gas.Tritium) > 0 ||
                                                                    sourceMixture.GetMoles(Gas.Frezon) > 0 ||
                                                                    sourceMixture.GetMoles(Gas.BZ) > 0 ||
                                                                    sourceMixture.GetMoles(Gas.NitrousOxide) > 0 ||
                                                                    sourceMixture.GetMoles(Gas.CarbonDioxide) > Atmospherics.MolesCellStandard * 0.1; // Allow some CO2

                                            if (hasDangerousGases || elevatorComp.CurrentFloor == elevatorComp.IntermediateFloorId)
                                            {
                                                // Create a mixture that preserves existing N2 and O2 but removes dangerous gases
                                                var preservedN2 = sourceMixture.GetMoles(Gas.Nitrogen);
                                                var preservedO2 = sourceMixture.GetMoles(Gas.Oxygen);

                                                replacementMixture = CreateReplacementMixture(elevatorComp.CurrentFloor == elevatorComp.IntermediateFloorId);

                                                // Restore the preserved N2 and O2 amounts
                                                if (preservedN2 > 0)
                                                    replacementMixture.SetMoles(Gas.Nitrogen, preservedN2);
                                                if (preservedO2 > 0)
                                                    replacementMixture.SetMoles(Gas.Oxygen, preservedO2);

                                                _atmosphere.SetTileMixture(gridEntity, null, sourcePos, replacementMixture);
                                            }
                                        }
                                    }

                                    if (floorId == elevatorComp.IntermediateFloorId)
                                    {
                                        var targetMixture = CreateReplacementMixture(true);
                                        _atmosphere.SetTileMixture(gridEntity, null, targetPos, targetMixture);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            _transform.SetCoordinates(uid, pointTransform.Coordinates);

            var newElevatorTransform = Transform(uid);
            var newPos = newElevatorTransform.LocalPosition;

            foreach (var (entUid, relativePos) in entitiesWithPositions)
            {
                var newCoordinates = new EntityCoordinates(newElevatorTransform.ParentUid, newElevatorTransform.LocalPosition + relativePos);
                _transform.SetCoordinates(entUid, newCoordinates);
            }

            break;
        }
    }

    private void HandleButtonPress(Entity<ElevatorButtonComponent> button, Entity<ElevatorComponent> elevator)
    {
        if (elevator.Comp.IsMoving)
            return;
        {
            switch (button.Comp.ButtonType)
            {
                case ElevatorButtonType.CallButton:
                    MoveToFloor(elevator, button.Comp.Floor);
                    break;
                case ElevatorButtonType.SendElevatorUp:
                    MoveUp(elevator);
                    break;
                case ElevatorButtonType.SendElevatorDown:
                    MoveDown(elevator);
                    break;
            }
            SetButtonDelay(button, elevator);
        }
    }

    private void OnButtonInteract(Entity<ElevatorButtonComponent> ent, ref InteractHandEvent args)
    {
        if (!TryFindElevator(ent.Comp.ElevatorId, out var elevator))
            return;
            
        HandleButtonPress(ent, elevator.Value);
        args.Handled = true;
    }

    private void OnButtonActivate(Entity<ElevatorButtonComponent> ent, ref ActivateInWorldEvent args)
    {
        if (!TryFindElevator(ent.Comp.ElevatorId, out var elevator))
            return;

        HandleButtonPress(ent, elevator.Value);
        args.Handled = true;
    }

    private bool TryFindElevator(string elevatorId, [NotNullWhen(true)] out Entity<ElevatorComponent>? ent)
    {
        var query = EntityQueryEnumerator<ElevatorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ElevatorId == elevatorId)
            {
                ent = (uid, comp);
                return true;
            }
        }
        ent = null;
        return false;
    }

    public void MoveToFloor(Entity<ElevatorComponent> ent, string targetFloor)
    {
        if (ent.Comp.IsMoving || !ent.Comp.Floors.Contains(targetFloor) || ent.Comp.CurrentFloor == targetFloor)
            return;

        if (!CanCloseDoorsForFloor(ent.Comp.ElevatorId, ent.Comp.CurrentFloor))
        {
            _audio.PlayPvs(ent.Comp.AlarmSound, ent);
            return;
        }

        ent.Comp.IsMoving = true;
        UpdateButtonLights(ent);

        SpawnCheckedTimer(ent, ent.Comp.SendDelay, () => StartMovement(ent, targetFloor));

        SpawnCheckedTimer(ent, ent.Comp.DoorCloseDelay, () =>
        {
            if (!CanCloseDoorsForFloor(ent.Comp.ElevatorId, ent.Comp.CurrentFloor))
            {
                FailMovement(ent);
            }
            else
            {
                TryCloseDoorsForFloor(ent.Comp.ElevatorId, ent.Comp.CurrentFloor);
            }
        });
    }

    public void MoveUp(Entity<ElevatorComponent> ent)
    {
        var nextFloor = GetNextFloorUp(ent);
        if (nextFloor != null)
            MoveToFloor(ent, nextFloor);
    }

    public void MoveDown(Entity<ElevatorComponent> ent)
    {
        var nextFloor = GetNextFloorDown(ent);
        if (nextFloor != null)
            MoveToFloor(ent, nextFloor);
    }

    private string? GetNextFloor(Entity<ElevatorComponent> ent, bool up)
    {
        if (ent.Comp.IsMoving || ent.Comp.Floors.Count == 0)
            return null;

        var currentIndex = ent.Comp.Floors.IndexOf(ent.Comp.CurrentFloor);
        if (currentIndex == -1)
            return null;

        if (up)
        {
            if (currentIndex <= 0)
                return null;
            return ent.Comp.Floors[currentIndex - 1];
        }
        else
        {
            if (currentIndex >= ent.Comp.Floors.Count - 1)
                return null;
            return ent.Comp.Floors[currentIndex + 1];
        }
    }

    private string? GetNextFloorUp(Entity<ElevatorComponent> ent)
    {
        return GetNextFloor(ent, true);
    }

    private string? GetNextFloorDown(Entity<ElevatorComponent> ent)
    {
        return GetNextFloor(ent, false);
    }

    private void OpenDoorsForFloor(string elevatorId, string floor)
    {
        foreach (var door in GetDoorsForFloor(elevatorId, floor))
        {
            _doorSystem.TryOpen(door);
        }
    }

    private bool CanCloseDoorsForFloor(string elevatorId, string floor)
    {
        if (!TryFindElevator(elevatorId, out var elevator))
            return true;

        foreach (var door in GetDoorsForFloor(elevatorId, floor))
        {
            if (IsDoorBlocked(door, elevator.Value.Comp.DoorBlockCheckRange))
                return false;
        }
        return true;
    }

    private bool TryCloseDoorsForFloor(string elevatorId, string floor)
    {
        if (!TryFindElevator(elevatorId, out var elevator))
            return false;

        EntityUid? lastDoor = null;
        foreach (var door in GetDoorsForFloor(elevatorId, floor))
        {
            if (!_doorSystem.TryClose(door))
                return false;
            lastDoor = door;
        }

        if (lastDoor.HasValue)
        {
            _audio.PlayPvs(elevator.Value.Comp.StartSound, lastDoor.Value);
        }

        return true;
    }

    private bool IsDoorBlocked(EntityUid doorUid, float range)
    {
        if (Deleted(doorUid))
            return false;

        var intersectingEntities = _lookup.GetEntitiesInRange<PhysicsComponent>(Transform(doorUid).Coordinates, range, LookupFlags.Dynamic | LookupFlags.Sensors);
        foreach (var ent in intersectingEntities)
        {
            if (ent.Owner != doorUid && !HasComp<ElevatorDoorComponent>(ent.Owner))
                return true;
        }
        return false;
    }

    private IEnumerable<EntityUid> GetDoorsForFloor(string elevatorId, string floor)
    {
        var query = EntityQueryEnumerator<ElevatorDoorComponent>();
        while (query.MoveNext(out var doorUid, out var doorComp))
        {
            if (doorComp.ElevatorId == elevatorId && doorComp.Floor == floor)
                yield return doorUid;
        }
    }

    private void KillEntitiesInTargetArea(Entity<ElevatorComponent> elevator, string floorId)
    {
        var query = EntityQueryEnumerator<ElevatorPointComponent>();
        while (query.MoveNext(out var pointUid, out var pointComp))
        {
            if (pointComp.FloorId != floorId)
                continue;

            var pointTransform = Transform(pointUid);

            var aabb = _lookup.GetWorldAABB(elevator.Owner, pointTransform);
            var intersectingEntities = _lookup.GetEntitiesIntersecting(pointTransform.MapID, aabb, LookupFlags.Dynamic);

            foreach (var entUid in intersectingEntities)
            {
                if (IsEntityValidForTeleport(entUid, elevator.Owner) && !HasComp<GhostComponent>(entUid))
                {
                    // Check if entity is buckled to something in the elevator area
                    if (TryComp<BuckleComponent>(entUid, out var buckleComp) && buckleComp.BuckledTo is not null)
                    {
                        var buckledTo = buckleComp.BuckledTo.Value;
                        var buckledToTransform = Transform(buckledTo);
                        if (_lookup.GetEntitiesIntersecting(pointTransform.MapID, aabb, LookupFlags.Dynamic).Contains(buckledTo))
                        {
                            // Don't kill buckled entities if their buckled object is also in the area
                            continue;
                        }
                    }

                    var damage = new DamageSpecifier();
                    damage.DamageDict["Blunt"] = 1000;
                    _damageable.TryChangeDamage(entUid, damage, true);
                }
            }
            break;
        }
    }

    private void UpdateButtonLights(Entity<ElevatorComponent> elevator)
    {
        var query = EntityQueryEnumerator<ElevatorButtonComponent>();
        while (query.MoveNext(out var buttonUid, out var buttonComp))
        {
            if (buttonComp.ElevatorId != elevator.Comp.ElevatorId)
                continue;

            ElevatorButtonState state = ElevatorButtonState.ElevatorElsewhere;
            if (buttonComp.ButtonType == ElevatorButtonType.CallButton)
            {
                if (elevator.Comp.IsMoving)
                    state = ElevatorButtonState.ElevatorMoving;
                else if (buttonComp.Floor == elevator.Comp.CurrentFloor)
                    state = ElevatorButtonState.ElevatorHere;
                else
                    state = ElevatorButtonState.ElevatorElsewhere;
            }

            _appearance.SetData(buttonUid, ElevatorButtonVisuals.ButtonState, state);
        }
    }
}