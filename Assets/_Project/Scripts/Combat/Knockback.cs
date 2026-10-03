using CloseTheDeal.Player;
using CloseTheDeal.Props;
using CloseTheDeal.Tower;
using UnityEngine;

namespace CloseTheDeal.Combat
{
    /// <summary>
    /// Turns a blast at a point into knockback on every player, throw on every prop and a hit
    /// on every locked door in range. Host only: PlayerMotor.ApplyKnockback, Prop.Throw and
    /// BreakableDoor.TakeBlast ignore the call anywhere else.
    /// </summary>
    public static class Knockback
    {
        /// <summary>Throws every player and prop within the profile's radius of the point. Returns how many players were hit.</summary>
        public static int Explode(Vector3 point, BlastProfile blast, LayerMask playerMask, LayerMask propMask, Collider[] buffer)
        {
            int found = Physics.OverlapSphereNonAlloc(point, blast.Radius, buffer, playerMask | propMask, QueryTriggerInteraction.Ignore);
            int playersHit = 0;

            for (int i = 0; i < found; i++)
            {
                Collider hit = buffer[i];

                PlayerMotor motor = hit.GetComponentInParent<PlayerMotor>();
                if (motor != null)
                {
                    Vector3 chest = motor.transform.position + Vector3.up * blast.PushHeight;
                    motor.ApplyKnockback(ThrowVelocity(point, chest, blast, blast.Speed), blast.ControlLossSeconds);
                    playersHit++;
                    continue;
                }

                BreakableDoor door = hit.GetComponentInParent<BreakableDoor>();
                if (door != null)
                {
                    door.TakeBlast();
                    continue;
                }

                Prop prop = hit.GetComponentInParent<Prop>();
                if (prop != null)
                    prop.Throw(ThrowVelocity(point, hit.bounds.center, blast, blast.PropSpeed));
            }

            return playersHit;
        }

        /// <summary>Away from the blast with some lift, full speed at the centre, EdgeStrength of it at the edge.</summary>
        static Vector3 ThrowVelocity(Vector3 point, Vector3 target, BlastProfile blast, float speed)
        {
            Vector3 away = target - point;
            float distance = away.magnitude;

            Vector3 direction = distance > 0.01f ? away / distance : Vector3.up;
            direction = (direction + Vector3.up * blast.UpBias).normalized;

            float closeness = 1f - Mathf.Clamp01(distance / blast.Radius);
            float strength = Mathf.Lerp(blast.EdgeStrength, 1f, closeness);
            return direction * (speed * strength);
        }
    }
}
