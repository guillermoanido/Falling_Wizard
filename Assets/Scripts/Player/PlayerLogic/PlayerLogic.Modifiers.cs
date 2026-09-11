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

            public float AirSpeedMultiplier;
            public float AirControlMultiplier;

            public float AirDragMultiplier;

            public int ExtraJumps;
            public bool Shielded;

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
