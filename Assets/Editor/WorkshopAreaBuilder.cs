using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using static GunnerVR.EditorTools.GunnerBuildKit;

namespace GunnerVR.EditorTools
{
    /// <summary>
    /// Builds the workshop half of the single combined scene: bench, parts tray, grabbable
    /// modules, marked assembly sockets, progress UI, reset button and the how-to sign,
    /// dressed so it reads as a room someone works in.
    ///
    /// Everything is placed in local space under one area root, so the whole workshop can be
    /// dropped anywhere in the world just by moving that root. The player rig, the lighting
    /// services and the scene plumbing are not this class's business — MainSceneBuilder owns
    /// one of each for the whole scene.
    /// </summary>
    public static class WorkshopAreaBuilder
    {
        public const string areaName = "WorkshopArea";

        const float k_HalfWidth = 4.5f;
        const float k_MinZ = -4f;
        const float k_MaxZ = 5f;
        const float k_CeilingY = 3.2f;
        const float k_WainscotY = 1.1f;

        const float k_BenchTop = 0.95f;
        const float k_BenchX = -1.4f;
        const float k_RigX = 1.9f;
        const float k_FurnitureZ = 2.2f;

        /// <summary>Half-width of the opening in the near wall that the hallway joins.</summary>
        public const float doorwayHalfWidth = 1.1f;

        /// <summary>Where the near wall sits, in area-local space. The hallway starts here.</summary>
        public const float doorwayZ = k_MinZ;

        static readonly Vector3 k_SpawnPosition = new Vector3(0f, 0f, -2.5f);

        // Text is only readable from the side its local +Z points at, so every label states
        // which way it is turned rather than relying on a default.
        static readonly Quaternion k_FacesPlayer = Quaternion.LookRotation(Vector3.back, Vector3.up);
        static readonly Quaternion k_FacesIntoRoom = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        static readonly Quaternion k_LiesFlat = Quaternion.LookRotation(Vector3.up, Vector3.forward);

        /// <summary>
        /// Builds the whole workshop under a new area root parented to <paramref name="parent"/>.
        /// Returns the manager so the caller can hand it to the shared systems.
        /// </summary>
        public static WorkshopManager BuildArea(Transform parent, Vector3 origin, Palette palette,
            out WeaponController weapon, out Vector3 spawnPosition)
        {
            var root = new GameObject(areaName).transform;
            root.SetParent(parent, false);
            root.localPosition = origin;

            BuildShell(root, palette);
            BuildLighting(root, palette);
            BuildDressing(root, palette);

            var parts = BuildBench(root, palette, out var resetButton);
            var station = BuildAssemblyRig(root, palette, out _);
            var hitMarker = BuildHitMarker(root, palette);
            weapon = BuildWeapon(root, palette, hitMarker, out var rack);

            // Loose magazines on the front edge of the rig stand, within reach of the prop.
            BuildSpareMagazines(root, palette,
                new Vector3(k_RigX, k_BenchTop + 0.05f, k_FurnitureZ - 0.3f), k_LiesFlat);

            BuildProgressPanel(root, palette, out var statusLabel, out var checklistLabel, out var completeBanner);
            BuildHowToSign(root, palette);

            var systems = new GameObject("WorkshopSystems");
            systems.transform.SetParent(parent, false);
            systems.AddComponent<AudioSource>();

            var manager = systems.AddComponent<WorkshopManager>();
            manager.Bind(station, parts, weapon, rack, null, statusLabel, checklistLabel, completeBanner);
            resetButton.Bind(manager);

            spawnPosition = origin + k_SpawnPosition;
            return manager;
        }

        // --------------------------------------------------------------- shell

        static void BuildShell(Transform root, Palette palette)
        {
            var shell = new GameObject("Room").transform;
            shell.SetParent(root, false);

            var width = k_HalfWidth * 2f;
            var depth = k_MaxZ - k_MinZ;
            var centreZ = (k_MinZ + k_MaxZ) * 0.5f;

            var floor = Box(shell, "Floor", new Vector3(0f, -0.05f, centreZ),
                new Vector3(width, 0.1f, depth), palette.concrete);
            var teleport = floor.AddComponent<TeleportationArea>();
            teleport.matchOrientation = MatchOrientation.WorldSpaceUp;

            Decor(PrimitiveType.Cube, shell, "Ceiling", new Vector3(0f, k_CeilingY, centreZ),
                new Vector3(width, 0.1f, depth), palette.ceiling);

            // Walls in two bands so the room is not one flat colour.
            WallPair(shell, palette, width, depth, centreZ);

            // Painted work zone, so the eye knows where the job happens.
            HazardBorder(shell, palette, new Vector3(0.2f, 0.002f, k_FurnitureZ - 0.1f), new Vector2(7.4f, 2.6f));

            Decor(PrimitiveType.Cube, shell, "Work Mat", new Vector3(k_BenchX, 0.004f, k_FurnitureZ - 0.75f),
                new Vector3(3.2f, 0.01f, 1.2f), palette.rubberMat);

            BuildDoorway(shell, palette);
        }

