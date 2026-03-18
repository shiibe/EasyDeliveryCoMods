using System;
using System.Collections.Generic;
using SebCore;
using UnityEngine;

namespace SebBinds
{
    public class BindsMenuWindow : MonoBehaviour
    {
        public const string FileName = "binds";
        public const string ListenerName = "SebBindsMenu";
        public const string ListenerData = "listener_SebBindsMenu";

        private float _mouseYLock;
        private UIUtil _util;

        private DesktopDotExe.WindowView _view;

        private Page _page = Page.SchemeSelect;
        private int _bindingsPageIndex;

        private BindingScheme _scheme = BindingScheme.Controller;

        private BindAction _bindingCaptureAction;
        private BindingLayer _bindingCaptureLayer;

        private float _bindingCaptureEnterTime = -999f;

        internal static float BindingCaptureHeartbeatTime { get; private set; }

        private bool _bindingDupConfirmActive;
        private BindingInput _bindingDupPendingCaptured;
        private BindingLayer _bindingDupPendingLayer;
        private List<BindingConflict> _bindingDupConflicts;
        private string _bindingDupAxisConflict;

        private struct BindingConflict
        {
            public BindingLayer Layer;
            public BindAction Action;
        }

        private enum Page
        {
            Bindings = 0,
            BindingCapture = 1,
            AxisCapture = 2,
            SchemeSelect = 3
        }

        private AxisAction _axisCaptureAction;

        private static readonly BindAction[] AllBindableActions =
        {
            BindAction.InteractOk,
            BindAction.Back,
            BindAction.MapItems,
            BindAction.Pause,
            BindAction.Drive,
            BindAction.Brake,
            BindAction.Map,
            BindAction.Items,
            BindAction.Jobs,
            BindAction.Camera,
            BindAction.ResetVehicle,
            BindAction.Headlights,
            BindAction.Horn,
            BindAction.FreeCam,
            BindAction.CameraLookX,
            BindAction.CameraLookY,
            BindAction.RadioPower,
            BindAction.RadioScanRight,
            BindAction.RadioScanLeft,
            BindAction.RadioScanToggle,
            // Truck-specific binds (SebTruck will expose these via the API page).
            BindAction.IgnitionToggle,
            BindAction.ToggleGearbox,
            BindAction.ShiftUp,
            BindAction.ShiftDown
        };

        internal static int LastMenuActiveFrame { get; private set; }
        internal static int LastBindingCaptureFrame { get; private set; }

        internal static bool MenuActive
        {
            get
            {
                int f = Time.frameCount;
                return LastMenuActiveFrame == f || LastMenuActiveFrame == f - 1;
            }
        }

        internal static bool BindingCaptureActive
        {
            get
            {
                int f = Time.frameCount;
                if (LastBindingCaptureFrame == f || LastBindingCaptureFrame == f - 1)
                {
                    return true;
                }
                return Time.unscaledTime - BindingCaptureHeartbeatTime < 0.5f;
            }
        }

        public void FrameUpdate(DesktopDotExe.WindowView view)
        {
            LastMenuActiveFrame = Time.frameCount;
            if (_page == Page.BindingCapture)
            {
                LastBindingCaptureFrame = Time.frameCount;
            }

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
            if (_page == Page.BindingCapture)
            {
                _bindingDupConfirmActive = false;
                _page = Page.Bindings;
                return;
            }

            if (_page == Page.AxisCapture)
            {
                _page = Page.Bindings;
                return;
            }

            if (_page == Page.Bindings)
            {
                _page = Page.SchemeSelect;
                return;
            }

            // Close the window.
            _view?.Kill();
        }

