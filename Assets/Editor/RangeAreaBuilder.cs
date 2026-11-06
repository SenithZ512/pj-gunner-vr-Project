using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using static GunnerVR.EditorTools.GunnerBuildKit;

namespace GunnerVR.EditorTools
{
    /// <summary>
    /// Builds the shooting range half of the single combined scene: lanes, paper targets at
    /// three distances, a booth with spare magazines and an ammo crate, the score board and
    /// the reset-score button.
    ///
    /// It deliberately builds no pistol. There is one prop in the run, assembled in the
    /// workshop and carried here by the player, which is the whole point of putting both
    /// areas in one scene.
    /// </summary>
    public static class RangeAreaBuilder
    {
        public const string areaName = "ShootingRangeArea";

        const float k_HalfWidth = 5f;
        const float k_MinZ = -6f;
        const float k_MaxZ = 30f;
        const float k_CeilingY = 4f;
        const float k_FiringLineZ = 0f;
        const float k_BenchTop = 1.05f;

        /// <summary>Half-width of the opening in the near wall that the hallway joins.</summary>
        public const float doorwayHalfWidth = 1.1f;

        /// <summary>Where the near wall sits, in area-local space. The hallway ends here.</summary>
        public const float doorwayZ = k_MinZ;

        static readonly float[] k_LaneX = { -3.2f, 0f, 3.2f };
        static readonly float[] k_TargetZ = { 8f, 15f, 23f };

        static readonly Quaternion k_FacesPlayer = Quaternion.LookRotation(Vector3.back, Vector3.up);
        static readonly Quaternion k_FacesIntoRoom = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        static readonly Quaternion k_LiesFlat = Quaternion.LookRotation(Vector3.up, Vector3.forward);

        /// <summary>
        /// Builds the whole range under a new area root parented to <paramref name="parent"/>.
        /// Returns the manager so the caller can hand it to the shared systems.
        /// </summary>
        public static ShootingRangeManager BuildArea(Transform parent, Vector3 origin, Palette palette)
        {
            var root = new GameObject(areaName).transform;
            root.SetParent(parent, false);
            root.localPosition = origin;

            BuildShell(root, palette);
            BuildLighting(root, palette);
            BuildLanes(root, palette);

            var targets = BuildTargets(root, palette);
            BuildBooth(root, palette, out var resetButton, out var ammoLabel, out var reloadPrompt);
            BuildScoreBoard(root, palette, out var scoreLabel, out var hitsLabel, out var statsLabel);
            BuildExit(root, palette);

            var systems = new GameObject("ShootingRangeSystems");
            systems.transform.SetParent(parent, false);

            var scoreTracker = systems.AddComponent<ScoreTracker>();
            // The weapon is bound later by MainSceneBuilder, once the workshop has made one.
            scoreTracker.Bind(targets, null, scoreLabel, hitsLabel, statsLabel, ammoLabel, reloadPrompt);

            var manager = systems.AddComponent<ShootingRangeManager>();
            manager.Bind(scoreTracker, targets);
            resetButton.Bind(scoreTracker: scoreTracker);

            return manager;
        }

        // --------------------------------------------------------------- shell

