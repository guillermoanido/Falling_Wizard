using System;
using UnityEngine;

namespace FallingWizard.Player
{
    public partial class PlayerLogic
    {
        [Serializable]
        public class Ragdoll
        {
            const float MinStandUp = 0.01f;
            const float MinTumbleSpread = 0.1f;

            [Header("Tumble")]
            [Tooltip("How fast the wizard spins as they go over, in degrees per second. They " +
                     "always roll the way they were going.")]
            public float spinSpeed = 520f;

            [Tooltip("How quickly that spin slows, in degrees per second squared. 0 keeps " +
                     "spinning at full rate until they get up.")]
            [Min(0f)] public float spinDown = 240f;

            [Tooltip("How much of their speed carries into the tumble. 1 keeps all of it, so a " +
                     "trip is a loss of footing rather than a wall.")]
            [Range(0f, 1f)] public float momentumKept = 1f;

            [Header("Launch")]
            [Tooltip("Shove ONWARD as they go over, in boxes per second, on top of whatever " +
                     "speed they already had. This is what makes a trip throw you rather than " +
                     "drop you.")]
            [Min(0f)] public float launchForward = 3f;

            [Tooltip("Lift as they go over, in boxes per second. A little goes a long way: it " +
                     "gets them off the floor so the launch is not immediately scrubbed off.")]
            [Min(0f)] public float launchUp = 5f;

            [Tooltip("Least speed they leave the ground with, in boxes per second. Tripping at " +
                     "a crawl and tripping at a sprint then differ in degree, not in kind.")]
            [Min(0f)] public float minimumLaunch = 4f;

            [Header("Getting Up")]
            [Tooltip("Minimum seconds spent tumbling before they can start getting up.")]
            [Min(0f)] public float minimumDuration = 0.9f;

            [Tooltip("Hard limit on a tumble, in seconds. They get up after this whether or not " +
                     "they ever found the ground. Without it, one trip somewhere the ground check " +
                     "cannot see is a wizard who never moves again.")]
            [Min(0.1f)] public float maximumDuration = 3f;

            [Tooltip("How fast the skid bleeds off once they are back on the ground, in boxes " +
                     "per second squared. This rather than physics friction, so the same trip " +
                     "always slides the same distance.")]
            [Min(0f)] public float slideFriction = 9f;

            [Tooltip("They only get up once grounded and slower than this, in boxes per second.")]
            [Min(0f)] public float recoverSpeed = 1.2f;

            [Tooltip("Seconds spent straightening back up.")]
            [Min(0.01f)] public float standUpDuration = 0.35f;

            [Header("Walls")]
            [Tooltip("How much sideways speed a tumbling wizard keeps when they hit a wall, as a " +
                     "fraction of what they arrived with. 0 is a dead stop; 1 is a full rebound.")]
            [Range(0f, 1f)] public float wallBounce = 0.55f;

            [Tooltip("Slowest arrival that still bounces, in boxes per second. Below it they come " +
                     "to rest against the wall instead of chattering off it.")]
            [Min(0f)] public float minimumBounceSpeed = 1.5f;

            [NonSerialized] Rigidbody2D body;
            [NonSerialized] Transform visual;
            [NonSerialized] float angle;
            [NonSerialized] float spin;
            [NonSerialized] float tumbleTimer;
            [NonSerialized] float elapsed;
            [NonSerialized] float standUpTimer;
            [NonSerialized] float standUpFrom;
            [NonSerialized] Collider2D hull;
            [NonSerialized] LayerMask walls;
            [NonSerialized] float bounceReadyAt;

            const float BounceCooldown = 0.08f;
            const float WallSkin = 0.04f;

            static readonly RaycastHit2D[] WallHits = new RaycastHit2D[2];

            public bool IsStandingUp => standUpTimer >= 0f;

            public void Attach(Rigidbody2D rigidbody2d, Transform sprite, Collider2D hitbox, LayerMask wallLayers)
            {
                body = rigidbody2d;
                visual = sprite;
                hull = hitbox;
                walls = wallLayers;
                standUpTimer = -1f;
                angle = 0f;
                bounceReadyAt = 0f;
                Show();
            }

