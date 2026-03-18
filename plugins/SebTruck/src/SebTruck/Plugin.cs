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
        private static Component _ignitionHoldSfxSource;

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

                return CreateAudioClip("ignition_on", samples / Math.Max(1, channels), channels, sampleRate, data);
            }
            catch (Exception e)
            {
                Log?.LogWarning("Ignition SFX load failed: " + path + " (" + e.Message + ")");
                return null;
            }
        }

        private static UnityEngine.Object CreateAudioClip(string name, int samplesPerChannel, int channels, int sampleRate, float[] data)
        {
            try
            {
                var audioClipType = Type.GetType("UnityEngine.AudioClip, UnityEngine.AudioModule");
                if (audioClipType == null)
                {
                    return null;
                }

                var create = audioClipType.GetMethod(
                    "Create",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(int), typeof(int), typeof(int), typeof(bool) },
                    null);
                if (create == null)
                {
                    return null;
                }

                object clipObj = create.Invoke(null, new object[] { name, samplesPerChannel, channels, sampleRate, false });
                if (clipObj == null)
                {
                    return null;
                }

                var setData = audioClipType.GetMethod("SetData", new[] { typeof(float[]), typeof(int) });
                if (setData != null)
                {
                    setData.Invoke(clipObj, new object[] { data, 0 });
                }

                return clipObj as UnityEngine.Object;
            }
            catch
            {
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
                var audioSourceType = Type.GetType("UnityEngine.AudioSource, UnityEngine.AudioModule");
                if (audioSourceType == null)
                {
                    return;
                }

                var src = go.AddComponent(audioSourceType);
                SetProp(src, "spatialBlend", 0f);
                SetProp(src, "volume", vol);
                SetProp(src, "playOnAwake", false);
                SetProp(src, "loop", false);
                SetProp(src, "clip", _ignitionSfxOn);
                Call(src, "Play");
                UnityEngine.Object.Destroy(go, 5f);
            }
            catch
            {
                // ignore
            }
        }

        private static void SetProp(Component c, string name, object value)
        {
            if (c == null || string.IsNullOrWhiteSpace(name))
            {
                return;
            }
            try
            {
                var p = c.GetType().GetProperty(name);
                if (p != null && p.CanWrite)
                {
                    p.SetValue(c, value, null);
                }
            }
            catch
            {
            }
        }

        private static void Call(Component c, string method)
        {
            if (c == null || string.IsNullOrWhiteSpace(method))
            {
                return;
            }
            try
            {
                var m = c.GetType().GetMethod(method, Type.EmptyTypes);
                if (m != null)
                {
                    m.Invoke(c, null);
                }
            }
            catch
            {
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
                    var audioSourceType = Type.GetType("UnityEngine.AudioSource, UnityEngine.AudioModule");
                    if (audioSourceType == null)
                    {
                        return;
                    }
                    _ignitionHoldSfxSource = _ignitionHoldSfxGo.AddComponent(audioSourceType);
                    SetProp(_ignitionHoldSfxSource, "loop", true);
                    SetProp(_ignitionHoldSfxSource, "playOnAwake", false);
                    SetProp(_ignitionHoldSfxSource, "spatialBlend", 1f);
                    SetProp(_ignitionHoldSfxSource, "dopplerLevel", 0f);
                    SetProp(_ignitionHoldSfxSource, "minDistance", 5f);
                    SetProp(_ignitionHoldSfxSource, "maxDistance", 40f);
                }

                if (_ignitionHoldSfxGo.transform.parent != car.transform)
                {
                    _ignitionHoldSfxGo.transform.SetParent(car.transform, false);
                }
                _ignitionHoldSfxGo.transform.localPosition = Vector3.zero;
                SetProp(_ignitionHoldSfxSource, "volume", vol);
                SetProp(_ignitionHoldSfxSource, "clip", _ignitionSfxOn);

                bool isPlaying = false;
                try
                {
                    var p = _ignitionHoldSfxSource.GetType().GetProperty("isPlaying");
                    if (p != null)
                    {
                        isPlaying = (bool)p.GetValue(_ignitionHoldSfxSource, null);
                    }
                }
                catch
                {
                }
                if (!isPlaying)
                {
                    Call(_ignitionHoldSfxSource, "Play");
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
                if (_ignitionHoldSfxSource != null)
                {
                    Call(_ignitionHoldSfxSource, "Stop");
                }
            }
            catch
            {
            }
        }
    }
}
