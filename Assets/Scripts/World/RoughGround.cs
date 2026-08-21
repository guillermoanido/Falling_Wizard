using UnityEngine;

namespace FallingWizard.World
{
    // A surface that is awkward to cross at speed - stairs, scree, loose gravel. The wizard's
    // ground check looks for this on whatever is under their feet, so it belongs on the same
    // object as the collider, or on a parent of it.
    //
    // For an obstacle you run INTO rather than across, use Rock instead.
    [RequireComponent(typeof(Collider2D))]
    public class RoughGround : MonoBehaviour
    {
        [Header("Stumble")]
        [Tooltip("Crossing this faster than this many boxes per second trips the wizard. Running " +
                 "is 6 and walking is 2, so 4 catches a run and lets a walk through. " +
                 "Stairs 4, loose gravel 3, scree 2.5.")]
        [Min(0f)] public float tripSpeed = 4f;

        [Tooltip("Off makes the surface merely noisy - no trip, but still 'rough' for anything " +
                 "else that cares, like footstep sounds.")]
        public bool trips = true;
    }
}
