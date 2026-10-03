using CloseTheDeal.Combat;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using UnityEngine;

namespace CloseTheDeal.Player
{
    public enum MoveState : byte
    {
        Grounded = 0,
        Airborne = 1,
        Mantling = 2,
        Knocked = 3
    }

    /// <summary>
    /// The player's body: walking, jumping, mantling ledges and being knocked about.
    /// Host-authoritative with client prediction (FishNet): the owner runs the same tick
    /// logic ahead of the host and is corrected by the host's reconcile when they differ.
    /// Design and ownership: docs/systems/PLAYER_MOVEMENT.md.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(PlayerInputReader))]
    public sealed class PlayerMotor : TickNetworkBehaviour
    {
        // ---- Replicated data ---------------------------------------------------------------

        public struct OneShots
        {
            public bool Jump;
            public bool Attack;

            public void Clear()
            {
                Jump = false;
                Attack = false;
            }
        }

        /// <summary>One tick of input from the owner. Sent owner → host every tick.</summary>
        public struct MoveInput : IReplicateData
        {
            public Vector2 Move;
            public float Yaw;
            /// <summary>Unit direction from the player's eye to whatever was under the crosshair. Only shots use it.</summary>
            public Vector3 Aim;
            public bool Sprint;
            /// <summary>Jump is held down; letting go while rising cuts the jump short.</summary>
            public bool JumpHeld;
            /// <summary>-1 lean left, +1 lean right, 0 upright.</summary>
            public sbyte Lean;
            public OneShots OneShots;
            uint _tick;

            public MoveInput(Vector2 move, float yaw, Vector3 aim, bool sprint, bool jumpHeld, sbyte lean, OneShots oneShots)
            {
                Move = move;
                Yaw = yaw;
                Aim = aim;
                Sprint = sprint;
                JumpHeld = jumpHeld;
                Lean = lean;
                OneShots = oneShots;
                _tick = 0;
            }

            public void Dispose() => OneShots.Clear();
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        /// <summary>The host's word on where the body is. Sent host → everyone every tick.</summary>
        public struct MoveReconcile : IReconcileData
        {
            public PredictionRigidbody Body;
            public MoveState State;
            public uint StateTicksLeft;
            public Vector3 MantleTarget;
            public byte MantlePhase;
            /// <summary>A throw the host has decided but the body's next tick has not applied yet.</summary>
            public Vector3 PendingKnock;
            public JumpTimers Jump;
            /// <summary>-1 to +1: how far the player is leaning, and which way.</summary>
            public float Lean;
            uint _tick;

            public MoveReconcile(PredictionRigidbody body, MoveState state, uint stateTicksLeft, Vector3 mantleTarget, byte mantlePhase, Vector3 pendingKnock, JumpTimers jump, float lean)
            {
                Body = body;
                State = state;
                StateTicksLeft = stateTicksLeft;
                MantleTarget = mantleTarget;
                MantlePhase = mantlePhase;
                PendingKnock = pendingKnock;
                Jump = jump;
                Lean = lean;
                _tick = 0;
            }

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        /// <summary>The jump forgiveness windows, counted in ticks remaining so a reconcile restores them exactly.</summary>
        public struct JumpTimers
        {
            /// <summary>Ticks left in which a jump still works after walking off an edge.</summary>
            public byte Coyote;
            /// <summary>Ticks left in which an early press still fires on landing.</summary>
            public byte Buffer;
            /// <summary>Still rising from a jump that letting go of Space can cut short.</summary>
            public bool Rising;
        }

        struct TickPosition
        {
            public uint Tick;
            public Vector3 Position;
        }

        // ---- Inspector -----------------------------------------------------------------------

        [Tooltip("Designer tuning for walking, jumping and climbing. Read at runtime, never written.")]
        [SerializeField] MovementProfile _profile;

        [Tooltip("Tuning for the greybox test blast on left click. A placeholder until real weapons exist.")]
        [SerializeField] BlastProfile _blast;

        [Tooltip("What counts as floor, wall and ledge: everything solid in the level, but not players.")]
        [SerializeField] LayerMask _groundMask = ~0;

        [Tooltip("What the test blast can throw: the Player layer.")]
        [SerializeField] LayerMask _playerMask;

