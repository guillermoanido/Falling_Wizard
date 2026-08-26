using UnityEngine;

namespace FallingWizard.Core
{
    public static class Placeholder
    {
        // Four pixels square at four pixels per unit: the smallest texture that comes out exactly
        // one box, which is the unit everything else in the game is measured in.
        const int Pixels = 4;
        const float PixelsPerUnit = Pixels;

        static readonly Color32 White = new Color32(255, 255, 255, 255);

        static Sprite box;

        public static Sprite Box
        {
            get
            {
                if (box != null)
                    return box;

                var texture = new Texture2D(Pixels, Pixels, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    hideFlags = HideFlags.HideAndDontSave,
                };

                var pixels = new Color32[Pixels * Pixels];

                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = White;

                texture.SetPixels32(pixels);
                texture.Apply();

                box = Sprite.Create(texture, new Rect(0f, 0f, Pixels, Pixels),
                    new Vector2(0.5f, 0.5f), PixelsPerUnit);
                box.name = "Placeholder Box";
                box.hideFlags = HideFlags.HideAndDontSave;

                return box;
            }
        }
    }
}
