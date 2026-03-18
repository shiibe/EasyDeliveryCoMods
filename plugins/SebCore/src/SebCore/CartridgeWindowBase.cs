using UnityEngine;

namespace SebCore
{
    // Optional base class for cartridge windows.
    // Provides consistent UIUtil wiring, scaled content rect, and default back behavior.
    public abstract class CartridgeWindowBase : MonoBehaviour
    {
        protected UIUtil Util;
        protected DesktopDotExe.WindowView View;

        // Used by UIUtil.Slider(..., ref mouseYLock)
        protected float MouseYLock;

        protected virtual bool EnableMouseYLock => true;

        public virtual void FrameUpdate(DesktopDotExe.WindowView view)
        {
            if (view == null)
            {
                return;
            }

            View = view;

            Util ??= new UIUtil();
            Util.M = view.M;
            Util.R = view.R;
            Util.Nav = view.M.nav;

            Rect p = GetContentRect(view);

            if (EnableMouseYLock)
            {
                if (Util.M.mouseButtonUp)
                {
                    MouseYLock = 0f;
                }
                if (MouseYLock > 0f)
                {
                    Util.M.mouse.y = MouseYLock;
                }
            }

            DrawWindow(p);
        }

        public virtual void BackButtonPressed()
        {
            ReturnToSebCoreAndClose();
        }

        protected abstract void DrawWindow(Rect p);

        protected static Rect GetContentRect(DesktopDotExe.WindowView view)
        {
            Rect p = new Rect(view.position * 8f, view.size * 8f);
            p.position += new Vector2(8f, 8f);
            return p;
        }

        protected float GetNavY(Rect p)
        {
            return p.y + p.height - 18f;
        }

        protected void ReturnToSebCoreAndClose()
        {
            DesktopAppLauncher.TryOpenProgramListener(
                Util?.M,
                Util?.R,
                SebCoreMenuWindow.FileName,
                SebCoreMenuWindow.ListenerData
            );
            View?.Kill();
        }
    }
}
