using UnityEngine;

namespace FallingWizard.Core
{
    /// <summary>
    /// Base for the one-of-a-kind actors in a scene. The first instance to wake claims the
    /// slot, any later duplicate destroys itself, and everyone else can reach the live one
    /// through <see cref="Instance"/> instead of being wired up by hand.
    /// </summary>
    public abstract class SingletonBehaviour<T> : MonoBehaviour where T : SingletonBehaviour<T>
    {
        static T instance;

        /// <summary>The live instance, or null if none is awake yet.</summary>
        // Unity's == reports a destroyed object as null, so a leftover from the previous
        // play session can never masquerade as a live instance here.
        public static T Instance => instance;

        public static bool Exists => instance != null;

        protected virtual void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning(
                    $"A second {typeof(T).Name} woke up in this scene. Destroying '{name}'.", this);
                Destroy(gameObject);
                return;
            }

            instance = (T)this;
            OnAwake();
        }

        /// <summary>Runs once, only on the instance that actually claimed the slot.</summary>
        protected virtual void OnAwake() { }

        protected virtual void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
