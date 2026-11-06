using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace GunnerVR
{
    /// <summary>
    /// A slot on the assembly rig. Takes one kind of module and nothing else, turns green
    /// while the right module is lined up with it, and refuses a second one once filled.
    /// </summary>
    public class PartSocket : XRSocketInteractor
    {
        [SerializeField, Tooltip("The only module this slot will take")]
        PartKind m_Accepts;

        [Header("Ghost")]
        [SerializeField, Tooltip("Preview mesh showing where the module goes")]
        Renderer m_Ghost;

        [SerializeField, Tooltip("Ghost look while the slot is waiting")]
        Material m_GhostIdle;

        [SerializeField, Tooltip("Ghost look while the matching module is held nearby")]
        Material m_GhostReady;

        [Header("Feel")]
        [SerializeField, Range(0f, 1f), Tooltip("Haptic strength when a module snaps home")]
        float m_SnapHapticAmplitude = 0.55f;

        [SerializeField, Tooltip("Haptic length in seconds when a module snaps home")]
        float m_SnapHapticDuration = 0.12f;

        public PartKind accepts => m_Accepts;

        /// <summary>True once a module is seated here.</summary>
        public bool isFilled => hasSelection;

        protected override void OnEnable()
        {
            base.OnEnable();

            hoverEntered.AddListener(OnPartHoverEntered);
            hoverExited.AddListener(OnPartHoverExited);
            selectEntered.AddListener(OnSeated);
            selectExited.AddListener(OnRemoved);

            ShowGhost(true);
            SetGhostReady(false);
        }

        protected override void OnDisable()
        {
            hoverEntered.RemoveListener(OnPartHoverEntered);
            hoverExited.RemoveListener(OnPartHoverExited);
            selectEntered.RemoveListener(OnSeated);
            selectExited.RemoveListener(OnRemoved);

            base.OnDisable();
        }

        public override bool CanHover(IXRHoverInteractable interactable)
        {
            if (!Matches(interactable))
                return false;

            // A filled slot stops advertising itself, so a second module gets no green light.
            // The module already seated here is the one exception, or it would flicker.
            if (hasSelection && !(interactable is IXRSelectInteractable selectable && IsSelecting(selectable)))
                return false;

            return base.CanHover(interactable);
        }

        public override bool CanSelect(IXRSelectInteractable interactable)
        {
            // The base already refuses when this slot is full or the module is held elsewhere;
            // the kind check is what makes a slot specific to one module.
            return base.CanSelect(interactable) && Matches(interactable);
        }

        bool Matches(IXRInteractable interactable)
        {
            if (interactable?.transform == null)
                return false;

            var part = interactable.transform.GetComponent<WeaponPart>();
            return part != null && part.kind == m_Accepts;
        }

        static WeaponPart PartOf(IXRInteractable interactable)
        {
            return interactable?.transform != null ? interactable.transform.GetComponent<WeaponPart>() : null;
        }

        void OnPartHoverEntered(HoverEnterEventArgs args)
        {
            if (isFilled)
                return;

            var part = PartOf(args.interactableObject);
            if (part == null)
                return;

            part.SetHighlighted(true);
            SetGhostReady(true);
        }

        void OnPartHoverExited(HoverExitEventArgs args)
        {
            PartOf(args.interactableObject)?.SetHighlighted(false);
            SetGhostReady(false);
        }

        void OnSeated(SelectEnterEventArgs args)
        {
            var part = PartOf(args.interactableObject);
            if (part == null)
                return;

            part.SetHighlighted(false);
            part.OnSeated();

            // The ghost has done its job once the real module is sitting in it.
            ShowGhost(false);

            // The hand has already let go, so the pulse goes to whoever was holding it.
            WeaponPart.PulseHand(part.lastHand, m_SnapHapticAmplitude, m_SnapHapticDuration);
        }

        void OnRemoved(SelectExitEventArgs args)
        {
            PartOf(args.interactableObject)?.OnRemoved();

            ShowGhost(true);
            SetGhostReady(false);
        }

        void ShowGhost(bool visible)
        {
            if (m_Ghost != null)
                m_Ghost.enabled = visible;
        }

        void SetGhostReady(bool ready)
        {
            if (m_Ghost == null)
                return;

            var material = ready ? m_GhostReady : m_GhostIdle;
            if (material != null)
                m_Ghost.sharedMaterial = material;
        }
    }
}
