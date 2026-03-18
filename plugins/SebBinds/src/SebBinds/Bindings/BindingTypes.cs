namespace SebBinds
{
    public enum BindAction
    {
        InteractOk = 0,
        Back = 1,
        MapItems = 2,
        Pause = 3,
        Camera = 5,
        ResetVehicle = 6,
        Headlights = 7,

        Horn = 8,

        RadioPower = 9,
        RadioScanToggle = 10,
        RadioScanLeft = 11,
        RadioScanRight = 12,

        ToggleGearbox = 13,
        ShiftUp = 14,
        ShiftDown = 15,

        IgnitionToggle = 16,

        // New actions (keep explicit values to avoid breaking stored keys).
        Drive = 100,
        Brake = 101,
        Map = 102,
        Items = 103,
        Jobs = 104

        ,SteerLeft = 105
        ,SteerRight = 106

        ,MoveUp = 111
        ,MoveDown = 112
        ,MoveLeft = 113
        ,MoveRight = 114

        ,LookUp = 115
        ,LookDown = 116
        ,LookLeft = 117
        ,LookRight = 118

        ,FreeCam = 107

        ,CameraLookX = 108
        ,CameraLookY = 109

        ,SteerAxis = 110

        // Vehicle indicators
        ,IndicatorLeft = 119
        ,IndicatorRight = 120
        ,IndicatorHazards = 121
    }

    public enum BindingLayer
    {
        Normal = 0,
        Modified = 1
    }

    public enum BindingKind
    {
        None = 0,
        Button = 1,
        Pov = 2,

        Key = 3,
        MouseButton = 4,
        GamepadButton = 5,
        GamepadDpad = 6,
        GamepadAxis = 7,
        WheelAxis = 8,
        GamepadDpadAxis = 9,
        WheelDpadAxis = 10
    }

    public struct BindingInput
    {
        public BindingKind Kind;
        public int Code;
    }
}
