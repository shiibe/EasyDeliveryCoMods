using SebCore;
using UnityEngine;

namespace __MOD_NAME__
{
    // This is the window type referenced by SebCore.CartridgeApps.App.WindowTypeName.
    public sealed class __MOD_NAME__MenuWindow : MonoBehaviour
    {
        public const string ListenerName = "__MOD_NAME__Menu";
        public const string ListenerData = "listener___MOD_NAME__Menu";

        private float _mouseYLock;
        private UIUtil _util;
        private DesktopDotExe.WindowView _view;

        private bool _toggle;
        private float _slider;

        public void FrameUpdate(DesktopDotExe.WindowView view)
        {
            if (view == null)
            {
                return;
            }

            _view = view;

            _util ??= new UIUtil();
            _util.M = view.M;
            _util.R = view.R;
            _util.Nav = view.M.nav;

            Rect p = new Rect(view.position * 8f, view.size * 8f);
            p.position += new Vector2(8f, 8f);

            if (_util.M.mouseButtonUp)
            {
                _mouseYLock = 0f;
            }
            if (_mouseYLock > 0f)
            {
                _util.M.mouse.y = _mouseYLock;
            }

            DrawMenu(p);
        }

        public void BackButtonPressed()
        {
            // Typical cartridge behavior: return to SebCore.
            SebCore.DesktopAppLauncher.TryOpenProgramListener(
                _util?.M,
                _util?.R,
                SebCore.SebCoreMenuWindow.FileName,
                SebCore.SebCoreMenuWindow.ListenerData
            );
            _view?.Kill();
        }

        private void DrawMenu(Rect p)
        {
            float cx = p.x + p.width / 2f;
            float center = cx - 16f;
            float y = p.y + 10f;
            float line = 12f;
            float sectionGap = 4f;

            // Bottom nav.
            float navY = p.y + p.height - 18f;
            if (_util.SimpleButtonRaw("Back", cx, navY))
            {
                BackButtonPressed();
                return;
            }

            _util.Label("__DISPLAY_NAME__", cx, y);
            y += line + sectionGap;

            bool? newToggle = _util.Toggle("Example Toggle", _toggle, center, y);
            if (newToggle.HasValue)
            {
                _toggle = newToggle.Value;
            }
            y += line;

            float? newSlider = _util.Slider("Example Slider", _slider, center, y, ref _mouseYLock);
            if (newSlider.HasValue)
            {
                _slider = newSlider.Value;
            }
            y += line;

            if (_util.FancyButton("Do Something", cx, y + 8f))
            {
                // TODO: your action here
            }
        }
    }
}