        static void BuildShell(Transform root, Palette palette)
        {
            var shell = new GameObject("Hall").transform;
            shell.SetParent(root, false);

            var width = k_HalfWidth * 2f;
            var depth = k_MaxZ - k_MinZ;
            var centreZ = (k_MinZ + k_MaxZ) * 0.5f;

            // Standing area behind the line gets its own slab and its own teleport volume;
            // downrange deliberately gets none, so nobody teleports in front of the muzzles.
            var standLength = k_FiringLineZ - k_MinZ;
            var stand = Box(shell, "Standing Floor", new Vector3(0f, -0.05f, k_MinZ + standLength * 0.5f),
                new Vector3(width, 0.1f, standLength), palette.concrete);
            var teleport = stand.AddComponent<TeleportationArea>();
            teleport.matchOrientation = MatchOrientation.WorldSpaceUp;

            var downrange = k_MaxZ - k_FiringLineZ;
            Box(shell, "Downrange Floor", new Vector3(0f, -0.05f, k_FiringLineZ + downrange * 0.5f),
                new Vector3(width, 0.1f, downrange), palette.concreteDark);

            Decor(PrimitiveType.Cube, shell, "Ceiling", new Vector3(0f, k_CeilingY, centreZ),
                new Vector3(width, 0.1f, depth), palette.ceiling);

            Box(shell, "Wall Left", new Vector3(-k_HalfWidth, k_CeilingY * 0.5f, centreZ),
                new Vector3(0.1f, k_CeilingY, depth), palette.wallUpper);
            Box(shell, "Wall Right", new Vector3(k_HalfWidth, k_CeilingY * 0.5f, centreZ),
                new Vector3(0.1f, k_CeilingY, depth), palette.wallUpper);

            // The near wall is built in two piers with a real gap, because the hallway passes
            // through it. A decorative frame over a solid wall would stop the player dead.
            var pierWidth = (width - doorwayHalfWidth * 2f) * 0.5f;
            var pierOffset = doorwayHalfWidth + pierWidth * 0.5f;

            foreach (var sign in new[] { -1f, 1f })
            {
                Box(shell, $"Wall Behind {(sign < 0 ? "Left" : "Right")}",
                    new Vector3(sign * pierOffset, k_CeilingY * 0.5f, k_MinZ),
                    new Vector3(pierWidth, k_CeilingY, 0.1f), palette.wallUpper);
            }

            Box(shell, "Wall Behind Lintel", new Vector3(0f, k_CeilingY - 0.85f, k_MinZ),
                new Vector3(doorwayHalfWidth * 2f, 1.7f, 0.1f), palette.wallUpper);

            // Backstop at the far end, angled panels so the end of the hall reads as built
            // for this rather than being a flat wall.
            Box(shell, "Backstop", new Vector3(0f, k_CeilingY * 0.5f, k_MaxZ),
                new Vector3(width, k_CeilingY, 0.1f), palette.concreteDark);
            for (var i = 0; i < 6; i++)
            {
                Decor(PrimitiveType.Cube, shell, $"Baffle {i + 1}",
                    new Vector3(0f, 0.6f + i * 0.55f, k_MaxZ - 0.5f),
                    new Vector3(width - 0.4f, 0.45f, 0.12f), palette.beam, new Vector3(-35f, 0f, 0f));
            }

            Decor(PrimitiveType.Cube, shell, "Line Paint",
                new Vector3(0f, 0.004f, k_FiringLineZ), new Vector3(width, 0.01f, 0.14f), palette.hazard);
        }

        static void BuildLighting(Transform root, Palette palette)
        {
            var lights = new GameObject("Lamps").transform;
            lights.SetParent(root, false);

            // Strip lights down the hall, one every six metres.
            for (var i = 0; i < 6; i++)
            {
                var z = k_MinZ + 3f + i * 6f;

                var lamp = new GameObject($"Strip {i + 1}").transform;
                lamp.SetParent(lights, false);
                lamp.localPosition = new Vector3(0f, k_CeilingY - 0.2f, z);

                Decor(PrimitiveType.Cube, lamp, "Housing", Vector3.zero,
                    new Vector3(k_HalfWidth * 1.4f, 0.12f, 0.26f), palette.lampHousing);
                Decor(PrimitiveType.Cube, lamp, "Tube", new Vector3(0f, -0.08f, 0f),
                    new Vector3(k_HalfWidth * 1.3f, 0.05f, 0.16f), palette.lampGlow);

                var bulb = new GameObject("Light");
                bulb.transform.SetParent(lamp, false);
                bulb.transform.localPosition = new Vector3(0f, -0.3f, 0f);

                var light = bulb.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 11f;
                light.intensity = 1.6f;
                light.color = new Color(0.96f, 0.97f, 1f);
                light.shadows = LightShadows.None;
            }
        }

