using HarmonyLib;
using System;
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
            var postfix = new HarmonyMethod(typeof(Plugin), nameof(SInputManager_Update_Postfix))
            {
                priority = Priority.Last
            };
            harmony.Patch(
                original: AccessTools.Method(typeof(sInputManager), "Update"),
                postfix: postfix
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(sRallyDriving), "CollectPlayerInput"),
                prefix: new HarmonyMethod(typeof(Plugin), nameof(SRallyDriving_CollectPlayerInput_Prefix)),
                postfix: new HarmonyMethod(typeof(Plugin), nameof(SRallyDriving_CollectPlayerInput_Postfix))
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

        private static bool IsRallyMenuActive()
        {
            try
            {
                return sEasyRallyToggle.active && sEasyRally.menuOpen;
            }
            catch
            {
                return false;
            }
        }

        private static void SInputManager_Update_Postfix(sInputManager __instance)
        {
            if (__instance == null)
            {
                return;
            }

            LastInputManager = __instance;

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

        [HarmonyPatch(typeof(sInputManager), "ManualMode")]
        [HarmonyPostfix]
        private static void SInputManager_ManualMode_Postfix(sInputManager __instance)
        {
            if (__instance == null || PauseSystem.paused || BindsMenuWindow.BindingCaptureActive)
            {
                return;
            }

            bool wheelPluginPresent = WheelInterop.IsWheelPluginPresent();
            if (wheelPluginPresent && (WheelInterop.IsWheelMenuActive() || WheelInterop.IsWheelBindingCaptureActive() || WheelInterop.IsWheelCalibrationWizardActive()))
            {
                return;
            }

            BindingEvaluator.BeginFrame();

            __instance.shiftUp = SebBindsApi.WasActionPressed(BindAction.ShiftUp);
            __instance.shiftDown = SebBindsApi.WasActionPressed(BindAction.ShiftDown);
        }

        private static void MigrateLegacyBinds()
        {
                foreach (BindingScheme scheme in (BindingScheme[])System.Enum.GetValues(typeof(BindingScheme)))
            {
                foreach (BindingLayer layer in (BindingLayer[])System.Enum.GetValues(typeof(BindingLayer)))
                {
                    var jobs = BindingStore.GetBinding(scheme, layer, BindAction.Jobs);
                    if (jobs.Kind == BindingKind.None && !BindingStore.HasBinding(scheme, layer, BindAction.Jobs))
                    {
                        var legacyMap = BindingStore.GetBinding(scheme, layer, BindAction.Map);
                        if (legacyMap.Kind != BindingKind.None)
                        {
                            BindingStore.SetBinding(scheme, layer, BindAction.Jobs, legacyMap);
                        }
                    }

                    var mapItems = BindingStore.GetBinding(scheme, layer, BindAction.MapItems);
                    if (mapItems.Kind == BindingKind.None && !BindingStore.HasBinding(scheme, layer, BindAction.MapItems))
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

            if (wheelMenuActive)
            {
                return;
            }

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
                input.shiftUp = false;
                input.shiftDown = false;
                input.freeCamTogglePressed = false;
                input.freeCamSelectPressed = false;
                input.radioInput = Vector2.zero;
                input.mouseInput = Vector2.zero;
                input.driveInput = Vector2.zero;
                input.playerInput = Vector2.zero;
                input.cameraLook = Vector2.zero;
                return;
            }

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
            input.shiftUp = false;
            input.shiftDown = false;
            input.freeCamTogglePressed = false;
            input.freeCamSelectPressed = false;
            input.radioInput = Vector2.zero;

            BindingEvaluator.BeginFrame();

            BindingInput GetBind(BindingScheme s, BindAction action) => SebBindsApi.GetActiveBinding(s, action);
            bool Down(BindingInput b) => b.Kind != BindingKind.None && BindingEvaluator.IsDown(b);
            bool AnyBindingExists(BindAction action) => SebBindsApi.IsActionBound(action);
            bool PressedAny(BindAction action) => SebBindsApi.WasActionPressed(action);
            bool ReleasedAny(BindAction action) => SebBindsApi.WasActionReleased(action);
            bool DownAny(BindAction action) => SebBindsApi.IsActionDown(action);

            if (IsRallyMenuActive())
            {
                ApplyRallyMenuBinds(input, DownAny, wheelPluginPresent);
                return;
            }

            bool inWalkingMode = false;
            if (wheelPluginPresent)
            {
                WheelInterop.TryGetIsInWalkingMode(out inWalkingMode);
            }
            bool wheelDrivingInputActive = wheelPluginPresent && WheelInterop.TryGetWheelLastInput(out _, out _);

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

                    if (mouse != Vector2.zero && input.radioInput.sqrMagnitude < 0.0001f)
                    {
                        input.radioInput = mouse;
                    }
                }
            }

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
                        input.playerInput = new Vector2(x, y);
                    }
                }
            }

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

            if (PressedAny(BindAction.Horn) || PressedAny(BindAction.ShiftUp))
            {
                input.shiftUp = true;
            }
            if (PressedAny(BindAction.MapItems) || PressedAny(BindAction.Items) || PressedAny(BindAction.ShiftDown))
            {
                input.shiftDown = true;
            }
            if (PressedAny(BindAction.Camera) || PressedAny(BindAction.FreeCam))
            {
                input.freeCamTogglePressed = true;
            }
            if (PressedAny(BindAction.InteractOk) || PressedAny(BindAction.FreeCamSelect))
            {
                input.freeCamSelectPressed = true;
            }

            if (input.lockInput)
            {
                return;
            }

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

            if (!PauseSystem.paused && !inWalkingMode)
            {
                if (DownAny(BindAction.Back))
                {
                    input.brakePressed = true;
                }
            }

            if (!PauseSystem.paused && !wheelDrivingInputActive)
            {
                var axis = AxisBindingStore.GetAxisBinding(AxisAction.Throttle);
                bool appliedAxis = false;
                if (axis.Kind == BindingKind.GamepadAxis)
                {
                    float v = BindingEvaluator.GetAxisValue(axis);
                    if (v > 0.05f)
                    {
                        var drive = input.driveInput;
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

            if (!PauseSystem.paused && !inWalkingMode && !wheelDrivingInputActive)
            {
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

            if (!PauseSystem.paused && !wheelDrivingInputActive)
            {
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
                    if (Mathf.Abs(v.x) < 0.15f)
                    {
                        v.x = steer;
                        input.driveInput = v;
                    }
                }
            }

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

            {
                if (PressedAny(BindAction.Pause))
                {
                    input.pausePressed = true;
                }
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                input.pausePressed = true;
            }

            {
                bool jobsBound = AnyBindingExists(BindAction.Jobs);
                if (!inWalkingMode || PauseSystem.paused)
                {
                    if (PressedAny(BindAction.Jobs) || (!jobsBound && PressedAny(BindAction.Map)))
                    {
                        input.mapPressed = true;
                    }
                }
            }

            {
                var action = AnyBindingExists(BindAction.MapItems) ? BindAction.MapItems
                    : (AnyBindingExists(BindAction.Items) ? BindAction.Items : BindAction.MapItems);

                if (PressedAny(action)) input.inventoryPressed = true;
                if (ReleasedAny(action)) input.inventoryReleased = true;
                if (DownAny(action)) input.inventoryHeld = true;
            }

            {
                if (PressedAny(BindAction.Camera))
                {
                    input.cameraPressed = true;
                }
            }

            {
                ApplyHoldToReset(
                    input,
                    isBound: AnyBindingExists(BindAction.ResetVehicle),
                    isDown: DownAny(BindAction.ResetVehicle),
                    holdSeconds: 0.6f
                );
            }

            {
                if (PressedAny(BindAction.Headlights))
                {
                    input.headlightsPressed = true;
                }
            }

            {
                if (PressedAny(BindAction.Horn))
                {
                    input.hornPressed = true;
                }
            }

            {
                Vector2 radio = Vector2.zero;
                if (DownAny(BindAction.RadioScanRight)) radio.x = 1f;
                else if (DownAny(BindAction.RadioScanLeft)) radio.x = -1f;
                if (DownAny(BindAction.RadioScanToggle)) radio.y = 1f;
                else if (DownAny(BindAction.RadioPower)) radio.y = -1f;
                input.radioInput = radio;

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

        private static void SRallyDriving_CollectPlayerInput_Prefix(sRallyDriving __instance, out float __state)
        {
            __state = __instance != null ? __instance.clutchInput : 0f;
        }

        private static void SRallyDriving_CollectPlayerInput_Postfix(sRallyDriving __instance, float __state)
        {
            if (__instance == null || __instance.playerNumber < 0 || PauseSystem.paused || BindsMenuWindow.BindingCaptureActive)
            {
                return;
            }

            bool wheelPluginPresent = WheelInterop.IsWheelPluginPresent();
            if (wheelPluginPresent && (WheelInterop.IsWheelMenuActive() || WheelInterop.IsWheelBindingCaptureActive() || WheelInterop.IsWheelCalibrationWizardActive()))
            {
                return;
            }

            if (IsRallyMenuActive())
            {
                SuppressRallyDrivingInput(__instance);
                return;
            }

            BindingEvaluator.BeginFrame();
            var schemes = wheelPluginPresent ? SchemesControllerKeyboardWheel : SchemesControllerKeyboard;

            ApplyRallyClutch(__instance, schemes, __state);

            if (__instance.automatic || __instance.car == null || __instance.car.GuyActive || __instance.gearbox == null)
            {
                return;
            }

            if (SebBindsApi.GetShiftMode() == ShiftMode.Hold)
            {
                ShiftRallyDirectGear(__instance, GetHeldDirectGear());
                return;
            }

            if (SebBindsApi.WasActionPressed(BindAction.GearReverse)) ShiftRallyDirectGear(__instance, -1);
            if (SebBindsApi.WasActionPressed(BindAction.GearNeutral)) ShiftRallyDirectGear(__instance, 0);
            if (SebBindsApi.WasActionPressed(BindAction.Gear1)) ShiftRallyDirectGear(__instance, 1);
            if (SebBindsApi.WasActionPressed(BindAction.Gear2)) ShiftRallyDirectGear(__instance, 2);
            if (SebBindsApi.WasActionPressed(BindAction.Gear3)) ShiftRallyDirectGear(__instance, 3);
            if (SebBindsApi.WasActionPressed(BindAction.Gear4)) ShiftRallyDirectGear(__instance, 4);
            if (SebBindsApi.WasActionPressed(BindAction.Gear5)) ShiftRallyDirectGear(__instance, 5);
            if (SebBindsApi.WasActionPressed(BindAction.Gear6)) ShiftRallyDirectGear(__instance, 6);
        }

        private static int GetHeldDirectGear()
        {
            if (SebBindsApi.IsActionDown(BindAction.GearReverse)) return -1;
            if (SebBindsApi.IsActionDown(BindAction.GearNeutral)) return 0;
            if (SebBindsApi.IsActionDown(BindAction.Gear1)) return 1;
            if (SebBindsApi.IsActionDown(BindAction.Gear2)) return 2;
            if (SebBindsApi.IsActionDown(BindAction.Gear3)) return 3;
            if (SebBindsApi.IsActionDown(BindAction.Gear4)) return 4;
            if (SebBindsApi.IsActionDown(BindAction.Gear5)) return 5;
            if (SebBindsApi.IsActionDown(BindAction.Gear6)) return 6;
            return 0;
        }

        private static void ShiftRallyDirectGear(sRallyDriving rally, int displayGear)
        {
            if (rally == null || rally.gearbox == null)
            {
                return;
            }
            int gearboxGear = displayGear < 0 ? 0 : displayGear + 1;
            if (rally.gearbox.currentGear != gearboxGear)
            {
                rally.gearbox.ShiftTo(gearboxGear);
            }
        }

        private static void ApplyRallyClutch(sRallyDriving rally, BindingScheme[] schemes, float previousClutch)
        {
            bool hasClutchBind = SebBindsApi.IsActionBound(BindAction.Clutch);
            bool hasClutchAxis = false;
            float clutch = SebBindsApi.IsActionDown(BindAction.Clutch) ? 1f : 0f;

            for (int i = 0; i < schemes.Length; i++)
            {
                var axis = AxisBindingStore.GetAxisBinding(schemes[i], AxisAction.Clutch);
                if (axis.Kind == BindingKind.None)
                {
                    continue;
                }

                hasClutchAxis = true;
                float value = BindingEvaluator.GetAxisValue(axis);
                if (axis.Kind == BindingKind.WheelAxis || axis.Kind == BindingKind.WheelDpadAxis || axis.Kind == BindingKind.GamepadDpadAxis)
                {
                    value = Mathf.Abs(value);
                }
                clutch = Mathf.Max(clutch, Mathf.Clamp01(value));
            }

            if (!hasClutchBind && !hasClutchAxis)
            {
                return;
            }

            // Start before vanilla's Numpad0 update so opposing inputs cannot cancel each frame.
            rally.clutchInput = RallyClutchInput.Advance(previousClutch, clutch, Time.fixedDeltaTime * rally.clutchSensitivity);
        }

        private static void SuppressRallyDrivingInput(sRallyDriving rally)
        {
            rally.throttleInputRaw = 0f;
            rally.throttleInput = 0f;
            rally.brakingInput = 0f;
            rally.steeringInputRaw = 0f;
            rally.steeringInput = 0f;
            rally.handbreakInput = false;
            rally.shiftUpInput = false;
            rally.shiftDownInput = false;
            rally.clutchInput = 0f;
        }

        private static void ApplyRallyMenuBinds(sInputManager input, Func<BindAction, bool> downAny, bool wheelPluginPresent)
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
            input.shiftUp = false;
            input.shiftDown = false;
            input.freeCamTogglePressed = false;
            input.freeCamSelectPressed = false;
            input.radioInput = Vector2.zero;
            input.mouseInput = Vector2.zero;
            input.driveInput = Vector2.zero;
            input.playerInput = Vector2.zero;
            input.cameraLook = Vector2.zero;
            input.brakePressed = false;

            if (input.btn == null || input.pbtn == null || input.btnp == null)
            {
                return;
            }

            bool prevUp = input.pbtn.up;
            bool prevDown = input.pbtn.down;
            bool prevLeft = input.pbtn.left;
            bool prevRight = input.pbtn.right;
            bool prevA = input.pbtn.a;
            bool prevB = input.pbtn.b;
            bool prevStart = input.pbtn.start;
            bool prevSelect = input.pbtn.select;

            if (!input.menuInput)
            {
                prevUp = input.btn.up;
                prevDown = input.btn.down;
                prevLeft = input.btn.left;
                prevRight = input.btn.right;
                prevA = input.btn.a;
                prevB = input.btn.b;
                prevStart = input.btn.start;
                prevSelect = input.btn.select;
            }

            bool up = input.menuInput && input.btn.up;
            bool down = input.menuInput && input.btn.down;
            bool left = input.menuInput && input.btn.left;
            bool right = input.menuInput && input.btn.right;
            bool a = input.menuInput && input.btn.a;
            bool b = input.menuInput && input.btn.b;
            bool start = input.menuInput && input.btn.start;
            bool select = input.menuInput && input.btn.select;

            left |= downAny(BindAction.RadioScanLeft) || downAny(BindAction.MoveLeft) || downAny(BindAction.SteerLeft);
            right |= downAny(BindAction.RadioScanRight) || downAny(BindAction.MoveRight) || downAny(BindAction.SteerRight);
            up |= downAny(BindAction.RadioScanToggle) || downAny(BindAction.MoveUp);
            down |= downAny(BindAction.RadioPower) || downAny(BindAction.MoveDown);
            a |= downAny(BindAction.InteractOk) || downAny(BindAction.FreeCamSelect);
            b |= downAny(BindAction.Back);
            start |= downAny(BindAction.Pause);
            select |= downAny(BindAction.Jobs) || downAny(BindAction.Map) || downAny(BindAction.MapItems);

            if (wheelPluginPresent && WheelInterop.TryGetPov8Vector(out var pov))
            {
                left |= pov.x < -0.25f;
                right |= pov.x > 0.25f;
                up |= pov.y > 0.25f;
                down |= pov.y < -0.25f;
            }

            input.btn.up = up;
            input.btn.down = down;
            input.btn.left = left;
            input.btn.right = right;
            input.btn.a = a;
            input.btn.b = b;
            input.btn.start = start;
            input.btn.select = select;

            input.pbtn.up = prevUp;
            input.pbtn.down = prevDown;
            input.pbtn.left = prevLeft;
            input.pbtn.right = prevRight;
            input.pbtn.a = prevA;
            input.pbtn.b = prevB;
            input.pbtn.start = prevStart;
            input.pbtn.select = prevSelect;

            input.btnp.up = up && !prevUp;
            input.btnp.down = down && !prevDown;
            input.btnp.left = left && !prevLeft;
            input.btnp.right = right && !prevRight;
            input.btnp.a = a && !prevA;
            input.btnp.b = b && !prevB;
            input.btnp.start = start && !prevStart;
            input.btnp.select = select && !prevSelect;
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
