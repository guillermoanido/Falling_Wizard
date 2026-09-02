using System.Collections.Generic;
using FallingWizard.UI;
using UnityEngine;

namespace FallingWizard.Player
{
    // Hold to wind up, aim with the stick, let go to fly. The dotted line is not decoration: the
    // launch it draws and the launch it fires are THE SAME Vector2, computed once per step and
    // handed to both. The moment those become two code paths the line becomes a suggestion.
    //
    // The flight is control-locked for exactly as long as the arc says it lasts. Without that,
    // Run drags horizontal speed back toward the stick every step and the wizard lands well short
    // of the drawing - which is the single thing most likely to make this spell feel broken.
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Fling", fileName = "Fling")]
    public class FlingAbility : Ability
    {
        const float StickDeadzone = 0.2f;

        [Header("Power")]
        [Tooltip("Launch speed at no charge, in boxes per second.")]
        [Min(0f)] public float minSpeed = 6f;

        [Tooltip("Launch speed at full charge. A standing jump leaves at about 9, for scale.")]
        [Min(0f)] public float maxSpeed = 14f;

        [Tooltip("Seconds of holding to wind up to full power.")]
        [Min(0.05f)] public float chargeTime = 0.7f;

        [Header("Angle")]
        [Tooltip("Flattest shot, in degrees above horizontal - stick pushed all the way down.")]
        [Range(0f, 90f)] public float minAngle = 15f;

        [Tooltip("Steepest shot - stick pushed all the way up.")]
        [Range(0f, 90f)] public float maxAngle = 80f;

        [Tooltip("Angle with the stick neutral, so a bare hold-and-release is still a sensible " +
                 "jump rather than a mistake.")]
        [Range(0f, 90f)] public float restAngle = 55f;

        [Header("Flight")]
        [Tooltip("Extra seconds of locked steering on top of the flight the arc predicted. A " +
                 "little makes the landing read as deliberate; a lot takes the recovery away.")]
        [Min(0f)] public float extraLock = 0.05f;

        [Header("The Line")]
        [Tooltip("What the arc is allowed to notice. Ground so it knows where you land, Hazard " +
                 "so it can warn you what you are about to fly through.")]
        public LayerMask seen = (1 << 6) | (1 << 8);

        [Tooltip("Seconds per simulated step. Smaller is smoother and more accurate.")]
        [Min(0.005f)] public float step = 0.02f;

        [Tooltip("Most steps to simulate, whatever else happens.")]
        [Range(8, 400)] public int steps = 200;

        [Tooltip("How far ahead to look, in boxes. The line stops here even if it never lands.")]
        [Min(1f)] public float lookAhead = 16f;

        [Header("Dots")]
        [Tooltip("Leave empty for a plain square. A ring or a chevron reads better once you have art.")]
        public Sprite dotArt;

        [Min(0.05f)] public float spacing = 0.32f;
        [Min(0.01f)] public float dotSize = 0.13f;
        [Range(4, 200)] public int maxDots = 60;

        [Tooltip("Dots shrink toward the far end, so the near ones read as the confident part.")]
        [Range(0f, 1f)] public float taper = 0.45f;

        public Color safe = new Color(0.95f, 0.93f, 0.75f, 0.85f);

        [Tooltip("Drawn when the flight passes through something that will change where you end " +
                 "up. The arc carries on - hazards here are things you fly through, not walls.")]
        public Color danger = new Color(0.95f, 0.35f, 0.30f, 0.9f);

        [Tooltip("Sorting order. Above the level, so the line is never drawn inside a wall.")]
        public int sortingOrder = 20;

        [Header("Ranks")]
        [Tooltip("One block per rank. Element 0 is what learning it gives you.")]
        public Tier[] tiers = { new Tier() };

        public override string WhyNot(PlayerLogic wizard) =>
            wizard.State == PlayerState.Normal ? null : $"you are {wizard.State}";

        // Rooted is set HERE, not in OnHeld. TryCast runs before Rebuild, and Rebuild opens with
        // stats.Reset() - so anything written during the hold is wiped on the very next line and
        // the wizard walks away while winding up, with nothing to show why.
        public override void ModifyStats(PlayerLogic wizard, PlayerLogic.Modifiers stats)
        {
            if (wizard.spellbook.StateOf<Charge>(this).winding)
                stats.Rooted = true;
        }

        public override void OnHeld(PlayerLogic wizard, float heldSeconds, float fixedDeltaTime)
        {
            Charge charge = wizard.spellbook.StateOf<Charge>(this);

            if (wizard.State != PlayerState.Normal)
            {
                Drop(wizard);
                return;
            }

            if (!charge.winding)
            {
                charge.winding = true;
                charge.angle = restAngle;
                charge.facing = wizard.movement.Facing;
            }

            Aim(wizard, charge, fixedDeltaTime);
            Draw(wizard, charge);
        }