        private void DrawMenu(Rect p)
        {
            float center = p.x + p.width / 2f - 16f;
            float y = p.y + 10f;
            float line = 12f;
            float sectionGap = 4f;

            if (_page == Page.SchemeSelect)
            {
                DrawSchemeSelect(p, center, ref y, line, sectionGap);
                return;
            }

            if (_page == Page.BindingCapture)
            {
                Plugin.LogDebug("Binding capture open: " + BindingStore.GetActionLabel(_bindingCaptureAction) + " (" + LayerLabel(_bindingCaptureLayer) + ")");
                DrawBindingCapture(p, center, ref y, line, sectionGap);
                return;
            }

            if (_page == Page.AxisCapture)
            {
                Plugin.LogDebug("Axis capture open: " + _axisCaptureAction);
                DrawAxisCapture(p, center, ref y, line, sectionGap);
                return;
            }

            DrawBindings(p, center, ref y, line, sectionGap);
        }

        private void DrawSchemeSelect(Rect p, float center, ref float y, float line, float sectionGap)
        {
            float cx = p.x + p.width / 2f;
            float midY = p.y + p.height / 2f - 20f;

            _util.Label("Binds", cx, p.y + 10f);
            _util.Label("Choose Input", cx, p.y + 22f);

            string controllerLabel = WheelInterop.IsWheelPluginPresent() ? "Controller/Wheel" : "Controller";
            if (_util.FancyButton(controllerLabel, cx, midY))
            {
                _scheme = BindingScheme.Controller;
                _bindingsPageIndex = 0;
                _page = Page.Bindings;
                return;
            }

            if (_util.FancyButton("Keyboard", cx, midY + 24f))
            {
                _scheme = BindingScheme.Keyboard;
                _bindingsPageIndex = 0;
                _page = Page.Bindings;
                return;
            }
        }

        private void DrawBindings(Rect p, float center, ref float y, float line, float sectionGap)
        {
            _util.Label("Binds", p.x + p.width / 2f, y);
            y += line;

            string schemeLabel = _scheme == BindingScheme.Keyboard ? "Keyboard" : (WheelInterop.IsWheelPluginPresent() ? "Controller/Wheel" : "Controller");
            _util.Label(schemeLabel, p.x + p.width - 18f, y);
            if (_util.SimpleButtonRaw("Switch", p.x + p.width - 50f, y))
            {
                _page = Page.SchemeSelect;
                return;
            }
            y += line;

            float cx = p.x + p.width / 2f;
            float navY = p.y + p.height - 18f;

            float prevX = p.x + 40f;
            float nextX = p.x + p.width - 40f;

            if (_util.SimpleButtonRaw("Prev", prevX, navY))
            {
                _bindingsPageIndex = PrevPageIndex(_bindingsPageIndex);
            }
            if (_util.SimpleButtonRaw("Back", cx, navY))
            {
                _page = Page.SchemeSelect;
             }
            if (_util.SimpleButtonRaw("Next", nextX, navY))
            {
                _bindingsPageIndex = NextPageIndex(_bindingsPageIndex);
            }

            y += sectionGap;

            var pages = GetPagesForScheme();
            if (pages.Count == 0)
            {
                _util.Label("(no pages)", p.x + p.width / 2f, y);
                return;
            }

            if (_bindingsPageIndex >= pages.Count)
            {
                _bindingsPageIndex = 0;
            }

            var page = pages[_bindingsPageIndex];
            _util.Label(page.Title, p.x + p.width / 2f, y);
            y += line + sectionGap;

            _util.Label((_bindingsPageIndex + 1) + "/" + pages.Count, p.x + p.width - 18f, p.y + 10f);
            if (page.Actions == null)
            {
                DrawAxesPage(p, center, ref y, line, sectionGap);
            }
            else
            {
                DrawBindingsButtonsPage(p, center, ref y, line, page.Actions);
            }
        }

        private sealed class PageDef
        {
            public string Title;
            public BindAction[] Actions;
        }

        private static List<PageDef> GetPages()
        {
            var pages = new List<PageDef>
            {
                // Placeholder; populated in GetPagesForScheme().
            };

            // This method is now unused; kept to reduce churn.
            return pages;
        }

