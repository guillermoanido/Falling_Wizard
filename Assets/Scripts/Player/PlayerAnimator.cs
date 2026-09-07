using UnityEngine;

namespace FallingWizard.Player
{
    [RequireComponent(typeof(Animator))]
    [DefaultExecutionOrder(50)]
    public class PlayerAnimator : MonoBehaviour
    {
        public const int ModeGround = 0;
        public const int ModeStaff = 1;
        public const int ModeRagdoll = 2;
        public const int ModeDead = 3;

        static readonly int ModeId = Animator.StringToHash("Mode");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int MovingId = Animator.StringToHash("Moving");
        static readonly int WalkingId = Animator.StringToHash("Walking");
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int ClimbRateId = Animator.StringToHash("ClimbRate");
        static readonly int ClimbUpId = Animator.StringToHash("ClimbUp");
        static readonly int ClimbDownId = Animator.StringToHash("ClimbDown");
        static readonly int RagdollVariantId = Animator.StringToHash("RagdollVariant");
        static readonly int HurtId = Animator.StringToHash("Hurt");

        [Header("Ragdoll")]
        [Tooltip("How many ragdoll reactions to build and cycle through. Rebuild after changing.")]
        [Min(1)] public int ragdollVariants = 4;

        [Header("Locomotion")]
        [Tooltip("Speed below which the wizard counts as standing still, in boxes per second.")]
        [Min(0f)] public float standStillSpeed = 0.1f;

        [Tooltip("Min and max cycle-rate multiplier for Walk and Run.")]
        public Vector2 cycleRateRange = new Vector2(0.5f, 1.5f);

        [Header("Staff")]
        [Tooltip("Hide the separate staff art while carried, for art that draws it into the character.")]
        public bool staffIsDrawnIntoTheArt = false;

        Animator animator;
        PlayerCharacter character;
        SpriteRenderer staffArt;

        int lastHealth;
        int variant;
        bool wasOnStaff;
        bool wasRagdolling;
        bool wasRagdollGrounded;

        void Awake()
        {
            animator = GetComponent<Animator>();
            character = GetComponent<PlayerCharacter>();

            if (character == null)
            {
                Debug.LogError($"'{name}' has a PlayerAnimator but no PlayerCharacter beside it, " +
                               "so there is no wizard to read. Put them on the same object.", this);
                enabled = false;
                return;
            }

            Staff staff = GetComponentInChildren<Staff>(true);
            staffArt = staff != null ? staff.GetComponentInChildren<SpriteRenderer>(true) : null;
        }

        void Start() => lastHealth = character.Logic.health.Current;

        void Update()
        {
            PlayerLogic wizard = character.Logic;
            PlayerLogic.Movement walk = wizard.movement;

            int mode = !wizard.health.IsAlive ? ModeDead
                     : wizard.State == PlayerState.Ragdoll ? ModeRagdoll
                     : wizard.State == PlayerState.OnStaff ? ModeStaff
                     : ModeGround;

            bool grounded = wizard.State != PlayerState.OnVine && walk.IsGrounded;
            bool moving = mode == ModeGround && grounded && walk.HorizontalSpeed > standStillSpeed;

            bool walking = wizard.Steering.Walk;

            animator.SetInteger(ModeId, mode);
            animator.SetBool(GroundedId, grounded);
            animator.SetBool(MovingId, moving);
            animator.SetBool(WalkingId, walking);
            animator.SetFloat(SpeedId, CycleRate(wizard, walk, walking));
            animator.SetFloat(ClimbRateId, ClimbRate(wizard, mode));

            WatchForAClimbStarting(wizard, mode);
            WatchForATumble(mode, walk);
            WatchForAHit(wizard);
            ShowTheStaffWhenThePoleOwnsIt(wizard);
        }

        float CycleRate(PlayerLogic wizard, PlayerLogic.Movement walk, bool walking)
        {
            float top = (walking ? walk.walkSpeed : walk.runSpeed) * wizard.Stats.MoveSpeedMultiplier;

            if (top <= Mathf.Epsilon)
                return 1f;

            return Mathf.Clamp(walk.HorizontalSpeed / top, cycleRateRange.x, cycleRateRange.y);
        }

        float ClimbRate(PlayerLogic wizard, int mode)
        {
            if (mode != ModeStaff || !wizard.HasPole)
                return 0f;

            Staff.Pole pole = wizard.Pole;
            float lean = wizard.Steering.Lean;

            if (Mathf.Abs(lean) <= pole.leanThreshold)
                return 0f;

            if ((lean > 0f && pole.AtTop) || (lean < 0f && pole.AtBottom))
                return 0f;

            return Mathf.Sign(lean);
        }

        void WatchForAClimbStarting(PlayerLogic wizard, int mode)
        {
            bool onStaff = mode == ModeStaff;

            if (onStaff && !wasOnStaff)
                animator.SetTrigger(wizard.HasPole && wizard.Pole.IsClimbing
                    ? ClimbUpId
                    : ClimbDownId);

            wasOnStaff = onStaff;
        }

        void WatchForATumble(int mode, PlayerLogic.Movement walk)
        {
            bool ragdolling = mode == ModeRagdoll;

            bool downOnTheFloor = ragdolling && walk.IsGrounded;

            if ((ragdolling && !wasRagdolling) || (downOnTheFloor && !wasRagdollGrounded))
            {
                variant = ragdollVariants <= 1 ? 0 : (variant + 1) % ragdollVariants;
                animator.SetInteger(RagdollVariantId, variant);
            }

            wasRagdolling = ragdolling;
            wasRagdollGrounded = downOnTheFloor;
        }

        void WatchForAHit(PlayerLogic wizard)
        {
            int hearts = wizard.health.Current;

            if (hearts < lastHealth && wizard.health.IsInvulnerable && wizard.health.IsAlive)
                animator.SetTrigger(HurtId);

            lastHealth = hearts;
        }

        void ShowTheStaffWhenThePoleOwnsIt(PlayerLogic wizard)
        {
            if (!staffIsDrawnIntoTheArt || staffArt == null)
                return;

            staffArt.enabled = wizard.HasPole && wizard.Pole.IsPlanted;
        }
    }
}
