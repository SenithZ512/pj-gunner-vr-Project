using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

namespace GunnerVR
{
    /// <summary>
    /// Watches every slot on the workbench rig and reports how far along the build is.
    /// Fires once when the last slot fills, and again if a finished weapon loses a piece.
    /// </summary>
    public class AssemblyStation : MonoBehaviour
    {
        [Serializable]
        public class ProgressEvent : UnityEvent<int, int> { }

        [SerializeField, Tooltip("Slots that make up a finished weapon")]
        List<PartSocket> m_Sockets = new List<PartSocket>();

        [SerializeField, Tooltip("Raised on every fit or removal, as (filled, total)")]
        ProgressEvent m_OnProgressChanged = new ProgressEvent();

        [SerializeField, Tooltip("Raised the moment the last slot fills")]
        UnityEvent m_OnCompleted = new UnityEvent();

        [SerializeField, Tooltip("Raised when a finished weapon loses a piece again")]
        UnityEvent m_OnBroken = new UnityEvent();

        bool m_WasComplete;

        public ProgressEvent onProgressChanged => m_OnProgressChanged;
        public UnityEvent onCompleted => m_OnCompleted;
        public UnityEvent onBroken => m_OnBroken;

        public IReadOnlyList<PartSocket> sockets => m_Sockets;
        public int totalCount => m_Sockets.Count;
        public int filledCount { get; private set; }
        public bool isComplete => m_Sockets.Count > 0 && filledCount == m_Sockets.Count;

        /// <summary>Replaces the watched slot list. Used by the scene builder.</summary>
        public void SetSockets(IEnumerable<PartSocket> sockets)
        {
            Unsubscribe();
            m_Sockets.Clear();
            m_Sockets.AddRange(sockets);
            if (isActiveAndEnabled)
                Subscribe();
            Refresh();
        }

        void OnEnable()
        {
            Subscribe();
            m_WasComplete = false;
            Refresh();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        void Subscribe()
        {
            foreach (var socket in m_Sockets)
            {
                if (socket == null)
                    continue;

                socket.selectEntered.AddListener(OnSocketFilled);
                socket.selectExited.AddListener(OnSocketEmptied);
            }
        }

        void Unsubscribe()
        {
            foreach (var socket in m_Sockets)
            {
                if (socket == null)
                    continue;

                socket.selectEntered.RemoveListener(OnSocketFilled);
                socket.selectExited.RemoveListener(OnSocketEmptied);
            }
        }

        void OnSocketFilled(SelectEnterEventArgs args)
        {
            // Haptics and the ghost swap are the slot's own business; this only counts.
            Refresh();
        }

        void OnSocketEmptied(SelectExitEventArgs args)
        {
            Refresh();
        }

        /// <summary>
        /// Recounts the filled slots and raises whatever changed. Safe to call at any time.
        /// </summary>
        public void Refresh()
        {
            var filled = 0;
            foreach (var socket in m_Sockets)
            {
                if (socket != null && socket.isFilled)
                    filled++;
            }

            filledCount = filled;
            m_OnProgressChanged.Invoke(filledCount, m_Sockets.Count);

            var complete = isComplete;
            if (complete && !m_WasComplete)
                m_OnCompleted.Invoke();
            else if (!complete && m_WasComplete)
                m_OnBroken.Invoke();

            m_WasComplete = complete;
        }
    }
}