        static void WallPair(Transform shell, Palette palette, float width, float depth, float centreZ)
        {
            var upperHeight = k_CeilingY - k_WainscotY;
            var upperY = k_WainscotY + upperHeight * 0.5f;
            var lowerY = k_WainscotY * 0.5f;

            var sides = new (string name, Vector3 centre, Vector3 lower, Vector3 upper)[]
            {
                ("Wall Left", new Vector3(-k_HalfWidth, 0f, centreZ),
                    new Vector3(0.1f, k_WainscotY, depth), new Vector3(0.1f, upperHeight, depth)),
                ("Wall Right", new Vector3(k_HalfWidth, 0f, centreZ),
                    new Vector3(0.1f, k_WainscotY, depth), new Vector3(0.1f, upperHeight, depth)),
                ("Wall Far", new Vector3(0f, 0f, k_MaxZ),
                    new Vector3(width, k_WainscotY, 0.1f), new Vector3(width, upperHeight, 0.1f)),
            };

            foreach (var side in sides)
            {
                Box(shell, $"{side.name} Lower", side.centre + Vector3.up * lowerY, side.lower, palette.wallLower);
                Box(shell, $"{side.name} Upper", side.centre + Vector3.up * upperY, side.upper, palette.wallUpper);

                var trimSize = side.lower.x > side.lower.z
                    ? new Vector3(side.lower.x, 0.06f, 0.14f)
                    : new Vector3(0.14f, 0.06f, side.lower.z);
                Decor(PrimitiveType.Cube, shell, $"{side.name} Trim",
                    side.centre + Vector3.up * k_WainscotY, trimSize, palette.beam);
            }

            // The near wall is built in two pieces with a real gap, because the hallway has to
            // pass through it. A decorative frame over a solid wall would stop the player dead.
            var pierWidth = (width - doorwayHalfWidth * 2f) * 0.5f;
            var pierOffset = doorwayHalfWidth + pierWidth * 0.5f;

            foreach (var sign in new[] { -1f, 1f })
            {
                var x = sign * pierOffset;
                Box(shell, $"Wall Near {(sign < 0 ? "Left" : "Right")} Lower",
                    new Vector3(x, lowerY, k_MinZ), new Vector3(pierWidth, k_WainscotY, 0.1f), palette.wallLower);
                Box(shell, $"Wall Near {(sign < 0 ? "Left" : "Right")} Upper",
                    new Vector3(x, upperY, k_MinZ), new Vector3(pierWidth, upperHeight, 0.1f), palette.wallUpper);
                Decor(PrimitiveType.Cube, shell, $"Wall Near {(sign < 0 ? "Left" : "Right")} Trim",
                    new Vector3(x, k_WainscotY, k_MinZ), new Vector3(pierWidth, 0.06f, 0.14f), palette.beam);
            }

            // Lintel over the opening.
            Box(shell, "Wall Near Lintel", new Vector3(0f, k_CeilingY - 0.45f, k_MinZ),
                new Vector3(doorwayHalfWidth * 2f, 0.9f, 0.1f), palette.wallUpper);
        }

        static void HazardBorder(Transform shell, Palette palette, Vector3 centre, Vector2 size)
        {
            var half = size * 0.5f;
            const float stripe = 0.12f;

            Decor(PrimitiveType.Cube, shell, "Hazard Front", centre + new Vector3(0f, 0f, -half.y),
                new Vector3(size.x, 0.01f, stripe), palette.hazard);
            Decor(PrimitiveType.Cube, shell, "Hazard Back", centre + new Vector3(0f, 0f, half.y),
                new Vector3(size.x, 0.01f, stripe), palette.hazard);
            Decor(PrimitiveType.Cube, shell, "Hazard Left", centre + new Vector3(-half.x, 0f, 0f),
                new Vector3(stripe, 0.01f, size.y), palette.hazard);
            Decor(PrimitiveType.Cube, shell, "Hazard Right", centre + new Vector3(half.x, 0f, 0f),
                new Vector3(stripe, 0.01f, size.y), palette.hazard);
        }

