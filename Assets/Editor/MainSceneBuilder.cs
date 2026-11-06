using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using static GunnerVR.EditorTools.GunnerBuildKit;

namespace GunnerVR.EditorTools
{
    /// <summary>
    /// Builds MainWorkshop.unity: the workshop and the shooting range as two areas of one
    /// continuous building, joined by a walkable hallway, under the hierarchy the brief
    /// asks for. There is one XR Origin, one pistol prop and one session for the whole run,
    /// and no scene is ever loaded to move between the areas.
    ///
    /// The hallway turns two corners on purpose. A straight 50 m hall would let the player
    /// see both ends at once and occlusion culling would have nothing to cull, which matters
    /// on a standalone Quest.
    /// </summary>
    public static class MainSceneBuilder
    {
        const string k_ScenePath = "Assets/Scenes/MainWorkshop.unity";
        const string k_RootName = "MainWorkshop";

        // Where each area sits in the world. The areas themselves are built in local space.
        static readonly Vector3 k_WorkshopOrigin = new Vector3(0f, 0f, 0f);
        static readonly Vector3 k_RangeOrigin = new Vector3(14f, 0f, -4f);

        const float k_HallWidth = 2.2f;
        const float k_HallHeight = 3f;
        const float k_HallBackZ = -13f;

