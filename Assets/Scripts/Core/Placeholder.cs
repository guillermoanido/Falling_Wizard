using UnityEngine;

namespace FallingWizard.Core
{
    public static class Placeholder
    {
        static Sprite box;

        public static Sprite Box
        {
            get
            {
                if (box != null)
                    return box;

                var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    hideFlags = HideFlags.HideAndDontSave,
                };

                var pixels = new Color32[16];

                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(255, 255, 255, 255);

                texture.SetPixels32(pixels);
                texture.Apply();

                box = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
                box.name = "Placeholder Box";
                box.hideFlags = HideFlags.HideAndDontSave;

                return box;
            }
        }
    }
}