        /// <summary>
        /// Dresses the opening the hallway runs through. There is no trigger here any more —
        /// the player simply walks out, which is what the single-scene brief asks for.
        /// </summary>
        static void BuildDoorway(Transform shell, Palette palette)
        {
            var z = k_MinZ;
            var doorway = new GameObject("Doorway To Hallway").transform;
            doorway.SetParent(shell, false);

            Decor(PrimitiveType.Cube, doorway, "Frame Left", new Vector3(-doorwayHalfWidth, 1.15f, z),
                new Vector3(0.14f, 2.3f, 0.26f), palette.beam);
            Decor(PrimitiveType.Cube, doorway, "Frame Right", new Vector3(doorwayHalfWidth, 1.15f, z),
                new Vector3(0.14f, 2.3f, 0.26f), palette.beam);
            Decor(PrimitiveType.Cube, doorway, "Frame Top", new Vector3(0f, 2.33f, z),
                new Vector3(doorwayHalfWidth * 2f + 0.14f, 0.16f, 0.26f), palette.beam);

            Label3D(doorway, "Sign", new Vector3(0f, 2.62f, z + 0.16f), new Vector2(3f, 0.4f), 0.24f,
                "SHOOTING RANGE  →", palette.hazard.color, TextAlignmentOptions.Center, k_FacesIntoRoom);
        }

        // ------------------------------------------------------------ lighting

        static void BuildLighting(Transform root, Palette palette)
        {
            var lights = new GameObject("Lamps").transform;
            lights.SetParent(root, false);

            var spots = new[]
            {
                new Vector3(-1.9f, 0f, k_FurnitureZ),
                new Vector3(1.9f, 0f, k_FurnitureZ),
                new Vector3(0f, 0f, -1.4f),
            };

            for (var i = 0; i < spots.Length; i++)
            {
                var lamp = new GameObject($"Lamp {i + 1}").transform;
                lamp.SetParent(lights, false);
                lamp.localPosition = spots[i] + Vector3.up * (k_CeilingY - 0.18f);

                Decor(PrimitiveType.Cube, lamp, "Housing", Vector3.zero,
                    new Vector3(1.3f, 0.12f, 0.3f), palette.lampHousing);
                Decor(PrimitiveType.Cube, lamp, "Tube", new Vector3(0f, -0.08f, 0f),
                    new Vector3(1.16f, 0.06f, 0.18f), palette.lampGlow);

                // Short chains, so the fittings do not look glued to the slab.
                Decor(PrimitiveType.Cylinder, lamp, "Rod L", new Vector3(-0.45f, 0.14f, 0f),
                    new Vector3(0.02f, 0.14f, 0.02f), palette.metalDark);
                Decor(PrimitiveType.Cylinder, lamp, "Rod R", new Vector3(0.45f, 0.14f, 0f),
                    new Vector3(0.02f, 0.14f, 0.02f), palette.metalDark);

                var bulb = new GameObject("Light");
                bulb.transform.SetParent(lamp, false);
                bulb.transform.localPosition = new Vector3(0f, -0.25f, 0f);

                var light = bulb.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 9f;
                light.intensity = 1.5f;
                light.color = new Color(1f, 0.96f, 0.88f);
                // No extra shadow casters: the 72 FPS target on Quest does not have room for them.
                light.shadows = LightShadows.None;
            }

            // Ceiling beams tie the fittings into the structure.
            for (var i = 0; i < 3; i++)
            {
                var z = k_MinZ + 1.6f + i * 2.8f;
                Decor(PrimitiveType.Cube, lights, $"Beam {i + 1}", new Vector3(0f, k_CeilingY - 0.14f, z),
                    new Vector3(k_HalfWidth * 2f, 0.18f, 0.22f), palette.beam);
            }
        }

        // ------------------------------------------------------------ dressing

        static void BuildDressing(Transform root, Palette palette)
        {
            var dressing = new GameObject("Dressing").transform;
            dressing.SetParent(root, false);

            BuildPegboard(dressing, palette);
            BuildShelving(dressing, palette);

            // Crates stacked in the far left corner.
            var crates = new GameObject("Crates").transform;
            crates.SetParent(dressing, false);
            Box(crates, "Crate A", new Vector3(-3.7f, 0.3f, 4.2f), new Vector3(0.6f, 0.6f, 0.6f), palette.crate);
            Box(crates, "Crate B", new Vector3(-3.1f, 0.25f, 4.4f), new Vector3(0.5f, 0.5f, 0.5f), palette.crate);
            Box(crates, "Crate C", new Vector3(-3.7f, 0.85f, 4.25f), new Vector3(0.52f, 0.5f, 0.52f), palette.woodDark,
                new Vector3(0f, 14f, 0f));

            // Barrels by the far right corner.
            Cylinder(dressing, "Barrel A", new Vector3(3.8f, 0.45f, 4.3f), new Vector3(0.55f, 0.45f, 0.55f),
                palette.barrel);
            Cylinder(dressing, "Barrel B", new Vector3(3.2f, 0.45f, 4.5f), new Vector3(0.55f, 0.45f, 0.55f),
                palette.barrel);

            // Rolling toolbox tucked beside the bench.
            var toolbox = new GameObject("Toolbox").transform;
            toolbox.SetParent(dressing, false);
            Box(toolbox, "Body", new Vector3(-3.5f, 0.42f, 2.2f), new Vector3(0.7f, 0.84f, 0.5f), palette.toolbox);
            Decor(PrimitiveType.Cube, toolbox, "Drawer 1", new Vector3(-3.5f, 0.62f, 1.94f),
                new Vector3(0.62f, 0.18f, 0.03f), palette.metalDark);
            Decor(PrimitiveType.Cube, toolbox, "Drawer 2", new Vector3(-3.5f, 0.38f, 1.94f),
                new Vector3(0.62f, 0.18f, 0.03f), palette.metalDark);
            Decor(PrimitiveType.Cube, toolbox, "Top", new Vector3(-3.5f, 0.86f, 2.2f),
                new Vector3(0.74f, 0.05f, 0.54f), palette.metal);
        }