        public override void OnChargeLost(PlayerLogic wizard) => Drop(wizard);

        public override void OnReleased(PlayerLogic wizard, float heldSeconds)
        {
            Charge charge = wizard.spellbook.StateOf<Charge>(this);

            if (!charge.winding)
                return;

            charge.winding = false;
            charge.arc?.Hide();

            if (wizard.State != PlayerState.Normal || !wizard.spellbook.Fire(this))
                return;

            // The same launch the line was drawing, and the same lock the line assumed.
            Vector2 launch = Launch(wizard, charge);

            wizard.PredictArc(launch, Look(wizard), charge.path, out PlayerLogic.Movement.ArcEnd end);
            wizard.Fling(launch, end.Seconds + extraLock);

            charge.wound = 0f;
        }

        public override float ChargeFor(PlayerLogic wizard)
        {
            Charge charge = wizard.spellbook.StateOf<Charge>(this);
            return charge.winding ? Mathf.Clamp01(charge.wound) : -1f;
        }

        public override void OnRunReset(PlayerLogic wizard) => Drop(wizard);

        public override void OnUnequipped(PlayerLogic wizard)
        {
            Charge charge = wizard.spellbook.StateOf<Charge>(this);

            Drop(wizard);

            if (charge.arc != null)
                Destroy(charge.arc.gameObject);

            charge.arc = null;
        }

        protected override void Validate()
        {
            maxSpeed = Mathf.Max(minSpeed, maxSpeed);
            maxAngle = Mathf.Max(minAngle, maxAngle);
            restAngle = Mathf.Clamp(restAngle, minAngle, maxAngle);

            if (tiers != null)
                foreach (Tier tier in tiers)
                    tier?.Validate();

            CheckTiers(tiers != null ? tiers.Length : 0);
        }

        void Aim(PlayerLogic wizard, Charge charge, float fixedDeltaTime)
        {
            Tier tier = Of(wizard);

            charge.wound = Mathf.Min(1f,
                charge.wound + fixedDeltaTime / Mathf.Max(0.05f, tier.chargeTime));

            Vector2 stick = wizard.Steering.Move;

            if (stick.sqrMagnitude < StickDeadzone * StickDeadzone)
                return;

            if (Mathf.Abs(stick.x) > StickDeadzone)
                charge.facing = stick.x < 0f ? -1 : 1;

            // Stick up steepens, stick down flattens, dead centre sits at the rest angle.
            charge.angle = stick.y >= 0f
                ? Mathf.Lerp(restAngle, maxAngle, stick.y)
                : Mathf.Lerp(restAngle, minAngle, -stick.y);
        }

        Vector2 Launch(PlayerLogic wizard, Charge charge)
        {
            Tier tier = Of(wizard);

            float speed = Mathf.Lerp(minSpeed, tier.maxSpeed, Mathf.Clamp01(charge.wound));
            float radians = Mathf.Deg2Rad * Mathf.Clamp(charge.angle, 0f, 90f);

            return new Vector2(charge.facing * Mathf.Cos(radians), Mathf.Sin(radians)) * speed;
        }

        PlayerLogic.Movement.ArcSettings Look(PlayerLogic wizard) =>
            new PlayerLogic.Movement.ArcSettings
            {
                Layers = seen,
                Step = step,
                Steps = steps,
                Distance = lookAhead,
            };

        void Draw(PlayerLogic wizard, Charge charge)
        {
            if (charge.arc == null)
                charge.arc = FlingArc.Make(dotArt, spacing, dotSize, maxDots, taper, safe, danger,
                    sortingOrder);

            wizard.PredictArc(Launch(wizard, charge), Look(wizard), charge.path,
                out PlayerLogic.Movement.ArcEnd end);

            charge.arc?.Show(charge.path, end, charge.wound);
        }

        void Drop(PlayerLogic wizard)
        {
            Charge charge = wizard.spellbook.StateOf<Charge>(this);

            charge.winding = false;
            charge.wound = 0f;
            charge.arc?.Hide();
        }

        Tier Of(PlayerLogic wizard) =>
            TierFor(tiers, wizard.spellbook.RankOf(this)) ?? new Tier();

        [System.Serializable]
        public class Tier
        {
            [Min(0f)] public float maxSpeed = 14f;
            [Min(0.05f)] public float chargeTime = 0.7f;

            // OnValidate does not reach into a nested class, so the block clamps itself.
            public void Validate()
            {
                maxSpeed = Mathf.Max(0f, maxSpeed);
                chargeTime = Mathf.Max(0.05f, chargeTime);
            }
        }

        public class Charge
        {
            public readonly List<Vector2> path = new List<Vector2>(256);

            public FlingArc arc;
            public bool winding;
            public float wound;
            public float angle = 55f;
            public int facing = 1;
        }
    }
}
