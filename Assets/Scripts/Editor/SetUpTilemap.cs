#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FallingWizard.EditorTools
{
    public static class SetUpTilemap
    {
        const string GroundLayer = "Ground";

        [MenuItem("Falling Wizard/Set Up Tilemap Collision")]
        public static void Run()
        {
            int ground = LayerMask.NameToLayer(GroundLayer);

            if (ground < 0)
            {
                Debug.LogError($"There is no '{GroundLayer}' layer in this project, so nothing " +
                               "the wizard walks on can be detected. Add it in Tags and Layers.");
                return;
            }

            Tilemap[] targets = Chosen();

            if (targets.Length == 0)
            {
                Debug.LogWarning("No Tilemap found to set up. Open the level scene, or select the " +
                                 "Tilemap objects you want collision on, and run this again.");
                return;
            }

            foreach (Tilemap map in targets)
                Wire(map, ground);

            EditorSceneManager.MarkSceneDirty(targets[0].gameObject.scene);
            Debug.Log($"Tilemap collision set up on {targets.Length} tilemap(s). Save the scene to keep it.");
        }

        static Tilemap[] Chosen()
        {
            var picked = new List<Tilemap>();

            foreach (GameObject go in Selection.gameObjects)
                picked.AddRange(go.GetComponentsInChildren<Tilemap>(true));

            if (picked.Count > 0)
                return picked.Distinct().ToArray();

            return Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        static void Wire(Tilemap map, int ground)
        {
            GameObject go = map.gameObject;
            var changes = new List<string>();

            if (go.layer != ground)
            {
                Undo.RecordObject(go, "Tilemap layer");
                go.layer = ground;
                changes.Add($"layer -> {GroundLayer}");
            }

            var body = go.GetComponent<Rigidbody2D>();

            if (body == null)
            {
                body = Undo.AddComponent<Rigidbody2D>(go);
                changes.Add("added Rigidbody2D");
            }

            if (body.bodyType != RigidbodyType2D.Static)
            {
                Undo.RecordObject(body, "Tilemap body type");
                body.bodyType = RigidbodyType2D.Static;
                changes.Add("body -> Static");
            }

            var tiles = go.GetComponent<TilemapCollider2D>();

            if (tiles == null)
            {
                tiles = Undo.AddComponent<TilemapCollider2D>(go);
                changes.Add("added TilemapCollider2D");
            }

            if (tiles.compositeOperation != Collider2D.CompositeOperation.Merge)
            {
                Undo.RecordObject(tiles, "Tilemap composite operation");
                tiles.compositeOperation = Collider2D.CompositeOperation.Merge;
                changes.Add("collider -> merged into composite");
            }

            var composite = go.GetComponent<CompositeCollider2D>();

            if (composite == null)
            {
                composite = Undo.AddComponent<CompositeCollider2D>(go);
                changes.Add("added CompositeCollider2D");
            }

            if (composite.geometryType != CompositeCollider2D.GeometryType.Polygons)
            {
                Undo.RecordObject(composite, "Composite geometry");
                composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
                changes.Add("composite -> Polygons");
            }

            EditorUtility.SetDirty(go);

            Debug.Log(changes.Count == 0
                ? $"'{go.name}' was already set up."
                : $"'{go.name}': {string.Join(", ", changes)}.", go);
        }
    }
}
#endif
