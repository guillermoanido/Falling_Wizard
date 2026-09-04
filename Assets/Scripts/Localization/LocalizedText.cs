using FallingWizard.Core;
using TMPro;
using UnityEngine;

namespace FallingWizard.UI
{
    // Put this on a label that was typed into a scene or a prefab, and its words come from the
    // translation instead of from whatever is sitting in the text box.
    //
    // Deliberately NOT [RequireComponent(typeof(TMP_Text))]: TMP_Text is abstract, so Unity cannot
    // add one, and the attribute would only ever pop an error dialog on a label that is already
    // there. The warning in Awake says the same thing, once, with an explanation.
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
            // Both, and in this order. Subscribing is what catches a language change while this
            // panel is open; re-reading right now is what catches a change that happened while it
            // was switched off, which is most of them - a disabled component is subscribed to
            // nothing and never saw the event go past.
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