        [MenuItem("Tools/Gunner VR/Build Main Scene (Workshop + Range)", priority = 0)]
        public static void Build()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                EditorUtility.DisplayDialog("Gunner VR",
                    "Could not find the URP Lit shader. Check the project is still on URP, then try again.", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = OpenOrCreateScene();
            var palette = new Palette(lit);

            ClearPrevious();

            var root = new GameObject(k_RootName).transform;

            EnsureSceneServices();
            var rig = EnsureRig();

            // ---- Environment -------------------------------------------------
            var environment = new GameObject("Environment").transform;
            environment.SetParent(root, false);

            var workshop = WorkshopAreaBuilder.BuildArea(environment, k_WorkshopOrigin, palette,
                out var weapon, out var spawnPosition);
            var range = RangeAreaBuilder.BuildArea(environment, k_RangeOrigin, palette);

            BuildHallway(environment, palette);

            // The area builders parent their systems to `environment`; move them to the
            // top-level groups the brief lays out.
            Regroup(environment, "WorkshopSystems", root);
            Regroup(environment, "ShootingRangeSystems", root);

            // ---- SharedSystems -----------------------------------------------
            var shared = new GameObject("SharedSystems").transform;
            shared.SetParent(root, false);

            var navigationGo = new GameObject("AreaNavigationManager");
            navigationGo.transform.SetParent(shared, false);
            var navigation = navigationGo.AddComponent<AreaNavigationManager>();
            navigation.Bind(rig != null ? rig.Camera.transform : null,
                AreaBounds(k_WorkshopOrigin, 9f, 9f, -4f, 5f),
                AreaBounds(k_RangeOrigin, 10f, 36f, -6f, 30f));

            var mainGo = new GameObject("MainGameManager");
            mainGo.transform.SetParent(shared, false);
            mainGo.AddComponent<MainGameManager>().Bind(workshop, range, navigation, weapon);

            // ---- empty groups the brief asks for -----------------------------
            foreach (var name in new[] { "UI", "Lighting", "Audio", "VFX" })
                new GameObject(name).transform.SetParent(root, false);

            if (rig != null)
                rig.transform.SetPositionAndRotation(spawnPosition, Quaternion.identity);

            RegisterSceneFirst(k_ScenePath);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Gunner VR] Main scene built at {k_ScenePath}: workshop at {k_WorkshopOrigin}, " +
                      $"range at {k_RangeOrigin}, joined by a hallway. Registered at build index 0.");
            Selection.activeTransform = root;
        }

        [MenuItem("Tools/Gunner VR/Remove Generated Main Scene", priority = 20)]
        public static void Remove()
        {
            ClearPrevious();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[Gunner VR] Removed '{k_RootName}' from the open scene.");
        }

        static Scene OpenOrCreateScene()
        {
            if (File.Exists(k_ScenePath))
                return EditorSceneManager.OpenScene(k_ScenePath, OpenSceneMode.Single);

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, k_ScenePath);
            return scene;
        }

        static void ClearPrevious()
        {
            var existing = GameObject.Find(k_RootName);
            if (existing != null)
                Object.DestroyImmediate(existing);
        }

        static void Regroup(Transform from, string childName, Transform to)
        {
            var child = from.Find(childName);
            if (child != null)
                child.SetParent(to, true);
        }

        /// <summary>A world-space box over one area's floor, used for working out where the player is.</summary>
        static Bounds AreaBounds(Vector3 origin, float width, float depth, float minZ, float maxZ)
        {
            var centre = origin + new Vector3(0f, 1f, (minZ + maxZ) * 0.5f);
            return new Bounds(centre, new Vector3(width, 4f, depth));
        }

        // ------------------------------------------------------------- hallway

        static void BuildHallway(Transform environment, Palette palette)
        {
            var hall = new GameObject("ConnectingHallway").transform;
            hall.SetParent(environment, false);

            var workshopDoor = k_WorkshopOrigin + new Vector3(0f, 0f, WorkshopAreaBuilder.doorwayZ);
            var rangeDoor = k_RangeOrigin + new Vector3(0f, 0f, RangeAreaBuilder.doorwayZ);

            var cornerA = new Vector3(workshopDoor.x, 0f, k_HallBackZ);
            var cornerB = new Vector3(rangeDoor.x, 0f, k_HallBackZ);

            // Three straight runs: out of the workshop, across, and up into the range.
            Corridor(hall, palette, "Leg A", workshopDoor, cornerA);
            Corridor(hall, palette, "Leg B", cornerA, cornerB);
            Corridor(hall, palette, "Leg C", cornerB, rangeDoor);

            // Signs at the corners, so each turn tells the player where it goes.
            Label3D(hall, "Sign To Workshop", cornerA + new Vector3(0f, 2.3f, -k_HallWidth * 0.5f + 0.08f),
                new Vector2(3f, 0.4f), 0.2f, "←  WORKSHOP", palette.hazard.color,
                TextAlignmentOptions.Center, Quaternion.LookRotation(Vector3.forward, Vector3.up));
            Label3D(hall, "Sign To Range", cornerB + new Vector3(0f, 2.3f, -k_HallWidth * 0.5f + 0.08f),
                new Vector2(3f, 0.4f), 0.2f, "SHOOTING RANGE  ↑", palette.hazard.color,
                TextAlignmentOptions.Center, Quaternion.LookRotation(Vector3.forward, Vector3.up));

            // Optional teleport pads. Nothing moves the player on its own — these only fire
            // when the player aims at one and chooses it.
            TeleportPad(hall, palette, "Pad Workshop End", workshopDoor + new Vector3(0f, 0f, -1.4f));
            TeleportPad(hall, palette, "Pad Corner A", cornerA + new Vector3(0f, 0f, 1.4f));
            TeleportPad(hall, palette, "Pad Corner B", cornerB + new Vector3(0f, 0f, 1.4f));
            TeleportPad(hall, palette, "Pad Range End", rangeDoor + new Vector3(0f, 0f, -1.4f));
        }

        /// <summary>
        /// One straight run of corridor between two points on the floor plane, with a floor
        /// the player can teleport onto, two side walls, a ceiling and a light.
        /// </summary>
        static void Corridor(Transform parent, Palette palette, string name, Vector3 from, Vector3 to)
        {
            var leg = new GameObject(name).transform;
            leg.SetParent(parent, false);

            var delta = to - from;
            var alongZ = Mathf.Abs(delta.z) >= Mathf.Abs(delta.x);
            var length = (alongZ ? Mathf.Abs(delta.z) : Mathf.Abs(delta.x)) + k_HallWidth;
            var centre = (from + to) * 0.5f;

            var floorSize = alongZ
                ? new Vector3(k_HallWidth, 0.1f, length)
                : new Vector3(length, 0.1f, k_HallWidth);

            var floor = Box(leg, "Floor", centre + new Vector3(0f, -0.05f, 0f), floorSize, palette.concrete);
            var teleport = floor.AddComponent<TeleportationArea>();
            teleport.matchOrientation = MatchOrientation.WorldSpaceUp;

            Decor(PrimitiveType.Cube, leg, "Ceiling", centre + new Vector3(0f, k_HallHeight, 0f),
                floorSize, palette.ceiling);

            // Side walls run along the length, one either side of the walkway.
            var wallSize = alongZ
                ? new Vector3(0.1f, k_HallHeight, length)
                : new Vector3(length, k_HallHeight, 0.1f);
            var offset = alongZ
                ? new Vector3(k_HallWidth * 0.5f, 0f, 0f)
                : new Vector3(0f, 0f, k_HallWidth * 0.5f);

            foreach (var sign in new[] { -1f, 1f })
            {
                Box(leg, sign < 0 ? "Wall A" : "Wall B",
                    centre + offset * sign + new Vector3(0f, k_HallHeight * 0.5f, 0f),
                    wallSize, palette.wallUpper);
            }

            var lampCount = Mathf.Max(1, Mathf.RoundToInt(length / 5f));
            for (var i = 0; i < lampCount; i++)
            {
                var t = (i + 0.5f) / lampCount;
                var at = Vector3.Lerp(from, to, t) + new Vector3(0f, k_HallHeight - 0.14f, 0f);

                Decor(PrimitiveType.Cube, leg, $"Lamp {i + 1}", at,
                    new Vector3(alongZ ? k_HallWidth - 0.6f : 0.5f, 0.06f, alongZ ? 0.5f : k_HallWidth - 0.6f),
                    palette.lampGlow);

                var bulbGo = new GameObject($"Light {i + 1}");
                bulbGo.transform.SetParent(leg, false);
                bulbGo.transform.localPosition = at + new Vector3(0f, -0.3f, 0f);

                var light = bulbGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 8f;
                light.intensity = 1.3f;
                light.color = new Color(0.97f, 0.96f, 0.92f);
                light.shadows = LightShadows.None;
            }
        }

        static void TeleportPad(Transform parent, Palette palette, string name, Vector3 position)
        {
            var pad = Decor(PrimitiveType.Cube, parent, name, position + new Vector3(0f, 0.01f, 0f),
                new Vector3(0.8f, 0.02f, 0.8f), palette.accent);

            var collider = pad.AddComponent<BoxCollider>();
            collider.size = new Vector3(1f, 1.2f, 1f);

            var anchorPoint = new GameObject("Anchor").transform;
            anchorPoint.SetParent(pad.transform, false);
            anchorPoint.position = position;

            var anchor = pad.AddComponent<TeleportationAnchor>();
            anchor.teleportAnchorTransform = anchorPoint;
            anchor.matchOrientation = MatchOrientation.WorldSpaceUp;
        }
    }
}
