using System.Collections.Generic;

namespace SebBinds
{
    // Press/release edges derived from a sampled down-state, for inputs and actions with no native edge events.
    internal sealed class EdgeTracker
    {
        private struct State
        {
            public int Frame;
            public bool Down;
            public bool WasDown;
        }

        private readonly Dictionary<int, State> _states = new Dictionary<int, State>();

        // The first sample in a frame wins. After a skipped frame there is no trustworthy previous state,
        // so no edge is reported: an input held through a gap (pause, lockInput) must not fire again.
        internal void Sample(int key, int frame, bool down, out bool isDown, out bool wasDown)
        {
            bool found = _states.TryGetValue(key, out var s);
            if (found && s.Frame == frame)
            {
                isDown = s.Down;
                wasDown = s.WasDown;
                return;
            }

            wasDown = found && s.Frame == frame - 1 ? s.Down : down;
            isDown = down;
            _states[key] = new State { Frame = frame, Down = down, WasDown = wasDown };
        }

        internal bool Pressed(int key, int frame, bool down)
        {
            Sample(key, frame, down, out bool isDown, out bool wasDown);
            return isDown && !wasDown;
        }

        internal bool Released(int key, int frame, bool down)
        {
            Sample(key, frame, down, out bool isDown, out bool wasDown);
            return !isDown && wasDown;
        }
    }
}