        [Tooltip("Furniture the test blast can throw: the Prop layer.")]
        [SerializeField] LayerMask _propMask;

        [Tooltip("The smoothed visual the camera follows. FishNet moves this child between ticks so motion never steps.")]
        [SerializeField] Transform _cameraTarget;

        [Tooltip("The part of the visual that tilts and slides when this player leans, pivoting at the hips, so other players see the peek. Built by the greybox tool; leave empty and leaning still works, it just isn't shown on the body.")]
        [SerializeField] Transform _leanPivot;

        // ---- Runtime -------------------------------------------------------------------------

        const float GroundSlopeLimit = 50f;
        const float MantleTimeoutMultiplier = 2f;
        const float ArriveDistance = 0.05f;
        const float ProbeInset = 0.05f;
        const float CorrectionNoticeMetres = 0.01f;
        const byte MantleRise = 0;
        const byte MantleStepOnto = 1;
        const int HistoryLength = 256;
        const float LeanShotRadius = 0.1f;

        static readonly Collider[] BlastBuffer = new Collider[32];

        /// <summary>The body this client controls, for local readouts. Null on a pure host with no player.</summary>
        public static PlayerMotor Local { get; private set; }

        readonly PredictionRigidbody _body = new();
        readonly TickPosition[] _history = new TickPosition[HistoryLength];
        Rigidbody _rigidbody;
        CapsuleCollider _capsule;
        PlayerInputReader _input;

        MoveState _state;
        uint _stateTicksLeft;
        Vector3 _mantleTarget;
        byte _mantlePhase;
        Vector3 _pendingKnock;
        JumpTimers _jump;
        float _lean;
        float _shownLean;
        Vector3 _leanPivotRest;
        Renderer[] _ownRenderers;
        uint _currentTick;
        MoveInput _lastTickedInput;
        bool _grounded;

        public MoveState State => _state;
        public bool Grounded => _grounded;
        public float PlanarSpeed => Planar(_rigidbody.linearVelocity).magnitude;
        public MovementProfile Profile => _profile;

        /// <summary>-1 to +1: how far this player is leaning, and which way. Moves once per tick.</summary>
        public float Lean => _lean;

        /// <summary>Height of the eye above the feet, in metres. Shots leave from here; the first-person camera sits here.</summary>
        public float EyeHeight => _blast.EyeHeight;

        /// <summary>How far the host's last correction moved this body from where it predicted itself, in metres.</summary>
        public float LastCorrectionMetres { get; private set; }

        /// <summary>How many corrections above a centimetre have arrived since spawn.</summary>
        public int CorrectionCount { get; private set; }

        // ---- Lifecycle -----------------------------------------------------------------------

        void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _capsule = GetComponent<CapsuleCollider>();
            _input = GetComponent<PlayerInputReader>();
            _input.enabled = false;
            _body.Initialize(_rigidbody);
            _ownRenderers = _cameraTarget.GetComponentsInChildren<Renderer>(true);
            if (_leanPivot != null)
                _leanPivotRest = _leanPivot.localPosition;
        }

        public override void OnStartNetwork()
        {
            SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner)
                return;

            Local = this;
            _input.enabled = true;
            ShowOwnBody(false);
            if (PlayerCamera.Instance != null)
                PlayerCamera.Instance.Follow(_cameraTarget, _input, this);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!IsOwner)
                return;

            if (Local == this)
                Local = null;
            ShowOwnBody(true);
            if (PlayerCamera.Instance != null)
                PlayerCamera.Instance.Release(_cameraTarget);
        }

        /// <summary>The first-person camera sits inside the body, so the owner sees only its shadow.</summary>
        void ShowOwnBody(bool visible)
        {
            var mode = visible ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            foreach (Renderer part in _ownRenderers)
                part.shadowCastingMode = mode;
        }

        /// <summary>Visual only: eases the hip pivot toward the ticked lean so every client sees the peek without stepping.</summary>
        void Update()
        {
            if (_leanPivot == null || _shownLean == _lean)
                return;

            _shownLean = Mathf.MoveTowards(_shownLean, _lean, _profile.LeanSpeed * Time.deltaTime);
            float tilt = _profile.LeanAngle * Mathf.Deg2Rad;
            float headAbovePivot = EyeHeight - _leanPivotRest.y;
            // Slide whatever the tilt alone doesn't cover, so the head lands where the owner's eye is.
            float slide = _profile.LeanDistance - headAbovePivot * Mathf.Sin(tilt);
            _leanPivot.localPosition = _leanPivotRest + Vector3.right * (slide * _shownLean);
            _leanPivot.localRotation = Quaternion.Euler(0f, 0f, -_shownLean * _profile.LeanAngle);
        }