        static void BuildPegboard(Transform parent, Palette palette)
        {
            var board = new GameObject("Pegboard").transform;
            board.SetParent(parent, false);

            var z = k_MaxZ - 0.07f;
            Decor(PrimitiveType.Cube, board, "Board", new Vector3(k_BenchX, 1.95f, z),
                new Vector3(2.6f, 1.3f, 0.05f), palette.pegboard);

            // Hanging tools, built from thin primitives so the wall is not blank.
            Decor(PrimitiveType.Cube, board, "Wrench", new Vector3(k_BenchX - 0.95f, 2.1f, z - 0.05f),
                new Vector3(0.07f, 0.52f, 0.03f), palette.metal);
            Decor(PrimitiveType.Cube, board, "Wrench Head", new Vector3(k_BenchX - 0.95f, 2.38f, z - 0.05f),
                new Vector3(0.15f, 0.12f, 0.03f), palette.metal);

            Decor(PrimitiveType.Cube, board, "Hammer Handle", new Vector3(k_BenchX - 0.45f, 2.05f, z - 0.05f),
                new Vector3(0.05f, 0.46f, 0.03f), palette.wood);
            Decor(PrimitiveType.Cube, board, "Hammer Head", new Vector3(k_BenchX - 0.45f, 2.32f, z - 0.05f),
                new Vector3(0.2f, 0.1f, 0.05f), palette.metalDark);

            Decor(PrimitiveType.Cylinder, board, "Coil", new Vector3(k_BenchX + 0.2f, 2.2f, z - 0.06f),
                new Vector3(0.34f, 0.02f, 0.34f), palette.metalDark, new Vector3(90f, 0f, 0f));

            Decor(PrimitiveType.Cube, board, "Clamp", new Vector3(k_BenchX + 0.8f, 2.18f, z - 0.05f),
                new Vector3(0.1f, 0.34f, 0.04f), palette.accent);
            Decor(PrimitiveType.Cube, board, "Ruler", new Vector3(k_BenchX + 1.1f, 2.1f, z - 0.05f),
                new Vector3(0.06f, 0.6f, 0.02f), palette.hazard);

            // Lower row of outlined hooks.
            for (var i = 0; i < 5; i++)
            {
                Decor(PrimitiveType.Cylinder, board, $"Hook {i}",
                    new Vector3(k_BenchX - 1f + i * 0.5f, 1.55f, z - 0.06f),
                    new Vector3(0.015f, 0.05f, 0.015f), palette.metal, new Vector3(90f, 0f, 0f));
            }
        }

        static void BuildShelving(Transform parent, Palette palette)
        {
            var shelf = new GameObject("Shelving").transform;
            shelf.SetParent(parent, false);

            var x = k_HalfWidth - 0.35f;
            const float z = -1.2f;

            Box(shelf, "Upright Front", new Vector3(x, 1f, z - 0.9f), new Vector3(0.08f, 2f, 0.08f), palette.metal);
            Box(shelf, "Upright Back", new Vector3(x, 1f, z + 0.9f), new Vector3(0.08f, 2f, 0.08f), palette.metal);

            for (var i = 0; i < 3; i++)
            {
                var y = 0.45f + i * 0.6f;
                Box(shelf, $"Shelf {i + 1}", new Vector3(x, y, z), new Vector3(0.6f, 0.05f, 1.9f), palette.woodDark);

                Box(shelf, $"Box {i}A", new Vector3(x, y + 0.17f, z - 0.55f),
                    new Vector3(0.4f, 0.28f, 0.4f), palette.crate);
                Box(shelf, $"Box {i}B", new Vector3(x, y + 0.14f, z + 0.35f),
                    new Vector3(0.38f, 0.22f, 0.5f), i % 2 == 0 ? palette.accent : palette.metalDark);
            }
        }

        // --------------------------------------------------------------- bench

