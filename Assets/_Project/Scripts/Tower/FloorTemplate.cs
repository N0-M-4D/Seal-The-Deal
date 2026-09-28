using UnityEngine;

namespace CloseTheDeal.Tower
{
    public enum FloorKind : byte
    {
        Lobby = 0,
        Office = 1,
        Checkpoint = 2,
        Boardroom = 3,
        Roof = 4
    }

    /// <summary>
    /// Marks a floor prefab. Pivot at the centre of the window edge at floor level; local +Z
    /// faces the rival tower. See docs/systems/TOWER.md for the shell every template shares.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloorTemplate : MonoBehaviour
    {
        [Tooltip("What this floor is for. The tower builder places each kind where the stack says.")]
        public FloorKind Kind = FloorKind.Office;

        [Tooltip("Where players of this tower's team appear. Only read on the lobby; one per player slot, reused round-robin if there are fewer.")]
        public Transform[] SpawnPoints = new Transform[0];
    }
}
