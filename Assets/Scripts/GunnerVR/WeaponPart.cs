using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GunnerVR
{
    /// <summary>
    /// One loose module in the tray. Tags itself with the slot it belongs in, lights up when
    /// it is lined up with that slot, goes still once seated, and remembers where it started
    /// so the bench can be reset.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    [RequireComponent(typeof(Rigidbody))]
    public class WeaponPart : MonoBehaviour
    {
        [SerializeField, Tooltip("Slot on the assembly rig that accepts this module")]
        PartKind m_Kind;

        [SerializeField, Tooltip("Look this module takes on while it is lined up with its slot")]
        Material m_HighlightMaterial;

        [SerializeField, Range(0f, 1f), Tooltip("Haptic strength on the hand that picks this module up")]
        float m_GrabHapticAmplitude = 0.3f;

        [SerializeField, Tooltip("Haptic length in seconds on pick up")]
        float m_GrabHapticDuration = 0.08f;

        XRGrabInteractable m_Interactable;
        Rigidbody m_Rigidbody;
        Renderer[] m_Renderers;
        Material[] m_RestingMaterials;

        Vector3 m_SpawnPosition;
        Quaternion m_SpawnRotation;
        bool m_Highlighted;

        public PartKind kind => m_Kind;

        /// <summary>True while this module is sitting in its slot on the rig.</summary>
        public bool isSeated { get; private set; }

        /// <summary>The interactor that last picked this module up, for post-release haptics.</summary>
        public Transform lastHand { get; private set; }

        void Awake()
        {
            m_Interactable = GetComponent<XRGrabInteractable>();
            m_Rigidbody = GetComponent<Rigidbody>();

            m_Renderers = GetComponentsInChildren<Renderer>(true);
            m_RestingMaterials = new Material[m_Renderers.Length];
            for (var i = 0; i < m_Renderers.Length; i++)
                m_RestingMaterials[i] = m_Renderers[i].sharedMaterial;

            var t = transform;
            m_SpawnPosition = t.position;
            m_SpawnRotation = t.rotation;
        }

        void OnEnable()
        {
            m_Interactable.selectEntered.AddListener(OnSelectEntered);
        }

        void OnDisable()
        {
            m_Interactable.selectEntered.RemoveListener(OnSelectEntered);
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            // A socket taking the module is not something to feel, and it is not a hand.
            if (args.interactorObject is PartSocket)
                return;

            lastHand = args.interactorObject?.transform;
            PulseHand(lastHand, m_GrabHapticAmplitude, m_GrabHapticDuration);
        }

        /// <summary>Called by the slot once this module has snapped home.</summary>
        public void OnSeated()
        {
            isSeated = true;
            Settle();
        }

        /// <summary>Called by the slot when this module is pulled back out.</summary>
        public void OnRemoved()
        {
            isSeated = false;

            if (m_Rigidbody != null)
                m_Rigidbody.isKinematic = false;
        }

        /// <summary>
        /// Stops the body dead and parks it as kinematic. Without this a freshly snapped
        /// module keeps whatever momentum the throw gave it and buzzes against the ones
        /// already on the rig.
        /// </summary>
        void Settle()
        {
            if (m_Rigidbody == null)
                return;

            m_Rigidbody.velocity = Vector3.zero;
            m_Rigidbody.angularVelocity = Vector3.zero;
            m_Rigidbody.isKinematic = true;
        }

        /// <summary>Swaps every renderer to the highlight look, or back to how it started.</summary>
        public void SetHighlighted(bool highlighted)
        {
            if (m_Highlighted == highlighted || m_Renderers == null)
                return;

            m_Highlighted = highlighted;

            for (var i = 0; i < m_Renderers.Length; i++)
            {
                if (m_Renderers[i] == null)
                    continue;

                var material = highlighted && m_HighlightMaterial != null
                    ? m_HighlightMaterial
                    : m_RestingMaterials[i];

                if (material != null)
                    m_Renderers[i].sharedMaterial = material;
            }
        }

        /// <summary>
        /// Sends a haptic pulse to whichever controller the given interactor lives under.
        /// Does nothing on a rig without haptics, so it is safe to call blind.
        /// </summary>
        public static void PulseHand(Transform interactorTransform, float amplitude, float duration)
        {
            if (interactorTransform == null)
                return;

            var player = interactorTransform.GetComponentInParent<HapticImpulsePlayer>();
            if (player != null)
                player.SendHapticImpulse(amplitude, duration);
        }

        /// <summary>
        /// Drops the module out of any hand or slot and puts it back in the tray where it started.
        /// </summary>
        public void ReturnToSpawn()
        {
            // The cast picks the IXRSelectInteractable overload; passing the concrete type
            // would bind to the XRBaseInteractable one, which XRI 3.x has deprecated.
            if (m_Interactable.isSelected && m_Interactable.interactionManager != null)
                m_Interactable.interactionManager.CancelInteractableSelection((IXRSelectInteractable)m_Interactable);

            isSeated = false;
            SetHighlighted(false);

            if (m_Rigidbody != null)
            {
                m_Rigidbody.isKinematic = false;
                m_Rigidbody.velocity = Vector3.zero;
                m_Rigidbody.angularVelocity = Vector3.zero;
            }

            transform.SetPositionAndRotation(m_SpawnPosition, m_SpawnRotation);
        }
    }
}
