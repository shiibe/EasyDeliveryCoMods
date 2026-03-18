using SebCore;
using UnityEngine;

namespace SebTweaks
{
    // This is the window type referenced by SebCore.CartridgeApps.App.WindowTypeName.
    public sealed class SebTweaksMenuWindow : CartridgeWindowBase
    {
        public const string ListenerName = "SebTweaksMenu";
        public const string ListenerData = "listener_SebTweaksMenu";

        private bool _toggle;
        private float _slider;

        protected override void DrawWindow(Rect p)
        {
            float cx = p.x + p.width / 2f;
            float center = cx - 16f;
            float y = p.y + 10f;
            float line = 12f;
            float sectionGap = 4f;

            // Bottom nav.
            float navY = GetNavY(p);
            if (Util.SimpleButtonRaw("Back", cx, navY))
            {
                BackButtonPressed();
                return;
            }

            Util.Label("Tweaks", cx, y);
            y += line + sectionGap;

            bool? newToggle = Util.Toggle("Example Toggle", _toggle, center, y);
            if (newToggle.HasValue)
            {
                _toggle = newToggle.Value;
            }
            y += line;

            float? newSlider = Util.Slider("Example Slider", _slider, center, y, ref MouseYLock);
            if (newSlider.HasValue)
            {
                _slider = newSlider.Value;
            }
            y += line;

            if (Util.FancyButton("Do Something", cx, y + 8f))
            {
                // TODO: your action here
            }
        }
    }
}