        private List<PageDef> GetPagesForScheme()
        {
            if (_scheme == BindingScheme.Controller)
            {
                var pages = new List<PageDef>
                {
                    new PageDef { Title = "Axes", Actions = null },
                    new PageDef
                    {
                        Title = "Global",
                        Actions = new[]
                        {
                            BindAction.InteractOk,
                            BindAction.Back,
                            BindAction.Pause,
                            BindAction.Map,
                            BindAction.Items,
                            BindAction.Jobs,
                            BindAction.ResetVehicle,
                            BindAction.Camera
                        }
                    },
                    new PageDef
                    {
                        Title = "Vehicle",
                        Actions = new[]
                        {
                            BindAction.Headlights,
                            BindAction.Horn,
                            BindAction.FreeCam
                        }
                    },
                    new PageDef
                    {
                        Title = "Radio",
                        Actions = new[]
                        {
                            BindAction.RadioPower,
                            BindAction.RadioScanRight,
                            BindAction.RadioScanLeft,
                            BindAction.RadioScanToggle
                        }
                    }
                };

                foreach (var extra in SebBindsApi.GetExtraPagesSnapshot())
                {
                    if (extra == null || string.IsNullOrWhiteSpace(extra.Title) || extra.Actions == null)
                    {
                        continue;
                    }
                    pages.Add(new PageDef { Title = extra.Title.Trim(), Actions = extra.Actions });
                }

                return pages;
            }

            // Keyboard pages.
            return new List<PageDef>
            {
                new PageDef
                {
                    Title = "Movement",
                    Actions = new[]
                    {
                        BindAction.MoveUp,
                        BindAction.MoveDown,
                        BindAction.MoveLeft,
                        BindAction.MoveRight
                    }
                },
                new PageDef
                {
                    Title = "Vehicle",
                    Actions = new[]
                    {
                        BindAction.SteerLeft,
                        BindAction.SteerRight,
                        BindAction.Drive,
                        BindAction.Brake
                    }
                },
                new PageDef
                {
                    Title = "Camera",
                    Actions = new[]
                    {
                        BindAction.LookUp,
                        BindAction.LookDown,
                        BindAction.LookLeft,
                        BindAction.LookRight
                    }
                }
            };
        }

        private void DrawBindingsButtonsPage(Rect p, float center, ref float y, float line, BindAction[] actions)
        {
            float cx = p.x + p.width / 2f;

            // Modifier capture (scheme-specific).
            if (_util.CycleButtonRaw("Modifier", BindingStore.GetBindingLabel(BindingStore.GetModifierBinding(_scheme)), center, y))
            {
                _bindingCaptureAction = BindAction.InteractOk;
                _bindingCaptureLayer = BindingLayer.Normal;
                _bindingDupConfirmActive = false;
                _page = Page.BindingCapture;
                _bindingCaptureEnterTime = Time.unscaledTime;
                // Special-case: capture modifier.
                _isCapturingModifier = true;
                return;
            }
            y += line;

            if (_util.SimpleButtonRaw("Reset Defaults", cx, p.y + p.height - 30f))
            {
                BindingStore.ClearAll();
                AxisBindingStore.ClearAll();
                Plugin.Log?.LogInfo("Bindings cleared; defaults will re-seed from game input");
            }

            y += 3f;
            DrawBindingTable(p, actions, ref y, line);
        }

        private void DrawAxesPage(Rect p, float center, ref float y, float line, float sectionGap)
        {
            float labelX = p.x + 12f;
            float bindRight = p.x + p.width - 12f;

            _util.R.fontOptions.alignment = sFancyText.FontOptions.Alignment.left;
            _util.R.fput("Axis", labelX, y);
            _util.R.fontOptions.alignment = sFancyText.FontOptions.Alignment.right;
            _util.R.fput("Binding", bindRight, y);
            y += line;

            DrawAxisRow(AxisAction.MoveY, "Move Up/Down", labelX, bindRight, y);
            y += line;
            DrawAxisRow(AxisAction.MoveX, "Move Left/Right", labelX, bindRight, y);
            y += line;
            DrawAxisRow(AxisAction.Steering, "Steering", labelX, bindRight, y);
            y += line;
            DrawAxisRow(AxisAction.Throttle, "Throttle", labelX, bindRight, y);
            y += line;
            DrawAxisRow(AxisAction.Brake, "Brake", labelX, bindRight, y);
            y += line;

            DrawAxisRow(AxisAction.CameraLookX, "Cam Look X", labelX, bindRight, y);
            y += line;
            DrawAxisRow(AxisAction.CameraLookY, "Cam Look Y", labelX, bindRight, y);
            y += line + sectionGap;

            _util.Label("Click a binding, then move/press", p.x + p.width / 2f, p.y + p.height - 30f);
        }