        static List<WeaponPart> BuildBench(Transform root, Palette palette, out ResetButton resetButton)
        {
            var bench = new GameObject("Workbench").transform;
            bench.SetParent(root, false);

            const float width = 2.6f;
            const float depth = 0.9f;

            Box(bench, "Top", new Vector3(k_BenchX, k_BenchTop - 0.04f, k_FurnitureZ),
                new Vector3(width, 0.08f, depth), palette.wood);
            Decor(PrimitiveType.Cube, bench, "Top Edge", new Vector3(k_BenchX, k_BenchTop - 0.09f, k_FurnitureZ),
                new Vector3(width + 0.04f, 0.03f, depth + 0.04f), palette.woodDark);
            Box(bench, "Lower Shelf", new Vector3(k_BenchX, 0.28f, k_FurnitureZ),
                new Vector3(width - 0.2f, 0.05f, depth - 0.15f), palette.woodDark);

            Legs(bench, new Vector3(k_BenchX, 0f, k_FurnitureZ), new Vector2(width, depth), k_BenchTop, palette.metal);

            BuildPartsTray(bench, palette, out var slots);

            var parts = new List<WeaponPart>();
            for (var i = 0; i < partSpecs.Length && i < slots.Count; i++)
                parts.Add(MakePart(bench, palette, partSpecs[i], slots[i]));

            resetButton = BuildResetButton(bench, palette);
            return parts;
        }

        static void Legs(Transform parent, Vector3 centre, Vector2 footprint, float height, Material material)
        {
            var halfX = footprint.x * 0.5f - 0.1f;
            var halfZ = footprint.y * 0.5f - 0.1f;

            for (var i = 0; i < 4; i++)
            {
                var x = (i % 2 == 0) ? -halfX : halfX;
                var z = (i < 2) ? -halfZ : halfZ;
                Box(parent, $"Leg {i + 1}", centre + new Vector3(x, height * 0.5f, z),
                    new Vector3(0.07f, height, 0.07f), material);
            }
        }

        /// <summary>
        /// The tray the loose parts sit in: a rimmed pan split into one compartment per part,
        /// so the bench reads as organised rather than strewn.
        /// </summary>
        static void BuildPartsTray(Transform parent, Palette palette, out List<Vector3> slots)
        {
            var tray = new GameObject("Parts Tray").transform;
            tray.SetParent(parent, false);

            const float trayWidth = 1.9f;
            const float trayDepth = 0.56f;
            var trayZ = k_FurnitureZ - 0.1f;
            var baseY = k_BenchTop + 0.015f;

            Box(tray, "Pan", new Vector3(k_BenchX, baseY, trayZ),
                new Vector3(trayWidth, 0.03f, trayDepth), palette.metalDark);

            var rimY = baseY + 0.045f;
            Box(tray, "Rim Front", new Vector3(k_BenchX, rimY, trayZ - trayDepth * 0.5f),
                new Vector3(trayWidth, 0.07f, 0.03f), palette.metal);
            Box(tray, "Rim Back", new Vector3(k_BenchX, rimY, trayZ + trayDepth * 0.5f),
                new Vector3(trayWidth, 0.07f, 0.03f), palette.metal);
            Box(tray, "Rim Left", new Vector3(k_BenchX - trayWidth * 0.5f, rimY, trayZ),
                new Vector3(0.03f, 0.07f, trayDepth), palette.metal);
            Box(tray, "Rim Right", new Vector3(k_BenchX + trayWidth * 0.5f, rimY, trayZ),
                new Vector3(0.03f, 0.07f, trayDepth), palette.metal);

            slots = new List<Vector3>();
            var compartment = trayWidth / partSpecs.Length;

            for (var i = 0; i < partSpecs.Length; i++)
            {
                var x = k_BenchX - trayWidth * 0.5f + compartment * (i + 0.5f);
                slots.Add(new Vector3(x, baseY + 0.12f, trayZ));

                if (i > 0)
                {
                    Box(tray, $"Divider {i}", new Vector3(k_BenchX - trayWidth * 0.5f + compartment * i, rimY, trayZ),
                        new Vector3(0.02f, 0.06f, trayDepth - 0.04f), palette.metal);
                }

                Label3D(tray, $"Slot Label {i + 1}", new Vector3(x, baseY + 0.022f, trayZ - trayDepth * 0.5f + 0.08f),
                    new Vector2(compartment - 0.04f, 0.09f), 0.045f, partSpecs[i].label,
                    new Color(0.80f, 0.83f, 0.88f), TextAlignmentOptions.Center, k_LiesFlat);
            }
        }

        static WeaponPart MakePart(Transform parent, Palette palette, PartSpec spec, Vector3 position)
        {
            var go = Box(parent, $"Part - {spec.kind}", position, spec.size, palette.ForPart(spec.kind));

            var body = go.AddComponent<Rigidbody>();
            body.mass = 0.5f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            // A little damping keeps a dropped module from skating across the bench.
            body.drag = 0.6f;
            body.angularDrag = 2f;

            go.AddComponent<XRGrabInteractable>();

            var part = go.AddComponent<WeaponPart>();
            SetField(part, "m_Kind", spec.kind);
            SetField(part, "m_HighlightMaterial", palette.partHighlight);
            return part;
        }