        // ---- Ticks ---------------------------------------------------------------------------

        protected override void TimeManager_OnTick()
        {
            RunMove(BuildInput());
        }

        protected override void TimeManager_OnPostTick()
        {
            // Physics has stepped for this tick; remember where prediction put the body.
            _history[_currentTick % HistoryLength] = new TickPosition { Tick = _currentTick, Position = _rigidbody.position };
            CreateReconcile();
        }

        MoveInput BuildInput()
        {
            if (!IsOwner)
                return default;

            PlayerCamera cam = PlayerCamera.Instance;
            float yaw = cam != null ? cam.Yaw : _rigidbody.rotation.eulerAngles.y;
            Vector3 eye = EyePosition(yaw);
            Vector3 aim = cam != null ? cam.AimDirectionFrom(eye) : Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var oneShots = new OneShots { Jump = _input.ConsumeJump(), Attack = _input.ConsumeAttack() };
            return new MoveInput(_input.Move, yaw, aim, _input.Sprint, _input.JumpHeld, _input.Lean, oneShots);
        }

        public override void CreateReconcile()
        {
            RunReconcile(new MoveReconcile(_body, _state, _stateTicksLeft, _mantleTarget, _mantlePhase, _pendingKnock, _jump, _lean));
        }

        [Replicate]
        void RunMove(MoveInput input, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            input = PredictSpectatorInput(input, state);
            if (!state.ContainsReplayed())
                _currentTick = input.GetTick();

            float dt = (float)TimeManager.TickDelta;
            _grounded = ProbeGround();

            // Remember the press for a moment, so one made just before landing still jumps.
            if (input.OneShots.Jump)
                _jump.Buffer = (byte)(TicksFor(_profile.JumpBufferTime) + 1);

            switch (_state)
            {
                case MoveState.Grounded: GroundedStep(input, dt); break;
                case MoveState.Airborne: AirborneStep(input, dt); break;
                case MoveState.Mantling: MantleStep(dt); break;
                case MoveState.Knocked: KnockedStep(dt); break;
            }

            // After any velocity write: setting velocity clears queued forces, and this must survive.
            float yaw = Mathf.MoveTowardsAngle(_rigidbody.rotation.eulerAngles.y, input.Yaw, _profile.TurnSpeed * dt);
            _body.MoveRotation(Quaternion.Euler(0f, yaw, 0f));

            // Fires from the lean the owner aimed with, before this tick moves it.
            if (input.OneShots.Attack && IsServerStarted)
                FireTestBlast(input.Aim, input.Yaw);

            LeanStep(input, dt);
            if (_jump.Buffer > 0)
                _jump.Buffer--;

            ApplyPendingKnock();
            _body.Simulate();
        }

        [Reconcile]
        void RunReconcile(MoveReconcile data, Channel channel = Channel.Unreliable)
        {
            _body.Reconcile(data.Body);
            _state = data.State;
            _stateTicksLeft = data.StateTicksLeft;
            _mantleTarget = data.MantleTarget;
            _mantlePhase = data.MantlePhase;
            _pendingKnock = data.PendingKnock;
            _jump = data.Jump;
            _lean = data.Lean;

            if (!IsServerStarted)
                MeasureCorrection(data.GetTick());
        }

        /// <summary>Compares the host's position for a tick with where prediction had put the body on that tick.</summary>
        void MeasureCorrection(uint tick)
        {
            TickPosition predicted = _history[tick % HistoryLength];
            if (predicted.Tick != tick)
                return;

            float metres = Vector3.Distance(predicted.Position, _rigidbody.position);
            if (metres < CorrectionNoticeMetres)
                return;

            LastCorrectionMetres = metres;
            CorrectionCount++;
        }

