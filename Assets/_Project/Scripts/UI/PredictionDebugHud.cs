using CloseTheDeal.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// Greybox readout for the local player's body: state, speed, how far the host's last
    /// correction moved it, and ping. Exists so the first network tests can tell "feels
    /// wrong" from "is wrong". Rebuilds its text at most ten times a second, and only when
    /// a value changed.
    /// </summary>
    public sealed class PredictionDebugHud : MonoBehaviour
    {
        struct Sample
        {
            public bool Present;
            public MoveState State;
            public bool Grounded;
            public int SpeedTenths;
            public int CorrectionCm;
            public int Corrections;
            public int PingMs;

            public bool SameAs(in Sample other)
            {
                return Present == other.Present
                    && State == other.State
                    && Grounded == other.Grounded
                    && SpeedTenths == other.SpeedTenths
                    && CorrectionCm == other.CorrectionCm
                    && Corrections == other.Corrections
                    && PingMs == other.PingMs;
            }
        }

        [SerializeField] Text _text;

        [Tooltip("How often the readout refreshes, in seconds. It only rebuilds the text when a value changed.")]
        [SerializeField] float _interval = 0.1f;

        float _nextTime;
        Sample _shown;

        void Update()
        {
            if (Time.unscaledTime < _nextTime)
                return;
            _nextTime = Time.unscaledTime + _interval;

            Sample now = Read(PlayerMotor.Local);
            if (now.SameAs(_shown))
                return;

            _shown = now;
            _text.text = Render(now);
        }

        static Sample Read(PlayerMotor motor)
        {
            if (motor == null)
                return default;

            return new Sample
            {
                Present = true,
                State = motor.State,
                Grounded = motor.Grounded,
                SpeedTenths = Mathf.RoundToInt(motor.PlanarSpeed * 10f),
                CorrectionCm = Mathf.RoundToInt(motor.LastCorrectionMetres * 100f),
                Corrections = motor.CorrectionCount,
                PingMs = (int)motor.TimeManager.RoundTripTime
            };
        }

        static string Render(in Sample s)
        {
            if (!s.Present)
                return string.Empty;

            return $"{s.State}  {(s.Grounded ? "grounded" : "in air")}  {s.SpeedTenths / 10f:0.0} m/s\n"
                 + $"last correction {s.CorrectionCm / 100f:0.00} m   corrections {s.Corrections}   ping {s.PingMs} ms";
        }
    }
}
