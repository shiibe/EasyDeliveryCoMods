namespace SebBinds
{
    internal static class AxisDefaults
    {
        internal static void EnsureControllerDefaults()
        {
            SetIfUnset(AxisAction.MoveY, new BindingInput { Kind = BindingKind.GamepadAxis, Code = 3 });
            SetIfUnset(AxisAction.MoveX, new BindingInput { Kind = BindingKind.GamepadAxis, Code = 2 });
            SetIfUnset(AxisAction.Steering, new BindingInput { Kind = BindingKind.GamepadAxis, Code = 2 });
            SetIfUnset(AxisAction.Throttle, new BindingInput { Kind = BindingKind.GamepadAxis, Code = 1 });
            SetIfUnset(AxisAction.Brake, new BindingInput { Kind = BindingKind.GamepadAxis, Code = 0 });
            SetIfUnset(AxisAction.CameraLookX, new BindingInput { Kind = BindingKind.GamepadAxis, Code = 4 });
            SetIfUnset(AxisAction.CameraLookY, new BindingInput { Kind = BindingKind.GamepadAxis, Code = 5 });
        }

        private static void SetIfUnset(AxisAction action, BindingInput input)
        {
            var existing = AxisBindingStore.GetAxisBinding(action);
            if (existing.Kind != BindingKind.None)
            {
                return;
            }
            AxisBindingStore.SetAxisBinding(action, input);
        }
    }
}
