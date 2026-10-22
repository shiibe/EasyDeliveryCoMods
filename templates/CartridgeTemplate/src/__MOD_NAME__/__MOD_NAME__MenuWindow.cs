using SebCore;
using UnityEngine;

namespace __MOD_NAME__
{
    // This is the window type referenced by SebCore.CartridgeApps.App.WindowTypeName.
    public sealed class __MOD_NAME__MenuWindow : CartridgeWindowBase
    {
        public const string ListenerName = "__MOD_NAME__Menu";
        public const string ListenerData = "listener___MOD_NAME__Menu";

        private const int PageCount = 2;

        private int _page;
        private int _helloCount;
        private bool _toggle;
        private float _volume = 0.5f;

        protected override void DrawWindow(Rect p)
        {
            float cx = p.x + p.width / 2f;
            float center = cx - 16f;
            float valueX = p.x + p.width - 12f;
            float y = p.y + 10f;
            float line = 12f;
            float sectionGap = 4f;

            // Prev / Back / Next footer, shared with the other cartridges.
            var navigation = Util.NavigationFooter(p);
            if (navigation == UIUtil.NavigationAction.Back)
            {
                BackButtonPressed();
                return;
            }
            if (navigation == UIUtil.NavigationAction.Previous)
            {
                _page = (_page + PageCount - 1) % PageCount;
            }
            if (navigation == UIUtil.NavigationAction.Next)
            {
                _page = (_page + 1) % PageCount;
            }

            Util.PageIndicator(p, _page, PageCount);

            Util.Label("__DISPLAY_NAME__", cx, y);
            y += line + sectionGap;

            if (_page == 0)
            {
                // Hello world: a button that updates a label.
                Util.Label(_helloCount == 0 ? "Hello, world!" : $"Hello x{_helloCount}", cx, y);
                y += line;

                if (Util.FancyButton("Say Hello", cx, y + 8f))
                {
                    _helloCount++;
                    Debug.Log("[__MOD_NAME__] Hello, world!");
                }
                return;
            }

            bool? newToggle = Util.Toggle("Example Toggle", _toggle, center, y);
            if (newToggle.HasValue)
            {
                _toggle = newToggle.Value;
            }
            y += line;

            float? newVolume = Util.SliderRow("Example Slider", _volume, 0f, 1f, $"{Mathf.RoundToInt(_volume * 100f)}%", center, valueX, y, ref MouseYLock);
            if (newVolume.HasValue)
            {
                _volume = newVolume.Value;
            }
        }
    }
}
