using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GunnerVR
{
    /// <summary>
    /// A detachable magazine prop. Holds the rounds itself, so pulling it out of the pistol
    /// takes the ammunition with it and pushing a fresh one in is what refills the prop.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    [RequireComponent(typeof(Rigidbody))]
    public class WeaponMagazine : MonoBehaviour
    {
        // Unity only serialises a concrete UnityEvent subclass, never UnityEvent<T1, T2> itself.
        [System.Serializable]
        public class RoundsEvent : UnityEvent<int, int> { }

        [SerializeField, Tooltip("Rounds this magazine holds when full")]
        int m_Capacity = 12;

        [SerializeField, Tooltip("Rounds in it right now")]
        int m_Rounds = 12;

        [SerializeField, Tooltip("Raised whenever the count changes, as (rounds, capacity)")]
        RoundsEvent m_OnRoundsChanged = new RoundsEvent();

        XRGrabInteractable m_Interactable;
        Rigidbody m_Rigidbody;
        Vector3 m_SpawnPosition;
        Quaternion m_SpawnRotation;

        public int capacity => m_Capacity;
        public int rounds => m_Rounds;
        public bool isEmpty => m_Rounds <= 0;
        public RoundsEvent onRoundsChanged => m_OnRoundsChanged;

        /// <summary>True while this magazine is seated in a pistol.</summary>
        public bool isSeated { get; private set; }

        /// <summary>The interactor that last picked it up, for post-release haptics.</summary>
        public Transform lastHand { get; private set; }

        void Awake()
        {
            m_Interactable = GetComponent<XRGrabInteractable>();
            m_Rigidbody = GetComponent<Rigidbody>();

            var t = transform;
            m_SpawnPosition = t.position;
            m_SpawnRotation = t.rotation;

            m_Rounds = Mathf.Clamp(m_Rounds, 0, m_Capacity);
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
            if (args.interactorObject is MagazineSocket)
                return;

            lastHand = args.interactorObject?.transform;
            WeaponPart.PulseHand(lastHand, 0.25f, 0.07f);
        }

        /// <summary>
        /// Takes one round. Returns false when the magazine is already dry, which is what
        /// makes the pistol click instead of fire.
        /// </summary>
        public bool TryTakeRound()
        {
            if (m_Rounds <= 0)
                return false;

            m_Rounds--;
            m_OnRoundsChanged.Invoke(m_Rounds, m_Capacity);
            return true;
        }

        /// <summary>Tops the magazine back up, as the ammo crate does.</summary>
        public void Refill()
        {
            if (m_Rounds == m_Capacity)
                return;

            m_Rounds = m_Capacity;
            m_OnRoundsChanged.Invoke(m_Rounds, m_Capacity);
        }

        /// <summary>Called by the socket on the pistol when this magazine seats or leaves.</summary>
        public void SetSeated(bool seated)
        {
            isSeated = seated;

            if (m_Rigidbody == null)
                return;

            if (seated)
            {
                m_Rigidbody.velocity = Vector3.zero;
                m_Rigidbody.angularVelocity = Vector3.zero;
            }

            // Kinematic while seated so it rides the pistol instead of fighting it.
            m_Rigidbody.isKinematic = seated;
        }

        /// <summary>Puts the magazine back on the bench, full.</summary>
        public void ReturnToSpawn()
        {
            if (m_Interactable.isSelected && m_Interactable.interactionManager != null)
                m_Interactable.interactionManager.CancelInteractableSelection((IXRSelectInteractable)m_Interactable);

            SetSeated(false);
            Refill();

            if (m_Rigidbody != null)
            {
                m_Rigidbody.velocity = Vector3.zero;
                m_Rigidbody.angularVelocity = Vector3.zero;
            }

            transform.SetPositionAndRotation(m_SpawnPosition, m_SpawnRotation);
        }
    }
}
