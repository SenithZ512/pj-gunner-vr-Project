using UnityEngine;

namespace GunnerVR
{
    /// <summary>
    /// The run's shared state: what the player has built and how they have shot. Both areas
    /// live in one scene now, so nothing here has to survive a scene load — it exists so the
    /// workshop and the range can read each other's progress without holding references
    /// across the hierarchy, and so each area can be reset without touching the other.
    ///
    /// Static on purpose: Unity clears statics on a domain reload, which happens when play
    /// mode stops, so a fresh run always starts empty with no cleanup code.
    /// </summary>
    public static class GameSessionData
    {
        // --------------------------------------------------------- workshop

        /// <summary>True once the pistol prop has been built on the workshop rig.</summary>
        public static bool weaponAssembled { get; private set; }

        /// <summary>Called by <see cref="WorkshopManager"/> when the last module goes in.</summary>
        public static void MarkAssembled()
        {
            weaponAssembled = true;
        }

        /// <summary>Called by <see cref="WorkshopManager"/> when a finished build loses a module.</summary>
        public static void MarkDisassembled()
        {
            weaponAssembled = false;
        }

        // ------------------------------------------------------------ range

        public static int score { get; private set; }
        public static int shots { get; private set; }
        public static int hits { get; private set; }

        public static float accuracy => shots == 0 ? 0f : hits / (float)shots;

        /// <summary>Called by the range so walking back to the workshop does not lose the tally.</summary>
        public static void StoreScore(int newScore, int newShots, int newHits)
        {
            score = newScore;
            shots = newShots;
            hits = newHits;
        }

        // ----------------------------------------------------------- resets

        /// <summary>
        /// Clears the build only. The range tally is deliberately left alone: resetting one
        /// area must never quietly reset the other.
        /// </summary>
        public static void ResetWorkshopProgress()
        {
            weaponAssembled = false;
        }

        /// <summary>Clears the range tally only, leaving the build intact.</summary>
        public static void ResetRangeScore()
        {
            score = 0;
            shots = 0;
            hits = 0;
        }

        /// <summary>Wipes both, as starting a brand new run would.</summary>
        public static void ResetAll()
        {
            ResetWorkshopProgress();
            ResetRangeScore();
        }

        /// <summary>
        /// Unity keeps statics alive between play sessions when Domain Reload is switched off
        /// in Project Settings, so the state is cleared explicitly on load as well.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            ResetAll();
        }
    }
}
