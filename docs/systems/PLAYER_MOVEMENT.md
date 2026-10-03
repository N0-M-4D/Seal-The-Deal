# PLAYER MOVEMENT

**Status:** Current · **Last updated:** 2026-10-03 · **Verified against:** the movement commit

## What happens in the game

You see the game in **first person** from your own eye. You walk and sprint with WASD and Shift, jump with Space, and look with the mouse; the body faces where you look. Your own body is hidden from you but still casts its shadow.
Jumps are snappy: tap Space for a short hop, hold it for a full 1.6 m jump, and you come down faster than you went up. A press just after walking off an edge, or just before landing, still jumps.
Hold **Q or E to lean** left or right on the floor: the view slides 0.45 m sideways and tilts, and stops at a wall rather than poking through it. Shots leave from the leaned eye, and other players see your body tilt and slide. Jumping or being thrown straightens you up.
Jump at a ledge up to chest height and you grab it and climb over: desks, sills, the edge of a floor.
A blast throws you through the air and you lose control for a moment. Left click fires a **test blast** at whatever is under the crosshair, so knockback can be felt before any real weapon exists.
Your own moves show the instant you press them. The host is still the judge: if it disagrees, you are nudged to where the host says you are.

## Who owns what (AGENTS.md §3)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Player body (position, velocity, state, jump timers, lean) | **Host.** The owning client predicts its own body; other clients see it about a tick behind with one tick of guessed input | Host → everyone: body state + move state, ~87 bytes | Every tick, 60/s |
| Player input (move, yaw, sprint, jump press and hold, lean side, blast) | Owning client | Client → host, ~22 bytes | Every tick, 60/s |
| Knockback | **Host only.** Clients never decide a hit | Arrives as part of the body state | On hit |
| Camera | Local only | Nothing | — |
| Aim | Owning client: direction from the eye to whatever is under its crosshair | Inside the input, 12 B | Every tick |

Bandwidth: with 4 players, the host sends each client 4 × ~80 B × 60/s ≈ **19 KB/s**. Fine on residential lines. If it ever matters, reconciles can be sent less often than every tick.

FishNet runs physics itself at 60 ticks/s (TimeManager physics mode) so predicted and authoritative simulation match. Unity's own gravity stays standard; the air feel comes from an extra pull in the profile.

## Designer-authored vs runtime-derived (AGENTS.md §4)

**Authored, never written by runtime:** every field on `MovementProfile` and `BlastProfile`; the capsule size on the Player prefab; spawn points; the Player layer.
**Derived at runtime:** jump launch speed (from jump height and air gravity); the ticks of control loss, climb, coyote and jump-buffer time left; the ledge stand point; the lean amount and the leaned eye.

