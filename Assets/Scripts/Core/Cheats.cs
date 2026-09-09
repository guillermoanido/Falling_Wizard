using FallingWizard.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FallingWizard.Core
{
    [DefaultExecutionOrder(-200)]
    public class Cheats : MonoBehaviour
    {
        static Cheats instance;

        public static bool Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (instance != null)
                return;

            var go = new GameObject("Cheats") { hideFlags = HideFlags.HideAndDontSave };
            instance = go.AddComponent<Cheats>();
            DontDestroyOnLoad(go);
        }

        void Update()
        {
            Keyboard keys = Keyboard.current;

            if (keys != null && keys.mKey.wasPressedThisFrame)
                Toggle();

            if (!Active)
                return;

            PlayerCharacter wizard = PlayerCharacter.Instance;

            if (wizard != null)
                wizard.Logic.Invulnerable = true;
        }

        void Toggle()
        {
            Active = !Active;
            Progress.FreeSpending = Active;

            PlayerCharacter wizard = PlayerCharacter.Instance;

            if (wizard != null)
                wizard.Logic.Invulnerable = Active;

            Debug.Log(Active
                ? "CHEATS ON - god mode, and every wisp cost is free. Press M again to turn them off."
                : "CHEATS OFF - damage and wisp costs are back to normal.");
        }

        void OnGUI()
        {
            if (!Active)
                return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperRight,
            };

            style.normal.textColor = new Color(1f, 0.82f, 0.25f);

            GUI.Label(new Rect(Screen.width - 320f, 8f, 300f, 30f), "CHEATS ON  (M)", style);
        }
    }
}
