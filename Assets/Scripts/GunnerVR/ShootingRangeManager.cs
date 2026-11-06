using System.Collections.Generic;
using UnityEngine;

namespace GunnerVR
{
    /// <summary>
    /// Owns the range side of the run: the score keeper and the paper targets. Kept separate
    /// from the workshop so either area can be reset on its own without disturbing the other.
    /// </summary>
    public class ShootingRangeManager : MonoBehaviour
    {
        [SerializeField, Tooltip("Keeps score for this range")]
        ScoreTracker m_ScoreTracker;

        [SerializeField, Tooltip("Targets whose paper gets wiped on a reset")]
        List<TargetBoard> m_Targets = new List<TargetBoard>();

        public int score => m_ScoreTracker != null ? m_ScoreTracker.score : 0;
        public int shots => m_ScoreTracker != null ? m_ScoreTracker.shots : 0;
        public int hits => m_ScoreTracker != null ? m_ScoreTracker.hits : 0;
        public float accuracy => m_ScoreTracker != null ? m_ScoreTracker.accuracy : 0f;

        /// <summary>Points the manager at the live scene objects. Used by the scene builder.</summary>
        public void Bind(ScoreTracker scoreTracker, IEnumerable<TargetBoard> targets)
        {
            m_ScoreTracker = scoreTracker;
            m_Targets.Clear();
            m_Targets.AddRange(targets);
        }

        /// <summary>
        /// Tells the score keeper which prop to watch. The pistol is built by the workshop,
        /// so it does not exist yet when the range is assembled.
        /// </summary>
        public void SetWeapon(WeaponController weapon)
        {
            if (m_ScoreTracker != null)
                m_ScoreTracker.SetWeapon(weapon);
        }

        /// <summary>
        /// Clears the tally and wipes the paper. The build in the workshop is deliberately
        /// left alone — resetting one area must never quietly reset the other.
        /// </summary>
        public void ResetRange()
        {
            if (m_ScoreTracker != null)
            {
                m_ScoreTracker.ResetSession();
                return;
            }

            // Still clear the paper if there is no tracker wired up.
            foreach (var target in m_Targets)
            {
                if (target != null)
                    target.ClearMarkers();
            }

            GameSessionData.ResetRangeScore();
        }
    }
}
