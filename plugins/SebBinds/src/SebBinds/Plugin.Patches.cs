using HarmonyLib;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine.InputSystem;
using UnityEngine;

namespace SebBinds
{
    public partial class Plugin
    {
        private static ConfigEntry<bool> _debugLogging;

        private static readonly BindingScheme[] SchemesControllerKeyboard = { BindingScheme.Controller, BindingScheme.Keyboard };
        private static readonly BindingScheme[] SchemesControllerKeyboardWheel = { BindingScheme.Controller, BindingScheme.Keyboard, BindingScheme.Wheel };

        private static int _defaultsEnsuredForInputInstanceId;

        private static float _resetHoldStart = -1f;
        private static bool _resetHoldFired;
        private static bool _resetHoldWasDown;

        private static bool _didMigrateLegacyBinds;

        private void Awake()
        {
            Log = Logger;
            _debugLogging = Config.Bind("Logging", "debug_logging", false, "Log extra debug information.");

            var harmony = new Harmony(PluginGuid);
            var postfix = new HarmonyMethod(typeof(Plugin), nameof(SInputManager_GetInput_Postfix))
            {
                priority = Priority.Last
            };
            harmony.Patch(
                original: AccessTools.Method(typeof(sInputManager), "GetInput"),
                postfix: postfix
            );

            Log.LogInfo("Loaded");
        }

        internal static void LogDebug(string message)
        {
            if (_debugLogging == null || !_debugLogging.Value)
            {
                return;
            }
            Log?.LogInfo("[debug] " + message);
        }

        private static void SInputManager_GetInput_Postfix(sInputManager __instance)
        {
            if (__instance == null)
            {
                return;
            }

            LastInputManager = __instance;

            // Seed defaults / do one-time migrations once per input manager instance.
            int iid;
            try { iid = __instance.GetInstanceID(); }
            catch { iid = 0; }
            if (_defaultsEnsuredForInputInstanceId != iid)
            {
                _defaultsEnsuredForInputInstanceId = iid;

                bool installed = DefaultPreset.TryInstallFromInputManager(__instance);
                if (installed)
                {
                    LogDebug("Installed default preset from game's InputActions");
                }

                AxisDefaults.EnsureControllerDefaults();
                KeyboardDefaults.EnsureDefaults();

                if (!_didMigrateLegacyBinds)
                {
                    _didMigrateLegacyBinds = true;
                    MigrateLegacyBinds();
                }
            }

            InjectBindings(__instance);
        }

        private static void MigrateLegacyBinds()
        {
            // Legacy: Map was used for the jobs/map menu toggle; Items was used for inventory.
            // New: Jobs drives input.mapPressed; MapItems drives input.inventory*.
            foreach (BindingScheme scheme in (BindingScheme[])System.Enum.GetValues(typeof(BindingScheme)))
            {
                foreach (BindingLayer layer in (BindingLayer[])System.Enum.GetValues(typeof(BindingLayer)))
                {
                    var jobs = BindingStore.GetBinding(scheme, layer, BindAction.Jobs);
                    if (jobs.Kind == BindingKind.None)
                    {
                        var legacyMap = BindingStore.GetBinding(scheme, layer, BindAction.Map);
                        if (legacyMap.Kind != BindingKind.None)
                        {
                            BindingStore.SetBinding(scheme, layer, BindAction.Jobs, legacyMap);
                        }
                    }

                    var mapItems = BindingStore.GetBinding(scheme, layer, BindAction.MapItems);
                    if (mapItems.Kind == BindingKind.None)
                    {
                        var legacyItems = BindingStore.GetBinding(scheme, layer, BindAction.Items);
                        if (legacyItems.Kind != BindingKind.None)
                        {
                            BindingStore.SetBinding(scheme, layer, BindAction.MapItems, legacyItems);
                        }
                    }
                }
            }
        }