        private void DrawAxisRow(AxisAction axis, string label, float labelX, float bindRight, float y)
        {
            _util.R.fontOptions.alignment = sFancyText.FontOptions.Alignment.left;
            _util.R.fput(label, labelX, y);

            var bind = AxisBindingStore.GetAxisBinding(axis);
            string value = bind.Kind == BindingKind.None ? "[none]" : ("[" + BindingStore.GetBindingLabel(bind) + "]");
            if (bind.Kind != BindingKind.None && HasAxisConflictMark(axis, bind))
            {
                value += " !";
            }

            int w = 96;
            int x = (int)(bindRight - w);
            int yy = (int)y;
            if (_util.M.MouseOver(x, yy, w, 8))
            {
                _util.M.mouseIcon = 128;
                if (_util.M.mouseButton)
                {
                    _util.M.mouseIcon = 160;
                }
                if (_util.M.mouseButtonUp)
                {
                    _axisCaptureAction = axis;
                    _page = Page.AxisCapture;
                    _bindingCaptureEnterTime = Time.unscaledTime;
                    return;
                }
            }

            _util.R.fontOptions.alignment = sFancyText.FontOptions.Alignment.right;
            _util.R.fput(value, bindRight, y);
        }

        private static bool HasAxisConflictMark(AxisAction axis, BindingInput input)
        {
            // Mark if the same binding is used by another axis slot.
            foreach (AxisAction a in System.Enum.GetValues(typeof(AxisAction)))
            {
                if (a == axis)
                {
                    continue;
                }

                // Exception: Move Left/Right and Steering can share the same axis.
                if ((axis == AxisAction.Steering && a == AxisAction.MoveX) || (axis == AxisAction.MoveX && a == AxisAction.Steering))
                {
                    continue;
                }

                var other = AxisBindingStore.GetAxisBinding(a);
                if (other.Kind == BindingKind.None)
                {
                    continue;
                }
                if (other.Kind == input.Kind && other.Code == input.Code)
                {
                    return true;
                }
            }
            return false;
        }

        private void DrawBindingTable(Rect p, BindAction[] actions, ref float y, float line)
        {
            float labelX = p.x + 12f;
            float pressRight = p.x + p.width - 84f;
            float holdRight = p.x + p.width - 12f;

            _util.R.fontOptions.alignment = sFancyText.FontOptions.Alignment.left;
            _util.R.fput("Action", labelX, y);
            _util.R.fontOptions.alignment = sFancyText.FontOptions.Alignment.right;
            _util.R.fput("Press", pressRight, y);
            _util.R.fput("Hold", holdRight, y);
            y += line;

            int maxVisible = 9;
            for (int i = 0; i < actions.Length && i < maxVisible; i++)
            {
                var action = actions[i];
                DrawBindingRow(p, action, labelX, pressRight, holdRight, y);
                y += line;
            }
        }

        private void DrawBindingRow(Rect p, BindAction action, float labelX, float pressRight, float holdRight, float y)
        {
            _util.R.fontOptions.alignment = sFancyText.FontOptions.Alignment.left;
            _util.R.fput(BindingStore.GetActionLabel(action), labelX, y);

            DrawBindingCell(action, BindingLayer.Normal, pressRight, y);
            DrawBindingCell(action, BindingLayer.Modified, holdRight, y);
        }

