using UnityEngine;

namespace SebTruck
{
    internal sealed class RearViewMirrorQuality
    {
        private readonly RenderTexture _texture;
        private readonly int _originalWidth;
        private readonly int _originalHeight;

        internal RearViewMirrorQuality(RenderTexture texture)
        {
            _texture = texture;
            _originalWidth = texture.width;
            _originalHeight = texture.height;
        }

        internal bool Apply(int quality)
        {
            if (_texture == null) return true;
            int scale = 1 << Mathf.Clamp(quality, 0, 3);
            int width = _originalWidth * scale;
            int height = _originalHeight * scale;
            if (_texture.width == width && _texture.height == height) return true;

            // Resize the existing target so camera and mirror material references stay intact.
            if (Resize(width, height)) return true;
            Resize(_originalWidth, _originalHeight);
            return false;
        }

        private bool Resize(int width, int height)
        {
            RenderTexture active = RenderTexture.active;
            try
            {
                if (active == _texture) RenderTexture.active = null;
                _texture.Release();
                _texture.width = width;
                _texture.height = height;
                return _texture.Create();
            }
            catch
            {
                return false;
            }
            finally
            {
                RenderTexture.active = active;
            }
        }
    }
}
