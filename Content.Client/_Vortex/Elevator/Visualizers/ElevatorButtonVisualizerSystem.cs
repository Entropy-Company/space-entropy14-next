using Content.Shared._Vortex.Elevator;
using Robust.Client.GameObjects;

namespace Content.Client._Vortex.Elevator.Visualizers;

public sealed class ElevatorButtonVisualizerSystem : VisualizerSystem<ElevatorButtonVisualsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, ElevatorButtonVisualsComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<ElevatorButtonState>(uid, ElevatorButtonVisuals.ButtonState, out var state, args.Component))
            return;

        if (comp.SpriteStateMap.TryGetValue(state, out var spriteState))
            SpriteSystem.LayerSetRsiState((uid, args.Sprite), ElevatorButtonLayers.Base, spriteState);
    }
}