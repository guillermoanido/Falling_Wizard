namespace FallingWizard.Player
{
    public partial class PlayerLogic
    {
        public class Modifiers
        {
            public float MoveSpeedMultiplier;
            public float JumpHeightMultiplier;
            public float FallSpeedMultiplier;
            public float FallDamageMultiplier;
            public float WindMultiplier;

            // Air only. MoveSpeedMultiplier cannot say that, and a canopy that made the wizard
            // sprint along the floor would be a different spell.
            public float AirSpeedMultiplier;
            public float AirControlMultiplier;

            // How fast sideways speed bleeds away in the air with nothing held. Separate from
            // AirControlMultiplier on purpose: a wing should bite HARDER when steered and coast
            // LONGER when not, and one multiplier over both does the second one backwards.
            public float AirDragMultiplier;

            public int ExtraJumps;
            public bool Shielded;

            // The stick is aiming something, not steering the wizard.
            public bool Rooted;

            public Modifiers() => Reset();

            public void Reset()
            {
                MoveSpeedMultiplier = 1f;
                JumpHeightMultiplier = 1f;
                FallSpeedMultiplier = 1f;
                FallDamageMultiplier = 1f;
                WindMultiplier = 1f;
                AirSpeedMultiplier = 1f;
                AirControlMultiplier = 1f;
                AirDragMultiplier = 1f;
                ExtraJumps = 0;
                Shielded = false;
                Rooted = false;
            }
        }
    }
}
