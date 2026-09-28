using UnityEngine;

namespace CloseTheDeal.Combat
{
    /// <summary>
    /// Designer-owned tuning for a knockback blast. Runtime only reads it (AGENTS.md §4).
    /// Used by the greybox test blast now; real weapons will each carry one.
    /// </summary>
    [CreateAssetMenu(menuName = "Close the Deal/Blast Profile", fileName = "BlastProfile")]
    public sealed class BlastProfile : ScriptableObject
    {
        [Tooltip("How far ahead the blast reaches, in metres. It explodes where it first hits something, or at this distance.")]
        public float Range = 30f;

        [Tooltip("Blast radius in metres. Every player inside it is thrown, the shooter included.")]
        public float Radius = 3f;

        [Tooltip("How fast a player at the centre of the blast is thrown, in metres per second. 12 sends them a few metres; 20 clears a room.")]
        public float Speed = 12f;

        [Tooltip("How much of the throw goes upward, as a fraction of the sideways push. 0 = straight back; 1 = as much up as back. Some lift makes it read as an explosion.")]
        public float UpBias = 0.6f;

        [Tooltip("How much of the throw a player at the very edge of the radius still gets, as a fraction. 1 = full strength everywhere inside.")]
        [Range(0f, 1f)] public float EdgeStrength = 0.4f;

        [Tooltip("How long a thrown player can't steer, in seconds from the hit.")]
        public float ControlLossSeconds = 0.7f;

        [Tooltip("How fast furniture at the centre of the blast is thrown, in metres per second, before each prop's own resistance. 10 scatters chairs; 20 empties the room.")]
        public float PropSpeed = 10f;

        [Tooltip("Height above the feet the blast pushes from, in metres. Chest height (0.9) tumbles; lower lifts more.")]
        public float PushHeight = 0.9f;

        [Tooltip("Height above the feet the shot leaves from, in metres. Eye level on a 1.8 m body is about 1.6.")]
        public float EyeHeight = 1.6f;
    }
}
