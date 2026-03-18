using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SebTruck
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("shibe.easydeliveryco.sebcore", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("shibe.easydeliveryco.sebbinds", BepInDependency.DependencyFlags.HardDependency)]
    public partial class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "shibe.easydeliveryco.sebtruck";
        public const string PluginName = "SebTruck";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;

        private static ConfigEntry<string> _ignitionSfxOnPath;
        private static ConfigEntry<float> _ignitionSfxVolume;

        private static UnityEngine.Object _ignitionSfxOn;

        private static GameObject _ignitionHoldSfxGo;
        private static AudioSource _ignitionHoldSfxSource;

        private void Awake()
        {
            Log = Logger;

            _ignitionSfxOnPath = Config.Bind("Ignition", "sfx_on_path", "", "Optional ignition ON sound. File name inside the plugin's sfx folder (e.g. ignition_on.wav). Leave blank to use sfx/ignition_on.wav.");
            _ignitionSfxVolume = Config.Bind("Ignition", "sfx_volume", 0.6f, "Ignition sound volume (0..1).");
            TryLoadIgnitionSfx();

            var harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(Plugin));

            // Expose truck-specific binds via SebBinds.
            try
            {
                SebBinds.SebBindsApi.RegisterActionsPage(
                    id: "sebtruck",
                    title: "Truck",
                    SebBinds.BindAction.IgnitionToggle,
                    SebBinds.BindAction.ToggleGearbox,
                    SebBinds.BindAction.ShiftUp,
                    SebBinds.BindAction.ShiftDown
                );
            }
            catch
            {
                // SebBinds may not be installed.
            }

            Log.LogInfo("Loaded");
        }

        private static void TryLoadIgnitionSfx()
        {
            string dir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? string.Empty;

            string Resolve(string cfg, string baseName)
            {
                string sfxDir = Path.Combine(dir, "sfx");
                if (string.IsNullOrWhiteSpace(cfg))
                {
                    string wav = Path.Combine(sfxDir, baseName + ".wav");
                    return File.Exists(wav) ? wav : string.Empty;
                }

                if (Path.IsPathRooted(cfg))
                {
                    return cfg;
                }
                string resolved = Path.Combine(sfxDir, cfg);
                return File.Exists(resolved) ? resolved : string.Empty;
            }

            string onPath = Resolve(_ignitionSfxOnPath != null ? _ignitionSfxOnPath.Value : string.Empty, "ignition_on");
            _ignitionSfxOn = LoadWavOrNull(onPath);
            Log?.LogInfo("Ignition SFX: on=" + (_ignitionSfxOn != null));
        }

        private static UnityEngine.Object LoadWavOrNull(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }
            if (string.Equals(Path.GetExtension(path), ".wav", StringComparison.OrdinalIgnoreCase) == false)
            {
                Log?.LogWarning("Ignition SFX: only .wav is supported in this build: " + path);
                return null;
            }
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length < 44)
                {
                    throw new Exception("WAV too small");
                }

                // Minimal PCM16 reader (ignition_on.wav is shipped as PCM16).
                if (System.Text.Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF" || System.Text.Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE")
                {
                    throw new Exception("Not a RIFF/WAVE file");
                }

                int channels = BitConverter.ToInt16(bytes, 22);
                int sampleRate = BitConverter.ToInt32(bytes, 24);
                int bitsPerSample = BitConverter.ToInt16(bytes, 34);
                if (bitsPerSample != 16)
                {
                    throw new Exception("Unsupported bit depth: " + bitsPerSample);
                }

                // Find data chunk.
                int pos = 12;
                int dataOffset = -1;
                int dataSize = 0;
                while (pos + 8 <= bytes.Length)
                {
                    string id = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
                    int size = BitConverter.ToInt32(bytes, pos + 4);
                    int dataPos = pos + 8;
                    if (dataPos + size > bytes.Length)
                    {
                        break;
                    }
                    if (id == "data")
                    {
                        dataOffset = dataPos;
                        dataSize = size;
                        break;
                    }
                    pos = dataPos + size;
                    if ((pos & 1) == 1) pos++;
                }
                if (dataOffset < 0 || dataSize <= 0)
                {
                    throw new Exception("Missing data chunk");
                }

                int samples = dataSize / 2;
                float[] data = new float[samples];
                int outI = 0;
                for (int i = 0; i + 1 < dataSize; i += 2)
                {
                    short s = BitConverter.ToInt16(bytes, dataOffset + i);
                    data[outI++] = s / 32768f;
                }

                var clip = AudioClip.Create("ignition_on", samples / Math.Max(1, channels), channels, sampleRate, false);
                clip.SetData(data, 0);
                return clip;
            }
            catch (Exception e)
            {
                Log?.LogWarning("Ignition SFX load failed: " + path + " (" + e.Message + ")");
                return null;
            }
        }

        internal static void PlayIgnitionOnSfx(sCarController car)
        {
            if (_ignitionSfxOn == null)
            {
                return;
            }
            if (_ignitionSfxVolume == null)
            {
                return;
            }
            if (car == null)
            {
                return;
            }

            float vol = Mathf.Clamp01(_ignitionSfxVolume.Value);

            try
            {
                // If the car has a headlights GameObject, attach a temporary AudioSource to it.
                GameObject parent = null;
                if (car.headlights != null)
                {
                    parent = car.headlights.gameObject;
                }
                if (parent == null)
                {
                    parent = car.gameObject;
                }

                var go = new GameObject("SebTruck_IgnitionSFX");
                go.hideFlags = HideFlags.HideAndDontSave;
                go.transform.SetParent(parent.transform, false);
                var src = go.AddComponent<AudioSource>();
                src.spatialBlend = 0f;
                src.volume = vol;
                src.playOnAwake = false;
                src.loop = false;

                if (_ignitionSfxOn is AudioClip ac)
                {
                    src.clip = ac;
                }
                else
                {
                    // Some Unity versions return AudioClip as Object; try reflection.
                    var clipProp = src.GetType().GetProperty("clip");
                    if (clipProp != null)
                    {
                        clipProp.SetValue(src, _ignitionSfxOn, null);
                    }
                }

                src.Play();
                UnityEngine.Object.Destroy(go, 5f);
            }
            catch
            {
                // ignore
            }
        }

        internal static void StartIgnitionHoldSfx(sCarController car)
        {
            if (_ignitionSfxOn == null || car == null || car.GuyActive)
            {
                return;
            }

            float vol = _ignitionSfxVolume != null ? Mathf.Clamp01(_ignitionSfxVolume.Value) : 0.6f;
            if (vol <= 0.001f)
            {
                return;
            }

            try
            {
                if (_ignitionHoldSfxGo == null)
                {
                    _ignitionHoldSfxGo = new GameObject("SebTruck_IgnitionHoldSFX");
                    _ignitionHoldSfxGo.hideFlags = HideFlags.HideAndDontSave;
                    _ignitionHoldSfxSource = _ignitionHoldSfxGo.AddComponent<AudioSource>();
                    _ignitionHoldSfxSource.loop = true;
                    _ignitionHoldSfxSource.playOnAwake = false;
                    _ignitionHoldSfxSource.spatialBlend = 1f;
                    _ignitionHoldSfxSource.dopplerLevel = 0f;
                    _ignitionHoldSfxSource.rolloffMode = AudioRolloffMode.Linear;
                    _ignitionHoldSfxSource.minDistance = 5f;
                    _ignitionHoldSfxSource.maxDistance = 40f;

                    try
                    {
                        if (PauseSystem.pauseSystem != null && PauseSystem.pauseSystem.masterMix != null)
                        {
                            var groups = PauseSystem.pauseSystem.masterMix.FindMatchingGroups("SFX");
                            if (groups != null && groups.Length > 0)
                            {
                                _ignitionHoldSfxSource.outputAudioMixerGroup = groups[0];
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                if (_ignitionHoldSfxGo.transform.parent != car.transform)
                {
                    _ignitionHoldSfxGo.transform.SetParent(car.transform, false);
                }
                _ignitionHoldSfxGo.transform.localPosition = Vector3.zero;
                _ignitionHoldSfxSource.volume = vol;

                if (_ignitionSfxOn is AudioClip ac)
                {
                    _ignitionHoldSfxSource.clip = ac;
                }
                else
                {
                    var clipProp = _ignitionHoldSfxSource.GetType().GetProperty("clip");
                    if (clipProp != null)
                    {
                        clipProp.SetValue(_ignitionHoldSfxSource, _ignitionSfxOn, null);
                    }
                }

                if (!_ignitionHoldSfxSource.isPlaying)
                {
                    _ignitionHoldSfxSource.Play();
                }
            }
            catch
            {
            }
        }

        internal static void StopIgnitionHoldSfx()
        {
            try
            {
                if (_ignitionHoldSfxSource != null && _ignitionHoldSfxSource.isPlaying)
                {
                    _ignitionHoldSfxSource.Stop();
                }
            }
            catch
            {
            }
        }
    }
}