        // --------------------------------------------------------------- lanes

        static void BuildLanes(Transform root, Palette palette)
        {
            var lanes = new GameObject("Lanes").transform;
            lanes.SetParent(root, false);

            // Dividers sit between the lanes, stopping short of the targets.
            for (var i = 0; i < k_LaneX.Length - 1; i++)
            {
                var x = (k_LaneX[i] + k_LaneX[i + 1]) * 0.5f;
                Box(lanes, $"Divider {i + 1}", new Vector3(x, 0.75f, k_FiringLineZ + 2.5f),
                    new Vector3(0.1f, 1.5f, 5f), palette.booth);
            }

            // Distance markings on the floor of each lane.
            var marks = new GameObject("Distance Marks").transform;
            marks.SetParent(lanes, false);

            for (var lane = 0; lane < k_LaneX.Length; lane++)
            {
                for (var step = 1; step * 5f <= k_TargetZ[lane]; step++)
                {
                    var z = k_FiringLineZ + step * 5f;
                    Decor(PrimitiveType.Cube, marks, $"Mark L{lane + 1} {step * 5}m",
                        new Vector3(k_LaneX[lane], 0.004f, z), new Vector3(1.1f, 0.01f, 0.06f), palette.laneMark);
                }

                Label3D(marks, $"Lane {lane + 1} Distance",
                    new Vector3(k_LaneX[lane], 0.006f, k_FiringLineZ + 1.2f), new Vector2(1.4f, 0.3f), 0.17f,
                    $"{k_TargetZ[lane]:0} m", new Color(0.82f, 0.84f, 0.88f),
                    TextAlignmentOptions.Center, k_LiesFlat);
            }
        }

        // ------------------------------------------------------------- targets

        static List<TargetBoard> BuildTargets(Transform root, Palette palette)
        {
            var holder = new GameObject("Targets").transform;
            holder.SetParent(root, false);

            var targets = new List<TargetBoard>();
            for (var i = 0; i < k_LaneX.Length; i++)
                targets.Add(MakeTarget(holder, palette, i + 1, new Vector3(k_LaneX[i], 1.45f, k_TargetZ[i])));

            return targets;
        }