        private void DrawBindingCell(BindAction action, BindingLayer layer, float rightX, float y)
        {
            if (!IsLayerSupported(action, layer))
            {
                _util.R.fontOptions.alignment = sFancyText.FontOptions.Alignment.right;
                _util.R.fput("n/a", rightX, y);
                return;
            }

            var bind = BindingStore.GetBinding(_scheme, layer, action);
            string value = bind.Kind == BindingKind.None ? "[none]" : ("[" + BindingStore.GetBindingLabel(bind) + "]");

            int w = 72;
            int x = (int)(rightX - w);
            int yy = (int)y;
            if (_util.M.MouseOver(x, yy, w, 8))
            {
                _util.M.mouseIcon = 128;
                if (_util.M.mouseButton)
                {
                    _util.M.mouseIcon = 160;
                }
                if (_util.M.mouseButtonUp)
                {
                    _bindingCaptureAction = action;
                    _bindingCaptureLayer = layer;
                    _bindingDupConfirmActive = false;
                    _page = Page.BindingCapture;
                    _bindingCaptureEnterTime = Time.unscaledTime;
                    return;
                }
            }

            _util.R.fontOptions.alignment = sFancyText.FontOptions.Alignment.right;
            _util.R.fput(value, rightX, y);
        }

        private void DrawBindingCapture(Rect p, float center, ref float y, float line, float sectionGap)
        {
            BindingCaptureHeartbeatTime = Time.unscaledTime;

            _util.Label("Binds", p.x + p.width / 2f, y);
            y += line;

            string secondLine = _isCapturingModifier
                ? "Modifier"
                : (BindingStore.GetActionLabel(_bindingCaptureAction) + " (" + LayerLabel(_bindingCaptureLayer) + ")");

            float promptY = p.y + p.height / 2f - 18f;
            _util.Label("Press a button for:", p.x + p.width / 2f, promptY);
            _util.Label(secondLine, p.x + p.width / 2f, promptY + line);

            float cx = p.x + p.width / 2f;
            float clearY = p.y + p.height - 30f;
            float cancelY = p.y + p.height - 18f;

            bool allowUiClicks = Time.unscaledTime - _bindingCaptureEnterTime > 0.25f;

            if (_bindingDupConfirmActive)
            {
                _util.Label("Already used by:", p.x + p.width / 2f, promptY + line * 4f);
                _util.Label(GetDupConflictsText(_bindingDupConflicts), p.x + p.width / 2f, promptY + line * 5f);

                if (!string.IsNullOrWhiteSpace(_bindingDupAxisConflict))
                {
                    _util.Label(_bindingDupAxisConflict, p.x + p.width / 2f, promptY + line * 6f);
                }

                if (allowUiClicks && _util.SimpleButtonRaw("Replace", cx, clearY))
                {
                    ApplyPendingBinding(replaceDuplicates: true);
                    return;
                }

                if (allowUiClicks && _util.SimpleButtonRaw("Cancel", cx, cancelY))
                {
                    _bindingDupConfirmActive = false;
                    return;
                }

                return;
            }

            if (allowUiClicks && _util.SimpleButtonRaw("Clear", cx, clearY))
            {
                if (_isCapturingModifier)
                {
                    BindingStore.SetModifierBinding(_scheme, new BindingInput { Kind = BindingKind.None, Code = 0 });
                }
                else
                {
                    BindingStore.SetBinding(_scheme, _bindingCaptureLayer, _bindingCaptureAction, new BindingInput { Kind = BindingKind.None, Code = 0 });
                }

                _bindingDupConfirmActive = false;
                _isCapturingModifier = false;
                _page = Page.Bindings;
                return;
            }

            if (allowUiClicks && _util.SimpleButtonRaw("Cancel", cx, cancelY))
            {
                _bindingDupConfirmActive = false;
                _isCapturingModifier = false;
                _page = Page.Bindings;
                return;
            }

            var mode = Plugin.GetActiveInputMode();
            if (!InputCapture.TryCaptureNextBinding(mode, _bindingCaptureAction, _bindingCaptureLayer, out var captured))
            {
                return;
            }

            if (_isCapturingModifier)
            {
                BindingStore.SetModifierBinding(_scheme, captured);
                _bindingDupConfirmActive = false;
                _isCapturingModifier = false;
                _page = Page.Bindings;
                return;
            }

            var targetLayer = _bindingCaptureLayer;

            if (TryFindDuplicateBindings(captured, _bindingCaptureAction, targetLayer, out var conflicts))
            {
                _bindingDupConfirmActive = true;
                _bindingDupPendingCaptured = captured;
                _bindingDupPendingLayer = targetLayer;
                _bindingDupConflicts = conflicts;
                _bindingDupAxisConflict = GetAxisConflictForButton(_bindingCaptureAction);
                return;
            }

            // Axis conflict only.
            string axisOnlyConflict = GetAxisConflictForButton(_bindingCaptureAction);
            if (!string.IsNullOrWhiteSpace(axisOnlyConflict))
            {
                _bindingDupConfirmActive = true;
                _bindingDupPendingCaptured = captured;
                _bindingDupPendingLayer = targetLayer;
                _bindingDupConflicts = new List<BindingConflict>();
                _bindingDupAxisConflict = axisOnlyConflict;
                return;
            }

            ApplyBindingNow(captured, targetLayer);

            _bindingDupConfirmActive = false;
            _bindingDupAxisConflict = null;
            _isCapturingModifier = false;
            _page = Page.Bindings;
        }

