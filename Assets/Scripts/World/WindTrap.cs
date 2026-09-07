using System;
using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public class WindTrap : WindZone2D
    {
        const float MinPeriod = 0.1f;

        const float KickGrace = 0.2f;

        const float Tiny = 0.0001f;

        [Header("Turbine")]
        [Tooltip("Seconds it sits shut between blasts. This is the window the player has to get " +
                 "across, so read it next to how long the crossing is: at a run of 4 boxes a " +
                 "second, 2.5 seconds is ten boxes of ground. The last part of it is spent " +
                 "opening, so the genuinely safe stretch is this minus Shutter Travel.")]
        [Min(0f)] public float shutSeconds = 2.5f;

        [Tooltip("Seconds it blows at full strength once the louvres are wide open. Short is " +
                 "kinder than weak: a brief hard gale is something you time, a long soft one is " +
                 "something you wait out.")]
        [Min(0f)] public float openSeconds = 1.25f;

        [Tooltip("Seconds the louvres take to swing, taken out of the shut time rather than " +
                 "added on - so Open Seconds really is how long it blows. THIS IS THE WHOLE " +
                 "WARNING. Set it to 0 and the trap fires with no tell at all, which is the one " +
                 "way to make this thing unfair. Half a second is about as short as a player " +
                 "can act on.")]
        [Min(0f)] public float shutterTravel = 0.6f;

        [Tooltip("Where in the cycle this one starts, as a fraction of the whole thing. A row " +
                 "of turbines all left at 0 fire in unison, which is one obstacle; set them to " +
                 "0, 0.33 and 0.66 and they fire in a stagger you have to run through, which is " +
                 "a corridor. It is a fraction rather than seconds so that retuning the timings " +
                 "later keeps the stagger.")]
        [Range(0f, 1f)] public float phase = 0f;

        [Tooltip("Let the Haste spell slow this turbine down along with everything else. Leave " +
                 "it on: the spell exists to buy you time to get through something, and a vent " +
                 "that kept its normal rhythm while the whole world waded would be the one " +
                 "thing in the level the spell does not help with. It also keeps the total " +
                 "shove per blast the same - gentler for longer, not weaker.")]
        public bool hasteSlowsCycle = true;

        [Header("Blast")]
        [Tooltip("The punch, in boxes per second, landed once on the step the louvres reach " +
                 "full open. Unlike the sustained push this is a real speed added straight to " +
                 "the wizard, which is what makes this a launcher rather than a fan: (0,14) " +
                 "throws them 3.3 boxes up against a jump of 1.5, and (7,0) fires them sideways " +
                 "at nearly twice a run. It only catches someone who was already inside when it " +
                 "fired - walk into a running turbine and you get the gale instead.")]
        public Vector2 kick = new Vector2(7f, 0f);

        [Tooltip("Seconds the wizard cannot steer for after the punch, so the launch is the " +
                 "arc it looked like rather than something they immediately drive out of. Keep " +
                 "it short - a sixth of a second reads as being hit, half a second reads as the " +
                 "controller having come unplugged. Note that sideways wind is also suspended " +
                 "for this long, by design: the punch owns those few frames and the gale takes " +
                 "over afterwards.")]
        [Min(0f)] public float kickLockout = 0.15f;

        [Tooltip("Send them tumbling as well as flying. Off by default, because a turbine you " +
                 "can steer out of is a ride and a turbine that knocks you down is a tax. Turn " +
                 "it on for one placed over a drop you meant them to fall down.")]
        public bool tumbles = false;

        [Tooltip("How much of the push reaches a wizard who is ALREADY tumbling, as a fraction. " +
                 "This is not a taste setting. Wind on a ragdoll is added straight to their " +
                 "speed every step with nothing pushing back - there is no running to clamp it " +
                 "and no drag to bleed it off - so at 1 a strong sideways trap accelerates a " +
                 "tumbling wizard forever and posts them off the edge of the level, and the " +
                 "Bubble spell multiplies that by three and a half. A quarter still carries " +
                 "them through the blast without the trap owning them.")]
        [Range(0f, 1f)] public float tumbleScale = 0.25f;

        [Header("Shutter")]
        [Tooltip("Build the louvres at all. Off leaves the haze and streaks doing the telling, " +
                 "which is thinner - raise Idle Haze if you turn this off, or a shut trap is " +
                 "invisible.")]
        public bool showShutter = true;

        [Tooltip("How many slats across the mouth of the vent. Six reads as a grille; two reads " +
                 "as a pair of doors. They are built when the level starts, so the scene view " +
                 "shows their spacing as gizmo lines instead.")]
        [Range(0, 24)] public int blades = 6;

        [Tooltip("Art for one slat. Empty draws a plain bar, which is fine - the shape that " +
                 "matters is the row, not the slat.")]
        public Sprite shutterArt;

        [Tooltip("Colour of the louvres while the vent is shut and safe. Keep it dull: it has " +
                 "to be obviously the same object as the alarm colour it turns into.")]
        public Color shutterTint = new Color(0.34f, 0.36f, 0.4f, 1f);

        [Tooltip("Colour of the louvres while the vent is winding up and blowing. This is the " +
                 "single thing the player learns to read, so it wants to be a colour nothing " +
                 "else in the level uses.")]
        public Color warningTint = new Color(1f, 0.42f, 0.18f, 1f);

        [Tooltip("How deep into the zone the grille sits, in boxes. Under half a box it reads " +
                 "as a line rather than as shutters. It sits on the face the wind comes OUT of, " +
                 "which is the edge opposite the push.")]
        [Min(0.05f)] public float mouthDepth = 0.45f;

        [Tooltip("How many times a second the louvres pulse while they are winding up. The " +
                 "colour change alone is a slow fade, which is exactly what a player looking " +
                 "somewhere else misses; the flicker is what catches the eye. 0 turns it off.")]
        [Min(0f)] public float flashRate = 4f;

        [Tooltip("How visible the haze is while the vent is shut, as a fraction of its full " +
                 "tint. 0 makes a closed trap completely invisible, which is only fair if the " +
                 "louvres are switched on and doing the telling.")]
        [Range(0f, 1f)] public float idleHaze = 0.2f;

        [NonSerialized] BoxCollider2D zone;
        [NonSerialized] Transform vent;
        [NonSerialized] Transform[] slats = Array.Empty<Transform>();
        [NonSerialized] SpriteRenderer[] slatArt = Array.Empty<SpriteRenderer>();
        [NonSerialized] Vector2 slatUnit = Vector2.one;
        [NonSerialized] float slatWidth;

        [NonSerialized] float clock;
        [NonSerialized] float armedUntil = -1f;
        [NonSerialized] bool wasOpen;

        public float Period =>
            Mathf.Max(MinPeriod, Mathf.Max(0f, shutSeconds) + Mathf.Max(0f, openSeconds));

        public float Phase => Mathf.Repeat(clock + phase * Period, Period);

        public bool IsOpen => Phase >= Mathf.Max(0f, shutSeconds);

        float Travel => Mathf.Clamp(shutterTravel, 0f, Mathf.Max(0f, shutSeconds) * 0.5f);

        protected override float Openness
        {
            get
            {
                float shut = Mathf.Max(0f, shutSeconds);

                if (shut <= 0f)
                    return 1f;

                float t = Phase;

                if (t >= shut)
                    return 1f;

                float travel = Travel;

                if (travel <= 0f)
                    return 0f;

                if (t < travel)
                    return 1f - t / travel;

                if (t >= shut - travel)
                    return (t - (shut - travel)) / travel;

                return 0f;
            }
        }

        public float Alarm
        {
            get
            {
                float shut = Mathf.Max(0f, shutSeconds);

                if (shut <= 0f)
                    return 1f;

                float t = Phase;

                if (t >= shut)
                    return 1f;

                float travel = Travel;

                if (travel <= 0f || t < shut - travel)
                    return 0f;

                return (t - (shut - travel)) / travel;
            }
        }

        protected override void Reset()
        {
            base.Reset();

            push = new Vector2(12f, 0f);
            rampup = 60f;
            groundScale = 0.6f;

            hazeTint = new Color(1f, 0.62f, 0.4f, 0.16f);
            streakTint = new Color(1f, 0.86f, 0.7f, 0.6f);

            affectsOnStaff = false;

            affectsRagdolled = true;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            Validate();
        }

        void Validate()
        {
            shutSeconds = Mathf.Max(0f, shutSeconds);
            openSeconds = Mathf.Max(0f, openSeconds);
            shutterTravel = Mathf.Max(0f, shutterTravel);
            mouthDepth = Mathf.Max(0.05f, mouthDepth);

            if (shutSeconds <= 0f && openSeconds <= 0f)
                Debug.LogWarning("WindTrap has a shut time and an open time of 0, so it has no " +
                                 "cycle at all and simply blows forever. Give it some of each, " +
                                 "or use a plain Wind zone instead - that is what one is.", this);

            if (push.sqrMagnitude < Tiny && kick.sqrMagnitude < Tiny)
                Debug.LogWarning("WindTrap has a push and a kick of 0, so it opens and closes " +
                                 "and does nothing to anyone standing in it. Put a number in " +
                                 "Kick to throw the wizard, or in Push to hold them.", this);

            if (shutSeconds > 0f && shutterTravel <= 0f)
                Debug.LogWarning("WindTrap has a Shutter Travel of 0, so it snaps from shut to " +
                                 "full blast in a single frame with no warning the player can " +
                                 "act on. Half a second is about the shortest tell that is " +
                                 "still fair.", this);

            if (shutterTravel > shutSeconds * 0.5f && shutSeconds > 0f)
                Debug.LogWarning("WindTrap's Shutter Travel is more than half its shut time, so " +
                                 "the louvres are always mid-swing and the vent is never " +
                                 "visibly closed. It is clamped at runtime, but the numbers in " +
                                 "the inspector will not be the ones you get.", this);

            if (affectsRagdolled && tumbleScale > 0.5f && push.sqrMagnitude > 36f)
                Debug.LogWarning("WindTrap pushes harder than 6 boxes a second and lets more " +
                                 "than half of it reach a tumbling wizard. Wind on a ragdoll " +
                                 "has nothing pushing back, so it keeps accumulating - a trip " +
                                 "inside this one is a wizard leaving the level sideways. Drop " +
                                 "Tumble Scale.", this);
        }

        protected override void Awake()
        {
            base.Awake();

            zone = GetComponent<BoxCollider2D>();

            Validate();
            BuildShutter();
        }

        protected override void Update()
        {
            Advance();

            base.Update();

            TintHaze();
            TickShutter();
        }

        void Advance()
        {
            clock += hasteSlowsCycle ? Haste.DeltaTime : Time.deltaTime;

            bool open = IsOpen;

            if (open && !wasOpen)
                armedUntil = clock + KickGrace;

            wasOpen = open;
        }

        protected override void OnPlayerInside(PlayerCharacter wizard, float fixedDeltaTime)
        {
            if (!Allowed(wizard))
                return;

            float open = Openness;

            if (open <= 0f)
                return;

            PlayerLogic logic = wizard.Logic;
            bool tumbling = logic.State == PlayerState.Ragdoll;

            float strength = open * (tumbling ? tumbleScale : 1f);

            logic.Push(push * strength * Haste.WorldScale, rampup, groundScale);

            if (open < 1f || tumbling || clock > armedUntil)
                return;

            armedUntil = -1f;
            Blast(logic);
        }

        void Blast(PlayerLogic wizard)
        {
            if (tumbles)
                wizard.Trip();

            wizard.Shove(kick, kickLockout);
        }

        void TintHaze()
        {
            if (haze == null)
                return;

            Color glow = Color.Lerp(hazeTint, warningTint, Alarm);
            glow.a = hazeTint.a * Mathf.Lerp(idleHaze, 1f, Openness);
            haze.color = glow;
        }

        void TickShutter()
        {
            if (slats.Length == 0)
                return;

            float open = Openness;

            float showing = Mathf.Cos(open * Mathf.PI * 0.5f);

            float alarm = Alarm;
            Color tint = Color.Lerp(shutterTint, warningTint, alarm);

            if (flashRate > 0f && alarm > 0f && alarm < 1f)
                tint = Color.Lerp(tint, warningTint,
                    Mathf.Abs(Mathf.Sin(Phase * Mathf.PI * flashRate)));

            var size = new Vector3(slatWidth * showing / slatUnit.x, mouthDepth / slatUnit.y, 1f);

            for (int i = 0; i < slats.Length; i++)
            {
                slats[i].localScale = size;
                slatArt[i].color = tint;
            }
        }

        void BuildShutter()
        {
            if (!showShutter || blades <= 0)
                return;

            if (!TryFindMouth(out Vector2 centre, out Vector2 across, out float span))
                return;

            vent = new GameObject($"{name} Shutter").transform;

            slats = new Transform[blades];
            slatArt = new SpriteRenderer[blades];
            slatWidth = span / blades;

            float turn = Mathf.Atan2(across.y, across.x) * Mathf.Rad2Deg;

            for (int i = 0; i < blades; i++)
            {
                var slat = new GameObject($"Slat {i + 1}");
                slat.transform.SetParent(vent, false);

                float offset = ((i + 0.5f) / blades - 0.5f) * span;

                slat.transform.position = new Vector3(
                    centre.x + across.x * offset,
                    centre.y + across.y * offset,
                    transform.position.z);

                slat.transform.rotation = Quaternion.Euler(0f, 0f, turn);

                var art = slat.AddComponent<SpriteRenderer>();
                art.sprite = shutterArt != null ? shutterArt : Placeholder.Box;
                art.color = shutterTint;

                art.sortingOrder = sortingOrder + 1;

                Vector2 unit = art.sprite.bounds.size;

                if (unit.x > Tiny && unit.y > Tiny)
                    slatUnit = unit;

                slats[i] = slat.transform;
                slatArt[i] = art;
            }

            TickShutter();
        }

        bool TryFindMouth(out Vector2 centre, out Vector2 across, out float span)
        {
            centre = Vector2.zero;
            across = Vector2.right;
            span = 0f;

            var shape = zone != null ? zone : GetComponent<BoxCollider2D>();

            if (shape == null || push.sqrMagnitude < Tiny)
                return false;

            Bounds box = shape.bounds;
            Vector2 way = push.normalized;
            across = new Vector2(-way.y, way.x);

            Vector2 half = box.extents;

            float reach = Mathf.Abs(way.x) * half.x + Mathf.Abs(way.y) * half.y;
            span = 2f * (Mathf.Abs(across.x) * half.x + Mathf.Abs(across.y) * half.y);

            if (span <= Tiny || reach <= Tiny)
                return false;

            float depth = Mathf.Min(mouthDepth, reach);
            centre = (Vector2)box.center - way * (reach - depth * 0.5f);
            return true;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (vent != null)
                Destroy(vent.gameObject);
        }

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();

            if (!TryFindMouth(out Vector2 centre, out Vector2 across, out float span))
                return;

            float turn = Mathf.Atan2(across.y, across.x) * Mathf.Rad2Deg;
            Matrix4x4 was = Gizmos.matrix;

            Gizmos.matrix = Matrix4x4.TRS(centre, Quaternion.Euler(0f, 0f, turn), Vector3.one);
            Gizmos.color = warningTint;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(span, mouthDepth, 0f));

            for (int i = 1; i < blades; i++)
            {
                float offset = ((float)i / blades - 0.5f) * span;

                Gizmos.DrawLine(new Vector3(offset, -mouthDepth * 0.5f, 0f),
                                new Vector3(offset, mouthDepth * 0.5f, 0f));
            }

            Gizmos.matrix = was;
        }
    }
}
