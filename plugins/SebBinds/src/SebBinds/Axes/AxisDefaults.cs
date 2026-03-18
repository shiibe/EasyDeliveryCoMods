namespace SebBinds
{
    internal static class AxisDefaults
    {
        internal static void EnsureControllerDefaults()
        {
            // Only fill unset axis bindings.
            SetIfUnset(AxisAction.MoveY, new BindingInput { Kind = BindingKind.GamepadAxis, Code = 3 });       // LS Y
            SetIfUnset(AxisAction.MoveX, new BindingInput { Kind = BindingKind.GamepadAxis, Code = 2 });       // LS X
            SetIfUnset(AxisAction.Steering, new BindingInput { Kind = BindingKind.GamepadAxis, Code = 2 });    // LS X
            SetIfUnset(AxisAction.Throttle, new BindingInput { Kind = BindingKind.GamepadAxis, Code = 1 });    // RT Axis
            SetIfUnset(AxisAction.Brake, new BindingInput { Kind = BindingKind.GamepadAxis, Code = 0 });       // LT Axis
            SetIfUnset(AxisAction.CameraLookX, new BindingInput { Kind = BindingKind.GamepadAxis, Code = 4 }); // RS X
            SetIfUnset(AxisAction.CameraLookY, new BindingInput { Kind = BindingKind.GamepadAxis, Code = 5 }); // RS Y
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
