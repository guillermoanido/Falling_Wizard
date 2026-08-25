using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public class HeartPickup : Pickup
    {
        [Header("Heart")]
        [Tooltip("Hearts added to the bar, for good. It is filled on the spot, it survives dying " +
                 "and turning back, and this heart is never here again - which is why the next " +
                 "one has to be found somewhere deeper.")]
        [Min(1)] public int hearts = 1;

        [Tooltip("Wisps given instead when the bar is already as long as it can ever get. Never " +
                 "leave a player standing over dead loot.")]
        [Min(0)] public int wispsIfFull = 3;

        protected override string Prefix => "heart";

        protected override StaysTaken StaysTaken => StaysTaken.ForGood;

        protected override bool Take(PlayerCharacter wizard)
        {
            if (wizard.Logic.GrowHeart(hearts))
                return true;

            Progress.CarryWisps(wispsIfFull);
            return true;
        }
    }
}
