using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SebLogiWheel
{
    public partial class Plugin
    {
        private static Plugin _instance;

        private void Awake()
        {
            _log = Logger;
            _instance = this;

            MigrateConfigIfNeeded(oldGuid: "shibe.easydeliveryco.logiwheel", newGuid: PluginGuid);

            _logDetectedDevices = Config.Bind("Debug", "log_detected_devices", true, "Log joystick names detected by Unity on startup.");
            _debugLogging = Config.Bind("Debug", "debug_logging", false, "Log debug information.");


            _ignoreXInputControllers = Config.Bind("General", "ignore_xinput_controllers", true, "Pass 'ignoreXInputControllers' to the Logitech SDK init (recommended).");

            MigratePrefsFromG920IfNeeded();

            var harmony = new Harmony(PluginGuid);

            // Icon/menu launching is handled by SebCore.
            PatchByName(harmony, "sCarController", "Update", prefix: nameof(SCarController_Update_Prefix));
            PatchByName(harmony, "sInputManager", "GetInput", postfix: nameof(SInputManager_GetInput_Postfix));

            DetectWheelOnce();
            TryInitLogitech();

            _log.LogInfo("SebLogiWheel loaded.");
        }

        private static void MigrateConfigIfNeeded(string oldGuid, string newGuid)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(oldGuid) || string.IsNullOrWhiteSpace(newGuid) || string.Equals(oldGuid, newGuid, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                string cfgDir = Paths.ConfigPath;
                string oldPath = Path.Combine(cfgDir, oldGuid + ".cfg");
                string newPath = Path.Combine(cfgDir, newGuid + ".cfg");

                if (File.Exists(oldPath) && !File.Exists(newPath))
                {
                    File.Copy(oldPath, newPath);
                }
            }
            catch
            {
            }
        }

        private void Update()
        {
            UpdateFfb();
        }

        private void OnDestroy()
        {
            ShutdownLogitech();
        }

        private static void SInputManager_GetInput_Postfix(sInputManager __instance)
        {
            if (!ShouldApply() || __instance == null)
            {
                return;
            }

            // Don't inject wheel input while the game is explicitly locking input (menus/cutscenes/buildings).
            if (__instance.lockInput || PauseSystem.paused)
            {
                return;
            }

            if (_isInWalkingMode)
            {
                return;
            }

            if (!TryGetCachedWheelState(out var state))
            {
                return;
            }

            int rawSteer = GetAxisValue(state, GetSteeringAxis());
            int rawThrottle = GetAxisValue(state, GetThrottleAxis());
            int rawBrake = GetAxisValue(state, GetBrakeAxis());

            float steering = NormalizeSteering(rawSteer);
            float throttle = NormalizePedal(rawThrottle, PedalKind.Throttle);
            float brake = NormalizePedal(rawBrake, PedalKind.Brake);

            // Prevent tiny pedal noise from causing creep.
            if (throttle < 0.05f)
            {
                throttle = 0f;
            }
            if (brake < 0.05f)
            {
                brake = 0f;
            }

            float accel = Mathf.Clamp(throttle - brake, -1f, 1f);
            __instance.driveInput = new Vector2(steering, accel);
            
            SetWheelLastInput(steering, accel);
        }

        #pragma warning disable IDE1006
        private static void SCarController_Update_Prefix(object __instance)
        {
            if (!ShouldApply() || __instance == null)
            {
                return;
            }

            var car = __instance as sCarController;
            if (car == null)
            {
                return;
            }

            _isInWalkingMode = car.GuyActive;

            if (car.rb != null)
            {
                _currentSpeedKmh = car.rb.linearVelocity.magnitude * 3.6f;
            }

            if (car.wheels != null)
            {
                float totalSlide = 0f;
                bool offroad = false;
                foreach (var w in car.wheels)
                {
                    if (w == null)
                    {
                        continue;
                    }
                    totalSlide += w.slide;
                    if (w.suspention != null && string.Equals(w.suspention.contactTag, "offRoad", StringComparison.OrdinalIgnoreCase))
                    {
                        offroad = true;
                    }
                }
                _isOffRoad = offroad;
                _isSliding = totalSlide > 2.0f;
            }
        }
    }
}
