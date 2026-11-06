using System;
using UnityEngine;
using UnityEngine.Events;

namespace GunnerVR
{
    /// <summary>Which part of the building the player is standing in.</summary>
    public enum GameArea
    {
        Hallway,
        Workshop,
        ShootingRange,
    }

    /// <summary>
    /// Tracks which area the player has walked into, by testing the headset position against
    /// two boxes. No triggers and no colliders: the rig moves by locomotion, teleport and
    /// room-scale walking all at once, and a position test is the one thing that catches all
    /// three.
    ///
    /// It never moves the player. Teleporting is left to the XRI teleport anchors placed in
    /// the hallway, which only fire when the player aims at one and chooses it.
    /// </summary>
    public class AreaNavigationManager : MonoBehaviour
    {
        [Serializable]
        public class AreaEvent : UnityEvent<GameArea> { }

        [SerializeField, Tooltip("The headset transform. The camera, not the rig root.")]
        Transform m_Head;

        [SerializeField, Tooltip("World-space box covering the workshop floor")]
        Bounds m_WorkshopBounds;

        [SerializeField, Tooltip("World-space box covering the shooting range floor")]
        Bounds m_RangeBounds;

        [SerializeField, Tooltip("Raised when the player crosses from one area into another")]
        AreaEvent m_OnAreaChanged = new AreaEvent();

        public AreaEvent onAreaChanged => m_OnAreaChanged;
        public GameArea currentArea { get; private set; } = GameArea.Workshop;

        /// <summary>Points the manager at the rig and the two area boxes. Used by the scene builder.</summary>
        public void Bind(Transform head, Bounds workshopBounds, Bounds rangeBounds)
        {
            m_Head = head;
            m_WorkshopBounds = workshopBounds;
            m_RangeBounds = rangeBounds;
        }

        void Update()
        {
            if (m_Head == null)
                return;

            // Height is ignored: a player crouching or standing is in the same room.
            var p = m_Head.position;
            var flat = new Vector3(p.x, m_WorkshopBounds.center.y, p.z);

            var area = GameArea.Hallway;
            if (m_WorkshopBounds.Contains(flat))
            {
                area = GameArea.Workshop;
            }
            else
            {
                flat.y = m_RangeBounds.center.y;
                if (m_RangeBounds.Contains(flat))
                    area = GameArea.ShootingRange;
            }

            if (area == currentArea)
                return;

            currentArea = area;
            m_OnAreaChanged.Invoke(area);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.25f);
            Gizmos.DrawCube(m_WorkshopBounds.center, m_WorkshopBounds.size);
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.25f);
            Gizmos.DrawCube(m_RangeBounds.center, m_RangeBounds.size);
        }
    }
}