        private static void InjectBindings(sInputManager input)
        {
            if (input == null)
            {
                return;
            }

            bool wheelPluginPresent = WheelInterop.IsWheelPluginPresent();
            bool wheelMenuActive = wheelPluginPresent && WheelInterop.IsWheelMenuActive();
            bool wheelBindingCaptureActive = wheelPluginPresent && WheelInterop.IsWheelBindingCaptureActive();
            bool wheelCalibrationWizardActive = wheelPluginPresent && WheelInterop.IsWheelCalibrationWizardActive();

            // Don't interfere with the wheel plugin's own UI (it expects vanilla input to work).
            if (wheelMenuActive)
            {
                return;
            }

            // Binding capture must block *all* other input so the user can press buttons
            // without closing menus or activating UI elements.
            if (BindsMenuWindow.BindingCaptureActive)
            {
                input.selectPressed = false;
                input.selectReleased = false;
                input.backPressed = false;
                input.backReleased = false;
                input.pausePressed = false;
                input.mapPressed = false;
                input.inventoryPressed = false;
                input.inventoryReleased = false;
                input.inventoryHeld = false;
                input.cameraPressed = false;
                input.resetPressed = false;
                input.resetHeld = false;
                input.headlightsPressed = false;
                input.hornPressed = false;
                input.radioPressed = false;
                input.radioInput = Vector2.zero;
                input.mouseInput = Vector2.zero;
                input.driveInput = Vector2.zero;
                input.playerInput = Vector2.zero;
                input.cameraLook = Vector2.zero;
                return;
            }

            // SebBinds owns the default action flags. Clear vanilla-computed values,
            // then re-apply from our bindings.
            input.selectPressed = false;
            input.selectReleased = false;
            input.backPressed = false;
            input.backReleased = false;
            input.pausePressed = false;
            input.mapPressed = false;
            input.inventoryPressed = false;
            input.inventoryReleased = false;
            input.inventoryHeld = false;
            input.cameraPressed = false;
            input.resetPressed = false;
            input.resetHeld = false;
            input.headlightsPressed = false;
            input.hornPressed = false;
            input.radioPressed = false;
            // We'll reconstruct radioInput from our bound inputs.
            input.radioInput = Vector2.zero;

            BindingEvaluator.BeginFrame();

            var schemes = wheelPluginPresent ? SchemesControllerKeyboardWheel : SchemesControllerKeyboard;

            // Cache modifier state per scheme for this frame.
            BindingLayer ctrlLayer = BindingLayer.Normal;
            BindingLayer kbLayer = BindingLayer.Normal;
            BindingLayer wheelLayer = BindingLayer.Normal;
            {
                var mod = BindingStore.GetModifierBinding(BindingScheme.Controller);
                if (mod.Kind != BindingKind.None && BindingEvaluator.IsDown(mod)) ctrlLayer = BindingLayer.Modified;
            }
            {
                var mod = BindingStore.GetModifierBinding(BindingScheme.Keyboard);
                if (mod.Kind != BindingKind.None && BindingEvaluator.IsDown(mod)) kbLayer = BindingLayer.Modified;
            }
            if (wheelPluginPresent)
            {
                var mod = BindingStore.GetModifierBinding(BindingScheme.Wheel);
                if (mod.Kind != BindingKind.None && BindingEvaluator.IsDown(mod)) wheelLayer = BindingLayer.Modified;
            }

            BindingLayer LayerFor(BindingScheme s)
            {
                return s == BindingScheme.Keyboard ? kbLayer : (s == BindingScheme.Wheel ? wheelLayer : ctrlLayer);
            }

            BindingInput GetBind(BindingScheme s, BindAction action)
            {
                var layer = LayerFor(s);
                return BindingStore.GetBinding(s, layer, action);
            }

            bool Pressed(BindingInput b) => b.Kind != BindingKind.None && BindingEvaluator.WasPressedThisFrame(b);
            bool Released(BindingInput b) => b.Kind != BindingKind.None && BindingEvaluator.WasReleasedThisFrame(b);
            bool Down(BindingInput b) => b.Kind != BindingKind.None && BindingEvaluator.IsDown(b);

            bool AnyBindingExists(BindAction action)
            {
                for (int i = 0; i < schemes.Length; i++)
                {
                    var bN = BindingStore.GetBinding(schemes[i], BindingLayer.Normal, action);
                    var bM = BindingStore.GetBinding(schemes[i], BindingLayer.Modified, action);
                    if (bN.Kind != BindingKind.None || bM.Kind != BindingKind.None)
                    {
                        return true;
                    }
                }
                return false;
            }

            bool PressedAny(BindAction action)
            {
                for (int i = 0; i < schemes.Length; i++)
                {
                    if (Pressed(GetBind(schemes[i], action)))
                    {
                        return true;
                    }
                }
                return false;
            }

            bool ReleasedAny(BindAction action)
            {
                for (int i = 0; i < schemes.Length; i++)
                {
                    if (Released(GetBind(schemes[i], action)))
                    {
                        return true;
                    }
                }
                return false;
            }

            bool DownAny(BindAction action)
            {
                for (int i = 0; i < schemes.Length; i++)
                {
                    if (Down(GetBind(schemes[i], action)))
                    {
                        return true;
                    }
                }
                return false;
            }

            // FreeCam is a dev tool; not exposed to players.

            bool inWalkingMode = false;
            if (wheelPluginPresent)
            {
                WheelInterop.TryGetIsInWalkingMode(out inWalkingMode);
            }

            // Note: bindings are evaluated across Controller + Keyboard (+ Wheel if present).

            // POV -> menu cursor (fake mouse) while paused.
            // Don't override real mouse/left-stick input.
            if (PauseSystem.paused &&
                !BindsMenuWindow.BindingCaptureActive &&
                !wheelBindingCaptureActive &&
                !wheelCalibrationWizardActive)
            {
                if (WheelInterop.TryGetPov8Vector(out var pov))
                {
                    Vector2 mouse = -pov;
                    if (mouse != Vector2.zero && input.mouseInput.sqrMagnitude < 0.0001f)
                    {
                        if (Mathf.Abs(mouse.x) > 0.1f && Mathf.Abs(mouse.y) > 0.1f)
                        {
                            mouse.Normalize();
                        }
                        input.mouseInput = mouse;
                    }

                    // Also feed menu zone navigation (vanilla uses radioInput for GamepadNavigation).
                    if (mouse != Vector2.zero && input.radioInput.sqrMagnitude < 0.0001f)
                    {
                        input.radioInput = mouse;
                    }
                }
            }

            // POV -> walking movement (on foot).
            if (inWalkingMode && !PauseSystem.paused && !input.lockInput)
            {
                if (WheelInterop.TryGetPov8Vector(out var pov))
                {
                    Vector2 move = -pov;
                    if (move != Vector2.zero && input.playerInput.sqrMagnitude < 0.0001f)
                    {
                        input.playerInput = move;
                    }
                }
            }

            // Controller movement axes (on foot).
            if (!PauseSystem.paused && !input.lockInput)
            {
                var axX = AxisBindingStore.GetAxisBinding(AxisAction.MoveX);
                var axY = AxisBindingStore.GetAxisBinding(AxisAction.MoveY);
                if (axX.Kind == BindingKind.GamepadAxis || axY.Kind == BindingKind.GamepadAxis)
                {
                    float x = axX.Kind == BindingKind.GamepadAxis ? BindingEvaluator.GetAxisValue(axX) : 0f;
                    float y = axY.Kind == BindingKind.GamepadAxis ? BindingEvaluator.GetAxisValue(axY) : 0f;
                    if (Mathf.Abs(x) > 0.05f || Mathf.Abs(y) > 0.05f)
                    {
                        // Note: playerInput is already used by vanilla for on-foot movement.
                        input.playerInput = new Vector2(x, y);
                    }
                }
            }

            // Keyboard on-foot movement buttons.
            if (!PauseSystem.paused && !input.lockInput)
            {
                float mx = 0f;
                float my = 0f;
                if (Down(GetBind(BindingScheme.Keyboard, BindAction.MoveRight))) mx += 1f;
                if (Down(GetBind(BindingScheme.Keyboard, BindAction.MoveLeft))) mx -= 1f;
                if (Down(GetBind(BindingScheme.Keyboard, BindAction.MoveUp))) my += 1f;
                if (Down(GetBind(BindingScheme.Keyboard, BindAction.MoveDown))) my -= 1f;

                var move = new Vector2(mx, my);
                if (move.sqrMagnitude > 1f)
                {
                    move.Normalize();
                }

                if (move != Vector2.zero && input.playerInput.sqrMagnitude < 0.0001f)
                {
                    input.playerInput = move;
                }
            }

            // Don't inject gameplay actions while the game is explicitly locking input.
            if (input.lockInput)
            {
                return;
            }

            // Click/Select (Interact/OK)
            {
                if (PressedAny(BindAction.InteractOk))
                {
                    input.selectPressed = true;
                }
                if (ReleasedAny(BindAction.InteractOk))
                {
                    input.selectReleased = true;
                }
            }

            // Back
            {
                if (PressedAny(BindAction.Back))
                {
                    input.backPressed = true;
                }
                if (ReleasedAny(BindAction.Back))
                {
                    input.backReleased = true;
                }
            }

            // Handbrake (hold) - vanilla uses the Back action (Space/B).
            if (!PauseSystem.paused && !inWalkingMode)
            {
                if (DownAny(BindAction.Back))
                {
                    input.breakPressed = true;
                }
            }

            // Drive (hold) - only boosts forward input; does not override existing analog.
            if (!PauseSystem.paused)
            {
                // Prefer axis binding.
                var axis = AxisBindingStore.GetAxisBinding(AxisAction.Throttle);
                bool appliedAxis = false;
                if (axis.Kind == BindingKind.GamepadAxis)
                {
                    float v = BindingEvaluator.GetAxisValue(axis);
                    if (v > 0.05f)
                    {
                        var drive = input.driveInput;
                        // Don't override braking/reverse (negative y).
                        if (drive.y > -0.01f)
                        {
                            drive.y = Mathf.Max(drive.y, Mathf.Clamp01(v));
                        }
                        input.driveInput = drive;
                    }
                    appliedAxis = true;
                }

                if (!appliedAxis)
                {
                    if (DownAny(BindAction.Drive))
                    {
                        var v = input.driveInput;
                        if (v.y > -0.01f)
                        {
                            v.y = Mathf.Max(v.y, 1f);
                        }
                        input.driveInput = v;
                    }
                }
            }

            // Brake/Reverse (hold) - affects driveInput.y negative.
            if (!PauseSystem.paused && !inWalkingMode)
            {
                // Prefer axis binding.
                var axis = AxisBindingStore.GetAxisBinding(AxisAction.Brake);
                bool appliedAxis = false;
                if (axis.Kind == BindingKind.GamepadAxis)
                {
                    float v = BindingEvaluator.GetAxisValue(axis);
                    if (v > 0.05f)
                    {
                        var drive = input.driveInput;
                        drive.y = Mathf.Min(drive.y, -Mathf.Clamp01(v));
                        input.driveInput = drive;
                    }
                    appliedAxis = true;
                }

                if (!appliedAxis)
                {
                    if (DownAny(BindAction.Brake))
                    {
                        var drive = input.driveInput;
                        drive.y = Mathf.Min(drive.y, -1f);
                        input.driveInput = drive;
                    }
                }
            }

            // Steering buttons (keyboard).
            if (!PauseSystem.paused)
            {
                // Prefer axis binding.
                var steerAxis = AxisBindingStore.GetAxisBinding(AxisAction.Steering);
                bool appliedAxis = false;
                if (steerAxis.Kind == BindingKind.GamepadAxis)
                {
                    float v = BindingEvaluator.GetAxisValue(steerAxis);
                    if (Mathf.Abs(v) > 0.05f)
                    {
                        var cur = input.driveInput;
                        if (Mathf.Abs(cur.x) < 0.15f)
                        {
                            cur.x = Mathf.Clamp(v, -1f, 1f);
                            input.driveInput = cur;
                        }
                    }
                    appliedAxis = true;
                }

                var left = GetBind(BindingScheme.Keyboard, BindAction.SteerLeft);
                var right = GetBind(BindingScheme.Keyboard, BindAction.SteerRight);

                float steer = 0f;
                if (Down(left)) steer -= 1f;
                if (Down(right)) steer += 1f;

                if (!appliedAxis && Mathf.Abs(steer) > 0.01f)
                {
                    var v = input.driveInput;
                    // Only override when the existing steer is near-neutral.
                    if (Mathf.Abs(v.x) < 0.15f)
                    {
                        v.x = steer;
                        input.driveInput = v;
                    }
                }
            }

            // Camera look axis remap.
            // Default mouse panning remains intact unless these binds are set.
            if (!PauseSystem.paused)
            {
                var lookX = AxisBindingStore.GetAxisBinding(AxisAction.CameraLookX);
                var lookY = AxisBindingStore.GetAxisBinding(AxisAction.CameraLookY);

                if (lookX.Kind == BindingKind.GamepadAxis)
                {
                    float v = BindingEvaluator.GetAxisValue(lookX);
                    if (Mathf.Abs(v) > 0.01f)
                    {
                        input.cameraLook.x = v;
                    }
                }

                if (lookY.Kind == BindingKind.GamepadAxis)
                {
                    float v = BindingEvaluator.GetAxisValue(lookY);
                    if (Mathf.Abs(v) > 0.01f)
                    {
                        input.cameraLook.y = v;
                    }
                }

                // Keyboard camera look buttons (only if nothing else is driving cameraLook).
                if (input.cameraLook.sqrMagnitude < 0.0001f)
                {
                    float lx = 0f;
                    float ly = 0f;
                    if (Down(GetBind(BindingScheme.Keyboard, BindAction.LookRight))) lx += 1f;
                    if (Down(GetBind(BindingScheme.Keyboard, BindAction.LookLeft))) lx -= 1f;
                    if (Down(GetBind(BindingScheme.Keyboard, BindAction.LookUp))) ly += 1f;
                    if (Down(GetBind(BindingScheme.Keyboard, BindAction.LookDown))) ly -= 1f;

                    var look = new Vector2(lx, ly);
                    if (look.sqrMagnitude > 1f)
                    {
                        look.Normalize();
                    }
                    if (look != Vector2.zero)
                    {
                        input.cameraLook = look;
                    }
                }
            }

            // Pause
            {
                if (PressedAny(BindAction.Pause))
                {
                    input.pausePressed = true;
                }
            }

            // Always allow Escape to open/close the main menu.
            if (Keyboard.current != null && Keyboard.current.escapeKey != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                input.pausePressed = true;
            }

            // Jobs (jobs/map menu toggle) - press to open/close.
            {
                bool jobsBound = AnyBindingExists(BindAction.Jobs);
                // Vanilla blocks job-map access on foot (unless already paused).
                if (!inWalkingMode || PauseSystem.paused)
                {
                    if (PressedAny(BindAction.Jobs) || (!jobsBound && PressedAny(BindAction.Map)))
                    {
                        input.mapPressed = true;
                    }
                }
            }

            // Map/Items (inventory action) - used by vanilla for on-foot items and in-vehicle hold-map.
            {
                // Back-compat: older builds used Items for this.
                var action = AnyBindingExists(BindAction.MapItems) ? BindAction.MapItems
                    : (AnyBindingExists(BindAction.Items) ? BindAction.Items : BindAction.MapItems);

                if (PressedAny(action)) input.inventoryPressed = true;
                if (ReleasedAny(action)) input.inventoryReleased = true;
                if (DownAny(action)) input.inventoryHeld = true;
            }

            // Camera toggle
            {
                if (PressedAny(BindAction.Camera))
                {
                    input.cameraPressed = true;
                }
            }

            // Reset vehicle (press + hold)
            {
                ApplyHoldToReset(
                    input,
                    isBound: AnyBindingExists(BindAction.ResetVehicle),
                    isDown: DownAny(BindAction.ResetVehicle),
                    holdSeconds: 0.6f
                );
            }

            // Headlights
            {
                if (PressedAny(BindAction.Headlights))
                {
                    input.headlightsPressed = true;
                }
            }

            // Horn
            {
                if (PressedAny(BindAction.Horn))
                {
                    input.hornPressed = true;
                }
            }

            // Radio actions
            {
                // Menu navigation uses radioInput continuously.
                Vector2 radio = Vector2.zero;
                if (DownAny(BindAction.RadioScanRight)) radio.x = 1f;
                else if (DownAny(BindAction.RadioScanLeft)) radio.x = -1f;
                if (DownAny(BindAction.RadioScanToggle)) radio.y = 1f;
                else if (DownAny(BindAction.RadioPower)) radio.y = -1f;
                input.radioInput = radio;

                // The radio system uses radioPressed + radioInput on press.
                bool radioPressed = false;
                Vector2 radioPressInput = Vector2.zero;
                if (PressedAny(BindAction.RadioPower)) { radioPressed = true; radioPressInput = new Vector2(0f, -1f); }
                else if (PressedAny(BindAction.RadioScanToggle)) { radioPressed = true; radioPressInput = new Vector2(0f, 1f); }
                else if (PressedAny(BindAction.RadioScanRight)) { radioPressed = true; radioPressInput = new Vector2(1f, 0f); }
                else if (PressedAny(BindAction.RadioScanLeft)) { radioPressed = true; radioPressInput = new Vector2(-1f, 0f); }

                if (radioPressed)
                {
                    input.radioPressed = true;
                    input.radioInput = radioPressInput;
                }
            }
        }

        private static void ApplyHoldToReset(sInputManager input, bool isBound, bool isDown, float holdSeconds)
        {
            if (input == null || !isBound)
            {
                _resetHoldStart = -1f;
                _resetHoldFired = false;
                _resetHoldWasDown = false;
                return;
            }

            if (isDown && !_resetHoldWasDown)
            {
                _resetHoldStart = Time.unscaledTime;
                _resetHoldFired = false;
            }

            if (isDown && !_resetHoldFired && _resetHoldStart >= 0f && Time.unscaledTime - _resetHoldStart >= holdSeconds)
            {
                input.resetHeld = true;
                input.resetPressed = true;
                _resetHoldFired = true;
            }

            if (!isDown)
            {
                _resetHoldStart = -1f;
                _resetHoldFired = false;
            }

            _resetHoldWasDown = isDown;
        }
    }
}
