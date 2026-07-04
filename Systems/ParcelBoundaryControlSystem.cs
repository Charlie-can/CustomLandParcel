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

        private ParcelStoreSystem m_ParcelStoreSystem;
        private bool m_ActionsEnabled;
        private bool m_EditMode;
        private int m_EditActionCooldownFrames;
        private int m_FramesUntilLog;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ParcelStoreSystem = World.GetOrCreateSystemManaged<ParcelStoreSystem>();
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
                    $"Parcel edit mode {(m_EditMode ? "enabled" : "disabled")}. {m_ParcelStoreSystem.GetSummary()}.");
            }

            if (!m_EditMode)
            {
                return;
            }

            if (m_EditActionCooldownFrames > 0)
            {
                m_EditActionCooldownFrames--;
                return;
            }

            var changed = false;
            if (WasPressed(CustomLandParcelSettings.MoveNorthAction))
            {
                m_ParcelStoreSystem.MoveSelectedParcel(new float2(0f, MoveStep), "move north hotkey");
                changed = true;
            }

            if (WasPressed(CustomLandParcelSettings.MoveSouthAction))
            {
                m_ParcelStoreSystem.MoveSelectedParcel(new float2(0f, -MoveStep), "move south hotkey");
                changed = true;
            }

            if (WasPressed(CustomLandParcelSettings.MoveWestAction))
            {
                m_ParcelStoreSystem.MoveSelectedParcel(new float2(-MoveStep, 0f), "move west hotkey");
                changed = true;
            }

            if (WasPressed(CustomLandParcelSettings.MoveEastAction))
            {
                m_ParcelStoreSystem.MoveSelectedParcel(new float2(MoveStep, 0f), "move east hotkey");
                changed = true;
            }

            if (WasPressed(CustomLandParcelSettings.GrowAction))
            {
                m_ParcelStoreSystem.ResizeSelectedParcel(ResizeStep, "grow hotkey");
                changed = true;
            }

            if (WasPressed(CustomLandParcelSettings.ShrinkAction))
            {
                m_ParcelStoreSystem.ResizeSelectedParcel(-ResizeStep, "shrink hotkey");
                changed = true;
            }

            if (changed)
            {
                m_EditActionCooldownFrames = EditActionCooldownFrames;
                m_FramesUntilLog = 300;
                Mod.log.Info(
                    $"Parcel edit action cooldown started for {EditActionCooldownFrames} frames to avoid rapid native area rebuilds.");
                return;
            }

            if (m_FramesUntilLog <= 0)
            {
                Mod.log.Info(
                    $"Parcel edit mode active. {m_ParcelStoreSystem.GetSummary()}, moveStep={MoveStep:F0}, resizeStep={ResizeStep:F0}, cooldownFrames={EditActionCooldownFrames}.");
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

            if (enabledCount != ActionNames.Length) return;
            m_ActionsEnabled = true;
            Mod.log.Info($"Parcel control enabled {enabledCount}/{ActionNames.Length} input actions.");
        }
    }
}
