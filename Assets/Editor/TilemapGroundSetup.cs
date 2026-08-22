using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FallingWizard.EditorTools
{
    // A Tilemap painted in the editor is scenery and nothing more: it renders tiles and has no
    // collision of any kind until you add one. The wizard's ground sense is an OverlapBox against
    // the Ground layer, so a bare Tilemap means never grounded - which reads as "the player cannot
    // jump" long before you notice they are also falling through the floor.
    //
    // This walks every Tilemap in the open scene and gives it the three things it needs: the Ground
    // layer, a collider built from the painted tiles, and a composite that welds those per-tile
    // boxes into one outline so the wizard cannot catch on the seam between two floor tiles.
    static class TilemapGroundSetup
    {
        const string GroundLayerName = "Ground";

        [MenuItem("Tools/Falling Wizard/Set Up Tilemap Collision", false, 40)]
        static void SetUpTilemapCollision()
        {
            var maps = new List<Tilemap>();

            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                maps.AddRange(root.GetComponentsInChildren<Tilemap>(true));

            if (maps.Count == 0)
            {
                Debug.LogWarning("No Tilemap found in the open scene.");
                return;
            }

            int ground = LayerMask.NameToLayer(GroundLayerName);

            foreach (Tilemap map in maps)
                MakeSolid(map, ground);

            WarnAboutStrayGridColliders();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"Set up collision on {maps.Count} tilemap(s).");
        }

        // A Grid is a coordinate system, not a thing you stand on. A collider on one is nearly
        // always a slip of the Add Component menu, and a baffling one: a single tile-sized box
        // sitting at the origin of the level, holding the wizard up in one spot and nowhere else.
        static void WarnAboutStrayGridColliders()
        {
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Grid grid in root.GetComponentsInChildren<Grid>(true))
                {
                    var stray = grid.GetComponent<Collider2D>();

                    if (stray != null)
                        Debug.LogWarning($"'{grid.name}' has a {stray.GetType().Name} on it. The " +
                                         "collider belongs on the Tilemap underneath, not on the " +
                                         "Grid. Delete this one.", grid);
                }
            }
        }

        static void MakeSolid(Tilemap map, int groundLayer)
        {
            GameObject go = map.gameObject;
            Undo.RecordObject(go, "Set Up Tilemap Collision");
            go.layer = groundLayer;

            var collider = Get<TilemapCollider2D>(go);

            // Static, not "no rigidbody at all": a CompositeCollider2D requires a body to hang off,
            // and a static one costs nothing to simulate while letting the composite rebuild when
            // you repaint tiles.
            var body = Get<Rigidbody2D>(go);
            body.bodyType = RigidbodyType2D.Static;

            var composite = Get<CompositeCollider2D>(go);
            composite.geometryType = CompositeCollider2D.GeometryType.Outlines;

            // Without this the tilemap collider keeps its own per-tile boxes and the composite
            // stays empty, so the floor looks welded in the gizmos but still isn't.
            collider.compositeOperation = Collider2D.CompositeOperation.Merge;

            EditorUtility.SetDirty(go);
        }

        static T Get<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(go);
        }
    }
}
