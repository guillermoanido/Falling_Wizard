using System;
using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    // A patch of floor that does not hold you: a puddle, wet flagstones, sheet ice.
    //
    // It does NOT write to PlayerLogic.Modifiers, and it cannot. Spellbook.Rebuild calls
    // stats.Reset() every fixed step and re-applies the equipped abilities on top, so a
    // multiplier set from out here would be wiped before Movement.Run ever looked at it. This
    // goes in the way wind does instead: PlayerLogic.Slicken every step the wizard is inside,
    // spent and cleared once per Simulate, and stepping off the ice is nothing more elaborate
    // than this stopping calling. There is no exit event to miss and no decay timer to tune.
    public class SlipperyFloor : Hazard
    {
        const float Epsilon = 0.0001f;

        const float UnselectedGizmo = 0.5f;
        const float SelectedGizmo = 1f;

        // Skid marks, all as fractions of the zone, so they read at any size of patch.
        const int SkidMarks = 6;
        const float SkidLengthOfZone = 0.3f;
        const float SkidLowRow = 0.25f;
        const float SkidHighRow = 0.6f;

        static readonly Color ZoneColour = new Color(0.6f, 0.9f, 1f, 0.35f);
        static readonly Color SkidColour = new Color(0.75f, 0.95f, 1f, 0.9f);

        [Header("Slippery")]
        [Tooltip("How much of their footing this floor gives the wizard back, from 0 to 1. It " +
                 "scales BOTH how hard they can push off and how hard they can dig in to stop, " +
                 "which together are the whole feel of ice: you keep going after you let go, and " +
                 "you cannot turn round in a hurry.\n\n" +
                 "1 is an ordinary floor and does nothing at all.\n" +
                 "0.5 is WATER or a wet flagstone - a full run takes twice as long to build and " +
                 "carries you about half a box past where you let go, which is a stumble rather " +
                 "than a hazard.\n" +
                 "0.15 is ICE - a run carries you more than a whole tile past where you let go " +
                 "and takes over two and a half seconds to turn around in.\n\n" +
                 "Below about 0.1 the wizard stops being able to steer at all, which players " +
                 "read as broken input rather than as ice. Walking is already four times safer " +
                 "than running here without any help - a walk tops out at half the speed and " +
                 "stopping distance goes as the square of it - so do not reach for a lower " +
                 "number to make a patch survivable.")]
        [Range(0f, 1f)] public float grip = 0.15f;

        [Header("Glaze")]
        [Tooltip("The thin sheet showing where the slippery floor is. Empty uses the first " +
                 "sprite found underneath. It is laid along the BOTTOM edge of the collider, so " +
                 "stretching the zone is the only thing you have to do.")]
        public SpriteRenderer sheet;

        [Tooltip("Colour of that sheet. Keep the alpha low - the floor tiles underneath are what " +
                 "tell the player where they are. A pale blue reads as ice, a dull green-grey " +
                 "as standing water.")]
        public Color sheetTint = new Color(0.72f, 0.9f, 1f, 0.28f);

        [Tooltip("How tall the visible sheet is, in boxes, measured up from the bottom of the " +
                 "collider. This is deliberately NOT the height of the zone: the trigger box has " +
                 "to be tall enough to swallow the wizard's boots without any fiddling, and a " +
                 "box filled with tint would draw half a box of blue hanging in the air over the " +
                 "floor. A tenth of a box or so is a glaze; raise it for a deep puddle.")]
        [Min(0.01f)] public float sheetThickness = 0.15f;

        [Tooltip("Resize the sheet to match the collider whenever the patch changes, so " +
                 "stretching the zone is the only thing you have to do. Turn it OFF the moment " +
                 "you want to size or place that art yourself - it will stop touching your " +
                 "transform.")]
        public bool fitSheetToZone = true;

        [Tooltip("Sorting order for the sheet. The level's floor tilemap draws at 0 and the " +
                 "wizard at 1, and there is no number in between - so at 0 the sheet and the " +
                 "floor tie and Unity is free to pick either, which is a glaze that flickers " +
                 "behind the tiles. 1 draws it over the floor. If the wizard's boots end up " +
                 "under the tint, raise the wizard's own Visual rather than lowering this.")]
        public int sortingOrder = 1;

        [Header("Editor")]
        [Tooltip("Draw the skid marks in the scene view without having to select the patch " +
                 "first, which is what you want while laying a level out. Their length reads the " +
                 "grip, so ice draws long streaks and a damp flagstone draws stubs.")]
        public bool alwaysShowSkids = true;

        [NonSerialized] bool warnedAboutSpeedGate;

        // Every step, not just on the way in. The whole hazard is a value that has to be there
        // on the step Movement.Run reads it, and Run reads it every step.
        protected override bool Continuous => true;

        void Reset()
        {
            // A floor cannot re-arm, cannot be dodged by being slow, and does not bite - so all
            // three of Hazard's gates are off. It DOES reach a tumbling wizard, so that standing
            // up on the ice finds the ice already there rather than a step of ordinary ground.
            minimumSpeed = 0f;
            rearmDelay = 0f;
            damage = 0;
            affectsRagdolled = true;
        }

        void OnValidate()
        {
            FitSheet();
            WarnAboutSpeedGate();
        }

        protected override void Awake()
        {
            base.Awake();

            if (sheet == null)
                sheet = GetComponentInChildren<SpriteRenderer>();

            if (sheet != null && sheet.sprite == null)
                sheet.sprite = Placeholder.Box;

            FitSheet();
            WarnAboutSpeedGate();
        }

        // The step the wizard first touches the patch. PlayerTrigger lets exactly one of Enter
        // and Stay through per physics step - they share the lastStep guard - and Enter wins, so
        // without this the very first step on the ice would run at full grip. That is one step of
        // ordinary braking at exactly the moment the player is trying to stop, and it is the
        // difference between the ice starting at the ice and the ice starting a foot into it.
        protected override void Affect(PlayerLogic wizard) => wizard.Slicken(grip);

        protected override void OnPlayerInside(PlayerCharacter wizard, float fixedDeltaTime)
        {
            if (!Allowed(wizard))
                return;

            // NOT scaled by Haste, unlike the wind's push next door. Haste is a flag, not
            // Time.timeScale, and the wizard never asks it anything - so the wind has to be
            // scaled by hand to slow down, while the wizard's own speed is already untouched.
            // Grip is a ratio over that untouched speed, so there is nothing here to slow:
            // multiplying it would make ice GRIPPIER the slower the world went, which is
            // backwards, and it would be the only place in the game where Haste reached the
            // wizard at all.
            wizard.Logic.Slicken(grip);
        }

        void FitSheet()
        {
            if (sheet == null)
                sheet = GetComponentInChildren<SpriteRenderer>();

            // No art is made here: OnValidate calls this, and building a texture inside a
            // serialisation callback is how you earn a console full of warnings. Awake fills in a
            // stand-in before the first call.
            if (sheet == null || sheet.sprite == null)
                return;

            sheet.color = sheetTint;
            sheet.sortingOrder = sortingOrder;

            var shape = GetComponent<BoxCollider2D>();

            if (shape == null)
                return;

            Vector2 unit = sheet.sprite.bounds.size;

            if (unit.x <= Epsilon || unit.y <= Epsilon)
                return;

            if (!fitSheetToZone)
                return;

            // Unlike the wind's haze this does NOT fill the zone, because ice is a surface and
            // wind is a volume. The sheet is laid along the bottom edge of the collider, which is
            // where the floor is: put the object down with its transform on the floor surface
            // and the glaze lands exactly on the tiles, while the rest of the box reaches up past
            // the wizard's knees where it can catch them without any measuring.
            float thick = Mathf.Min(sheetThickness, shape.size.y);

            sheet.transform.localPosition =
                shape.offset + new Vector2(0f, (thick - shape.size.y) * 0.5f);
            sheet.transform.localScale =
                new Vector3(shape.size.x / unit.x, thick / unit.y, 1f);
        }

        void WarnAboutSpeedGate()
        {
            if (minimumSpeed <= 0f || warnedAboutSpeedGate)
                return;

            warnedAboutSpeedGate = true;

            // minimumSpeed is checked in Hazard.OnPlayerEntered and NOWHERE else. All the work
            // here happens on the per-step path, which PlayerTrigger does not speed gate at all,
            // so a number typed in here silently does nothing except skip the first step of
            // contact - which is the one step it would be worst to skip.
            Debug.LogWarning(
                $"{name}: SlipperyFloor.minimumSpeed is {minimumSpeed}, but a slippery floor " +
                "works every step you are stood in it and that per-step path is not speed " +
                "gated. The number cannot make a patch safe to walk across - all it does is " +
                "skip the first step of contact. Set it to 0. Walking is already four times " +
                "safer than running on ice on its own, because a walk tops out at half the " +
                "speed and stopping distance goes as the square of it.");
        }

        void OnDrawGizmos()
        {
            if (alwaysShowSkids)
                DrawSkids(UnselectedGizmo);
        }

        void OnDrawGizmosSelected() => DrawSkids(SelectedGizmo);

        void DrawSkids(float strength)
        {
            var shape = GetComponent<BoxCollider2D>();

            if (shape == null)
                return;

            Bounds zone = shape.bounds;

            Gizmos.color = Faded(ZoneColour, strength);
            Gizmos.DrawWireCube(zone.center, zone.size);

            // Streak length reads the grip, the way the wind's arrows read its strength. Nothing
            // out here knows the wizard's real run speed, so it is a proportion of the patch
            // rather than a distance in boxes - the job is telling two patches apart at a glance,
            // not measuring one.
            float slide = 1f - Mathf.Clamp01(grip);

            if (slide <= Epsilon)
                return;

            float length = zone.size.x * SkidLengthOfZone * slide;
            float spacing = zone.size.x / (SkidMarks + 1);

            Gizmos.color = Faded(SkidColour, strength);

            for (int i = 1; i <= SkidMarks; i++)
            {
                // Staggered across two rows rather than all on one line, so a long patch does
                // not draw as a single dashed rule the eye mistakes for the collider itself.
                float lift = zone.size.y * (i % 2 == 0 ? SkidHighRow : SkidLowRow);

                var from = new Vector2(zone.min.x + spacing * i, zone.min.y + lift);
                Gizmos.DrawLine(from, from + Vector2.right * length);
            }
        }

        static Color Faded(Color colour, float strength)
        {
            colour.a *= strength;
            return colour;
        }
    }
}
