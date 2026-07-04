using Game;
using Game.Input;
using Unity.Mathematics;

namespace CustomLandParcel.Systems
{
    /// <summary>
    /// Handles mod keybindings that edit the current save's parcel bounds.
    /// </summary>
    public partial class ParcelBoundaryControlSystem : GameSystemBase
    {
        private const float MoveStep = 100f;
        private const float ResizeStep = 100f;

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

        private ParcelBoundsSystem m_ParcelBoundsSystem;
        private bool m_ActionsEnabled;
        private bool m_EditMode;
        private int m_FramesUntilLog;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ParcelBoundsSystem = World.GetOrCreateSystemManaged<ParcelBoundsSystem>();
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
                m_EditMode = !m_EditMode;
                Mod.log.Info(
                    $"Parcel edit mode {(m_EditMode ? "enabled" : "disabled")}. parcel={m_ParcelBoundsSystem.Bounds}, parcelVersion={m_ParcelBoundsSystem.Version}.");
            }

            if (!m_EditMode)
            {
                return;
            }

            var changed = false;
            if (WasPressed(CustomLandParcelSettings.MoveNorthAction))
            {
                m_ParcelBoundsSystem.Move(new float2(0f, MoveStep), "move north hotkey");
                changed = true;
            }

            if (WasPressed(CustomLandParcelSettings.MoveSouthAction))
            {
                m_ParcelBoundsSystem.Move(new float2(0f, -MoveStep), "move south hotkey");
                changed = true;
            }

            if (WasPressed(CustomLandParcelSettings.MoveWestAction))
            {
                m_ParcelBoundsSystem.Move(new float2(-MoveStep, 0f), "move west hotkey");
                changed = true;
            }

            if (WasPressed(CustomLandParcelSettings.MoveEastAction))
            {
                m_ParcelBoundsSystem.Move(new float2(MoveStep, 0f), "move east hotkey");
                changed = true;
            }

            if (WasPressed(CustomLandParcelSettings.GrowAction))
            {
                m_ParcelBoundsSystem.Resize(ResizeStep, "grow hotkey");
                changed = true;
            }

            if (WasPressed(CustomLandParcelSettings.ShrinkAction))
            {
                m_ParcelBoundsSystem.Resize(-ResizeStep, "shrink hotkey");
                changed = true;
            }

            if (changed)
            {
                m_FramesUntilLog = 300;
                return;
            }

            if (m_FramesUntilLog <= 0)
            {
                Mod.log.Info(
                    $"Parcel edit mode active. parcel={m_ParcelBoundsSystem.Bounds}, parcelVersion={m_ParcelBoundsSystem.Version}, moveStep={MoveStep:F0}, resizeStep={ResizeStep:F0}.");
                m_FramesUntilLog = 300;
            }

            m_FramesUntilLog--;
        }

        private static bool WasPressed(string actionName)
        {
            ProxyAction action = Mod.Settings.GetAction(actionName);
            return action != null && action.WasPressedThisFrame();
        }

        private void EnsureActionsEnabled()
        {
            if (m_ActionsEnabled)
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

            if (enabledCount == ActionNames.Length)
            {
                m_ActionsEnabled = true;
                Mod.log.Info($"Parcel control enabled {enabledCount}/{ActionNames.Length} input actions.");
            }
        }
    }
}
