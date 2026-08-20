using FallingWizard.Core;
using UnityEngine;
using UnityEngine.Playables;

namespace FallingWizard.Cutscenes
{
    public class CutsceneRunner : MonoBehaviour
    {
        [Tooltip("Timeline to play. Leave empty to simply wait for the placeholder duration.")]
        [SerializeField] PlayableDirector director;

        [Tooltip("How long the scene lasts when there is no Timeline yet, in seconds.")]
        [SerializeField] float placeholderDuration = 4f;

        [SerializeField] bool canSkip = true;

        [Tooltip("Optional 'press any button to skip' label.")]
        [SerializeField] GameObject skipPrompt;

        bool finished;

        void Start()
        {
            if (skipPrompt != null)
                skipPrompt.SetActive(canSkip);

            if (director == null)
            {
                Invoke(nameof(Finish), placeholderDuration);
                return;
            }

            director.extrapolationMode = DirectorWrapMode.None;
            director.Play();
        }

        void Update()
        {
            if (finished)
                return;

            if (canSkip && MenuInput.SkipPressedThisFrame)
                Finish();
            else if (director != null && director.state != PlayState.Playing)
                Finish();
        }

        void Finish()
        {
            if (finished)
                return;

            finished = true;
            Game.LoadFirstLevel();
        }
    }
}
