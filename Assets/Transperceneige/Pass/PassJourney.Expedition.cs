using UnityEngine;

namespace Winterbourne.Pass
{
    public sealed partial class PassJourney
    {
        public const int TargetValue = 240000;
        public const float RunDuration = 12 * 60f;
        [SerializeField, Min(0)] int carriedValue;
        public int CarriedValue => Mathf.Max(0,carriedValue);
        public int Health { get; private set; } = 100;
        public int LastImpactDamage { get; private set; }
        public float RunSeconds { get; private set; }
        public bool HasSettlement { get; private set; }
        public bool SettledSuccessfully { get; private set; }
        public bool RunExpired => Phase == JourneyPhase.TimedOut;
        double clockSample;
        bool clockStopped;

        void ResetExpedition()
        {
            carriedValue = 0;
            Health = 100;
            LastImpactDamage = 0;
            HasSettlement = false;
            SettledSuccessfully = false;
            RunSeconds = RunDuration;
            ResumeExpedition();
        }

        void ResumeExpedition()
        {
            clockStopped = false;
            clockSample = Time.realtimeSinceStartupAsDouble;
        }

        public static int GetFallDamage(float impactSpeed)
        {
            if(impactSpeed<=7f) return 0;
            // Ordinary jumps land below 5 m/s. A long, unarrested fall is lethal.
            float severity=Mathf.InverseLerp(7f,16f,impactSpeed);
            return Mathf.CeilToInt(100f*severity*severity);
        }

        void ApplyGroundImpact(float speed)
        {
            if(Terminal || InStartMenu) return;
            int damage=Mathf.Min(Health,GetFallDamage(speed));
            if(damage<=0) return;
            LastImpactDamage=damage;
            Health-=damage;
            if(Health==0) Finish(JourneyPhase.Fell);
        }

        void TickExpedition()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            float elapsed = (float)(now-clockSample);
            clockSample = now;
            if (!ManualClock) AdvanceExpedition(elapsed);
        }

        // Independent of railway simulation. The global clock also runs in both
        // rooms, in the backpack, during parachute descent and after landing.
        public void AdvanceExpedition(float seconds)
        {
            if (clockStopped || InStartMenu || !(seconds>0) || float.IsInfinity(seconds)) return;
            RunSeconds=Mathf.Max(0,RunSeconds-seconds);
            if (RunSeconds==0) Finish(JourneyPhase.TimedOut);
        }

        void LockExpedition(JourneyPhase end)
        {
            clockStopped = true;
            if (end==JourneyPhase.TimedOut || end==JourneyPhase.Fell ||
                end==JourneyPhase.Frozen || end==JourneyPhase.Crashed) Health=0;
        }

        bool SettleExpedition()
        {
            TickExpedition();
            if (!InHub || RunExpired || Terminal || RunSeconds<=0) return false;
            clockStopped = true;
            HasSettlement = true;
            SettledSuccessfully = CarriedValue >= TargetValue;
            return true;
        }
    }
}