        /// <summary>
        /// On clients that don't own this body, guess one tick ahead by repeating the last
        /// real input (minus presses), as FishNet's own demos do. Beyond that, no input.
        /// </summary>
        MoveInput PredictSpectatorInput(MoveInput input, ReplicateState state)
        {
            if (IsServerStarted || IsOwner)
                return input;

            if (state.ContainsTicked())
            {
                _lastTickedInput = input;
                return input;
            }

            if (!state.IsFuture())
                return input;

            MoveInput guess = input.GetTick() - _lastTickedInput.GetTick() > 1 ? default : _lastTickedInput;
            guess.OneShots.Clear();
            guess.SetTick(input.GetTick());
            return guess;
        }

        // ---- States --------------------------------------------------------------------------

        void GroundedStep(MoveInput input, float dt)
        {
            if (!_grounded)
            {
                Enter(MoveState.Airborne);
                AirborneStep(input, dt);
                return;
            }

            _jump.Coyote = TicksFor(_profile.CoyoteTime);

            Vector3 wish = WishVelocity(input);
            float rate = wish.sqrMagnitude > 0.01f ? _profile.GroundAcceleration : _profile.GroundBraking;
            Vector3 planar = Vector3.MoveTowards(Planar(_rigidbody.linearVelocity), wish, rate * dt);

            if (_jump.Buffer > 0)
            {
                Jump(planar);
                return;
            }

            _body.Velocity(new Vector3(planar.x, Mathf.Min(_rigidbody.linearVelocity.y, 0f), planar.z));
        }

        /// <summary>Launches at full jump speed, keeping the planar speed the player already has.</summary>
        void Jump(Vector3 planar)
        {
            Enter(MoveState.Airborne);
            _jump = new JumpTimers { Rising = true };
            _body.Velocity(new Vector3(planar.x, JumpSpeed(), planar.z));
        }

        void AirborneStep(MoveInput input, float dt)
        {
            Vector3 velocity = _rigidbody.linearVelocity;
            if (_grounded && velocity.y <= 0f)
            {
                Enter(MoveState.Grounded);
                GroundedStep(input, dt);
                return;
            }

            // Coyote time: just walked off an edge, so a jump still counts as from the floor.
            if (_jump.Buffer > 0 && _jump.Coyote > 0)
            {
                Jump(Planar(velocity));
                return;
            }

            if (_jump.Coyote > 0)
                _jump.Coyote--;

            if (TryStartMantle(input))
                return;

            Vector3 wish = WishVelocity(input);
            Vector3 planar = Planar(velocity);
            if (wish.sqrMagnitude > 0.01f)
                planar = Vector3.MoveTowards(planar, wish, _profile.AirAcceleration * dt);

            float vertical = CutJumpOnRelease(velocity.y, input.JumpHeld);
            _body.Velocity(new Vector3(planar.x, ClampFall(vertical), planar.z));
            AddExtraGravity(vertical > 0f ? _profile.AirGravityMultiplier : _profile.FallGravityMultiplier);
        }

        /// <summary>Letting go of jump while still rising keeps only part of the upward speed: a tap hops, a hold leaps. Once per jump.</summary>
        float CutJumpOnRelease(float vertical, bool jumpHeld)
        {
            if (!_jump.Rising)
                return vertical;

            if (vertical <= 0f)
            {
                _jump.Rising = false;
                return vertical;
            }

            if (jumpHeld)
                return vertical;

            _jump.Rising = false;
            return vertical * _profile.JumpReleaseKeep;
        }

        void MantleStep(float dt)
        {
            if (_stateTicksLeft == 0)
            {
                Enter(MoveState.Airborne);
                return;
            }

            _stateTicksLeft--;

            // Fixed pace: the tallest climb takes the profile duration, shorter ones less.
            float pace = (_profile.MaxLedgeHeight + _profile.LedgeReach + _capsule.radius) / _profile.MantleDuration;
            float cancelGravity = -Physics.gravity.y * dt;
            Vector3 position = _rigidbody.position;

            if (_mantlePhase == MantleRise)
            {
                float rise = _mantleTarget.y + ProbeInset - position.y;
                if (rise > ArriveDistance)
                {
                    _body.Velocity(Vector3.up * (Mathf.Min(pace, rise / dt) + cancelGravity));
                    return;
                }

                _mantlePhase = MantleStepOnto;
            }

            Vector3 across = Planar(_mantleTarget - position);
            float remaining = across.magnitude;
            if (remaining <= ArriveDistance)
            {
                Enter(MoveState.Grounded);
                _body.Velocity(Vector3.zero);
                return;
            }

            Vector3 step = across / remaining * Mathf.Min(pace, remaining / dt);
            _body.Velocity(new Vector3(step.x, cancelGravity, step.z));
        }

