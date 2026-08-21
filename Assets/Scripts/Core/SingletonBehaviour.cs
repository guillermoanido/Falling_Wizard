using UnityEngine;

namespace FallingWizard.Core
{
    public abstract class SingletonBehaviour<T> : MonoBehaviour where T : SingletonBehaviour<T>
    {
        static T instance;

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

        protected virtual void OnAwake() { }

        protected virtual void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
