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
                 "when two pickups share a spot, or when you want to move one without the game " +
                 "forgetting it was taken.")]
        public string id = "";

        [Header("Look")]
        [Tooltip("Colour of the stand-in block drawn when there is no sprite anywhere underneath. " +
                 "Once you give it real art this does nothing.")]
        public Color tint = Color.white;

        [Tooltip("Height of the idle bob, in boxes. 0 holds it still. Only the art bobs - the " +
                 "trigger and the object itself never move, because a pickup that drifts would " +
                 "keep changing the name it is remembered by.")]
        [Min(0f)] public float bobHeight = 0.15f;

        [Tooltip("Bobs per second.")]
        [Min(0f)] public float bobSpeed = 1.5f;

        [Tooltip("Optional prefab spawned where it stood.")]
        public GameObject collectedEffect;

        [NonSerialized] string key;
        [NonSerialized] Transform visual;
        [NonSerialized] Vector3 restingPoint;

        protected abstract string Prefix { get; }

        protected abstract StaysTaken StaysTaken { get; }

        protected abstract bool Take(PlayerCharacter wizard);

        public string Key => key ??= NameIt();

        void Reset() => GetComponent<Collider2D>().isTrigger = true;

        protected virtual void Awake()
        {
            if (Progress.IsGone(Key))
            {
                Destroy(gameObject);
                return;
            }

            Dress();
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

        string NameIt()
        {
            string scene = SceneManager.GetActiveScene().name;

            if (!string.IsNullOrEmpty(id))
                return $"{Prefix}{scene}:{id}";

            Vector2 point = transform.position;

            return $"{Prefix}{scene}:" +
                   $"{Mathf.RoundToInt(point.x * 4f)},{Mathf.RoundToInt(point.y * 4f)}";
        }

        void Dress()
        {
            var art = GetComponentInChildren<SpriteRenderer>();

            if (art == null)
            {
                var child = new GameObject("Art");
                child.transform.SetParent(transform, false);

                art = child.AddComponent<SpriteRenderer>();
                art.sortingOrder = 1;
            }

            if (art.sprite == null)
            {
                art.sprite = Placeholder.Box;
                art.color = tint;
            }

            if (art.transform == transform)
                return;

            visual = art.transform;
            restingPoint = visual.localPosition;
        }
    }
}
