using UnityEngine;

namespace FallingWizard.Core
{
    // The world slowing down, as a flag anything can read - NOT Time.timeScale.
    //
    // Scaling time would have been fewer lines and wrong in three ways: pausing already owns
    // timeScale (Game.SetPaused), physics runs on a fixed step so slowing it changes how the
    // wizard's own collisions resolve, and there would be no way to exempt anything. Here the
    // wizard is untouched by construction, and so is their ragdoll: only things that ASK are
    // slowed, and the ragdoll never asks.
    public static class Haste
    {
        public static bool Active { get; private set; }

        // Multiply anything that moves under its own steam by this. 1 is the world running
        // normally; below that it is wading.
        public static float WorldScale { get; private set; } = 1f;

        // Seconds, for anything that would rather scale its own clock than its own speed.
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