        static ResetButton BuildResetButton(Transform parent, Palette palette)
        {
            var housing = new GameObject("Reset Assembly Button").transform;
            housing.SetParent(parent, false);

            var position = new Vector3(k_BenchX + 1.1f, k_BenchTop + 0.02f, k_FurnitureZ + 0.28f);

            Decor(PrimitiveType.Cube, housing, "Base", position, new Vector3(0.22f, 0.05f, 0.22f), palette.metalDark);

            var cap = Cylinder(housing, "Cap", position + new Vector3(0f, 0.055f, 0f),
                new Vector3(0.14f, 0.035f, 0.14f), palette.buttonRed);
            cap.AddComponent<XRSimpleInteractable>();

            Label3D(housing, "Label", position + new Vector3(0f, 0.032f, -0.19f), new Vector2(0.4f, 0.09f), 0.05f,
                "RESET", new Color(0.88f, 0.89f, 0.93f), TextAlignmentOptions.Center, k_LiesFlat);

            return cap.AddComponent<ResetButton>();
        }

        // -------------------------------------------------------- assembly rig

        static AssemblyStation BuildAssemblyRig(Transform root, Palette palette, out List<PartSocket> sockets)
        {
            var standRoot = new GameObject("Assembly Station").transform;
            standRoot.SetParent(root, false);

            const float width = 1.4f;
            const float depth = 0.9f;

            Box(standRoot, "Stand Top", new Vector3(k_RigX, k_BenchTop - 0.04f, k_FurnitureZ),
                new Vector3(width, 0.08f, depth), palette.wood);
            Decor(PrimitiveType.Cube, standRoot, "Stand Edge", new Vector3(k_RigX, k_BenchTop - 0.09f, k_FurnitureZ),
                new Vector3(width + 0.04f, 0.03f, depth + 0.04f), palette.woodDark);
            Legs(standRoot, new Vector3(k_RigX, 0f, k_FurnitureZ), new Vector2(width, depth), k_BenchTop,
                palette.metal);

            // Backing plate so the ghosts read against something solid.
            Decor(PrimitiveType.Cube, standRoot, "Backing", new Vector3(k_RigX, k_BenchTop + 0.42f, k_FurnitureZ + 0.3f),
                new Vector3(1.2f, 0.84f, 0.04f), palette.socketPlate);

            var rig = new GameObject("Rig").transform;
            rig.SetParent(standRoot, false);
            rig.localPosition = new Vector3(k_RigX, k_BenchTop + 0.5f, k_FurnitureZ);

            // The prop is only 30 cm across, so six name tags would be unreadable clutter.
            // The checklist panel does the naming; the ghosts do the pointing.
            Label3D(rig, "Rig Title", new Vector3(0f, 0.2f, -0.03f), new Vector2(0.6f, 0.07f), 0.045f,
                "ASSEMBLY RIG", new Color(0.45f, 0.82f, 1f), TextAlignmentOptions.Center, k_FacesPlayer);

            sockets = new List<PartSocket>();
            foreach (var spec in partSpecs)
                sockets.Add(MakeSocket(rig, palette, spec));

            var station = standRoot.gameObject.AddComponent<AssemblyStation>();
            station.SetSockets(sockets);
            return station;
        }

        static PartSocket MakeSocket(Transform rig, Palette palette, PartSpec spec)
        {
            var go = new GameObject($"Socket - {spec.kind}");
            go.transform.SetParent(rig, false);
            go.transform.localPosition = spec.socketOffset;
            // The socket's own pose is the pose a module snaps to, so a tilted module
            // (the grip, the magazine) gets its tilt from here.
            go.transform.localRotation = Quaternion.Euler(spec.tilt);

            // A ghost the exact size of the module it wants, so the target is unmistakable.
            var ghost = Decor(PrimitiveType.Cube, go.transform, "Ghost", Vector3.zero, spec.size,
                palette.socketGhost);

            // Corner pins read as a fixture rather than a floating block.
            var half = spec.size * 0.5f;
            var pin = Mathf.Clamp(Mathf.Min(spec.size.x, spec.size.y) * 0.25f, 0.008f, 0.016f);
            for (var i = 0; i < 4; i++)
            {
                var x = (i % 2 == 0) ? -half.x : half.x;
                var y = (i < 2) ? -half.y : half.y;
                Decor(PrimitiveType.Cube, go.transform, $"Pin {i}", new Vector3(x, y, 0f),
                    new Vector3(pin, pin, spec.size.z + 0.012f), palette.accent);
            }

            // Sized against the module rather than fixed, so the small sight module does not
            // claim as much space as the frame and steal its approach.
            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = Mathf.Clamp(spec.size.magnitude * 0.55f, 0.045f, 0.1f);

            var socket = go.AddComponent<PartSocket>();
            SetField(socket, "m_Accepts", spec.kind);
            SetField(socket, "m_Ghost", ghost.GetComponent<MeshRenderer>());
            SetField(socket, "m_GhostIdle", palette.socketGhost);
            SetField(socket, "m_GhostReady", palette.socketReady);
            return socket;
        }

