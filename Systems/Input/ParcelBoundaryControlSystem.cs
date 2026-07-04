using Game;
using Game.Input;
using Unity.Mathematics;

namespace CustomLandParcel.Systems
{
    /// <summary>
    /// Handles mod keybindings that edit the current save's selected parcel.
    /// </summary>
    public partial class ParcelBoundaryControlSystem : GameSystemBase
    {
        private const float MoveStep = 100f;
        private const float ResizeStep = 100f;
        private const int EditActionCooldownFrames = 30;

        private static readonly string[] ActionNames =
        {
            CustomLandParcelSettings.ToggleEditModeAction,
            CustomLandParcelSettings.MoveNorthAction,
            CustomLandParcelSettings.MoveSouthAction,
            CustomLandParcelSettings.MoveWestAction,
            CustomLandParcelSettings.MoveEastAction,
            CustomLandParcelSettings.GrowAction,
            CustomLandParcelSettings.ShrinkAction
        };

        private ParcelStoreSystem _mParcelStoreSystem;
        private bool _mActionsEnabled;
        private bool _mEditMode;
        private int _mEditActionCooldownFrames;
        private int _mFramesUntilLog;

        protected override void OnCreate()
        {
            base.OnCreate();
            _mParcelStoreSystem = World.GetOrCreateSystemManaged<ParcelStoreSystem>();
            Mod.log.Info(
                "ParcelBoundaryControlSystem enabled. Toggle edit mode with the configured mod keybinding, then move/resize the parcel with configured bindings.");
        }

        protected override void OnUpdate()
        {
            if (Mod.Settings == null)
            {
                return;
            }

            EnsureActionsEnabled();

            if (WasPressed(CustomLandParcelSettings.ToggleEditModeAction))
            {
                _mEditMode = !_mEditMode;
                Mod.log.Info(
                    $"Parcel edit mode {(_mEditMode ? "enabled" : "disabled")}. {_mParcelStoreSystem.GetSummary()}.");
            }

            if (!_mEditMode)
            {
                return;
            }

            if (_mEditActionCooldownFrames > 0)
            {
                _mEditActionCooldownFrames--;
                return;
            }

            var changed = false;
            if (WasPressed(CustomLandParcelSettings.MoveNorthAction))
            {
                _mParcelStoreSystem.MoveSelectedParcel(new float2(0f, MoveStep), "move north hotkey");
                changed = true;
            }

            if (WasPressed(CustomLandParcelSettings.MoveSouthAction))
            {
                _mParcelStoreSystem.MoveSelectedParcel(new float2(0f, -MoveStep), "move south hotkey");
                changed = true;
            }

            if (WasPressed(CustomLandParcelSettings.MoveWestAction))
            {
                _mParcelStoreSystem.MoveSelectedParcel(new float2(-MoveStep, 0f), "move west hotkey");
                changed = true;
            }

            if (WasPressed(CustomLandParcelSettings.MoveEastAction))
            {
                _mParcelStoreSystem.MoveSelectedParcel(new float2(MoveStep, 0f), "move east hotkey");
                changed = true;
            }

            if (WasPressed(CustomLandParcelSettings.GrowAction))
            {
                _mParcelStoreSystem.ResizeSelectedParcel(ResizeStep, "grow hotkey");
                changed = true;
            }

            if (WasPressed(CustomLandParcelSettings.ShrinkAction))
            {
                _mParcelStoreSystem.ResizeSelectedParcel(-ResizeStep, "shrink hotkey");
                changed = true;
            }

            if (changed)
            {
                _mEditActionCooldownFrames = EditActionCooldownFrames;
                _mFramesUntilLog = 300;
                Mod.log.Info(
                    $"Parcel edit action cooldown started for {EditActionCooldownFrames} frames to avoid rapid native area rebuilds.");
                return;
            }

            if (_mFramesUntilLog <= 0)
            {
                Mod.log.Info(
                    $"Parcel edit mode active. {_mParcelStoreSystem.GetSummary()}, moveStep={MoveStep:F0}, resizeStep={ResizeStep:F0}, cooldownFrames={EditActionCooldownFrames}.");
                _mFramesUntilLog = 300;
            }

            _mFramesUntilLog--;
        }

        private static bool WasPressed(string actionName)
        {
            ProxyAction action = Mod.Settings.GetAction(actionName);
            return action != null && action.WasPressedThisFrame();
        }

        private void EnsureActionsEnabled()
        {
            if (_mActionsEnabled)
            {
                return;
            }

            var enabledCount = 0;
            for (var i = 0; i < ActionNames.Length; i++)
            {
                var actionName = ActionNames[i];
                var action = Mod.Settings.GetAction(actionName);
                if (action == null)
                {
                    Mod.log.Warn($"Parcel control action '{actionName}' was not found in InputManager yet.");
                    continue;
                }

                action.shouldBeEnabled = true;
                enabledCount++;
                Mod.log.Info(
                    $"Parcel control action ready: name={actionName}, enabled={action.enabled}, shouldBeEnabled={action.shouldBeEnabled}, isSet={action.isSet}, binding={action}.");
            }

            if (enabledCount != ActionNames.Length) return;
            _mActionsEnabled = true;
            Mod.log.Info($"Parcel control enabled {enabledCount}/{ActionNames.Length} input actions.");
        }
    }
}
