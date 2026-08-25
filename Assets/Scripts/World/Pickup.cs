using System;
using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallingWizard.World
{
    [RequireComponent(typeof(Collider2D))]
    public abstract class Pickup : PlayerTrigger
    {
        [Header("Identity")]
        [Tooltip("Leave empty and this pickup is remembered by where it stands, which survives " +
                 "renaming it and re-parenting it. Moving it makes it a new pickup. Fill it in " +
                 "when two pickups share a spot, or when you want to move one without the run " +
                 "forgetting it was taken.")]
        public string id = "";

        [Header("Look")]
        [Tooltip("Height of the idle bob, in boxes. 0 holds it still.")]
        [Min(0f)] public float bobHeight = 0.15f;

        [Tooltip("Bobs per second.")]
        [Min(0f)] public float bobSpeed = 1.5f;

        [Tooltip("Optional prefab spawned where it stood.")]
        public GameObject collectedEffect;

        [NonSerialized] Transform visual;
        [NonSerialized] Vector3 restingPoint;

        protected abstract string Prefix { get; }

        protected abstract StaysTaken StaysTaken { get; }

        protected abstract bool Take(PlayerCharacter wizard);

        public string Key
        {
            get
            {
                string scene = SceneManager.GetActiveScene().name;

                if (!string.IsNullOrEmpty(id))
                    return $"{Prefix}{scene}:{id}";

                Vector2 point = transform.position;

                return $"{Prefix}{scene}:" +
                       $"{Mathf.RoundToInt(point.x * 4f)},{Mathf.RoundToInt(point.y * 4f)}";
            }
        }

        void Reset() => GetComponent<Collider2D>().isTrigger = true;

        protected virtual void Awake()
        {
            if (Progress.IsGone(Key))
            {
                Destroy(gameObject);
                return;
            }

            var art = GetComponentInChildren<SpriteRenderer>();
            visual = art != null ? art.transform : null;

            if (visual != null)
                restingPoint = visual.localPosition;
        }

        void Update()
        {
            if (visual == null || bobHeight <= 0f || bobSpeed <= 0f)
                return;

            float rise = Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f) * bobHeight;
            visual.localPosition = restingPoint + new Vector3(0f, rise, 0f);
        }

        protected sealed override void OnPlayerEntered(PlayerCharacter wizard)
        {
            if (!wizard.Logic.health.IsAlive || !Take(wizard))
                return;

            Progress.MarkFound(Key, StaysTaken);

            if (collectedEffect != null)
                Instantiate(collectedEffect, transform.position, Quaternion.identity);

            Destroy(gameObject);
        }
    }
}