Timed states count **ticks remaining** rather than an end tick, so a reconcile restores the count and the replay decrements it again; nothing depends on client and host agreeing on tick numbers. (Taken from ValhallaPVP's motor, which does the same.)

## States

| State | Enters when | Does | Leaves when |
|---|---|---|---|
| Grounded | feet within the ground probe of a floor flatter than 50° | accelerates toward the wanted direction, brakes when nothing is held; a Space press from the last 0.12 s launches a jump; Q/E lean | leaves the floor; jumps |
| Airborne | left the floor or jumped | steers at air acceleration, only while a direction is held; air gravity while rising, heavier fall gravity once falling; letting go of Space while rising keeps half the upward speed; can still jump for 0.1 s after walking off an edge | lands moving downward; grabs a ledge |
| Mantling | airborne, moving into a wall whose top is between the min and max ledge height, with room to stand | rises to ledge height, then moves onto it, at a fixed speed; gravity ignored | reaches the stand point, or times out at 2× the mantle duration |
| Knocked | the host applies a knockback | no steering; extra gravity; skids to a stop on the floor | the control-loss time passes, then Grounded or Airborne |

The blast is a host-only raycast from eye height along the aim direction the client sent (eye to the point under its crosshair), up to the profile range; it explodes where it first hits. The client chooses where it aims; the host alone decides what that hits. Everyone inside the radius, the shooter included, is thrown away from the point with some lift, weaker toward the edge.

## Scripts

Under [Assets/_Project/Scripts/](../../Assets/_Project/Scripts/):

- `Player/PlayerMotor.cs`: the predicted state machine. Replicate = one tick of the states above; Reconcile = body + state from the host. `ApplyKnockback` is the only outside way to move a player, and only the host may call it. It holds the throw until the victim's own tick applies it, last before the physics step, and the reconcile carries it so replays apply it too: a force queued from another player's tick was cleared by the victim's next velocity write, so blasts used to take control away without throwing anyone but the shooter.
- `Player/MovementProfile.cs`, `Combat/BlastProfile.cs`: tuning assets in `Assets/_Project/Profiles/`.
- `Player/PlayerInputReader.cs`: polls the Input System every frame; jump and attack presses are latched so one that lands between ticks is not lost.
- `Player/PlayerCamera.cs`: first-person camera on the Main Camera (was `ThirdPersonCamera`; same script asset, so the scene reference survived the rename). Sits at the eye on the smoothed visual, eases toward the motor's ticked lean and stops it at the building (furniture doesn't block it), gives the motor its yaw and aim direction. Mouse look only while the menu is closed (MENU_AND_HUD.md).
- Lean is predicted state on the motor: the input carries the held side, the motor moves the lean toward it each tick on the floor, and the reconcile carries it. The host fires the blast from the leaned eye, clamped against walls, so a lean never shoots from inside one. The motor's `Update` tilts and slides the `Lean` pivot under `Graphics` for everyone's view.
- `Combat/Knockback.cs`: turns a blast point into `ApplyKnockback` calls.
- `UI/PredictionDebugHud.cs`: bottom-left readout of the local body's state, speed, last correction size and ping. The motor measures a correction by remembering where prediction put the body on each tick and comparing that with the host's position for the same tick when the reconcile arrives.

Prefab: `Player.prefab` is a 1.8 m capsule with the pivot at the feet, mass 80, frictionless, rotation frozen. Its `Graphics` child is the FishNet graphical object: FishNet moves it smoothly between ticks, so visuals and camera never step. Under it, a `Lean` pivot at hip height (0.9 m) holds the body and nose; the owner's copy renders shadow-only.

## Level rules (for whoever builds floors)

- **Stairs need ramp collision.** The body is a physics capsule with no step-up; a box-stepped staircase will stop it. Put an invisible ramp collider over every staircase.
- Steps lower than the min ledge height (0.4 m) are walked over only if they are ramps too.
- Anything climbable must be on a layer in the ground mask (everything solid, not players). Props are in it, so desks can be stood on and climbed; a moving prop under your feet is host-simulated, so expect a small correction if it slides.

## Tuning

All in `Assets/_Project/Profiles/DefaultMovement.asset` and `TestBlast.asset`, each field with a tooltip saying what it changes. Defaults: walk 6 m/s, sprint 9, jump 1.6 m, air steering 20 m/s², air gravity 2× rising, 3× falling, half the upward speed kept on an early release, coyote 0.1 s, jump buffer 0.12 s, lean 0.45 m and 12° at 6 leans/s, fall capped at 30 m/s, body turns at 720°/s, ledges 0.4–1.6 m, mantle 0.35 s; blast 3 m radius, 12 m/s throw, 0.7 s control loss. The stick dead zone (0.1) sits on the Player prefab's input reader.

For comparison, ValhallaPVP's playtested baseline was ground acceleration 24 m/s², braking 32, gravity 24 m/s², turn 720°/s, at the same 60 ticks/s. Ours start sharper (45 / 60) because this is a race, not a duel; tune by feel.

## Known limitations

- No animation, no ragdoll: a knocked player is a flying capsule. Ragdoll blending is its own task.
- The test blast is not a weapon: no projectile, no ammo, no cooldown, no visual. It exists so knockback can be tuned.
- Spectators (the players you are not) guess one tick of input ahead; a sudden turn by them can show a small correction.
- No ladders or ziplines yet; climbing means mantling.
- **A leaning player's head is not hittable.** The collider stays an upright capsule, so the 0.45 m of head that pokes round a corner can see and shoot but can't be hit. Fix when real hit detection lands: a head hitbox on the lean pivot.
- No gamepad lean testing yet; it is bound to the shoulder buttons. Interact moved from E to F to free E for lean; nothing used Interact yet.
