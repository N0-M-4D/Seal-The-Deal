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

        [Tooltip("How fast the body turns to face where the camera looks, in degrees per second. 720 feels immediate; 360 shows the turn; below 180 feels sluggish.")]
        public float TurnSpeed = 720f;

        [Header("Air")]
        [Tooltip("How much the player can steer in the air, in metres per second per second. 0 = none; about a quarter of the ground value feels natural.")]
        public float AirAcceleration = 12f;

        [Tooltip("Jump height in metres on flat ground.")]
        public float JumpHeight = 1.3f;

        [Tooltip("Pull-down while rising from a jump and all through a blast flight, as a multiple of normal gravity. 1 = floaty real-world arcs; 2 = snappy game jumps. Also shapes how far a blast throws you. The way down from a jump uses Fall Gravity Multiplier instead.")]
        public float AirGravityMultiplier = 2f;

        [Tooltip("Fastest a player can fall, in metres per second. Caps long drops and big blasts. 30 is a hard but readable fall; 0 = no cap.")]
        public float MaxFallSpeed = 30f;

        [Header("Jump feel")]
        [Tooltip("Pull-down on the way down from a jump, as a multiple of normal gravity. Replaces the air gravity once the jump peaks. Equal to air gravity = symmetric arc; higher = quick, punchy landings. Doesn't change blasts.")]
        public float FallGravityMultiplier = 3f;

        [Tooltip("Fraction of upward speed kept when Space is let go early, so a tap is a short hop and a hold is a full jump. 1 = every jump is full height; 0.5 = a tap reaches about a quarter of the height.")]
        [Range(0f, 1f)] public float JumpReleaseKeep = 0.5f;

        [Tooltip("How long after walking off an edge a jump still works, in seconds. 0.1 forgives a late press without feeling like a double jump.")]
        public float CoyoteTime = 0.1f;

        [Tooltip("How early before landing a jump press is remembered and fired on touchdown, in seconds. 0.12 makes chained hops easy; 0 = must press on the ground.")]
        public float JumpBufferTime = 0.12f;

        [Header("Lean")]
        [Tooltip("How far the head slides sideways at full lean, in metres. 0.45 clears a door frame from a body tucked behind it. Shots leave from the leaned eye.")]
        public float LeanDistance = 0.45f;

        [Tooltip("How far the view tilts at full lean, in degrees. 12 reads clearly; above 20 gets disorienting.")]
        public float LeanAngle = 12f;

        [Tooltip("How fast the lean goes in and out, in full leans per second. 6 = about a sixth of a second to full lean.")]
        public float LeanSpeed = 6f;

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