        // -------------------------------------------------------------- weapon

        /// <summary>
        /// The marker that pops up where a shot lands. It lives at the scene root, not under
        /// the prop: it is placed in world space and must not ride along with the barrel, nor
        /// disappear with the prop while the build is unfinished.
        /// </summary>
        static GameObject BuildHitMarker(Transform root, Palette palette)
        {
            var marker = new GameObject("Hit Marker");
            marker.transform.SetParent(root, false);

            Decor(PrimitiveType.Cube, marker.transform, "Bar A", Vector3.zero,
                new Vector3(0.09f, 0.012f, 0.004f), palette.hitMarker, new Vector3(0f, 0f, 45f));
            Decor(PrimitiveType.Cube, marker.transform, "Bar B", Vector3.zero,
                new Vector3(0.09f, 0.012f, 0.004f), palette.hitMarker, new Vector3(0f, 0f, -45f));

            marker.SetActive(false);
            return marker;
        }

        static WeaponController BuildWeapon(Transform root, Palette palette, GameObject hitMarker,
            out Transform rack)
        {
            var rackRoot = new GameObject("Weapon Rack").transform;
            rackRoot.SetParent(root, false);

            var rackPosition = new Vector3(k_HalfWidth - 0.55f, 1.35f, k_FurnitureZ + 1.3f);

            Decor(PrimitiveType.Cube, rackRoot, "Plate", rackPosition + new Vector3(0.22f, 0f, 0f),
                new Vector3(0.06f, 0.7f, 1.1f), palette.socketPlate);
            Decor(PrimitiveType.Cube, rackRoot, "Peg Upper", rackPosition + new Vector3(0.1f, 0.18f, 0f),
                new Vector3(0.2f, 0.04f, 0.04f), palette.metal);
            Decor(PrimitiveType.Cube, rackRoot, "Peg Lower", rackPosition + new Vector3(0.1f, -0.2f, 0f),
                new Vector3(0.2f, 0.04f, 0.04f), palette.metal);

            rack = new GameObject("Rack Point").transform;
            rack.SetParent(rackRoot, false);
            rack.localPosition = rackPosition;
            rack.localRotation = Quaternion.Euler(0f, -90f, 0f);

            var go = new GameObject("Weapon");
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(rack.position, rack.rotation);

            // Child 0 must stay the model: WeaponController slides it back for recoil.
            var model = new GameObject("Model").transform;
            model.SetParent(go.transform, false);

            // The same six modules as the rig, minus the magazine: once the prop is built,
            // its magazine is a separate object that slots into the well and comes back out.
            BuildPistolModel(model, palette, withMagazine: false);

            // Colliders on the model would fight the grab; only the root carries one.
            foreach (var collider in model.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(collider);

            var tracerGo = new GameObject("Tracer");
            tracerGo.transform.SetParent(go.transform, false);
            var tracer = tracerGo.AddComponent<LineRenderer>();
            tracer.sharedMaterial = palette.tracer;
            tracer.widthMultiplier = 0.012f;
            tracer.numCapVertices = 2;
            tracer.useWorldSpace = true;
            tracer.positionCount = 2;
            tracer.enabled = false;

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(go.transform, false);
            muzzle.localPosition = muzzleLocalPosition;

            // Flash hangs off the muzzle, so it always sits exactly where the ray starts.
            var flash = new GameObject("Muzzle Flash");
            flash.transform.SetParent(muzzle, false);
            Decor(PrimitiveType.Sphere, flash.transform, "Burst", new Vector3(0f, 0f, 0.015f),
                new Vector3(0.055f, 0.055f, 0.08f), palette.muzzleFlash);

            var flashLight = new GameObject("Flash Light");
            flashLight.transform.SetParent(flash.transform, false);
            var burstLight = flashLight.AddComponent<Light>();
            burstLight.type = LightType.Point;
            burstLight.range = 3.5f;
            burstLight.intensity = 3.5f;
            burstLight.color = new Color(1f, 0.82f, 0.45f);
            burstLight.shadows = LightShadows.None;
            flash.SetActive(false);

            var attach = new GameObject("Grip Attach").transform;
            attach.SetParent(go.transform, false);
            attach.localPosition = gripAttachLocalPosition;

            // One collider on the root covering the whole prop; the modules themselves are
            // visual only once assembled.
            var bodyCollider = go.AddComponent<BoxCollider>();
            bodyCollider.center = bodyColliderCentre;
            bodyCollider.size = bodyColliderSize;

            var body = go.AddComponent<Rigidbody>();
            body.mass = 1.1f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            var interactable = go.AddComponent<XRGrabInteractable>();
            interactable.attachTransform = attach;

            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.maxDistance = 40f;

            var weapon = go.AddComponent<WeaponController>();
            SetField(weapon, "m_Muzzle", muzzle);
            SetField(weapon, "m_MuzzleFlash", flash);
            SetField(weapon, "m_HitMarker", hitMarker);
            AddMagazineWell(go, weapon);

            go.SetActive(false);
            return weapon;
        }

        // ------------------------------------------------------------------ ui

        static void BuildProgressPanel(Transform root, Palette palette, out TMP_Text status, out TMP_Text checklist,
            out GameObject completeBanner)
        {
            var panel = new GameObject("Progress Panel").transform;
            panel.SetParent(root, false);

            // Tall enough for a header plus one line per module.
            var position = new Vector3(k_RigX, 2.12f, k_MaxZ - 0.1f);
            Decor(PrimitiveType.Cube, panel, "Frame", position, new Vector3(1.9f, 1.5f, 0.06f), palette.signBoard);
            Decor(PrimitiveType.Cube, panel, "Trim", position + new Vector3(0f, 0.785f, 0f),
                new Vector3(1.9f, 0.07f, 0.08f), palette.accent);

            // The player reads this from the bench, about three and a half metres away. At
            // that range a 0.11 m cap height subtends close to two degrees, which is legible
            // in a headset without walking up to the wall.
            status = Label(panel, "Status", position + new Vector3(0f, 0.52f, -0.05f), new Vector2(1.75f, 0.4f),
                0.125f, "Build the pistol prop on the rig to unlock it", new Color(0.95f, 0.96f, 1f));
            checklist = Label(panel, "Checklist", position + new Vector3(0f, -0.2f, -0.05f),
                new Vector2(1.75f, 0.92f), 0.11f, $"ASSEMBLY  0 / {partSpecs.Length}",
                new Color(0.62f, 0.86f, 1f));

            // Lights up under the panel the moment the last slot fills. WorkshopManager owns it,
            // so it is switched off here and never touched again by the builder.
            completeBanner = new GameObject("Complete Banner");
            completeBanner.transform.SetParent(panel, false);

            var bannerPosition = position + new Vector3(0f, -0.92f, 0f);
            Decor(PrimitiveType.Cube, completeBanner.transform, "Bar", bannerPosition,
                new Vector3(1.9f, 0.3f, 0.06f), palette.lampComplete);
            Label(completeBanner.transform, "Text", bannerPosition + new Vector3(0f, 0f, -0.05f),
                new Vector2(1.8f, 0.24f), 0.12f, "ASSEMBLY COMPLETE", new Color(0.05f, 0.18f, 0.08f));

            completeBanner.SetActive(false);
        }

        static void BuildHowToSign(Transform root, Palette palette)
        {
            var sign = new GameObject("How To Play Sign").transform;
            sign.SetParent(root, false);

            // Sits above the pegboard, in the player's eyeline on the way to the bench.
            // Kept under the ceiling slab, whose underside is at k_CeilingY - 0.05.
            var position = new Vector3(k_BenchX, 2.70f, k_MaxZ - 0.1f);

            Decor(PrimitiveType.Cube, sign, "Board", position, new Vector3(2.8f, 0.72f, 0.06f), palette.signBoard);
            Decor(PrimitiveType.Cube, sign, "Trim", position + new Vector3(0f, 0.395f, 0f),
                new Vector3(2.8f, 0.07f, 0.08f), palette.hazard);

            Label(sign, "Title", position + new Vector3(0f, 0.22f, -0.05f), new Vector2(2.6f, 0.22f), 0.13f,
                "HOW TO PLAY", palette.hazard.color);
            Label(sign, "Steps", position + new Vector3(0f, -0.12f, -0.05f), new Vector2(2.6f, 0.46f), 0.105f,
                "1  Take a module from the tray     2  Hold it near its ghost — it turns green\n" +
                $"3  Let go to snap it in     4  Fit all {partSpecs.Length}, load a magazine, then head to the range",
                new Color(0.9f, 0.92f, 0.96f));
        }

        /// <summary>
        /// A label on the far wall, turned to face back down the room. Plain world geometry
        /// rather than a canvas: one less scale to get wrong, and nothing here is clickable.
        /// </summary>
        static TMP_Text Label(Transform parent, string name, Vector3 localPosition, Vector2 size,
            float capHeight, string text, Color colour)
        {
            return Label3D(parent, name, localPosition, size, capHeight, text, colour,
                TextAlignmentOptions.Center, k_FacesPlayer);
        }
    }
}
