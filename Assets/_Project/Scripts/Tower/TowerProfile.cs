using UnityEngine;

namespace CloseTheDeal.Tower
{
    /// <summary>
    /// Designer-owned tower tuning. Runtime only reads it (AGENTS.md §4).
    /// The dimension fields are also what the greybox template builder uses, so after
    /// changing them run Close the Deal > Greybox > Rebuild Floor Templates.
    /// </summary>
    [CreateAssetMenu(menuName = "Close the Deal/Tower Profile", fileName = "TowerProfile")]
    public sealed class TowerProfile : ScriptableObject
    {
        [Header("Stack")]
        [Tooltip("How many ordinary office floors sit between the lobby and the boardroom. More = a longer run. 9 gives a 15-floor tower with the defaults, about a 12-minute match.")]
        public int RandomFloors = 9;

        [Tooltip("A checkpoint floor is inserted after every this many ordinary floors, including straight before the boardroom when the count divides evenly. 0 = no checkpoints.")]
        public int CheckpointEvery = 3;

        [Tooltip("Tick and both towers are built from the same seed, so neither team gets an easier climb. Untick for different layouts per tower.")]
        public bool SharedSeed = true;

        [Header("Dimensions (metres)")]
        [Tooltip("Floor-to-floor height. Each spiral flight climbs exactly this in one turn; above about 6 m its inner edge gets too steep to walk.")]
        public float FloorHeight = 4f;

        [Tooltip("Width of every floor along the street.")]
        public float Width = 20f;

        [Tooltip("Depth of the open main floor, from the windows back to the service spine.")]
        public float MainDepth = 10f;

        [Tooltip("Depth of the service spine at the back (WC, store, utility).")]
        public float SpineDepth = 4f;

        [Tooltip("Width of the street between the two towers' window walls. Wider = harder cross-gap shots; keep it within weapon range.")]
        public float Gap = 12f;

        [Header("Templates")]
        [Tooltip("The ground floor, with the team's spawn markers.")]
        public FloorTemplate Lobby;

        [Tooltip("Inserted every few floors; will show the rival's progress.")]
        public FloorTemplate Checkpoint;

        [Tooltip("The final fight, just under the roof.")]
        public FloorTemplate Boardroom;

        [Tooltip("The top: reach it to win.")]
        public FloorTemplate Roof;

        [Tooltip("Ordinary floors, picked at random per floor. Needs at least one.")]
        public FloorTemplate[] Offices = new FloorTemplate[0];
    }
}
