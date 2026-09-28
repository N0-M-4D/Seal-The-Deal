using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Designer-owned movement tuning. Runtime only reads it (AGENTS.md §4).
    /// </summary>
    [CreateAssetMenu(menuName = "Close the Deal/Movement Profile", fileName = "MovementProfile")]
    public sealed class MovementProfile : ScriptableObject
    {
        [Header("Ground")]
        [Tooltip("Walking speed in metres per second. Higher = faster across a floor. 6 is a brisk walk.")]
        public float WalkSpeed = 6f;

        [Tooltip("Speed in metres per second while Sprint is held.")]
        public float SprintSpeed = 9f;

        [Tooltip("How quickly the player gets up to speed on the floor, in metres per second per second. Higher = snappier starts; above about 80 feels twitchy.")]
        public float GroundAcceleration = 45f;

        [Tooltip("How quickly the player stops on the floor when no direction is held, in metres per second per second. Higher = stops dead; lower = slides.")]
        public float GroundBraking = 60f;

        [Tooltip("How quickly a knocked-down player skids to a stop once on the floor, in metres per second per second. Lower = longer comedy slide.")]
        public float KnockedBraking = 15f;

        [Header("Air")]
        [Tooltip("How much the player can steer in the air, in metres per second per second. 0 = none; about a quarter of the ground value feels natural.")]
        public float AirAcceleration = 12f;

        [Tooltip("Jump height in metres on flat ground.")]
        public float JumpHeight = 1.3f;

        [Tooltip("Extra pull-down while airborne, as a multiple of normal gravity. 1 = floaty real-world arcs; 2 = snappy game jumps. Also shapes how far a blast throws you.")]
        public float AirGravityMultiplier = 2f;

        [Header("Climbing")]
        [Tooltip("Highest ledge the player can climb onto, in metres above their feet. Keep below the player's height (1.8) or they climb things they can't reach.")]
        public float MaxLedgeHeight = 1.6f;

        [Tooltip("Lowest edge that counts as a climb, in metres above the feet. Anything lower must be a ramp to be walked over.")]
        public float MinLedgeHeight = 0.4f;

        [Tooltip("How far in front of the body a ledge can be and still be grabbed, in metres.")]
        public float LedgeReach = 0.7f;

        [Tooltip("How long the tallest climb takes from grab to standing on top, in seconds. Shorter climbs finish sooner.")]
        public float MantleDuration = 0.35f;

        [Header("Ground check")]
        [Tooltip("How far below the feet still counts as touching the floor, in metres. Keep about 0.1: higher and the player 'lands' before reaching it.")]
        public float GroundProbe = 0.1f;
    }
}