        void KnockedStep(float dt)
        {
            Vector3 velocity = _rigidbody.linearVelocity;
            if (_grounded)
            {
                Vector3 planar = Vector3.MoveTowards(Planar(velocity), Vector3.zero, _profile.KnockedBraking * dt);
                _body.Velocity(new Vector3(planar.x, velocity.y, planar.z));
            }
            else
            {
                _body.Velocity(new Vector3(velocity.x, ClampFall(velocity.y), velocity.z));
                AddExtraGravity(_profile.AirGravityMultiplier);
            }

            if (_stateTicksLeft > 0)
                _stateTicksLeft--;
            if (_stateTicksLeft == 0)
                Enter(_grounded ? MoveState.Grounded : MoveState.Airborne);
        }

        /// <summary>
        /// Throws the player and takes their control away for a while. Host only: the host
        /// is the sole judge of hits, and the result reaches everyone through the reconcile.
        /// The throw is held until this body's own tick applies it: a blast usually lands
        /// during another player's tick, and any force queued then would be cleared by this
        /// body's next velocity write before physics ever saw it.
        /// </summary>
        public void ApplyKnockback(Vector3 velocityChange, float controlLossSeconds)
        {
            if (!IsServerStarted)
                return;

            _pendingKnock += velocityChange;
            Enter(MoveState.Knocked);
            _stateTicksLeft = System.Math.Max(1u, TimeManager.TimeToTicks(controlLossSeconds, TickRounding.RoundUp));
        }

        /// <summary>Last thing before the physics step, so no velocity write this tick can clear it.</summary>
        void ApplyPendingKnock()
        {
            if (_pendingKnock == Vector3.zero)
                return;

            _body.AddForce(_pendingKnock, ForceMode.VelocityChange);
            _pendingKnock = Vector3.zero;
        }

        // ---- Lean ----------------------------------------------------------------------------

        /// <summary>Leans toward the held side at the profile pace; only on the floor, so a jump or a blast straightens you up.</summary>
        void LeanStep(MoveInput input, float dt)
        {
            float target = _state == MoveState.Grounded ? Mathf.Clamp(input.Lean, -1, 1) : 0f;
            _lean = Mathf.MoveTowards(_lean, target, _profile.LeanSpeed * dt);
        }

        /// <summary>The eye, slid sideways by the current lean but never through a wall. Shots leave from here.</summary>
        Vector3 EyePosition(float yaw)
        {
            Vector3 head = _rigidbody.position + Vector3.up * EyeHeight;
            float reach = _profile.LeanDistance * Mathf.Abs(_lean);
            if (reach < 0.001f)
                return head;

            Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * Vector3.right * Mathf.Sign(_lean);
            if (Physics.SphereCast(head, LeanShotRadius, direction, out RaycastHit wall, reach, _groundMask, QueryTriggerInteraction.Ignore))
                reach = wall.distance;

            return head + direction * reach;
        }

        // ---- Mantling ------------------------------------------------------------------------

        bool TryStartMantle(MoveInput input)
        {
            if (input.Move.sqrMagnitude < 0.25f)
                return false;

            if (!FindLedge(WishDirection(input).normalized, out Vector3 standPoint))
                return false;

            Enter(MoveState.Mantling);
            _mantleTarget = standPoint;
            _stateTicksLeft = TimeManager.TimeToTicks(_profile.MantleDuration * MantleTimeoutMultiplier, TickRounding.RoundUp);
            _body.Velocity(Vector3.zero);
            return true;
        }

