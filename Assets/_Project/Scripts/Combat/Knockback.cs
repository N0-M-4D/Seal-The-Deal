using CloseTheDeal.Player;
using UnityEngine;

namespace CloseTheDeal.Combat
{
    /// <summary>
    /// Turns a blast at a point into knockback on every player in range.
    /// Host only: PlayerMotor.ApplyKnockback ignores the call anywhere else.
    /// </summary>
    public static class Knockback
    {
        /// <summary>Throws every player within the profile's radius of the point. Returns how many were hit.</summary>
        public static int Explode(Vector3 point, BlastProfile blast, LayerMask playerMask, Collider[] buffer)
        {
            int found = Physics.OverlapSphereNonAlloc(point, blast.Radius, buffer, playerMask, QueryTriggerInteraction.Ignore);
            int hit = 0;

            for (int i = 0; i < found; i++)
            {
                PlayerMotor motor = buffer[i].GetComponentInParent<PlayerMotor>();
                if (motor == null)
                    continue;

                Vector3 chest = motor.transform.position + Vector3.up * blast.PushHeight;
                Vector3 away = chest - point;
                float distance = away.magnitude;

                Vector3 direction = distance > 0.01f ? away / distance : Vector3.up;
                direction = (direction + Vector3.up * blast.UpBias).normalized;

                float closeness = 1f - Mathf.Clamp01(distance / blast.Radius);
                float strength = Mathf.Lerp(blast.EdgeStrength, 1f, closeness);

                motor.ApplyKnockback(direction * (blast.Speed * strength), blast.ControlLossSeconds);
                hit++;
            }

            return hit;
        }
    }
}