        static TargetBoard MakeTarget(Transform parent, Palette palette, int index, Vector3 position)
        {
            var frame = new GameObject($"Target {index}").transform;
            frame.SetParent(parent, false);

            Box(frame, "Post Left", new Vector3(position.x - 0.5f, position.y * 0.5f, position.z + 0.08f),
                new Vector3(0.07f, position.y, 0.07f), palette.metal);
            Box(frame, "Post Right", new Vector3(position.x + 0.5f, position.y * 0.5f, position.z + 0.08f),
                new Vector3(0.07f, position.y, 0.07f), palette.metal);
            Box(frame, "Cross Brace", new Vector3(position.x, position.y + 0.55f, position.z + 0.08f),
                new Vector3(1.1f, 0.06f, 0.06f), palette.metal);

            // The board itself: this is what the raycast hits and what flashes on a hit.
            var board = Box(frame, $"Board {index}", position, new Vector3(1f, 1f, 0.04f), palette.paper);

            var ringColours = new[] { palette.ringOuter, palette.ringMid, palette.ringInner, palette.ringBull };
            for (var i = 0; i < ringColours.Length; i++)
            {
                var scale = 0.8f - i * 0.18f;
                var ring = Decor(PrimitiveType.Cylinder, board.transform, $"Ring {i}",
                    new Vector3(0f, 0f, -0.5f - (i + 1) * 0.05f), new Vector3(scale, 0.02f, scale),
                    ringColours[i], new Vector3(90f, 0f, 0f));
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            Label3D(board.transform, "Number", new Vector3(0f, 0.62f, -0.6f), new Vector2(0.6f, 0.2f), 0.14f,
                $"LANE {index}", new Color(0.9f, 0.92f, 0.96f), TextAlignmentOptions.Center, k_FacesPlayer);

            var audio = board.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.maxDistance = 45f;

            var target = board.AddComponent<TargetBoard>();
            SetField(target, "m_Radius", 0.5f);
            SetField(target, "m_HitFlashMaterial", palette.targetFlash);
            return target;
        }

        // --------------------------------------------------------------- booth

        static void BuildBooth(Transform root, Palette palette, out ResetButton resetButton,
            out TMP_Text ammoLabel, out GameObject reloadPrompt)
        {
            var booth = new GameObject("Booth").transform;
            booth.SetParent(root, false);

            var benchZ = k_FiringLineZ - 0.75f;

            Box(booth, "Bench Top", new Vector3(0f, k_BenchTop - 0.04f, benchZ),
                new Vector3(3.4f, 0.08f, 0.7f), palette.booth);
            Decor(PrimitiveType.Cube, booth, "Bench Lip", new Vector3(0f, k_BenchTop - 0.09f, benchZ),
                new Vector3(3.44f, 0.03f, 0.74f), palette.metalDark);

            for (var i = 0; i < 4; i++)
            {
                var x = -1.5f + i * 1f;
                Box(booth, $"Bench Leg {i + 1}", new Vector3(x, (k_BenchTop - 0.08f) * 0.5f, benchZ),
                    new Vector3(0.07f, k_BenchTop - 0.08f, 0.07f), palette.metal);
            }

            // Side partitions, as a real booth has.
            Box(booth, "Partition Left", new Vector3(-1.75f, 1.1f, benchZ + 0.1f),
                new Vector3(0.08f, 2.2f, 0.9f), palette.booth);
            Box(booth, "Partition Right", new Vector3(1.75f, 1.1f, benchZ + 0.1f),
                new Vector3(0.08f, 2.2f, 0.9f), palette.booth);

            // Somewhere to put the prop down. The range builds no pistol of its own — the
            // player carries in the one they assembled, and there is only ever that one.
            var restPosition = new Vector3(-0.55f, k_BenchTop + 0.02f, benchZ);
            Decor(PrimitiveType.Cube, booth, "Weapon Rest", restPosition,
                new Vector3(0.34f, 0.03f, 0.24f), palette.metalDark);
            Label3D(booth, "Rest Label", restPosition + new Vector3(0f, 0.012f, -0.2f),
                new Vector2(0.5f, 0.07f), 0.04f, "REST PISTOL HERE", new Color(0.78f, 0.8f, 0.85f),
                TextAlignmentOptions.Center, k_LiesFlat);

            BuildSpareMagazines(booth, palette, new Vector3(0.45f, k_BenchTop + 0.05f, benchZ), k_LiesFlat);
            BuildAmmoCrate(booth, palette, new Vector3(1.35f, k_BenchTop + 0.14f, benchZ));
            resetButton = BuildResetButton(booth, palette, new Vector3(-1.4f, k_BenchTop + 0.02f, benchZ));

            // On the back edge of the bench, so it faces the shooter rather than the lane.
            BuildBenchConsole(booth, palette, new Vector3(-1.05f, k_BenchTop + 0.3f, benchZ - 0.26f),
                out ammoLabel, out reloadPrompt);
        }

        static void BuildAmmoCrate(Transform parent, Palette palette, Vector3 position)
        {
            var crate = Box(parent, "Ammo Crate", position, new Vector3(0.4f, 0.26f, 0.4f), palette.accent);

            var trigger = crate.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.6f, 2.6f, 1.6f);
            crate.AddComponent<ReloadZone>();

            Label3D(parent, "Ammo Label", position + new Vector3(0f, 0.14f, -0.21f), new Vector2(0.5f, 0.07f),
                0.045f, "TOP UP", new Color(0.1f, 0.08f, 0.04f), TextAlignmentOptions.Center, k_LiesFlat);
        }

