using UnityEngine;

namespace FallingWizard.World
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class ParallaxLayer : MonoBehaviour
    {
        [Header("Distance")]
        [Tooltip("How much this sticks to the camera as it pans sideways. 0 is world-locked and " +
                 "slides fully past you; 1 is welded to the camera and never appears to move.")]
        [Range(0f, 1f)] public float parallax = 0.5f;

        [Tooltip("The same, for the camera moving up and down. Levels are usually far taller " +
                 "than a background sheet, so this wants to be high or the sky runs out.")]
        [Range(0f, 1f)] public float verticalParallax = 0.9f;

        [Header("Repeat")]
        [Tooltip("Shuffle the sheet along by whole widths so it never runs out sideways, however " +
                 "far you walk. The art has to tile seamlessly left to right for this to be " +
                 "invisible.")]
        public bool repeatForever = true;

        [Tooltip("Copies laid side by side to fill the gap the shuffling leaves. Three covers a " +
                 "sheet wider than the screen; go up if yours is narrow.")]
        [Range(1, 9)] public int copies = 3;

        Camera eye;
        SpriteRenderer art;

        Vector3 origin;
        Vector3 eyeOrigin;
        float width;

        void Awake()
        {
            art = GetComponent<SpriteRenderer>();
            origin = transform.position;

            width = art.sprite != null ? art.bounds.size.x : 0f;

            if (repeatForever && width > 0f)
                BuildCopies();
        }

        void LateUpdate()
        {
            if (!FindEye())
                return;

            Vector3 travel = eye.transform.position - eyeOrigin;

            float x = origin.x + travel.x * parallax;
            float y = origin.y + travel.y * verticalParallax;

            if (repeatForever && width > 0f)
            {
                float half = width * 0.5f;
                float drift = Mathf.Repeat(eye.transform.position.x - x + half, width) - half;
                x = eye.transform.position.x - drift;
            }

            transform.position = new Vector3(x, y, origin.z);
        }

        void BuildCopies()
        {
            int side = Mathf.Max(0, (copies - 1) / 2);

            for (int step = -side; step <= side; step++)
            {
                if (step == 0)
                    continue;

                var clone = new GameObject($"{name} {step:+#;-#}");
                clone.transform.SetParent(transform, false);
                clone.transform.localPosition = new Vector3(width * step, 0f, 0f);

                var copy = clone.AddComponent<SpriteRenderer>();
                copy.sprite = art.sprite;
                copy.color = art.color;
                copy.flipX = art.flipX;
                copy.flipY = art.flipY;
                copy.material = art.sharedMaterial;
                copy.sortingLayerID = art.sortingLayerID;
                copy.sortingOrder = art.sortingOrder;
            }
        }

        bool FindEye()
        {
            if (eye != null)
                return true;

            eye = Camera.main;

            if (eye == null)
                return false;

            eyeOrigin = eye.transform.position;
            return true;
        }
    }
}
