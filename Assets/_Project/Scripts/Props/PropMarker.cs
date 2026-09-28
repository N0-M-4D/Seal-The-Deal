using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.Props
{
    /// <summary>
    /// Sits in a floor template where a prop should stand. The host spawns the real,
    /// networked prop here after the towers are built; the marker itself is invisible.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PropMarker : MonoBehaviour
    {
        [Tooltip("The prop prefab that appears here. Must be a networked prop from Assets/_Project/Prefabs/Props.")]
        public NetworkObject Prefab;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, 0.4f, 0f), new Vector3(0.6f, 0.8f, 0.6f));
        }
    }
}