        static ResetButton BuildResetButton(Transform parent, Palette palette, Vector3 position)
        {
            var housing = new GameObject("Reset Score Button").transform;
            housing.SetParent(parent, false);

            Decor(PrimitiveType.Cube, housing, "Base", position, new Vector3(0.22f, 0.05f, 0.22f),
                palette.metalDark);

            var cap = Cylinder(housing, "Cap", position + new Vector3(0f, 0.055f, 0f),
                new Vector3(0.14f, 0.035f, 0.14f), palette.buttonRed);
            cap.AddComponent<XRSimpleInteractable>();

            Label3D(housing, "Label", position + new Vector3(0f, 0.032f, -0.19f), new Vector2(0.44f, 0.09f),
                0.045f, "RESET SCORE", new Color(0.88f, 0.89f, 0.93f), TextAlignmentOptions.Center, k_LiesFlat);

            return cap.AddComponent<ResetButton>();
        }

        // ----------------------------------------------------------- score, exit

        /// <summary>
        /// The wall board carries the numbers worth reading from anywhere on the firing line:
        /// score, hits and accuracy. It sits about five metres from where the player stands,
        /// so the cap heights are sized for that distance — the score subtends roughly three
        /// degrees, which is comfortable in a headset, and nothing here is under one and a half.
        /// Ammo deliberately lives on the bench console instead, next to the player's hands.
        /// </summary>
        static void BuildScoreBoard(Transform root, Palette palette, out TMP_Text score, out TMP_Text hits,
            out TMP_Text stats)
        {
            var board = new GameObject("Score Board").transform;
            board.SetParent(root, false);

            // Hangs off the left wall, angled in so it is readable from the firing line
            // without covering any lane.
            var position = new Vector3(-k_HalfWidth + 0.4f, 2.45f, k_FiringLineZ + 2f);
            var facing = Quaternion.LookRotation(new Vector3(1f, 0f, -0.45f).normalized, Vector3.up);

            var frame = Decor(PrimitiveType.Cube, board, "Frame", position,
                new Vector3(0.08f, 1.65f, 2.6f), palette.signBoard);
            frame.transform.rotation = facing * Quaternion.Euler(0f, 90f, 0f);

            var trim = Decor(PrimitiveType.Cube, board, "Trim", position + facing * new Vector3(0f, 0.86f, 0f),
                new Vector3(0.1f, 0.08f, 2.6f), palette.accent);
            trim.transform.rotation = facing * Quaternion.Euler(0f, 90f, 0f);

            score = Label3D(board, "Score", position + facing * new Vector3(0f, 0.5f, -0.06f),
                new Vector2(2.4f, 0.5f), 0.26f, "SCORE  0", new Color(0.95f, 0.96f, 1f),
                TextAlignmentOptions.Center, facing);
            hits = Label3D(board, "Hits", position + facing * new Vector3(0f, 0.02f, -0.06f),
                new Vector2(2.4f, 0.36f), 0.18f, "HITS  0", new Color(0.62f, 0.86f, 1f),
                TextAlignmentOptions.Center, facing);
            stats = Label3D(board, "Stats", position + facing * new Vector3(0f, -0.42f, -0.06f),
                new Vector2(2.4f, 0.28f), 0.13f, "0 shots · 0% accuracy", new Color(0.78f, 0.82f, 0.9f),
                TextAlignmentOptions.Center, facing);

            Label3D(board, "Return Hint", position + facing * new Vector3(0f, -0.72f, -0.06f),
                new Vector2(2.4f, 0.24f), 0.1f, "←  WORKSHOP THROUGH THE HALLWAY",
                new Color(0.72f, 0.74f, 0.8f), TextAlignmentOptions.Center, facing);
        }

