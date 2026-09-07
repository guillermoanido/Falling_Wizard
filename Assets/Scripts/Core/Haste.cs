using UnityEngine;

namespace FallingWizard.Core
{
    public static class Haste
    {
        public static bool Active { get; private set; }

        public static float WorldScale { get; private set; } = 1f;

        public static float DeltaTime => Time.deltaTime * WorldScale;

        public static float FixedDeltaTime => Time.fixedDeltaTime * WorldScale;

        public static void Begin(float scale)
        {
            WorldScale = Mathf.Clamp(scale, 0.05f, 1f);
            Active = true;
        }

        public static void End()
        {
            WorldScale = 1f;
            Active = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => End();
    }
}
