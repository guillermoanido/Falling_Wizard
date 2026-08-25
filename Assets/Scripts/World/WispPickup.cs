using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public class WispPickup : Pickup
    {
        [Header("Wisps")]
        [Tooltip("Wisps carried away from here. You keep them only by reaching a rest site and " +
                 "turning back - die anywhere below and they are gone.")]
        [Min(1)] public int amount = 1;

        protected override string Prefix => "wisp";

        protected override StaysTaken StaysTaken => StaysTaken.OnceBanked;

        protected override bool Take(PlayerCharacter wizard)
        {
            Progress.CarryWisps(amount);
            return true;
        }
    }
}
