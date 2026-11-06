using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GunnerVR
{
    /// <summary>
    /// The big button on the bench. Grab or poke it to strip the build back to the start
    /// so the weapon can be put together again.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class ResetButton : MonoBehaviour
    {
        [SerializeField, Tooltip("Assembly flow this button resets. Leave empty in the range.")]
        WorkshopManager m_Flow;

        [SerializeField, Tooltip("Score keeper this button resets. Leave empty in the workshop.")]
        ScoreTracker m_ScoreTracker;

        [SerializeField, Range(0f, 1f), Tooltip("Haptic strength played on the pressing hand")]
        float m_HapticAmplitude = 0.6f;

        [SerializeField, Tooltip("Haptic length in seconds played on the pressing hand")]
        float m_HapticDuration = 0.1f;

        XRSimpleInteractable m_Interactable;

        void Awake()
        {
            m_Interactable = GetComponent<XRSimpleInteractable>();
        }

        void OnEnable()
        {
            m_Interactable.selectEntered.AddListener(OnPressed);
        }

        void OnDisable()
        {
            m_Interactable.selectEntered.RemoveListener(OnPressed);
        }

        /// <summary>Points the button at whatever it should reset. Used by the scene builders.</summary>
        public void Bind(WorkshopManager flow = null, ScoreTracker scoreTracker = null)
        {
            m_Flow = flow;
            m_ScoreTracker = scoreTracker;
        }

        void OnPressed(SelectEnterEventArgs args)
        {
            WeaponPart.PulseHand(args.interactorObject?.transform, m_HapticAmplitude, m_HapticDuration);

            if (m_Flow != null)
                m_Flow.ResetWorkshop();

            if (m_ScoreTracker != null)
                m_ScoreTracker.ResetSession();
        }
    }
}
