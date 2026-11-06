using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace GunnerVR
{
    /// <summary>
    /// Drives the loop: build the weapon on the bench, have it unlock, carry it to the
    /// range, shoot, and come back to strip it down and start over.
    ///
    /// In-world labels are deliberately English — the stock TMP font asset ships with no
    /// Thai glyphs, so Thai strings would render as empty boxes in the headset. Add a Thai
    /// font asset to TextMeshPro and these strings can be translated.
    /// </summary>
    public class WorkshopManager : MonoBehaviour
    {
        [Header("Workshop")]
        [SerializeField, Tooltip("Rig whose sockets make up the weapon")]
        AssemblyStation m_Station;

        [SerializeField, Tooltip("Every loose piece on the bench, for the reset button")]
        List<WeaponPart> m_Parts = new List<WeaponPart>();

        [Header("Weapon")]
        [SerializeField, Tooltip("The finished weapon, hidden until the build is done")]
        WeaponController m_Weapon;

        [SerializeField, Tooltip("Where the finished weapon appears once unlocked")]
        Transform m_WeaponRack;

        [Header("Range")]
        [SerializeField, Tooltip("Score keeper for the firing line")]
        ScoreTracker m_ScoreTracker;

        [Header("Labels")]
        // TMP_Text rather than the UGUI subclass, so a label can live on a canvas or stand
        // on its own as world geometry.
        [SerializeField, Tooltip("Headline telling the player what to do next")]
        TMP_Text m_StatusLabel;

        [SerializeField, Tooltip("Per-part checklist on the workbench")]
        TMP_Text m_ChecklistLabel;

        [SerializeField, Tooltip("Shown only once every slot is filled")]
        GameObject m_CompleteBanner;

        AudioSource m_Audio;
        AudioClip m_UnlockClip;

        public bool isWeaponUnlocked { get; private set; }

        void Awake()
        {
            m_Audio = GetComponent<AudioSource>();
            if (m_Audio != null)
            {
                m_Audio.playOnAwake = false;
                m_Audio.spatialBlend = 0f;
            }

            m_UnlockClip = ProceduralAudio.CreateUnlock();
        }

        void OnEnable()
        {
            if (m_Station != null)
            {
                m_Station.onProgressChanged.AddListener(OnProgressChanged);
                m_Station.onCompleted.AddListener(OnAssemblyCompleted);
                m_Station.onBroken.AddListener(OnAssemblyBroken);
            }

            LockWeapon();

            if (m_Station != null)
                m_Station.Refresh();

            // A build carried in from an earlier visit puts the prop straight back on the
            // rack. The rig itself is empty, so the checklist still reads zero — that is the
            // state of the rig, not of the prop the player already owns.
            if (GameSessionData.weaponAssembled)
                UnlockWeapon();
        }

        void OnDisable()
        {
            if (m_Station == null)
                return;

            m_Station.onProgressChanged.RemoveListener(OnProgressChanged);
            m_Station.onCompleted.RemoveListener(OnAssemblyCompleted);
            m_Station.onBroken.RemoveListener(OnAssemblyBroken);
        }

        /// <summary>Wires the flow to live scene objects. Used by the scene builder.</summary>
        public void Bind(AssemblyStation station, IEnumerable<WeaponPart> parts, WeaponController weapon,
            Transform weaponRack, ScoreTracker scoreTracker, TMP_Text statusLabel, TMP_Text checklistLabel,
            GameObject completeBanner = null)
        {
            var wasEnabled = isActiveAndEnabled;
            if (wasEnabled)
                OnDisable();

            m_Station = station;
            m_Parts.Clear();
            m_Parts.AddRange(parts);
            m_Weapon = weapon;
            m_WeaponRack = weaponRack;
            m_ScoreTracker = scoreTracker;
            m_StatusLabel = statusLabel;
            m_ChecklistLabel = checklistLabel;
            m_CompleteBanner = completeBanner;

            if (wasEnabled)
                OnEnable();
        }

        void OnProgressChanged(int filled, int total)
        {
            if (m_ChecklistLabel != null && m_Station != null)
            {
                var builder = new StringBuilder();
                builder.AppendLine($"ASSEMBLY  {filled} / {total}");

                foreach (var socket in m_Station.sockets)
                {
                    if (socket == null)
                        continue;

                    builder.AppendLine($"{(socket.isFilled ? "[x]" : "[ ]")}  {socket.accepts}");
                }

                m_ChecklistLabel.text = builder.ToString().TrimEnd();
            }

            var complete = total > 0 && filled == total;

            if (m_CompleteBanner != null)
                m_CompleteBanner.SetActive(complete);

            if (m_StatusLabel == null || isWeaponUnlocked)
                return;

            if (complete)
                m_StatusLabel.text = "ASSEMBLY COMPLETE";
            else if (filled == 0)
                m_StatusLabel.text = "Take a module from the tray and hold it near its slot";
            else
                m_StatusLabel.text = $"Keep going — {total - filled} module(s) to go";
        }

        /// <summary>
        /// Only a finished build writes to the session, and only a deliberate break clears
        /// it. Opening the workshop with an empty rig must leave the session alone, or
        /// walking back from the range would undo the build every single time.
        /// </summary>
        void OnAssemblyCompleted()
        {
            GameSessionData.MarkAssembled();
            UnlockWeapon();
        }

        void OnAssemblyBroken()
        {
            // With both areas in one scene there is no mid-run teardown to guard against:
            // the sockets only empty because the player pulled a module out.
            GameSessionData.MarkDisassembled();
            LockWeapon();
        }

        /// <summary>Puts the finished weapon on the rack and lets the player take it.</summary>
        public void UnlockWeapon()
        {
            if (isWeaponUnlocked || m_Weapon == null)
                return;

            isWeaponUnlocked = true;

            var weaponTransform = m_Weapon.transform;
            if (m_WeaponRack != null)
                weaponTransform.SetPositionAndRotation(m_WeaponRack.position, m_WeaponRack.rotation);

            m_Weapon.gameObject.SetActive(true);
            m_Weapon.SetArmed(true);

            if (m_Audio != null)
                m_Audio.PlayOneShot(m_UnlockClip);

            if (m_StatusLabel != null)
                m_StatusLabel.text = "Weapon ready — take it to the range and fire at the targets";
        }

        /// <summary>Takes the weapon away again, used when a finished build loses a part.</summary>
        public void LockWeapon()
        {
            isWeaponUnlocked = false;

            if (m_CompleteBanner != null)
                m_CompleteBanner.SetActive(false);

            if (m_Weapon == null)
                return;

            // Disarmed as well as hidden: the prop must not fire on a half-finished build.
            m_Weapon.SetArmed(false);

            var body = m_Weapon.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            if (m_WeaponRack != null)
                m_Weapon.transform.SetPositionAndRotation(m_WeaponRack.position, m_WeaponRack.rotation);

            m_Weapon.gameObject.SetActive(false);

            if (m_StatusLabel != null)
                m_StatusLabel.text = "Build the weapon on the bench to unlock it";
        }

        /// <summary>
        /// Strips the build back to the start: every piece returns to the bench, the weapon
        /// goes away, and the range session is cleared.
        /// </summary>
        public void ResetWorkshop()
        {
            foreach (var part in m_Parts)
            {
                if (part != null)
                    part.ReturnToSpawn();
            }

            LockWeapon();

            if (m_ScoreTracker != null)
                m_ScoreTracker.ResetSession();

            if (m_Station != null)
                m_Station.Refresh();
        }
    }
}
