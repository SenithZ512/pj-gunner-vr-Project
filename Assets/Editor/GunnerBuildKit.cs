using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace GunnerVR.EditorTools
{
    /// <summary>
    /// Shared bits the scene builders lean on: primitive helpers, the material palette and
    /// the one table that says how big each weapon part is. Keeping the sizes here means a
    /// part and the socket ghost that previews it can never drift apart.
    /// </summary>
    public static class GunnerBuildKit
    {
        public const string materialFolder = "Assets/Materials/GunnerVR";

        /// <summary>
        /// Metres per text unit. TextMeshPro's SDF sampling breaks down below a font size of
        /// roughly 1, so labels are authored at comfortable sizes and the object is scaled
        /// down to metres instead of setting a fractional font size.
        /// </summary>
        public const float textUnit = 0.002f;

        /// <summary>
        /// Size, label and resting pose of one module, used for the loose piece in the tray,
        /// the ghost in its socket and the matching chunk of the finished prop. One table so
        /// those three can never drift apart.
        ///
        /// Offsets lay the prop out side-on with the barrel along +X, which is how the rig
        /// faces the player. <see cref="tilt"/> is the pose a seated module snaps to.
        /// </summary>
        public readonly struct PartSpec
        {
            public readonly PartKind kind;
            public readonly Vector3 size;
            public readonly Vector3 socketOffset;
            public readonly Vector3 tilt;
            public readonly string label;

            public PartSpec(PartKind kind, Vector3 size, Vector3 socketOffset, Vector3 tilt, string label)
            {
                this.kind = kind;
                this.size = size;
                this.socketOffset = socketOffset;
                this.tilt = tilt;
                this.label = label;
            }
        }

        public static readonly PartSpec[] partSpecs =
        {
            new PartSpec(PartKind.Frame, new Vector3(0.19f, 0.05f, 0.035f),
                new Vector3(0f, 0f, 0f), Vector3.zero, "FRAME"),
            new PartSpec(PartKind.Slide, new Vector3(0.21f, 0.046f, 0.04f),
                new Vector3(0.015f, 0.048f, 0f), Vector3.zero, "SLIDE"),
            new PartSpec(PartKind.BarrelModule, new Vector3(0.075f, 0.026f, 0.026f),
                new Vector3(0.145f, 0.048f, 0f), Vector3.zero, "BARREL MODULE"),
            new PartSpec(PartKind.Grip, new Vector3(0.05f, 0.14f, 0.034f),
                new Vector3(-0.055f, -0.085f, 0f), new Vector3(0f, 0f, -12f), "GRIP"),
            new PartSpec(PartKind.SightModule, new Vector3(0.03f, 0.018f, 0.03f),
                new Vector3(0.085f, 0.082f, 0f), Vector3.zero, "SIGHT MODULE"),
            new PartSpec(PartKind.Magazine, new Vector3(0.038f, 0.065f, 0.028f),
                new Vector3(-0.072f, -0.168f, 0f), new Vector3(0f, 0f, -12f), "MAGAZINE PROP"),
        };

        /// <summary>Where the detachable magazine sits on the pistol, relative to its root.</summary>
        public static readonly Vector3 magazineWellPosition = new Vector3(0f, -0.168f, -0.072f);

        public static readonly Vector3 magazineWellTilt = new Vector3(12f, 0f, 0f);

        /// <summary>Size of the detachable magazine prop.</summary>
        public static readonly Vector3 magazineSize = new Vector3(0.028f, 0.065f, 0.038f);

        /// <summary>
        /// Mount points on the prop root, in the prop's own space with the barrel down +Z.
        /// Both scene builders read these, so a new model only has to be measured once.
        /// </summary>
        public static readonly Vector3 muzzleLocalPosition = new Vector3(0f, 0.048f, 0.19f);

        /// <summary>Where the holding hand ends up: the grab interactable's attach transform.</summary>
        public static readonly Vector3 gripAttachLocalPosition = new Vector3(0f, -0.085f, -0.055f);

        /// <summary>The one collider that stands in for the whole prop while it is grabbed.</summary>
        public static readonly Vector3 bodyColliderCentre = new Vector3(0f, -0.05f, 0.01f);

        public static readonly Vector3 bodyColliderSize = new Vector3(0.06f, 0.30f, 0.42f);

        // -------------------------------------------------------- pistol model

        /// <summary>
        /// Prefab or imported model the pistol's looks come from. Empty keeps the built-in
        /// primitive pistol, so the scenes still build in a project with no art in it yet.
        ///
        /// The VR template ships one to try the path with:
        /// "Assets/VRTemplateAssets/Prefabs/Interactables/Blaster Variant.prefab".
        /// </summary>
        public const string pistolModelPath = "";

        /// <summary>Shifts the art inside the prop, for a model whose origin is not the frame.</summary>
        public static readonly Vector3 pistolModelOffset = Vector3.zero;

        /// <summary>
        /// Rotation that brings the model's muzzle round to +Z. A model built barrel-down-X
        /// wants (0, 90, 0); one exported from a Z-up tool usually wants (-90, 0, 0).
        /// </summary>
        public static readonly Vector3 pistolModelTilt = Vector3.zero;

        /// <summary>Metres per model unit. The prop wants to come out roughly 0.3 m long.</summary>
        public const float pistolModelScale = 1f;

        /// <summary>
        /// Node inside the model that holds the magazine. It is switched off where the magazine
        /// is a separate grabbable; no node by this name simply means nothing is hidden.
        /// </summary>
        public const string pistolModelMagazineNode = "Magazine";

        /// <summary>
        /// Builds the visible pistol prop under <paramref name="parent"/>, pointing down +Z so
        /// the muzzle direction and the recoil slide need no extra maths. Leave
        /// <paramref name="withMagazine"/> off where the magazine is a separate grabbable.
        /// </summary>
        public static void BuildPistolModel(Transform parent, Palette palette, bool withMagazine)
        {
            if (TryBuildModelFromAsset(parent, withMagazine))
                return;

            Box(parent, "Frame", Vector3.zero, new Vector3(0.035f, 0.05f, 0.19f), palette.partFrame);
            Box(parent, "Slide", new Vector3(0f, 0.048f, 0.015f), new Vector3(0.04f, 0.046f, 0.21f),
                palette.partSlide);
            Box(parent, "Barrel Module", new Vector3(0f, 0.048f, 0.145f), new Vector3(0.026f, 0.026f, 0.075f),
                palette.partBarrelModule);
            Box(parent, "Sight Module", new Vector3(0f, 0.082f, 0.085f), new Vector3(0.03f, 0.018f, 0.03f),
                palette.partSight);
            Box(parent, "Grip", new Vector3(0f, -0.085f, -0.055f), new Vector3(0.034f, 0.14f, 0.05f),
                palette.partGrip, magazineWellTilt);

            if (withMagazine)
                Box(parent, "Magazine Prop", magazineWellPosition, magazineSize, palette.partMagazine,
                    magazineWellTilt);
        }

        /// <summary>
        /// Puts <see cref="pistolModelPath"/> under <paramref name="parent"/> and strips it back
        /// to pure looks, or reports false so the caller falls back to the primitive pistol.
        ///
        /// A plain copy rather than a prefab link: the art lands inside a generated root that
        /// the next rebuild throws away, and a component removed from a prefab instance would
        /// linger as an override instead of simply being gone.
        /// </summary>
        static bool TryBuildModelFromAsset(Transform parent, bool withMagazine)
        {
            if (string.IsNullOrEmpty(pistolModelPath))
                return false;

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(pistolModelPath);
            if (asset == null)
            {
                Debug.LogWarning($"[Gunner VR] No model at '{pistolModelPath}'; " +
                                 "building the primitive pistol instead.");
                return false;
            }

            var art = Object.Instantiate(asset, parent);
            art.name = "Art";
            art.transform.localPosition = pistolModelOffset;
            art.transform.localRotation = Quaternion.Euler(pistolModelTilt);
            art.transform.localScale = Vector3.one * pistolModelScale;

            // An art prefab arrives with its own grab and weapon behaviours, which would fight
            // the ones on the prop root. Scripts go first: a collider or body pulled out from
            // under a RequireComponent script logs an error instead of going away.
            foreach (var behaviour in art.GetComponentsInChildren<MonoBehaviour>(true))
                Object.DestroyImmediate(behaviour);

            foreach (var body in art.GetComponentsInChildren<Rigidbody>(true))
                Object.DestroyImmediate(body);

            // Only the prop root may carry a collider, or a grab catches the art instead.
            foreach (var collider in art.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            if (!withMagazine)
            {
                var magazine = FindDescendant(art.transform, pistolModelMagazineNode);
                if (magazine != null)
                    magazine.gameObject.SetActive(false);
            }

            return true;
        }

        /// <summary>Depth-first search by name that also sees deactivated nodes.</summary>
        static Transform FindDescendant(Transform root, string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            foreach (var node in root.GetComponentsInChildren<Transform>(true))
            {
                if (node != root && node.name == name)
                    return node;
            }

            return null;
        }

        public const string rigPrefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.1.2/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        /// <summary>
        /// Creates the pieces a scene needs exactly one of. They are deliberately left outside
        /// any generated root, so rebuilding a room never throws away the player's rig.
        /// </summary>
        public static void EnsureSceneServices()
        {
            if (Object.FindObjectOfType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem");
                events.AddComponent<EventSystem>();
                events.AddComponent<XRUIInputModule>();
            }

            if (Object.FindObjectOfType<Light>() == null)
            {
                var sun = new GameObject("Directional Light");
                sun.transform.rotation = Quaternion.Euler(48f, 150f, 0f);

                var light = sun.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 0.7f;
                light.color = new Color(1f, 0.97f, 0.91f);
                light.shadows = LightShadows.Soft;
            }

            // Point lamps do the shaping; ambient keeps the shadowed side readable without
            // paying for more shadow casters than a 72 FPS Quest target can afford.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.38f, 0.40f, 0.45f);
            RenderSettings.ambientEquatorColor = new Color(0.28f, 0.28f, 0.31f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.16f, 0.18f);
        }

        /// <summary>Finds the player rig in the open scene, or drops the Starter Assets one in.</summary>
        public static XROrigin EnsureRig()
        {
            var existing = Object.FindObjectOfType<XROrigin>();
            if (existing != null)
                return existing;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rigPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[Gunner VR] XR Origin prefab not found at '{rigPrefabPath}'. " +
                                 "Import the XR Interaction Toolkit Starter Assets, then build again.");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "XR Origin (XR Rig)";
            return instance.GetComponent<XROrigin>();
        }

        /// <summary>Puts a scene at the front of Build Settings, adding it if it is not listed.</summary>
        public static void RegisterSceneFirst(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == scenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Makes sure a scene is listed in Build Settings without changing the order.</summary>
        public static void EnsureSceneListed(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == scenePath))
                return;

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>
        /// Adds the magazine well to an assembled pistol, placed exactly where the magazine
        /// module sits on the finished prop, and switches the weapon over to magazine-fed.
        /// </summary>
        public static MagazineSocket AddMagazineWell(GameObject weaponRoot, WeaponController weapon)
        {
            var well = new GameObject("Magazine Well");
            well.transform.SetParent(weaponRoot.transform, false);
            well.transform.localPosition = magazineWellPosition;
            well.transform.localRotation = Quaternion.Euler(magazineWellTilt);

            var trigger = well.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.07f;

            var socket = well.AddComponent<MagazineSocket>();
            socket.Bind(weapon);

            SetField(weapon, "m_UsesDetachableMagazine", true);
            return socket;
        }

        /// <summary>A small rack of loose magazines on a bench, ready to be picked up and seated.</summary>
        public static void BuildSpareMagazines(Transform parent, Palette palette, Vector3 position,
            Quaternion labelRotation, int count = 3)
        {
            var holder = new GameObject("Spare Magazines").transform;
            holder.SetParent(parent, false);

            Decor(PrimitiveType.Cube, holder, "Tray", position + new Vector3(0f, -0.03f, 0f),
                new Vector3(0.14f * count + 0.02f, 0.02f, 0.24f), palette.metalDark);

            var first = -0.07f * (count - 1);
            for (var i = 0; i < count; i++)
            {
                var slot = position + new Vector3(first + i * 0.14f, 0.03f, 0f);
                var go = Box(holder, $"Magazine {i + 1}", slot, magazineSize, palette.partMagazine);

                var body = go.AddComponent<Rigidbody>();
                body.mass = 0.3f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.drag = 0.6f;
                body.angularDrag = 2f;

                go.AddComponent<XRGrabInteractable>();
                go.AddComponent<WeaponMagazine>();
            }

            Label3D(holder, "Label", position + new Vector3(0f, -0.018f, -0.17f), new Vector2(0.5f, 0.07f),
                0.042f, "MAGAZINES", new Color(0.82f, 0.84f, 0.88f), TextAlignmentOptions.Center, labelRotation);
        }

        // ----------------------------------------------------------- primitives

        public static GameObject Box(Transform parent, string name, Vector3 localPosition, Vector3 size,
            Material material, Vector3? euler = null)
        {
            return Primitive(PrimitiveType.Cube, parent, name, localPosition, size, material, euler);
        }

        public static GameObject Cylinder(Transform parent, string name, Vector3 localPosition, Vector3 size,
            Material material, Vector3? euler = null)
        {
            return Primitive(PrimitiveType.Cylinder, parent, name, localPosition, size, material, euler);
        }

        public static GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 localPosition,
            Vector3 size, Material material, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;

            if (euler.HasValue)
                go.transform.localRotation = Quaternion.Euler(euler.Value);

            if (material != null)
                go.GetComponent<MeshRenderer>().sharedMaterial = material;

            return go;
        }

        /// <summary>A primitive with no collider, for pure decoration.</summary>
        public static GameObject Decor(PrimitiveType type, Transform parent, string name, Vector3 localPosition,
            Vector3 size, Material material, Vector3? euler = null)
        {
            var go = Primitive(type, parent, name, localPosition, size, material, euler);
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);

            return go;
        }

        /// <summary>
        /// A flat world-space label, handy for signage that does not need a canvas.
        /// <paramref name="sizeMetres"/> and <paramref name="capHeightMetres"/> are both real
        /// metres; the scaling into text units is handled here.
        /// </summary>
        public static TextMeshPro Label3D(Transform parent, string name, Vector3 localPosition, Vector2 sizeMetres,
            float capHeightMetres, string text, Color colour,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center, Quaternion? rotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.transform.localScale = Vector3.one * textUnit;

            var label = go.AddComponent<TextMeshPro>();
            label.text = text;
            label.fontSize = capHeightMetres / textUnit;
            label.color = colour;
            label.alignment = alignment;
            label.enableWordWrapping = true;

            var rect = label.rectTransform;
            rect.sizeDelta = sizeMetres / textUnit;

            if (TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogWarning("[Gunner VR] No default TMP font asset. " +
                                 "Run Window > TextMeshPro > Import TMP Essential Resources and build again.");
            }

            return label;
        }

        // -------------------------------------------------------------- fields

        /// <summary>
        /// Writes a private serialized field through SerializedObject, so the builder can set
        /// inspector-only values without widening the runtime API.
        /// </summary>
        public static void SetField(Object target, string fieldName, object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning($"[Gunner VR] No field '{fieldName}' on {target.GetType().Name}.");
                return;
            }

            switch (value)
            {
                case PartKind kind:
                    property.enumValueIndex = (int)kind;
                    break;
                case int number:
                    property.intValue = number;
                    break;
                case float number:
                    property.floatValue = number;
                    break;
                case bool flag:
                    property.boolValue = flag;
                    break;
                case Object reference:
                    property.objectReferenceValue = reference;
                    break;
                default:
                    Debug.LogWarning($"[Gunner VR] Unhandled type for field '{fieldName}'.");
                    return;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------- palette

        /// <summary>
        /// Materials for the generated scenes, saved as assets so colours survive a rebuild
        /// and can be tweaked by hand afterwards.
        /// </summary>
        public class Palette
        {
            readonly Dictionary<string, Material> m_Cache = new Dictionary<string, Material>();
            readonly Shader m_Lit;

            public Palette(Shader lit)
            {
                m_Lit = lit;
                EnsureFolder();
            }

            public Material concrete => Get("Concrete", new Color(0.33f, 0.33f, 0.35f), 0f, 0.1f);
            public Material concreteDark => Get("ConcreteDark", new Color(0.22f, 0.22f, 0.24f), 0f, 0.1f);
            public Material wallUpper => Get("WallUpper", new Color(0.62f, 0.63f, 0.64f), 0f, 0.05f);
            public Material wallLower => Get("WallLower", new Color(0.27f, 0.35f, 0.42f), 0f, 0.15f);
            public Material ceiling => Get("Ceiling", new Color(0.46f, 0.47f, 0.49f), 0f, 0.05f);
            public Material beam => Get("Beam", new Color(0.30f, 0.31f, 0.33f), 0.5f, 0.3f);
            public Material hazard => Get("Hazard", new Color(0.86f, 0.70f, 0.12f), 0f, 0.2f);
            public Material rubberMat => Get("RubberMat", new Color(0.14f, 0.15f, 0.17f), 0f, 0.05f);

            public Material wood => Get("Wood", new Color(0.44f, 0.31f, 0.19f), 0f, 0.2f);
            public Material woodDark => Get("WoodDark", new Color(0.30f, 0.21f, 0.13f), 0f, 0.2f);
            public Material metal => Get("Metal", new Color(0.36f, 0.37f, 0.39f), 0.8f, 0.6f);
            public Material metalDark => Get("MetalDark", new Color(0.19f, 0.20f, 0.22f), 0.8f, 0.5f);
            public Material pegboard => Get("Pegboard", new Color(0.52f, 0.37f, 0.23f), 0f, 0.1f);
            public Material crate => Get("Crate", new Color(0.50f, 0.37f, 0.22f), 0f, 0.15f);
            public Material barrel => Get("Barrel", new Color(0.24f, 0.42f, 0.30f), 0.3f, 0.4f);
            public Material toolbox => Get("Toolbox", new Color(0.72f, 0.18f, 0.14f), 0.2f, 0.45f);

            public Material accent => Get("Accent", new Color(0.92f, 0.46f, 0.10f), 0f, 0.4f);
            public Material buttonRed => Get("ButtonRed", new Color(0.82f, 0.13f, 0.12f), 0f, 0.5f);
            public Material signBoard => Get("SignBoard", new Color(0.08f, 0.09f, 0.12f), 0f, 0.15f);
            public Material lampHousing => Get("LampHousing", new Color(0.18f, 0.19f, 0.21f), 0.7f, 0.4f);
            public Material lampGlow => GetEmissive("LampGlow", new Color(0.98f, 0.95f, 0.85f), 2.2f);
            public Material socketGhost => GetTransparent("SocketGhost", new Color(0.25f, 0.72f, 1f, 0.24f));

            /// <summary>What a ghost turns into while the matching module is held nearby.</summary>
            public Material socketReady => GetTransparent("SocketReady", new Color(0.35f, 1f, 0.55f, 0.55f));

            /// <summary>What a module turns into while it is lined up with its socket.</summary>
            public Material partHighlight => GetEmissive("PartHighlight", new Color(0.40f, 1f, 0.58f), 0.9f);

            public Material socketPlate => Get("SocketPlate", new Color(0.13f, 0.14f, 0.16f), 0.4f, 0.3f);
            public Material lampComplete => GetEmissive("LampComplete", new Color(0.32f, 0.95f, 0.45f), 1.4f);
            public Material muzzleFlash => GetEmissive("MuzzleFlash", new Color(1f, 0.78f, 0.35f), 4f);
            public Material hitMarker => GetEmissive("HitMarker", new Color(1f, 0.95f, 0.5f), 3f);

            public Material paper => Get("Paper", new Color(0.94f, 0.93f, 0.88f), 0f, 0.05f);
            public Material ringOuter => Get("RingOuter", new Color(0.80f, 0.82f, 0.85f), 0f, 0.05f);
            public Material ringMid => Get("RingMid", new Color(0.30f, 0.45f, 0.75f), 0f, 0.05f);
            public Material ringInner => Get("RingInner", new Color(0.85f, 0.25f, 0.25f), 0f, 0.05f);
            public Material ringBull => Get("RingBull", new Color(0.95f, 0.80f, 0.15f), 0f, 0.1f);
            public Material targetFlash => GetEmissive("TargetFlash", new Color(1f, 0.58f, 0.22f), 2.2f);
            public Material booth => Get("Booth", new Color(0.23f, 0.25f, 0.28f), 0.3f, 0.3f);
            public Material laneMark => Get("LaneMark", new Color(0.88f, 0.88f, 0.9f), 0f, 0.1f);

            public Material partFrame => Get("PartFrame", new Color(0.31f, 0.33f, 0.37f), 0.7f, 0.5f);
            public Material partSlide => Get("PartSlide", new Color(0.20f, 0.21f, 0.24f), 0.9f, 0.7f);
            public Material partBarrelModule => Get("PartBarrelModule", new Color(0.16f, 0.17f, 0.19f), 0.9f, 0.75f);
            public Material partGrip => Get("PartGrip", new Color(0.37f, 0.26f, 0.16f), 0f, 0.25f);
            public Material partSight => Get("PartSight", new Color(0.55f, 0.57f, 0.60f), 0.6f, 0.6f);
            public Material partMagazine => Get("PartMagazine", new Color(0.39f, 0.41f, 0.45f), 0.6f, 0.45f);
            // Named apart from the older flat "Tracer" material so the emissive version is not
            // shadowed by one already sitting in the folder.
            public Material tracer => GetEmissive("TracerGlow", new Color(1f, 0.85f, 0.35f), 1.6f);

            public Material ForPart(PartKind kind)
            {
                switch (kind)
                {
                    case PartKind.Frame: return partFrame;
                    case PartKind.Slide: return partSlide;
                    case PartKind.BarrelModule: return partBarrelModule;
                    case PartKind.Grip: return partGrip;
                    case PartKind.SightModule: return partSight;
                    default: return partMagazine;
                }
            }

            static void EnsureFolder()
            {
                if (AssetDatabase.IsValidFolder(materialFolder))
                    return;

                if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                    AssetDatabase.CreateFolder("Assets", "Materials");

                AssetDatabase.CreateFolder("Assets/Materials", "GunnerVR");
            }

            Material Get(string name, Color colour, float metallic, float smoothness)
            {
                if (m_Cache.TryGetValue(name, out var cached))
                    return cached;

                var path = $"{materialFolder}/{name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null)
                {
                    material = new Material(m_Lit) { color = colour };
                    material.SetFloat("_Metallic", metallic);
                    material.SetFloat("_Smoothness", smoothness);
                    AssetDatabase.CreateAsset(material, path);
                }

                m_Cache[name] = material;
                return material;
            }

            Material GetEmissive(string name, Color colour, float intensity)
            {
                if (m_Cache.TryGetValue(name, out var cached))
                    return cached;

                var path = $"{materialFolder}/{name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null)
                {
                    material = new Material(m_Lit) { color = colour };
                    material.SetFloat("_Smoothness", 0.4f);
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    material.SetColor("_EmissionColor", colour * intensity);
                    AssetDatabase.CreateAsset(material, path);
                }

                m_Cache[name] = material;
                return material;
            }

            Material GetTransparent(string name, Color colour)
            {
                if (m_Cache.TryGetValue(name, out var cached))
                    return cached;

                var path = $"{materialFolder}/{name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material == null)
                {
                    material = new Material(m_Lit) { color = colour };

                    // URP needs the surface type flipped, not just a low alpha value.
                    material.SetFloat("_Surface", 1f);
                    material.SetFloat("_Blend", 0f);
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_ZWrite", 0);
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                    AssetDatabase.CreateAsset(material, path);
                }

                m_Cache[name] = material;
                return material;
            }
        }
    }
}
