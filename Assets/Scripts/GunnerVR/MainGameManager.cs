using UnityEngine;

namespace GunnerVR
{
    /// <summary>
    /// The one object that knows about both halves of the building. It does not run either
    /// of them — the workshop and the range each manage themselves — it coordinates: it hands
    /// the assembled prop to the range's score keeper, and it offers the two resets as
    /// separate operations so clearing a score can never cost the player their build.
    /// </summary>
    public class MainGameManager : MonoBehaviour
    {
        [SerializeField, Tooltip("Runs the assembly side")]
        WorkshopManager m_Workshop;

        [SerializeField, Tooltip("Runs the shooting side")]
        ShootingRangeManager m_Range;

        [SerializeField, Tooltip("Reports which area the player is standing in")]
        AreaNavigationManager m_Navigation;

        [SerializeField, Tooltip("The single pistol prop in the run, built in the workshop")]
        WeaponController m_Weapon;

        /// <summary>True once the player has finished the build on the workshop rig.</summary>
        public bool weaponReady => GameSessionData.weaponAssembled;

        public WorkshopManager workshop => m_Workshop;
        public ShootingRangeManager range => m_Range;
        public AreaNavigationManager navigation => m_Navigation;
        public GameArea currentArea => m_Navigation != null ? m_Navigation.currentArea : GameArea.Workshop;

        /// <summary>Wires the shared systems to the two areas. Used by the scene builder.</summary>
        public void Bind(WorkshopManager workshopManager, ShootingRangeManager rangeManager,
            AreaNavigationManager navigationManager, WeaponController weapon)
        {
            m_Workshop = workshopManager;
            m_Range = rangeManager;
            m_Navigation = navigationManager;
            m_Weapon = weapon;
        }

        void Start()
        {
            // There is one prop in the scene and the workshop owns it, so the range's score
            // keeper has to be told about it rather than finding its own.
            if (m_Range != null && m_Weapon != null)
                m_Range.SetWeapon(m_Weapon);
        }

        /// <summary>Strips the build back to the start. The range score is left untouched.</summary>
        public void ResetWorkshopOnly()
        {
            if (m_Workshop != null)
                m_Workshop.ResetWorkshop();
            else
                GameSessionData.ResetWorkshopProgress();
        }

        /// <summary>Clears the range tally and the paper. The build is left untouched.</summary>
        public void ResetRangeOnly()
        {
            if (m_Range != null)
                m_Range.ResetRange();
            else
                GameSessionData.ResetRangeScore();
        }

        /// <summary>Starts the whole run over.</summary>
        public void ResetEverything()
        {
            ResetWorkshopOnly();
            ResetRangeOnly();
        }
    }
}
