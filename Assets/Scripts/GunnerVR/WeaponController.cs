using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GunnerVR
{
    /// <summary>
    /// The assembled pistol prop once it is in the player's hand: trigger to fire, a
    /// magazine that runs dry, a reload, and the flash, noise and kick that sell it.
    ///
    /// Firing is driven either by the interactor's own activate input (the default, which
    /// follows whichever hand is holding the prop) or by an Input Action assigned in the
    /// Inspector. Assign one and it takes over, so the trigger never fires twice.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    [RequireComponent(typeof(AudioSource))]
    public class WeaponController : MonoBehaviour
    {
        [Serializable]
        public class AmmoEvent : UnityEvent<int, int> { }

        [Header("Aim")]
        [SerializeField, Tooltip("Empty at the end of the barrel. Its blue Z axis is the shot direction, " +
                                 "so it must point the same way as the barrel.")]
        Transform m_Muzzle;

        [SerializeField, Tooltip("How far a shot carries, in metres")]
        float m_Range = 60f;

        [SerializeField, Tooltip("Layers a shot can hit")]
        LayerMask m_HitMask = ~0;

        [Header("Input (optional)")]
        [SerializeField, Tooltip("Leave empty to use the holding hand's own activate input. " +
                                 "Assign an action to drive firing explicitly instead.")]
        InputActionProperty m_FireAction;

        [SerializeField, Tooltip("No reload action ships with the project. Add one and assign it here, " +
                                 "or leave it empty and reload at the ammo crate.")]
        InputActionProperty m_ReloadAction;

        [Header("Magazine")]
        [SerializeField, Tooltip("Rounds in a full magazine")]
        int m_MagazineSize = 12;

        [SerializeField, Tooltip("Shortest gap between shots, in seconds")]
        float m_FireCooldown = 0.18f;

        [SerializeField, Tooltip("When on, the prop refuses to fire until the assembly is finished")]
        bool m_RequiresAssembly = true;

        [SerializeField, Tooltip("When on, rounds live in a magazine that can be pulled out and swapped, " +
                                 "and the prop will not fire with the well empty")]
        bool m_UsesDetachableMagazine;

        [Header("Effects")]
        [SerializeField, Tooltip("Shown for a few frames at the muzzle on every shot")]
        GameObject m_MuzzleFlash;

        [SerializeField, Tooltip("Shown briefly where a shot lands on a target")]
        GameObject m_HitMarker;

        [SerializeField, Tooltip("How long the flash and the hit marker stay up, in seconds")]
        float m_EffectDuration = 0.05f;

        [Header("Feel")]
        [SerializeField, Range(0f, 1f), Tooltip("Haptic strength on the firing hand")]
        float m_FireHapticAmplitude = 0.8f;

        [SerializeField, Tooltip("Haptic length in seconds on the firing hand")]
        float m_FireHapticDuration = 0.1f;

        [SerializeField, Tooltip("How far the prop kicks back, in metres")]
        float m_RecoilDistance = 0.022f;

        [SerializeField, Tooltip("How far the muzzle flips up on a shot, in degrees")]
        float m_RecoilAngle = 7f;

        [SerializeField, Tooltip("How quickly the prop settles after a kick")]
        float m_RecoilRecovery = 12f;

        [Header("Events")]
        [SerializeField, Tooltip("Raised on every change to the magazine, as (current, size)")]
        AmmoEvent m_OnAmmoChanged = new AmmoEvent();

        [SerializeField, Tooltip("Raised after a round leaves the barrel")]
        UnityEvent m_OnFired = new UnityEvent();

        [SerializeField, Tooltip("Raised after a fresh magazine is seated")]
        UnityEvent m_OnReloaded = new UnityEvent();

        XRGrabInteractable m_Interactable;
        AudioSource m_Audio;
        LineRenderer m_Tracer;

        AudioClip m_ShotClip;
        AudioClip m_DryFireClip;
        AudioClip m_ReloadClip;

        Transform m_Model;
        Vector3 m_ModelRestPosition;
        Quaternion m_ModelRestRotation;

        WeaponMagazine m_Magazine;
        int m_InternalAmmo;
        Transform m_Hand;
        float m_NextFireTime;
        Coroutine m_TracerRoutine;
        Coroutine m_FlashRoutine;
        Coroutine m_MarkerRoutine;

        public AmmoEvent onAmmoChanged => m_OnAmmoChanged;
        public UnityEvent onFired => m_OnFired;
        public UnityEvent onReloaded => m_OnReloaded;

        /// <summary>The magazine currently seated in the well, when one is used at all.</summary>
        public WeaponMagazine magazine => m_Magazine;

        /// <summary>True unless a detachable magazine is called for and the well is empty.</summary>
        public bool hasMagazine => !m_UsesDetachableMagazine || m_Magazine != null;

        public int magazineSize => m_UsesDetachableMagazine && m_Magazine != null
            ? m_Magazine.capacity
            : m_MagazineSize;

        public int ammo => m_UsesDetachableMagazine
            ? (m_Magazine != null ? m_Magazine.rounds : 0)
            : m_InternalAmmo;

        public bool isEmpty => ammo <= 0;

        /// <summary>
        /// Whether the prop is finished and allowed to fire. <see cref="WorkshopManager"/> sets this
        /// when the build completes, and clears it if a module is pulled back out.
        /// </summary>
        public bool isArmed { get; private set; }

        /// <summary>True when the trigger would actually produce a shot right now.</summary>
        public bool canFire => (!m_RequiresAssembly || isArmed) && hasMagazine && !isEmpty;

        /// <summary>
        /// The fire action, or null when the field was left empty. An untouched
        /// <see cref="InputActionProperty"/> still serialises an empty action rather than a
        /// null one, so "assigned" has to mean "has at least one binding".
        /// </summary>
        InputAction fireAction => Resolve(m_FireAction);

        InputAction reloadAction => Resolve(m_ReloadAction);

        static InputAction Resolve(InputActionProperty property)
        {
            var action = property.action;
            return action != null && action.bindings.Count > 0 ? action : null;
        }

        void Awake()
        {
            m_Interactable = GetComponent<XRGrabInteractable>();
            m_Audio = GetComponent<AudioSource>();
            m_Audio.playOnAwake = false;
            m_Audio.spatialBlend = 1f;

            m_ShotClip = ProceduralAudio.CreateShot();
            m_DryFireClip = ProceduralAudio.CreateDryFire();
            m_ReloadClip = ProceduralAudio.CreateReload();

            if (m_Muzzle == null)
                m_Muzzle = transform;

            m_Tracer = GetComponentInChildren<LineRenderer>(true);
            if (m_Tracer != null)
            {
                m_Tracer.enabled = false;
                m_Tracer.useWorldSpace = true;
                m_Tracer.positionCount = 2;
            }

            // The first child is the visible body, so recoil can move the look of the prop
            // without fighting the grab interactable on the root.
            m_Model = transform.childCount > 0 ? transform.GetChild(0) : null;
            if (m_Model != null)
            {
                m_ModelRestPosition = m_Model.localPosition;
                m_ModelRestRotation = m_Model.localRotation;
            }

            if (m_MuzzleFlash != null)
                m_MuzzleFlash.SetActive(false);

            if (m_HitMarker != null)
                m_HitMarker.SetActive(false);

            m_InternalAmmo = m_MagazineSize;
        }

        void OnEnable()
        {
            m_Interactable.activated.AddListener(OnActivated);
            m_Interactable.selectEntered.AddListener(OnGrabbed);
            m_Interactable.selectExited.AddListener(OnReleased);
            m_OnAmmoChanged.Invoke(ammo, magazineSize);
        }

        void OnDisable()
        {
            m_Interactable.activated.RemoveListener(OnActivated);
            m_Interactable.selectEntered.RemoveListener(OnGrabbed);
            m_Interactable.selectExited.RemoveListener(OnReleased);
            UnbindActions();
        }

        void Update()
        {
            if (m_Model == null)
                return;

            var t = Time.deltaTime * m_RecoilRecovery;
            m_Model.localPosition = Vector3.Lerp(m_Model.localPosition, m_ModelRestPosition, t);
            m_Model.localRotation = Quaternion.Slerp(m_Model.localRotation, m_ModelRestRotation, t);
        }

        // --------------------------------------------------------------- input

        void OnGrabbed(SelectEnterEventArgs args)
        {
            m_Hand = args.interactorObject?.transform;
            BindActions();
        }

        void OnReleased(SelectExitEventArgs args)
        {
            UnbindActions();
            m_Hand = null;
        }

        void BindActions()
        {
            var fire = fireAction;
            if (fire != null)
            {
                fire.performed += OnFirePerformed;
                fire.Enable();
            }

            var reload = reloadAction;
            if (reload != null)
            {
                reload.performed += OnReloadPerformed;
                reload.Enable();
            }
        }

        void UnbindActions()
        {
            var fire = fireAction;
            if (fire != null)
                fire.performed -= OnFirePerformed;

            var reload = reloadAction;
            if (reload != null)
                reload.performed -= OnReloadPerformed;
        }

        void OnFirePerformed(InputAction.CallbackContext context) => TryFire(m_Hand);

        void OnReloadPerformed(InputAction.CallbackContext context) => Reload();

        void OnActivated(ActivateEventArgs args)
        {
            // An explicit fire action, once assigned, owns the trigger. Without this guard a
            // single pull would be counted twice.
            if (fireAction != null)
                return;

            TryFire(args.interactorObject?.transform);
        }

        // -------------------------------------------------------------- firing

        /// <summary>
        /// Fires one round if the prop is armed and the magazine and cooldown allow it.
        /// Returns false on an unfinished build, an empty magazine, or while still cycling.
        /// </summary>
        public bool TryFire(Transform firingHand = null)
        {
            if (Time.time < m_NextFireTime)
                return false;

            m_NextFireTime = Time.time + m_FireCooldown;

            if (m_RequiresAssembly && !isArmed)
                return false;

            // No magazine in the well counts the same as a dry one: a click, not a shot.
            if (!hasMagazine || isEmpty)
            {
                m_Audio.PlayOneShot(m_DryFireClip);
                return false;
            }

            if (m_UsesDetachableMagazine)
                m_Magazine.TryTakeRound();
            else
                m_InternalAmmo--;

            m_OnAmmoChanged.Invoke(ammo, magazineSize);

            var origin = m_Muzzle.position;
            var direction = m_Muzzle.forward;
            var endPoint = origin + direction * m_Range;

            if (Physics.Raycast(origin, direction, out var hit, m_Range, m_HitMask, QueryTriggerInteraction.Ignore))
            {
                endPoint = hit.point;

                var target = hit.collider.GetComponentInParent<TargetBoard>();
                if (target != null)
                {
                    target.RegisterHit(hit.point);
                    ShowHitMarker(hit.point, hit.normal);
                }
            }

            m_Audio.PlayOneShot(m_ShotClip);
            WeaponPart.PulseHand(firingHand ?? m_Hand, m_FireHapticAmplitude, m_FireHapticDuration);
            Kick();
            ShowFlash();
            ShowTracer(origin, endPoint);

            m_OnFired.Invoke();
            return true;
        }

        /// <summary>Seats a full magazine. Does nothing if the prop is already topped up.</summary>
        public bool Reload()
        {
            if (m_UsesDetachableMagazine)
            {
                if (m_Magazine == null || m_Magazine.rounds >= m_Magazine.capacity)
                    return false;

                m_Magazine.Refill();
            }
            else
            {
                if (m_InternalAmmo >= m_MagazineSize)
                    return false;

                m_InternalAmmo = m_MagazineSize;
            }

            m_Audio.PlayOneShot(m_ReloadClip);
            m_OnAmmoChanged.Invoke(ammo, magazineSize);
            m_OnReloaded.Invoke();

            WeaponPart.PulseHand(m_Hand, 0.4f, 0.15f);
            return true;
        }

        /// <summary>Called by the magazine well when a magazine seats.</summary>
        public void AttachMagazine(WeaponMagazine seated)
        {
            m_Magazine = seated;
            m_OnAmmoChanged.Invoke(ammo, magazineSize);
            m_OnReloaded.Invoke();
        }

        /// <summary>Called by the magazine well when a magazine is pulled out.</summary>
        public void DetachMagazine(WeaponMagazine removed)
        {
            if (removed != null && m_Magazine != removed)
                return;

            m_Magazine = null;
            m_OnAmmoChanged.Invoke(ammo, magazineSize);
        }

        /// <summary>Called by <see cref="WorkshopManager"/> when the build finishes or breaks.</summary>
        public void SetArmed(bool armed)
        {
            isArmed = armed;
        }

        // ------------------------------------------------------------- effects

        void Kick()
        {
            if (m_Model == null)
                return;

            m_Model.localPosition = m_ModelRestPosition - Vector3.forward * m_RecoilDistance;
            m_Model.localRotation = m_ModelRestRotation * Quaternion.Euler(-m_RecoilAngle, 0f, 0f);
        }

        void ShowFlash()
        {
            if (m_MuzzleFlash == null)
                return;

            if (m_FlashRoutine != null)
                StopCoroutine(m_FlashRoutine);

            m_FlashRoutine = StartCoroutine(ShowBriefly(m_MuzzleFlash, m_EffectDuration, () => m_FlashRoutine = null));
        }

        void ShowHitMarker(Vector3 point, Vector3 normal)
        {
            if (m_HitMarker == null)
                return;

            // Parked in world space so it stays put while the prop keeps moving.
            m_HitMarker.transform.SetPositionAndRotation(point + normal * 0.01f,
                Quaternion.LookRotation(normal, Vector3.up));

            if (m_MarkerRoutine != null)
                StopCoroutine(m_MarkerRoutine);

            m_MarkerRoutine = StartCoroutine(ShowBriefly(m_HitMarker, m_EffectDuration * 4f,
                () => m_MarkerRoutine = null));
        }

        void ShowTracer(Vector3 from, Vector3 to)
        {
            if (m_Tracer == null)
                return;

            if (m_TracerRoutine != null)
                StopCoroutine(m_TracerRoutine);

            m_TracerRoutine = StartCoroutine(TracerRoutine(from, to));
        }

        IEnumerator TracerRoutine(Vector3 from, Vector3 to)
        {
            m_Tracer.SetPosition(0, from);
            m_Tracer.SetPosition(1, to);
            m_Tracer.enabled = true;

            yield return new WaitForSeconds(m_EffectDuration);

            m_Tracer.enabled = false;
            m_TracerRoutine = null;
        }

        static IEnumerator ShowBriefly(GameObject target, float seconds, Action done)
        {
            target.SetActive(true);
            yield return new WaitForSeconds(seconds);
            target.SetActive(false);
            done?.Invoke();
        }

        void OnDrawGizmosSelected()
        {
            // Draws the line a shot would actually take, so the barrel and the ray can be
            // checked against each other in the scene view without entering play mode.
            if (m_Muzzle == null)
                return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(m_Muzzle.position, m_Muzzle.position + m_Muzzle.forward * 3f);
            Gizmos.DrawWireSphere(m_Muzzle.position, 0.01f);
        }
    }
}