        /// <summary>A wall ahead whose top is between the min and max ledge height, with room to stand on it.</summary>
        bool FindLedge(Vector3 direction, out Vector3 standPoint)
        {
            standPoint = default;
            Vector3 feet = _rigidbody.position;
            float radius = _capsule.radius;

            Vector3 wallProbe = feet + Vector3.up * (_profile.MinLedgeHeight + ProbeInset);
            if (!Physics.Raycast(wallProbe, direction, out RaycastHit wall, radius + _profile.LedgeReach, _groundMask, QueryTriggerInteraction.Ignore))
                return false;

            float topOfReach = _profile.MaxLedgeHeight + radius;
            Vector3 above = new Vector3(wall.point.x, feet.y + topOfReach, wall.point.z) + direction * (radius + ProbeInset);
            if (!Physics.Raycast(above, Vector3.down, out RaycastHit top, topOfReach - _profile.MinLedgeHeight, _groundMask, QueryTriggerInteraction.Ignore))
                return false;

            float height = top.point.y - feet.y;
            if (height < _profile.MinLedgeHeight || height > _profile.MaxLedgeHeight)
                return false;

            if (Vector3.Angle(top.normal, Vector3.up) > GroundSlopeLimit)
                return false;

            Vector3 lowSphere = top.point + Vector3.up * (radius + ProbeInset);
            Vector3 highSphere = top.point + Vector3.up * (_capsule.height - radius);
            if (Physics.CheckCapsule(lowSphere, highSphere, radius - ProbeInset, _groundMask, QueryTriggerInteraction.Ignore))
                return false;

            standPoint = top.point;
            return true;
        }

        // ---- Blast ---------------------------------------------------------------------------

        /// <summary>Fires from the eye toward what the owner had under the crosshair; a missing aim falls back to straight ahead.</summary>
        void FireTestBlast(Vector3 aim, float yaw)
        {
            Vector3 origin = EyePosition(yaw);
            Vector3 direction = aim.sqrMagnitude > 0.5f ? aim.normalized : Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            int mask = _groundMask | _playerMask;

            Vector3 point = Physics.Raycast(origin, direction, out RaycastHit hit, _blast.Range, mask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : origin + direction * _blast.Range;

            Knockback.Explode(point, _blast, _playerMask, _propMask, BlastBuffer);
        }

        // ---- Helpers -------------------------------------------------------------------------

        bool ProbeGround()
        {
            float radius = _capsule.radius - ProbeInset;
            Vector3 origin = _rigidbody.position + Vector3.up * (_capsule.radius + ProbeInset);
            float distance = ProbeInset * 2f + _profile.GroundProbe;

            if (!Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, distance, _groundMask, QueryTriggerInteraction.Ignore))
                return false;

            return Vector3.Angle(hit.normal, Vector3.up) <= GroundSlopeLimit;
        }

        Vector3 WishDirection(MoveInput input)
        {
            Vector3 planar = Quaternion.Euler(0f, input.Yaw, 0f) * new Vector3(input.Move.x, 0f, input.Move.y);
            return planar.sqrMagnitude > 1f ? planar.normalized : planar;
        }

        Vector3 WishVelocity(MoveInput input)
        {
            return WishDirection(input) * (input.Sprint ? _profile.SprintSpeed : _profile.WalkSpeed);
        }

        float JumpSpeed()
        {
            return Mathf.Sqrt(2f * -Physics.gravity.y * _profile.AirGravityMultiplier * _profile.JumpHeight);
        }

        float ClampFall(float verticalSpeed)
        {
            return _profile.MaxFallSpeed > 0f ? Mathf.Max(verticalSpeed, -_profile.MaxFallSpeed) : verticalSpeed;
        }

        /// <summary>Tops Unity's own gravity up to the given multiple of it.</summary>
        void AddExtraGravity(float multiplier)
        {
            if (multiplier > 1f)
                _body.AddForce(Physics.gravity * (multiplier - 1f), ForceMode.Acceleration);
        }

        byte TicksFor(float seconds)
        {
            if (seconds <= 0f)
                return 0;

            return (byte)System.Math.Min(TimeManager.TimeToTicks(seconds, TickRounding.RoundUp), 254u);
        }

        void Enter(MoveState next)
        {
            _state = next;
            _stateTicksLeft = 0;
            _mantlePhase = MantleRise;
            _jump.Rising = false;
            // Only walking off an edge keeps coyote time; a blast or a climb never hands out a free jump.
            if (next != MoveState.Airborne)
                _jump.Coyote = 0;
        }

        static Vector3 Planar(Vector3 v) => new(v.x, 0f, v.z);
    }
}
