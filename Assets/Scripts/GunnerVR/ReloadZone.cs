using UnityEngine;

namespace GunnerVR
{
    /// <summary>
    /// An ammo crate at the firing line. Dunk the weapon into it to seat a fresh magazine —
    /// no extra button binding needed, which keeps the controls the same on every headset.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ReloadZone : MonoBehaviour
    {
        [SerializeField, Tooltip("Seconds before the same weapon can reload here again")]
        float m_Cooldown = 0.5f;

        float m_NextAllowedTime;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (Time.time < m_NextAllowedTime)
                return;

            var weapon = other.GetComponentInParent<WeaponController>();
            if (weapon == null)
                return;

            if (weapon.Reload())
                m_NextAllowedTime = Time.time + m_Cooldown;
        }
    }
}
