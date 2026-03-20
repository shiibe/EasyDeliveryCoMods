using System;
using System.IO;
using System.Reflection;
using System.Globalization;
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
        public const string PluginVersion = "1.0.4";

        internal static ManualLogSource Log;

        private static ConfigEntry<bool> _debugLogging;

        private static ConfigEntry<string> _ignitionSfxOnPath;

        private static UnityEngine.Object _ignitionSfxOn;

        private static GameObject _ignitionHoldSfxGo;
        private static Component _ignitionHoldSfxSource;

        private static GameObject _indicatorClickSfxGo;
        private static Component _indicatorClickSfxSource;

        private void Awake()
        {
            Log = Logger;

            _debugLogging = Config.Bind("Debug", "debug_logging", false, "Log debug information (verbose).");

            _ignitionSfxOnPath = Config.Bind("Ignition", "sfx_on_path", "", "Optional ignition ON sound. File name inside the plugin's sfx folder (e.g. ignition_on.wav). Leave blank to use sfx/ignition_on.wav.");
            TryMigrateLegacyIgnitionSfxVolumeFromConfig();
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
                    SebBinds.BindAction.IndicatorLeft,
                    SebBinds.BindAction.IndicatorRight,
                    SebBinds.BindAction.IndicatorHazards,
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

        private static void TryMigrateLegacyIgnitionSfxVolumeFromConfig()
        {
            try
            {
                if (PlayerPrefs.HasKey(PrefKeyIgnitionSfxVolume))
                {
                    return;
                }

                string cfgPath = Path.Combine(Paths.ConfigPath, PluginGuid + ".cfg");
                if (!File.Exists(cfgPath))
                {
                    return;
                }

                bool inIgnition = false;
                float found = -1f;

                foreach (string raw in File.ReadAllLines(cfgPath))
                {
                    string line = raw != null ? raw.Trim() : string.Empty;
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line.StartsWith("#") || line.StartsWith(";")) continue;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        string sec = line.Substring(1, line.Length - 2).Trim();
                        inIgnition = string.Equals(sec, "Ignition", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }

                    if (!inIgnition) continue;

                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();
                    if (!string.Equals(key, "sfx_volume", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                    {
                        found = Mathf.Clamp01(v);
                    }
                    else if (float.TryParse(val, out v))
                    {
                        found = Mathf.Clamp01(v);
                    }
                    break;
                }

                if (found >= 0f)
                {
                    SetIgnitionSfxVolume(found);
                    PlayerPrefs.Save();
                    LogDebug("Migrated legacy ignition volume from config: " + found.ToString("0.00", CultureInfo.InvariantCulture));
                }
            }
            catch
            {
                // ignore
            }
        }

        internal static void LogDebug(string msg)
        {
            if (_debugLogging != null && _debugLogging.Value)
            {
                Log?.LogMessage(msg);
            }
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
            if (!GetIgnitionSfxEnabled())
            {
                return;
            }
            if (_ignitionSfxOn == null)
            {
                return;
            }
            if (car == null)
            {
                return;
            }

            float vol = GetIgnitionSfxVolume();

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

        private static float GetFloatProp(Component c, string name, float def = 0f)
        {
            if (c == null || string.IsNullOrWhiteSpace(name))
            {
                return def;
            }
            try
            {
                var p = c.GetType().GetProperty(name);
                if (p != null && p.CanRead)
                {
                    object v = p.GetValue(c, null);
                    if (v is float f) return f;
                    if (v is double d) return (float)d;
                    if (v is int i) return i;
                }
            }
            catch
            {
            }
            return def;
        }

        private static void Call(Component c, string method, params object[] args)
        {
            if (c == null || string.IsNullOrWhiteSpace(method))
            {
                return;
            }
            try
            {
                var ms = c.GetType().GetMethods();
                MethodInfo m = null;
                for (int i = 0; i < ms.Length; i++)
                {
                    var cand = ms[i];
                    if (cand == null || !string.Equals(cand.Name, method, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    var ps = cand.GetParameters();
                    if (ps != null && ps.Length == (args != null ? args.Length : 0))
                    {
                        m = cand;
                        break;
                    }
                }
                if (m != null)
                {
                    m.Invoke(c, args);
                }
            }
            catch
            {
            }
        }

        internal static void PlayIndicatorClick(sCarController car, bool highPitch)
        {
            if (!GetIndicatorSfxEnabled())
            {
                return;
            }
            if (car == null || car.GuyActive)
            {
                return;
            }

            var hl = car.headlights;
            if (hl == null)
            {
                return;
            }

            try
            {
                // Reuse the headlight toggle OFF clip as the indicator click.
                var clip = AccessTools.Field(hl.GetType(), "headlightsOff")?.GetValue(hl) as UnityEngine.Object;
                if (clip == null)
                {
                    return;
                }

                float vol = 0.22f;
                float pitch = highPitch ? 1.08f : 0.92f;

                if (_indicatorClickSfxGo == null)
                {
                    _indicatorClickSfxGo = new GameObject("SebTruck_IndicatorClickSFX");
                    _indicatorClickSfxGo.hideFlags = HideFlags.HideAndDontSave;
                    var audioSourceType = Type.GetType("UnityEngine.AudioSource, UnityEngine.AudioModule");
                    if (audioSourceType == null)
                    {
                        return;
                    }
                    _indicatorClickSfxSource = _indicatorClickSfxGo.AddComponent(audioSourceType);
                    SetProp(_indicatorClickSfxSource, "loop", false);
                    SetProp(_indicatorClickSfxSource, "playOnAwake", false);
                    SetProp(_indicatorClickSfxSource, "spatialBlend", 1f);
                    SetProp(_indicatorClickSfxSource, "dopplerLevel", 0f);
                    SetProp(_indicatorClickSfxSource, "minDistance", 3f);
                    SetProp(_indicatorClickSfxSource, "maxDistance", 30f);
                }

                if (_indicatorClickSfxGo.transform.parent != car.transform)
                {
                    _indicatorClickSfxGo.transform.SetParent(car.transform, false);
                }
                _indicatorClickSfxGo.transform.localPosition = Vector3.zero;

                SetProp(_indicatorClickSfxSource, "volume", vol);
                SetProp(_indicatorClickSfxSource, "pitch", pitch);
                Call(_indicatorClickSfxSource, "PlayOneShot", clip, vol);
            }
            catch
            {
            }
        }

        internal static void StartIgnitionHoldSfx(sCarController car)
        {
            if (!GetIgnitionSfxEnabled())
            {
                return;
            }
            if (_ignitionSfxOn == null || car == null || car.GuyActive)
            {
                return;
            }

            float vol = GetIgnitionSfxVolume();
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
                    // Always play the ignition sound once, independent of hold duration.
                    SetProp(_ignitionHoldSfxSource, "loop", false);
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