        /// <summary>
        /// A lectern on the bench for the readouts the player checks mid-shot. It sits about
        /// a metre away and tilts back toward them, so ammo can be glanced at without taking
        /// the eyes off the lane and without leaning in to read anything.
        /// </summary>
        static void BuildBenchConsole(Transform booth, Palette palette, Vector3 position,
            out TMP_Text ammo, out GameObject reloadPrompt)
        {
            var console = new GameObject("Bench Console").transform;
            console.SetParent(booth, false);

            // Tilted back off vertical, the way a lectern is.
            var facing = k_FacesPlayer * Quaternion.Euler(28f, 0f, 0f);

            Decor(PrimitiveType.Cylinder, console, "Post", position + new Vector3(0f, -0.14f, 0f),
                new Vector3(0.05f, 0.14f, 0.05f), palette.metal);

            var panel = Decor(PrimitiveType.Cube, console, "Panel", position,
                new Vector3(0.58f, 0.26f, 0.03f), palette.signBoard);
            panel.transform.localRotation = facing;

            ammo = Label3D(console, "Ammo", position + facing * new Vector3(0f, 0.04f, -0.025f),
                new Vector2(0.54f, 0.12f), 0.062f, "NO MAG", new Color(0.98f, 0.82f, 0.4f),
                TextAlignmentOptions.Center, facing);

            Label3D(console, "Ammo Caption", position + facing * new Vector3(0f, -0.07f, -0.025f),
                new Vector2(0.54f, 0.06f), 0.03f, "ROUNDS", new Color(0.65f, 0.67f, 0.72f),
                TextAlignmentOptions.Center, facing);

            // Shown by ScoreTracker only while the prop cannot fire.
            reloadPrompt = new GameObject("Reload Prompt");
            reloadPrompt.transform.SetParent(console, false);

            var promptAt = position + facing * new Vector3(0f, 0.26f, -0.02f);
            var bar = Decor(PrimitiveType.Cube, reloadPrompt.transform, "Bar", promptAt,
                new Vector3(0.58f, 0.13f, 0.02f), palette.buttonRed);
            bar.transform.localRotation = facing;

            Label3D(reloadPrompt.transform, "Text", promptAt + facing * new Vector3(0f, 0f, -0.02f),
                new Vector2(0.56f, 0.1f), 0.055f, "RELOAD", new Color(1f, 0.93f, 0.9f),
                TextAlignmentOptions.Center, facing);

            reloadPrompt.SetActive(false);
        }

        /// <summary>
        /// Dresses the opening the hallway runs through. No trigger and no scene load — the
        /// player simply walks back, which is what the single-scene brief asks for.
        /// </summary>
        static void BuildExit(Transform root, Palette palette)
        {
            var exit = new GameObject("Doorway To Hallway").transform;
            exit.SetParent(root, false);

            var z = k_MinZ;

            Decor(PrimitiveType.Cube, exit, "Frame Left", new Vector3(-doorwayHalfWidth, 1.15f, z),
                new Vector3(0.14f, 2.3f, 0.26f), palette.beam);
            Decor(PrimitiveType.Cube, exit, "Frame Right", new Vector3(doorwayHalfWidth, 1.15f, z),
                new Vector3(0.14f, 2.3f, 0.26f), palette.beam);
            Decor(PrimitiveType.Cube, exit, "Frame Top", new Vector3(0f, 2.33f, z),
                new Vector3(doorwayHalfWidth * 2f + 0.14f, 0.16f, 0.26f), palette.beam);

            Label3D(exit, "Sign", new Vector3(0f, 2.62f, z + 0.16f), new Vector2(3.4f, 0.4f), 0.24f,
                "←  WORKSHOP", palette.hazard.color, TextAlignmentOptions.Center, k_FacesIntoRoom);
        }
    }
}
