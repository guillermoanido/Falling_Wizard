using FallingWizard.Core;
using TMPro;
using UnityEngine;

namespace FallingWizard.Localization
{
    public class LocalizedText : MonoBehaviour
    {
        [Tooltip("Which line of the translation this label shows. It is a key, not a sentence: " +
                 "'menu.play', 'settings.volume', 'pause.title'. In English the label reads " +
                 "exactly as it does today, so nothing looks any different until a language is " +
                 "picked.\n\nLeave it empty and this does nothing at all, which is what you want " +
                 "on a label the game writes into itself - the volume percentage, the current " +
                 "choice in a dropdown, the button glyph under a spell. Putting a key on one of " +
                 "those makes the two fight over the same words.")]
        public string key = "";

        TMP_Text label;

        void Awake()
        {
            label = GetComponent<TMP_Text>();

            if (label == null)
                Debug.LogWarning("Localized Text is on an object with no text on it, so there is " +
                                 "nothing here for it to translate. It goes on the same object " +
                                 "as the TextMeshPro - Text (UI) component, not on the row or " +
                                 "the button above it.", this);
        }

        void OnEnable()
        {
            Loc.Changed += Show;
            Show();
        }

        void OnDisable() => Loc.Changed -= Show;

        void Show()
        {
            if (label == null || string.IsNullOrEmpty(key))
                return;

            label.text = Loc.Get(key);
        }
    }
}