        private void DrawAxisCapture(Rect p, float center, ref float y, float line, float sectionGap)
        {
            BindingCaptureHeartbeatTime = Time.unscaledTime;

            _util.Label("Binds", p.x + p.width / 2f, y);
            y += line;

            float promptY = p.y + p.height / 2f - 18f;
            _util.Label("Move an axis for:", p.x + p.width / 2f, promptY);
            _util.Label(_axisCaptureAction.ToString(), p.x + p.width / 2f, promptY + line);

            float cx = p.x + p.width / 2f;
            float clearY = p.y + p.height - 30f;
            float cancelY = p.y + p.height - 18f;

            bool allowUiClicks = Time.unscaledTime - _bindingCaptureEnterTime > 0.25f;

            if (allowUiClicks && _util.SimpleButtonRaw("Clear", cx, clearY))
            {
                AxisBindingStore.ClearAxisBinding(_axisCaptureAction);
                _page = Page.Bindings;
                return;
            }

            if (allowUiClicks && _util.SimpleButtonRaw("Cancel", cx, cancelY))
            {
                _page = Page.Bindings;
                return;
            }

            var mode = Plugin.GetActiveInputMode();

            // Prefer axis movement.
            if (AxisCapture.TryCaptureNextAxis(mode, out var capturedAxis))
            {
                AxisBindingStore.SetAxisBinding(_axisCaptureAction, capturedAxis);
                _page = Page.Bindings;
                return;
            }

            // Also allow binding a button/key/mouse to an axis slot.
            if (InputCapture.TryCaptureNextBinding(mode, BindAction.InteractOk, BindingLayer.Normal, out var capturedBtn))
            {
                AxisBindingStore.SetAxisBinding(_axisCaptureAction, capturedBtn);
                _page = Page.Bindings;
            }
        }

        private void ApplyPendingBinding(bool replaceDuplicates)
        {
            if (!_bindingDupConfirmActive)
            {
                return;
            }

            if (replaceDuplicates && _bindingDupConflicts != null)
            {
                foreach (var c in _bindingDupConflicts)
                {
                    BindingStore.SetBinding(_scheme, c.Layer, c.Action, new BindingInput { Kind = BindingKind.None, Code = 0 });
                }
            }

            // If we are replacing and there's an axis conflict, clear it too.
            if (replaceDuplicates)
            {
                ClearAxisConflictForButton(_bindingCaptureAction);
            }

            ApplyBindingNow(_bindingDupPendingCaptured, _bindingDupPendingLayer);

            _bindingDupConfirmActive = false;
            _bindingDupAxisConflict = null;
            _isCapturingModifier = false;
            _page = Page.Bindings;
        }

