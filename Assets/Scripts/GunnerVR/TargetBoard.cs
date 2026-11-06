using System;
using UnityEngine;
using UnityEngine.Events;

namespace GunnerVR
{
    /// <summary>
    /// A paper target. Scores a hit by how close it landed to the middle and leaves a
    /// visible hole behind. Markers are pooled, so a long session does not grow the scene.
    /// </summary>
    public class TargetBoard : MonoBehaviour
    {
        [Serializable]
        public class HitEvent : UnityEvent<int, Vector3> { }

        [SerializeField, Tooltip("Centre of the rings. Defaults to this transform.")]
        Transform m_Centre;

        [SerializeField, Tooltip("Radius of the outermost scoring ring, in metres")]
        float m_Radius = 0.5f;

        [SerializeField, Tooltip("Number of scoring rings from the middle out")]
        int m_RingCount = 5;

        [SerializeField, Tooltip("Points for a bullseye. Each ring out is worth two less.")]
        int m_CentreScore = 10;

        [SerializeField, Tooltip("How many bullet holes stay on the paper before the oldest is reused")]
        int m_MarkerPoolSize = 24;

        [SerializeField, Tooltip("Diameter of a bullet hole, in metres")]
        float m_MarkerSize = 0.03f;

        [SerializeField, Tooltip("Look the board flashes to when a shot lands on it")]
        Material m_HitFlashMaterial;

        [SerializeField, Tooltip("How long the flash lasts, in seconds")]
        float m_FlashDuration = 0.12f;

        [SerializeField, Tooltip("Raised on every hit, as (score, world point)")]
        HitEvent m_OnHit = new HitEvent();

        Transform[] m_Markers;
        int m_NextMarker;
        AudioSource m_Audio;
        AudioClip m_HitClip;
        Renderer m_Face;
        Material m_RestingMaterial;
        Coroutine m_FlashRoutine;

        public HitEvent onHit => m_OnHit;

        void Awake()
        {
            if (m_Centre == null)
                m_Centre = transform;

            m_Audio = GetComponent<AudioSource>();
            if (m_Audio != null)
            {
                m_Audio.playOnAwake = false;
                m_Audio.spatialBlend = 1f;
            }

            m_HitClip = ProceduralAudio.CreateHit();

            m_Face = GetComponent<Renderer>();
            if (m_Face != null)
                m_RestingMaterial = m_Face.sharedMaterial;

            BuildMarkerPool();
        }

        void BuildMarkerPool()
        {
            var holder = new GameObject("Bullet Holes").transform;
            holder.SetParent(transform, false);

            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            {
                color = new Color(0.05f, 0.05f, 0.06f),
            };

            m_Markers = new Transform[Mathf.Max(1, m_MarkerPoolSize)];
            for (var i = 0; i < m_Markers.Length; i++)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
                marker.name = "Hole";
                Destroy(marker.GetComponent<Collider>());
                marker.GetComponent<MeshRenderer>().sharedMaterial = material;

                var markerTransform = marker.transform;
                markerTransform.SetParent(holder, false);
                markerTransform.localScale = Vector3.one * m_MarkerSize;
                marker.SetActive(false);

                m_Markers[i] = markerTransform;
            }
        }

        /// <summary>
        /// Scores a shot that landed at <paramref name="worldPoint"/> and marks the paper.
        /// Returns the points awarded.
        /// </summary>
        public int RegisterHit(Vector3 worldPoint)
        {
            var score = ScoreAt(worldPoint);
            PlaceMarker(worldPoint);
            Flash();

            if (m_Audio != null)
                m_Audio.PlayOneShot(m_HitClip);

            m_OnHit.Invoke(score, worldPoint);
            return score;
        }

        /// <summary>Points a shot at this world position would earn, without marking the paper.</summary>
        public int ScoreAt(Vector3 worldPoint)
        {
            // Measure across the face of the board, ignoring how deep the ray went.
            var local = m_Centre.InverseTransformPoint(worldPoint);
            var distance = new Vector2(local.x, local.y).magnitude;

            if (m_Radius <= 0f)
                return m_CentreScore;

            var normalised = Mathf.Clamp01(distance / m_Radius);
            var ring = Mathf.Clamp(Mathf.FloorToInt(normalised * m_RingCount), 0, m_RingCount - 1);
            return Mathf.Max(0, m_CentreScore - ring * 2);
        }

        void PlaceMarker(Vector3 worldPoint)
        {
            if (m_Markers == null || m_Markers.Length == 0)
                return;

            var marker = m_Markers[m_NextMarker];
            m_NextMarker = (m_NextMarker + 1) % m_Markers.Length;

            // Float the hole a hair off the paper so it does not z-fight with the board.
            marker.position = worldPoint + m_Centre.forward * -0.002f;
            marker.rotation = Quaternion.LookRotation(-m_Centre.forward, m_Centre.up);
            marker.gameObject.SetActive(true);
        }

        /// <summary>
        /// Blinks the board face so a hit is obvious even out at the far lane, where a single
        /// bullet hole is only a few pixels across.
        /// </summary>
        void Flash()
        {
            if (m_Face == null || m_HitFlashMaterial == null)
                return;

            if (m_FlashRoutine != null)
                StopCoroutine(m_FlashRoutine);

            m_FlashRoutine = StartCoroutine(FlashRoutine());
        }

        System.Collections.IEnumerator FlashRoutine()
        {
            m_Face.sharedMaterial = m_HitFlashMaterial;
            yield return new WaitForSeconds(m_FlashDuration);
            m_Face.sharedMaterial = m_RestingMaterial;
            m_FlashRoutine = null;
        }

        /// <summary>Wipes every bullet hole off the paper.</summary>
        public void ClearMarkers()
        {
            if (m_Markers == null)
                return;

            foreach (var marker in m_Markers)
            {
                if (marker != null)
                    marker.gameObject.SetActive(false);
            }

            m_NextMarker = 0;
        }
    }
}
