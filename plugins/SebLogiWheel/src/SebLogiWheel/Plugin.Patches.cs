using System;
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

            SebCore.ConfigUtil.CopyLegacyConfigIfNeeded(oldGuid: "shibe.easydeliveryco.logiwheel", newGuid: PluginGuid);

            _logDetectedDevices = Config.Bind("Debug", "log_detected_devices", true, "Log joystick names detected by Unity on startup.");
            _debugLogging = Config.Bind("Debug", "debug_logging", false, "Log debug information.");


            _ignoreXInputControllers = Config.Bind("General", "ignore_xinput_controllers", true, "Pass 'ignoreXInputControllers' to the Logitech SDK init (recommended).");

            MigratePrefsFromG920IfNeeded();
            SebBinds.SebBindsApi.RegisterWheelSource(new BindsWheelSource());

            var harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(Plugin).Assembly);

            // Icon/menu launching is handled by SebCore.
            PatchByName(harmony, "sCarController", "Update", prefix: nameof(SCarController_Update_Prefix));
            PatchByName(harmony, "sInputManager", "GetInput", postfix: nameof(SInputManager_GetInput_Postfix));
            PatchByName(harmony, "CarDamage", "OnCollisionEnter", postfix: nameof(CarDamage_OnCollisionEnter_Postfix));

            DetectWheelOnce();
            TryInitLogitech();

            _log.LogInfo("SebLogiWheel loaded.");
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

            float accel = Mathf.Clamp(throttle - brake, -1f, 1f);
            __instance.driveInput = new Vector2(steering, accel);
            
            SetWheelLastInput(steering, accel);
        }

        // __args instead of a named parameter so a changed game signature skips the jolt instead of failing the patch.
        private static void CarDamage_OnCollisionEnter_Postfix(Component __instance, object[] __args)
        {
            if (!_logiAvailable || _isInWalkingMode || PauseSystem.paused || !GetFfbEnabled() || __instance == null)
            {
                return;
            }

            if (__args == null || __args.Length == 0 || !(__args[0] is Collision collision) || collision.contactCount == 0)
            {
                return;
            }

            // Ignore scrapes; ~15 m/s impact hits full strength.
            float impact = collision.relativeVelocity.magnitude;
            if (impact < 2f)
            {
                return;
            }
            int magnitude = Mathf.Clamp(Mathf.RoundToInt(impact * 6.5f), 0, 100);

            try
            {
                Vector3 normal = collision.GetContact(0).normal;
                if (Mathf.Abs(Vector3.Dot(normal, __instance.transform.forward)) > 0.7f)
                {
                    LogitechGSDK.LogiPlayFrontalCollisionForce(_logiIndex, magnitude);
                }
                else
                {
                    LogitechGSDK.LogiPlaySideCollisionForce(_logiIndex, magnitude);
                }
            }
            catch
            {
                // ignore
            }
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
                _wheelSlide = totalSlide;
                _isSliding = totalSlide > 2.0f;
            }
        }
    }
}
