using System;
using System.IO;
using System.Text;
using SebBinds;
using SebTruck;
using UnityEngine;

internal static class Program
{
    private static int _checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++;
    }

    private static void Main()
    {
        foreach (BindingScheme scheme in Enum.GetValues(typeof(BindingScheme)))
        {
            PlayerPrefs.DeleteAll();
            BindingStore.ClearRuntimeCache();
            string prefix = scheme == BindingScheme.Keyboard ? "SebBinds_KB_Bind_" :
                scheme == BindingScheme.Wheel ? "SebBinds_Wheel_Bind_" : "ELWS_Bind_";
            PlayerPrefs.SetInt(prefix + "Normal_Horn", 2001);
            Check(BindingStore.GetBinding(scheme, BindingLayer.Normal, BindAction.Horn).Kind == BindingKind.Key, "Legacy migration failed");
            BindingStore.SetBinding(scheme, BindingLayer.Normal, BindAction.Horn, default);
            BindingStore.ClearRuntimeCache();
            Check(BindingStore.GetBinding(scheme, BindingLayer.Normal, BindAction.Horn).Kind == BindingKind.None, "Cleared legacy binding returned");
        }

        PlayerPrefs.DeleteAll();
        BindingStore.ClearRuntimeCache();
        AxisBindingStore.ClearRuntimeCache();
        KeyboardDefaults.EnsureDefaults();
        BindingStore.SetBinding(BindingScheme.Keyboard, BindingLayer.Normal, BindAction.Horn, default);
        BindingStore.ClearRuntimeCache();
        KeyboardDefaults.EnsureDefaults();
        Check(BindingStore.GetBinding(BindingScheme.Keyboard, BindingLayer.Normal, BindAction.Horn).Kind == BindingKind.None, "Keyboard default replaced explicit None");
        AxisDefaults.EnsureControllerDefaults();
        AxisBindingStore.ClearAxisBinding(BindingScheme.Controller, AxisAction.Throttle);
        AxisBindingStore.ClearRuntimeCache();
        AxisDefaults.EnsureControllerDefaults();
        Check(AxisBindingStore.GetAxisBinding(BindingScheme.Controller, AxisAction.Throttle).Kind == BindingKind.None, "Axis default replaced explicit None");
        PlayerPrefs.SetInt("SebBinds_Axis_" + (int)AxisAction.Clutch, 15001);
        AxisBindingStore.ClearAxisBinding(BindingScheme.Wheel, AxisAction.Clutch);
        AxisBindingStore.ClearRuntimeCache();
        Check(AxisBindingStore.GetAxisBinding(BindingScheme.Wheel, AxisAction.Clutch).Kind == BindingKind.None, "Wheel migration replaced explicit None");

        foreach (BindingKind kind in Enum.GetValues(typeof(BindingKind)))
        {
            var input = new BindingInput { Kind = kind, Code = 1 };
            var decoded = BindingStore.Decode(BindingStore.Encode(input));
            Check(decoded.Kind == kind && (kind == BindingKind.None || decoded.Code == 1), "Binding codec round-trip failed for " + kind);
        }

        float clutch = 0f;
        for (int frame = 0; frame < 20; frame++)
        {
            float beforeVanilla = clutch;
            clutch = Mathf.MoveTowards(clutch, 0f, 0.1f);
            clutch = RallyClutchInput.Advance(beforeVanilla, 1f, 0.1f);
        }
        Check(clutch == 1f, "Rebound clutch never reaches full travel");
        for (int frame = 0; frame < 20; frame++) clutch = RallyClutchInput.Advance(clutch, 0f, 0.1f);
        Check(clutch == 0f, "Clutch release does not reach zero");

        BindingStore.SetBinding(BindingScheme.Wheel, BindingLayer.Normal, BindAction.Horn, new BindingInput { Kind = BindingKind.Button, Code = 17 });
        AxisBindingStore.SetAxisBinding(BindingScheme.Wheel, AxisAction.Clutch, new BindingInput { Kind = BindingKind.WheelAxis, Code = 1 });
        BepInEx.Bootstrap.Chainloader.PluginInfos[Plugin.PluginGuid] = new BepInEx.Bootstrap.PluginInfo { Instance = new Plugin() };
        Plugin.LastInputManager = new sInputManager();
        Plugin.Log = new BepInEx.Logging.ManualLogSource();
        var reset = SebCore.ModPrefsResetter.ClearAllKnownModPrefs();
        Check(reset.DeletedCount > 0, "Global reset deleted no preferences");
        Check(BindingStore.GetBinding(BindingScheme.Wheel, BindingLayer.Normal, BindAction.Horn).Kind == BindingKind.None, "Global reset left cached wheel binding");
        Check(AxisBindingStore.GetAxisBinding(BindingScheme.Wheel, AxisAction.Clutch).Kind == BindingKind.None, "Global reset left cached wheel axis");
        Check(BindingStore.GetBinding(BindingScheme.Keyboard, BindingLayer.Normal, BindAction.Horn).Kind == BindingKind.Key, "Global reset did not reseed keyboard defaults");
        Check(AxisBindingStore.GetAxisBinding(BindingScheme.Controller, AxisAction.Throttle).Kind == BindingKind.GamepadAxis, "Global reset did not reseed axes");
        Check(DefaultPreset.InstallCalls == 1 && Plugin.DefaultsNeedReseeding, "Global reset did not request controller default reseeding");

        var edges = new EdgeTracker();
        Check(!edges.Pressed(1, 1, true), "First sample reported a press");
        Check(edges.Released(1, 2, false), "Release after held frame not reported");
        Check(edges.Pressed(1, 3, true) && edges.Pressed(1, 3, false), "Press not stable within a frame");
        Check(!edges.Released(1, 6, false), "Release reported across a skipped-frame gap");
        Check(!edges.Pressed(1, 9, true) && !edges.Pressed(1, 10, true), "Input held through a gap fired again");
        Check(edges.Released(1, 11, false) && !edges.Released(2, 11, false), "Edge keys are not independent");
        Check(BindingStore.GetBindingLabel(new BindingInput { Kind = BindingKind.GamepadButton, Code = 6 }) == "A" &&
            BindingStore.GetBindingLabel(new BindingInput { Kind = BindingKind.GamepadButton, Code = 32 }) == "LT", "Gamepad button labels wrong");

        // Graphics pref migrations: v1 value -> v2 value, and the version is stamped.
        int[][] pixelation = { new[] { 0, 0 }, new[] { 1, 2 }, new[] { 2, 3 }, new[] { 3, 4 } };
        foreach (var c in pixelation)
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.SetInt("UltrawidePixelationMode", c[0]);
            Check(SebCore.ModGraphics.GetPixelationMode() == c[1] && PlayerPrefs.GetInt("UltrawidePixelationModeVersion", 1) == 2, "Pixelation v1 " + c[0] + " migrated wrong");
            Check(SebCore.ModGraphics.GetPixelationMode() == c[1], "Pixelation migrated twice");
        }
        int[][] viewDistance = { new[] { 0, 1 }, new[] { 1, 0 }, new[] { 2, 1 }, new[] { 3, 2 }, new[] { 4, 3 } };
        foreach (var c in viewDistance)
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.SetInt("UltrawideViewDistanceMode", c[0]);
            Check(SebCore.ModGraphics.GetViewDistanceMode() == c[1] && PlayerPrefs.GetInt("UltrawideViewDistanceModeVersion", 1) == 2, "View distance v1 " + c[0] + " migrated wrong");
            Check(SebCore.ModGraphics.GetViewDistanceMode() == c[1], "View distance migrated twice");
        }
        int[][] vsync = { new[] { 0, 1 }, new[] { 1, 1 }, new[] { 2, 0 } };
        foreach (var c in vsync)
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.SetInt("SebTweaks_Gfx_VSyncMode", c[0]);
            Check(SebCore.ModGraphics.GetVsyncMode() == c[1] && PlayerPrefs.GetInt("SebTweaks_Gfx_VSyncModeVersion", 0) == 2, "VSync v1 " + c[0] + " migrated wrong");
            Check(SebCore.ModGraphics.GetVsyncMode() == c[1], "VSync migrated twice");
        }
        PlayerPrefs.DeleteAll();
        Check(SebCore.ModGraphics.GetPixelationMode() == 3 && SebCore.ModGraphics.GetViewDistanceMode() == 1 && SebCore.ModGraphics.GetVsyncMode() == 1, "Graphics defaults wrong");

        byte[] wav = MakeWav();
        float[] data = WavPcm16.Read(wav, out int channels, out int rate);
        Check(channels == 1 && rate == 44100 && data.Length == 2 && data[0] == -1f && data[1] > 0.99f, "PCM16 samples decoded incorrectly");
        foreach (int size in new[] { -8, -1, int.MaxValue })
        {
            byte[] bad = (byte[])wav.Clone();
            Encoding.ASCII.GetBytes("JUNK").CopyTo(bad, 12);
            BitConverter.GetBytes(size).CopyTo(bad, 16);
            Reject(bad, "Invalid chunk size accepted: " + size);
        }
        byte[] truncated = new byte[wav.Length - 1];
        Array.Copy(wav, truncated, truncated.Length);
        Reject(truncated, "Truncated WAV accepted");
        byte[] badFormat = (byte[])wav.Clone();
        badFormat[20] = 3;
        Reject(badFormat, "Non-PCM WAV accepted");
        byte[] zeroChannels = (byte[])wav.Clone();
        zeroChannels[22] = 0;
        Reject(zeroChannels, "Zero-channel WAV accepted");
        if (File.Exists("plugins/SebTruck/src/SebTruck/sfx/ignition_on.wav"))
        {
            Check(WavPcm16.Read(File.ReadAllBytes("plugins/SebTruck/src/SebTruck/sfx/ignition_on.wav"), out _, out _).Length > 0, "Shipped WAV failed to load");
        }
        Console.WriteLine($"Passed {_checks} regression checks.");
    }

    private static void Reject(byte[] bytes, string message)
    {
        try { WavPcm16.Read(bytes, out _, out _); }
        catch (InvalidDataException) { _checks++; return; }
        throw new Exception(message);
    }

    private static byte[] MakeWav()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(40);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
        writer.Write((short)1); writer.Write((short)1); writer.Write(44100);
        writer.Write(88200); writer.Write((short)2); writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(4);
        writer.Write(short.MinValue); writer.Write(short.MaxValue);
        return stream.ToArray();
    }
}
