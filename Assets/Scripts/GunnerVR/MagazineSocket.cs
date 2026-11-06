using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace GunnerVR
{
    /// <summary>
    /// The magazine well on the pistol prop. Takes a <see cref="WeaponMagazine"/> and nothing
    /// else, and tells the weapon which magazine it is now feeding from.
    /// </summary>
    public class MagazineSocket : XRSocketInteractor
    {
        [SerializeField, Tooltip("Weapon this well belongs to")]
        WeaponController m_Weapon;

        [SerializeField, Range(0f, 1f), Tooltip("Haptic strength when a magazine seats")]
        float m_SeatHapticAmplitude = 0.5f;

        [SerializeField, Tooltip("Haptic length in seconds when a magazine seats")]
        float m_SeatHapticDuration = 0.12f;

        public bool isFilled => hasSelection;

        /// <summary>Points the well at its weapon. Used by the scene builder.</summary>
        public void Bind(WeaponController weapon)
        {
            m_Weapon = weapon;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            selectEntered.AddListener(OnSeated);
            selectExited.AddListener(OnRemoved);
        }

        protected override void OnDisable()
        {
            selectEntered.RemoveListener(OnSeated);
            selectExited.RemoveListener(OnRemoved);
            base.OnDisable();
        }

        public override bool CanHover(IXRHoverInteractable interactable)
        {
            return base.CanHover(interactable) && MagazineOf(interactable) != null;
        }

        public override bool CanSelect(IXRSelectInteractable interactable)
        {
            return base.CanSelect(interactable) && MagazineOf(interactable) != null;
        }

        static WeaponMagazine MagazineOf(IXRInteractable interactable)
        {
            return interactable?.transform != null
                ? interactable.transform.GetComponent<WeaponMagazine>()
                : null;
        }

        void OnSeated(SelectEnterEventArgs args)
        {
            var magazine = MagazineOf(args.interactableObject);
            if (magazine == null)
                return;

            magazine.SetSeated(true);

            if (m_Weapon != null)
                m_Weapon.AttachMagazine(magazine);

            WeaponPart.PulseHand(magazine.lastHand, m_SeatHapticAmplitude, m_SeatHapticDuration);
        }

        void OnRemoved(SelectExitEventArgs args)
        {
            var magazine = MagazineOf(args.interactableObject);
            magazine?.SetSeated(false);

            if (m_Weapon != null)
                m_Weapon.DetachMagazine(magazine);
        }
    }
}