        private static void ClearAxisConflictForButton(BindAction action)
        {
            if (action == BindAction.Drive)
            {
                AxisBindingStore.ClearAxisBinding(AxisAction.Throttle);
            }
            else if (action == BindAction.Brake)
            {
                AxisBindingStore.ClearAxisBinding(AxisAction.Brake);
            }
            else if (action == BindAction.SteerLeft || action == BindAction.SteerRight)
            {
                AxisBindingStore.ClearAxisBinding(AxisAction.Steering);
            }
        }

        private static string GetAxisConflictForButton(BindAction action)
        {
            // If an axis is mapped for the same conceptual control, warn.
            bool HasAxis(AxisAction a)
            {
                var b = AxisBindingStore.GetAxisBinding(a);
                return b.Kind != BindingKind.None;
            }

            if (action == BindAction.Drive && HasAxis(AxisAction.Throttle))
            {
                return "Axis: Throttle";
            }
            if (action == BindAction.Brake && HasAxis(AxisAction.Brake))
            {
                return "Axis: Brake";
            }
            if ((action == BindAction.SteerLeft || action == BindAction.SteerRight) && HasAxis(AxisAction.Steering))
            {
                return "Axis: Steering";
            }
            return null;
        }

        private void ApplyBindingNow(BindingInput captured, BindingLayer targetLayer)
        {
            BindingStore.SetBinding(_scheme, targetLayer, _bindingCaptureAction, captured);
        }

        private static bool SameBinding(BindingInput a, BindingInput b)
        {
            return a.Kind == b.Kind && a.Code == b.Code;
        }

        private bool TryFindDuplicateBindings(BindingInput captured, BindAction targetAction, BindingLayer targetLayer, out List<BindingConflict> conflicts)
        {
            conflicts = null;

            foreach (var action in AllBindableActions)
            {
                if (IsDuplicateAllowed(action, targetAction))
                {
                    continue;
                }

                if (action == targetAction)
                {
                    continue;
                }

                var existing = BindingStore.GetBinding(_scheme, targetLayer, action);
                if (existing.Kind == BindingKind.None)
                {
                    continue;
                }

                if (SameBinding(existing, captured))
                {
                    conflicts ??= new List<BindingConflict>();
                    conflicts.Add(new BindingConflict { Layer = targetLayer, Action = action });
                }
            }

            return conflicts != null && conflicts.Count > 0;
        }

        private static bool IsDuplicateAllowed(BindAction a, BindAction b)
        {
            if (a == b)
            {
                return true;
            }

            // Vanilla uses the same input for Back + Brake.
            if ((a == BindAction.Back && b == BindAction.Brake) || (a == BindAction.Brake && b == BindAction.Back))
            {
                return true;
            }

            // Jobs is a convenience alias of Map.
            if ((a == BindAction.Map && b == BindAction.Jobs) || (a == BindAction.Jobs && b == BindAction.Map))
            {
                return true;
            }

            return false;
        }

        private static string GetDupConflictsText(List<BindingConflict> conflicts)
        {
            if (conflicts == null || conflicts.Count == 0)
            {
                return string.Empty;
            }

            var c = conflicts[0];
            return LayerLabel(c.Layer) + ": " + BindingStore.GetActionLabel(c.Action);
        }

        private static string LayerLabel(BindingLayer layer)
        {
            return layer == BindingLayer.Normal ? "Press" : "Hold";
        }

        private static bool IsLayerSupported(BindAction action, BindingLayer layer)
        {
            // Press/Hold are both valid for button bindings; axes are handled on the Axes page.
            return true;
        }

        private static int NextPageIndex(int index)
        {
            int count = GetPages().Count;
            if (count <= 0)
            {
                return 0;
            }
            int v = index + 1;
            if (v >= count)
            {
                v = 0;
            }
            return v;
        }

        private static int PrevPageIndex(int index)
        {
            int count = GetPages().Count;
            if (count <= 0)
            {
                return 0;
            }
            int v = index - 1;
            if (v < 0)
            {
                v = count - 1;
            }
            return v;
        }
    }
}
