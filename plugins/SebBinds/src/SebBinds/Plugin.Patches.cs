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

        private static float _resetHoldStart = -1f;
        private static bool _resetHoldFired;
        private static bool _resetHoldWasDown;

        private static float _mapHoldStart = -1f;
        private static bool _mapHoldFired;
        private static bool _mapHoldWasDown;

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

            // Try every time; it's non-destructive (only fills unset actions).
            bool installed = DefaultPreset.TryInstallFromInputManager(__instance);
            if (installed)
            {
                Log?.LogInfo("Installed default preset from game's InputActions");
            }

            // Non-destructive controller axis defaults.
            AxisDefaults.EnsureControllerDefaults();

            // Non-destructive keyboard defaults.
            KeyboardDefaults.EnsureDefaults();

            InjectBindings(__instance);
        }

        private static void InjectBindings(sInputManager input)
        {
            if (input == null)
            {
                return;
            }

            bool wheelMenuActive = WheelInterop.IsWheelMenuActive();
            bool wheelBindingCaptureActive = WheelInterop.IsWheelBindingCaptureActive();
            bool wheelCalibrationWizardActive = WheelInterop.IsWheelCalibrationWizardActive();

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

            var schemes = WheelInterop.IsWheelPluginPresent()
                ? new[] { BindingScheme.Controller, BindingScheme.Keyboard, BindingScheme.Wheel }
                : new[] { BindingScheme.Controller, BindingScheme.Keyboard };

            BindingLayer LayerFor(BindingScheme s)
            {
                var mod = BindingStore.GetModifierBinding(s);
                return (mod.Kind != BindingKind.None && BindingEvaluator.IsDown(mod)) ? BindingLayer.Modified : BindingLayer.Normal;
            }

            BindingInput GetBind(BindingScheme s, BindAction action)
            {
                var layer = LayerFor(s);
                var b = BindingStore.GetBinding(s, layer, action);
                if (b.Kind == BindingKind.None && layer == BindingLayer.Modified)
                {
                    b = BindingStore.GetBinding(s, BindingLayer.Normal, action);
                }
                return b;
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

            // FreeCam toggle (dev tool). When enabled, the game suppresses select/map/inventory.
            if (PressedAny(BindAction.FreeCam))
            {
                input.freeCamMode = !input.freeCamMode;
                LogDebug("FreeCamMode -> " + (input.freeCamMode ? "on" : "off"));
            }

            bool inWalkingMode = false;
            WheelInterop.TryGetIsInWalkingMode(out inWalkingMode);

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

            // Brake (hold)
            {
                // Prefer axis binding.
                var axis = AxisBindingStore.GetAxisBinding(AxisAction.Brake);
                bool appliedAxis = false;
                if (axis.Kind == BindingKind.GamepadAxis)
                {
                    float v = BindingEvaluator.GetAxisValue(axis);
                    if (v > 0.1f)
                    {
                        input.breakPressed = true;
                    }
                    appliedAxis = true;
                }

                if (!appliedAxis)
                {
                    if (DownAny(BindAction.Brake))
                    {
                        input.breakPressed = true;
                    }
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
                        drive.y = Mathf.Max(drive.y, Mathf.Clamp01(v));
                        input.driveInput = drive;
                    }
                    appliedAxis = true;
                }

                if (!appliedAxis)
                {
                    if (DownAny(BindAction.Drive))
                    {
                        var v = input.driveInput;
                        v.y = Mathf.Max(v.y, 1f);
                        input.driveInput = v;
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

            // Map / Jobs
            {
                // If Map is bound, require a short hold to avoid accidental opens.
                if (AnyBindingExists(BindAction.Map))
                {
                    ApplyHoldToMapPressed(input, isDown: DownAny(BindAction.Map), holdSeconds: 0.25f);
                }
                else
                {
                    bool jobsBound = AnyBindingExists(BindAction.Jobs);
                    if (PressedAny(BindAction.Jobs) || (!jobsBound && PressedAny(BindAction.MapItems)))
                    {
                        input.mapPressed = true;
                    }
                }
            }

            // Items
            {
                var action = AnyBindingExists(BindAction.Items) ? BindAction.Items : BindAction.MapItems;

                if (PressedAny(action))
                {
                    input.inventoryPressed = true;
                }
                if (ReleasedAny(action))
                {
                    input.inventoryReleased = true;
                }
                if (DownAny(action))
                {
                    input.inventoryHeld = true;
                }
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

        private static void ApplyHoldToMapPressed(sInputManager input, bool isDown, float holdSeconds)
        {
            if (input == null)
            {
                _mapHoldStart = -1f;
                _mapHoldFired = false;
                _mapHoldWasDown = false;
                return;
            }
            if (isDown && !_mapHoldWasDown)
            {
                _mapHoldStart = Time.unscaledTime;
                _mapHoldFired = false;
            }

            if (isDown && !_mapHoldFired && _mapHoldStart >= 0f && Time.unscaledTime - _mapHoldStart >= holdSeconds)
            {
                input.mapPressed = true;
                _mapHoldFired = true;
            }

            if (!isDown)
            {
                _mapHoldStart = -1f;
                _mapHoldFired = false;
            }

            _mapHoldWasDown = isDown;
        }
    }
}
