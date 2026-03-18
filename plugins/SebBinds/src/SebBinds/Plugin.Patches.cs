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
        private static ConfigEntry<InputMode> _inputMode;

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
            _inputMode = Config.Bind("Input", "mode", InputMode.Controller, "Active input mode: Controller, KeyboardMouse, Wheel.");

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

        internal static InputMode GetConfiguredInputMode()
        {
            return _inputMode != null ? _inputMode.Value : InputMode.Controller;
        }

        internal static void CycleInputMode()
        {
            if (_inputMode == null)
            {
                return;
            }

            bool wheelAvailable = WheelInterop.IsWheelPluginPresent();

            InputMode current = GetConfiguredInputMode();
            InputMode next = current + 1;
            if (next > InputMode.Wheel)
            {
                next = InputMode.Controller;
            }
            if (!wheelAvailable && next == InputMode.Wheel)
            {
                next = InputMode.Controller;
            }

            _inputMode.Value = next;
            Log?.LogInfo("Input mode -> " + next);
        }

        private static InputMode ResolveInputMode(InputMode configured)
        {
            bool wheelAvailable = WheelInterop.IsWheelPluginPresent();
            if (configured == InputMode.Wheel && !wheelAvailable)
            {
                configured = InputMode.Controller;
            }
            return configured;
        }

        internal static InputMode GetActiveInputMode()
        {
            return ResolveInputMode(GetConfiguredInputMode());
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

            InputMode mode = ResolveInputMode(GetConfiguredInputMode());

            BindingScheme scheme = mode == InputMode.KeyboardMouse ? BindingScheme.Keyboard : BindingScheme.Controller;

            BindingEvaluator.BeginFrame();

            // FreeCam toggle (dev tool). When enabled, the game suppresses select/map/inventory.
            {
                var freeCam = BindingStore.GetBinding(BindingLayer.Normal, BindAction.FreeCam);
                if (Pressed(freeCam))
                {
                    input.freeCamMode = !input.freeCamMode;
                    LogDebug("FreeCamMode -> " + (input.freeCamMode ? "on" : "off"));
                }
            }

            bool inWalkingMode = false;
            WheelInterop.TryGetIsInWalkingMode(out inWalkingMode);

            bool Allowed(BindingInput b)
            {
                if (b.Kind == BindingKind.None)
                {
                    return false;
                }

                if (mode == InputMode.Wheel)
                {
                    return b.Kind == BindingKind.Button || b.Kind == BindingKind.Pov;
                }
                if (mode == InputMode.Controller)
                {
                    return b.Kind == BindingKind.GamepadButton || b.Kind == BindingKind.GamepadDpad || b.Kind == BindingKind.GamepadAxis;
                }
                return b.Kind == BindingKind.Key || b.Kind == BindingKind.MouseButton;
            }

            bool Pressed(BindingInput b) => Allowed(b) && BindingEvaluator.WasPressedThisFrame(b);
            bool Released(BindingInput b) => Allowed(b) && BindingEvaluator.WasReleasedThisFrame(b);
            bool Down(BindingInput b) => Allowed(b) && BindingEvaluator.IsDown(b);

            BindingLayer layer = Down(BindingStore.GetModifierBinding(scheme)) ? BindingLayer.Modified : BindingLayer.Normal;

            BindingInput GetBind(BindAction action)
            {
                var b = BindingStore.GetBinding(scheme, layer, action);
                if (b.Kind == BindingKind.None && layer == BindingLayer.Modified)
                {
                    b = BindingStore.GetBinding(scheme, BindingLayer.Normal, action);
                }
                return b;
            }

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
            if (mode == InputMode.KeyboardMouse && !PauseSystem.paused && !input.lockInput)
            {
                float mx = 0f;
                float my = 0f;
                if (Down(GetBind(BindAction.MoveRight))) mx += 1f;
                if (Down(GetBind(BindAction.MoveLeft))) mx -= 1f;
                if (Down(GetBind(BindAction.MoveUp))) my += 1f;
                if (Down(GetBind(BindAction.MoveDown))) my -= 1f;

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
                var bind = GetBind(BindAction.InteractOk);
                if (Pressed(bind))
                {
                    input.selectPressed = true;
                }
                if (Released(bind))
                {
                    input.selectReleased = true;
                }
            }

            // Back
            {
                var bind = GetBind(BindAction.Back);
                if (Pressed(bind))
                {
                    input.backPressed = true;
                }
                if (Released(bind))
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
                    var bind = GetBind(BindAction.Brake);
                    if (Down(bind))
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
                    var bind = GetBind(BindAction.Drive);
                    if (Down(bind))
                    {
                        var v = input.driveInput;
                        v.y = Mathf.Max(v.y, 1f);
                        input.driveInput = v;
                    }
                }
            }

            // Steering buttons (useful for KeyboardMouse mode).
            if (mode == InputMode.KeyboardMouse && !PauseSystem.paused)
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

                var left = GetBind(BindAction.SteerLeft);
                var right = GetBind(BindAction.SteerRight);

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
                if (mode == InputMode.KeyboardMouse && input.cameraLook.sqrMagnitude < 0.0001f)
                {
                    float lx = 0f;
                    float ly = 0f;
                    if (Down(GetBind(BindAction.LookRight))) lx += 1f;
                    if (Down(GetBind(BindAction.LookLeft))) lx -= 1f;
                    if (Down(GetBind(BindAction.LookUp))) ly += 1f;
                    if (Down(GetBind(BindAction.LookDown))) ly -= 1f;

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
                var b0 = BindingStore.GetBinding(scheme, BindingLayer.Normal, BindAction.Pause);
                var b1 = BindingStore.GetBinding(scheme, BindingLayer.Modified, BindAction.Pause);
                if (Pressed(b0) || Pressed(b1))
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
                var map = GetBind(BindAction.Map);
                var jobs = GetBind(BindAction.Jobs);
                var legacy = GetBind(BindAction.MapItems);

                // If Map is bound, require a short hold to avoid accidental opens.
                if (map.Kind != BindingKind.None)
                {
                    ApplyHoldToMapPressed(input, map, Down, 0.25f);
                }
                else
                {
                    if (Pressed(jobs) || (jobs.Kind == BindingKind.None && Pressed(legacy)))
                    {
                        input.mapPressed = true;
                    }
                }
            }

            // Items
            {
                var bind = GetBind(BindAction.Items);
                if (bind.Kind == BindingKind.None)
                {
                    bind = GetBind(BindAction.MapItems); // legacy
                }

                if (Pressed(bind))
                {
                    input.inventoryPressed = true;
                }
                if (Released(bind))
                {
                    input.inventoryReleased = true;
                }
                if (Down(bind))
                {
                    input.inventoryHeld = true;
                }
            }

            // Camera toggle
            {
                var bind = GetBind(BindAction.Camera);
                if (Pressed(bind))
                {
                    input.cameraPressed = true;
                }
            }

            // Reset vehicle (press + hold)
            {
                var bind = GetBind(BindAction.ResetVehicle);
                ApplyHoldToReset(input, bind, Down, 0.6f);
            }

            // Headlights
            {
                var bind = GetBind(BindAction.Headlights);
                if (Pressed(bind))
                {
                    input.headlightsPressed = true;
                }
            }

            // Horn
            {
                var bind = GetBind(BindAction.Horn);
                if (Pressed(bind))
                {
                    input.hornPressed = true;
                }
            }

            // Radio actions
            {
                var up = GetBind(BindAction.RadioScanToggle);
                var right = GetBind(BindAction.RadioScanRight);
                var down = GetBind(BindAction.RadioPower);
                var left = GetBind(BindAction.RadioScanLeft);

                // Menu navigation uses radioInput continuously.
                Vector2 radio = Vector2.zero;
                if (Down(right)) radio.x = 1f;
                else if (Down(left)) radio.x = -1f;
                if (Down(up)) radio.y = 1f;
                else if (Down(down)) radio.y = -1f;
                input.radioInput = radio;

                // The radio system uses radioPressed + radioInput on press.
                bool radioPressed = false;
                Vector2 radioPressInput = Vector2.zero;
                if (Pressed(down)) { radioPressed = true; radioPressInput = new Vector2(0f, -1f); }
                else if (Pressed(up)) { radioPressed = true; radioPressInput = new Vector2(0f, 1f); }
                else if (Pressed(right)) { radioPressed = true; radioPressInput = new Vector2(1f, 0f); }
                else if (Pressed(left)) { radioPressed = true; radioPressInput = new Vector2(-1f, 0f); }

                if (radioPressed)
                {
                    input.radioPressed = true;
                    input.radioInput = radioPressInput;
                }
            }
        }

        private static void ApplyHoldToReset(sInputManager input, BindingInput bind, System.Func<BindingInput, bool> down, float holdSeconds)
        {
            if (input == null || bind.Kind == BindingKind.None)
            {
                _resetHoldStart = -1f;
                _resetHoldFired = false;
                _resetHoldWasDown = false;
                return;
            }

            bool isDown = down(bind);
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

        private static void ApplyHoldToMapPressed(sInputManager input, BindingInput bind, System.Func<BindingInput, bool> down, float holdSeconds)
        {
            if (input == null || bind.Kind == BindingKind.None)
            {
                _mapHoldStart = -1f;
                _mapHoldFired = false;
                _mapHoldWasDown = false;
                return;
            }

            bool isDown = down(bind);
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
