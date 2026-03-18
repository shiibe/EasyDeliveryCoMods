namespace SebBinds
{
    public interface IAxisProvider
    {
        bool IsAvailable();
        bool SupportsClutch();

        bool TryGetAxisValue(BindingInput input, out float value);
        bool TryGetAxisLabel(BindingInput input, out string label);

        bool TryCaptureNextAxis(out BindingInput input);
    }
}