            public void Begin(int direction) => Begin(direction, true);

            public void Begin(int direction, bool keepMomentum)
            {
                spin = -direction * spinSpeed;
                angle = 0f;

                float carried = keepMomentum ? body.linearVelocityX * momentumKept : 0f;
                float thrown = carried + direction * launchForward;

                if (Mathf.Abs(thrown) < minimumLaunch)
                    thrown = direction * minimumLaunch;

                body.linearVelocityX = thrown;

                body.linearVelocityY = Mathf.Max(body.linearVelocityY, launchUp);

                tumbleTimer = minimumDuration;
                standUpTimer = -1f;
                elapsed = 0f;
            }

            public bool Tick(float fixedDeltaTime, bool grounded, float horizontalSpeed)
            {
                if (standUpTimer >= 0f)
                    return StandUp(fixedDeltaTime);

                angle += spin * fixedDeltaTime;
                spin = Mathf.MoveTowards(spin, 0f, spinDown * fixedDeltaTime);
                Show();

                tumbleTimer -= fixedDeltaTime;
                elapsed += fixedDeltaTime;

                BounceOffWalls(fixedDeltaTime);

                if (grounded && slideFriction > 0f)
                    body.linearVelocityX =
                        Mathf.MoveTowards(body.linearVelocityX, 0f, slideFriction * fixedDeltaTime);

                bool waitedLongEnough = elapsed >= maximumDuration;

                if (!waitedLongEnough &&
                    (tumbleTimer > 0f || !grounded || horizontalSpeed > recoverSpeed))
                    return false;

                standUpFrom = angle;
                standUpTimer = 0f;
                return false;
            }

            void BounceOffWalls(float fixedDeltaTime)
            {
                if (wallBounce <= 0f || body == null || hull == null || Time.time < bounceReadyAt)
                    return;

                float speed = body.linearVelocityX;

                if (Mathf.Abs(speed) < minimumBounceSpeed)
                    return;

                var way = new Vector2(speed < 0f ? -1f : 1f, 0f);
                Bounds box = hull.bounds;

                var probe = new Vector2(box.size.x * 0.5f, box.size.y * 0.7f);
                float reach = box.extents.x * 0.5f + Mathf.Abs(speed) * fixedDeltaTime + WallSkin;

                var filter = new ContactFilter2D
                {
                    useLayerMask = true,
                    layerMask = walls,
                    useTriggers = false,
                };

                if (Physics2D.BoxCast(box.center, probe, 0f, way, filter, WallHits, reach) <= 0)
                    return;

                body.linearVelocityX = -speed * wallBounce;
                spin = -spin;
                bounceReadyAt = Time.time + BounceCooldown;
            }

            public void Cancel()
            {
                spin = 0f;
                angle = 0f;
                standUpTimer = -1f;
                Show();
            }

            public void Validate()
            {
                standUpDuration = Mathf.Max(MinStandUp, standUpDuration);
                maximumDuration = Mathf.Max(maximumDuration, minimumDuration + MinTumbleSpread);
                wallBounce = Mathf.Clamp01(wallBounce);
                minimumBounceSpeed = Mathf.Max(0f, minimumBounceSpeed);

                if (slideFriction <= 0f && recoverSpeed < minimumLaunch)
                    Debug.LogWarning("Ragdoll.slideFriction is 0 and recoverSpeed is below " +
                                     "minimumLaunch, so a tripped wizard can never slow down " +
                                     "enough to stand back up.");
            }

            bool StandUp(float fixedDeltaTime)
            {
                standUpTimer += fixedDeltaTime;

                float t = Mathf.Clamp01(standUpTimer / Mathf.Max(MinStandUp, standUpDuration));
                angle = Mathf.LerpAngle(standUpFrom, 0f, t);
                Show();

                if (t < 1f)
                    return false;

                Cancel();
                return true;
            }

            void Show()
            {
                if (visual != null)
                    visual.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }
    }
}
