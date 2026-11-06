using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GunnerVR
{
    /// <summary>
    /// Totals up the range session: points, rounds sent, hits landed, and accuracy.
    /// Subscribes to every target it is pointed at and to the weapon that does the shooting.
    /// </summary>
    public class ScoreTracker : MonoBehaviour
    {
        [SerializeField, Tooltip("Targets whose hits count towards the score")]
        List<TargetBoard> m_Targets = new List<TargetBoard>();

        [SerializeField, Tooltip("Weapon whose shots count towards accuracy")]
        WeaponController m_Weapon;

        // TMP_Text rather than the UGUI subclass, so a label can live on a canvas or stand
        // on its own as world geometry.
        [SerializeField, Tooltip("Big label on the wall board showing the running total")]
        TMP_Text m_ScoreLabel;

        [SerializeField, Tooltip("Wall board label showing how many shots have landed")]
        TMP_Text m_HitsLabel;

        [SerializeField, Tooltip("Wall board label showing shots fired and accuracy")]
        TMP_Text m_StatsLabel;

        [SerializeField, Tooltip("Bench console label showing rounds left in the magazine")]
        TMP_Text m_AmmoLabel;

        [SerializeField, Tooltip("Shown on the bench console only when the prop cannot fire")]
        GameObject m_ReloadPrompt;

        public int score { get; private set; }
        public int shots { get; private set; }
        public int hits { get; private set; }
        public float accuracy => shots == 0 ? 0f : hits / (float)shots;

        void OnEnable()
        {
            // Pick the tally back up where the last visit left it, so a trip to the workshop
            // and back does not wipe the session.
            score = GameSessionData.score;
            shots = GameSessionData.shots;
            hits = GameSessionData.hits;

            foreach (var target in m_Targets)
            {
                if (target != null)
                    target.onHit.AddListener(OnTargetHit);
            }

            if (m_Weapon != null)
            {
                m_Weapon.onFired.AddListener(OnWeaponFired);
                m_Weapon.onAmmoChanged.AddListener(OnAmmoChanged);
                OnAmmoChanged(m_Weapon.ammo, m_Weapon.magazineSize);
            }

            Redraw();
        }

        void OnDisable()
        {
            foreach (var target in m_Targets)
            {
                if (target != null)
                    target.onHit.RemoveListener(OnTargetHit);
            }

            if (m_Weapon != null)
            {
                m_Weapon.onFired.RemoveListener(OnWeaponFired);
                m_Weapon.onAmmoChanged.RemoveListener(OnAmmoChanged);
            }
        }

        /// <summary>Points the tracker at the live scene objects. Used by the scene builder.</summary>
        public void Bind(IEnumerable<TargetBoard> targets, WeaponController weapon,
            TMP_Text scoreLabel, TMP_Text hitsLabel, TMP_Text statsLabel, TMP_Text ammoLabel,
            GameObject reloadPrompt)
        {
            var wasEnabled = isActiveAndEnabled;
            if (wasEnabled)
                OnDisable();

            m_Targets.Clear();
            m_Targets.AddRange(targets);
            m_Weapon = weapon;
            m_ScoreLabel = scoreLabel;
            m_HitsLabel = hitsLabel;
            m_StatsLabel = statsLabel;
            m_AmmoLabel = ammoLabel;
            m_ReloadPrompt = reloadPrompt;

            if (wasEnabled)
                OnEnable();
        }

        /// <summary>
        /// Attaches the prop after the fact. In the combined scene the pistol is built by the
        /// workshop, so it does not exist when the range is assembled.
        /// </summary>
        public void SetWeapon(WeaponController weapon)
        {
            if (m_Weapon == weapon)
                return;

            if (isActiveAndEnabled && m_Weapon != null)
            {
                m_Weapon.onFired.RemoveListener(OnWeaponFired);
                m_Weapon.onAmmoChanged.RemoveListener(OnAmmoChanged);
            }

            m_Weapon = weapon;

            if (isActiveAndEnabled && m_Weapon != null)
            {
                m_Weapon.onFired.AddListener(OnWeaponFired);
                m_Weapon.onAmmoChanged.AddListener(OnAmmoChanged);
                OnAmmoChanged(m_Weapon.ammo, m_Weapon.magazineSize);
            }
        }

        void OnTargetHit(int points, Vector3 worldPoint)
        {
            score += points;
            hits++;
            Redraw();
        }

        void OnWeaponFired()
        {
            shots++;
            Redraw();
        }

        void OnAmmoChanged(int current, int size)
        {
            // An empty well and an empty magazine need different answers: one wants a fresh
            // magazine, the other wants a trip to the ammo crate.
            var noMagazine = m_Weapon != null && !m_Weapon.hasMagazine;
            var cannotFire = noMagazine || current <= 0;

            if (m_AmmoLabel != null)
            {
                if (noMagazine)
                    m_AmmoLabel.text = "NO MAG";
                else
                    m_AmmoLabel.text = $"{current} / {size}";
            }

            // The prompt is a separate object rather than a different string in the ammo
            // readout, so it can be loud enough to catch the eye without shouting all the time.
            if (m_ReloadPrompt != null)
                m_ReloadPrompt.SetActive(cannotFire);
        }

        /// <summary>Clears the session and wipes the paper on every tracked target.</summary>
        public void ResetSession()
        {
            score = 0;
            shots = 0;
            hits = 0;
            // Range tally only — the workshop build is deliberately untouched.
            GameSessionData.ResetRangeScore();

            foreach (var target in m_Targets)
            {
                if (target != null)
                    target.ClearMarkers();
            }

            Redraw();
        }

        void Redraw()
        {
            GameSessionData.StoreScore(score, shots, hits);

            if (m_ScoreLabel != null)
                m_ScoreLabel.text = $"SCORE  {score}";

            if (m_HitsLabel != null)
                m_HitsLabel.text = $"HITS  {hits}";

            if (m_StatsLabel != null)
                m_StatsLabel.text = $"{shots} shots · {accuracy * 100f:0}% accuracy";
        }
    }
}
