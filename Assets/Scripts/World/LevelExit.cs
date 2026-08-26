using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    [RequireComponent(typeof(Collider2D))]
    public class LevelExit : PlayerTrigger
    {
        [Header("Exit")]
        [Tooltip("Scene loaded on reaching the bottom. Leave empty and the level simply starts " +
                 "again, which is what you want until there is a second level to go to.")]
        public string nextScene = "";

        [Tooltip("Forget the last rest site, so the level starts from the top rather than " +
                 "dropping you back where you last sat down. Turn this off once this leads " +
                 "somewhere new instead of round again.")]
        public bool startsOver = true;

        [Tooltip("Bank the wisps being carried, the way turning back at a rest site does. Off " +
                 "while this only resets the level, or a lap would pay out forever.")]
        public bool banksWisps = false;

        [Tooltip("Optional prefab spawned where the wizard crossed.")]
        public GameObject reachedEffect;

        void Reset() => GetComponent<Collider2D>().isTrigger = true;

        protected override void OnPlayerEntered(PlayerCharacter wizard)
        {
            if (UI.Screens.ModalOpen || !wizard.Logic.health.IsAlive)
                return;

            if (reachedEffect != null)
                Instantiate(reachedEffect, transform.position, Quaternion.identity);

            if (banksWisps)
                Progress.BankCarried();

            if (startsOver)
                Progress.ClearCheckpoint();

            if (string.IsNullOrEmpty(nextScene))
                Game.ReloadCurrentScene();
            else
                Game.LoadLevel(nextScene);
        }
    }
}
