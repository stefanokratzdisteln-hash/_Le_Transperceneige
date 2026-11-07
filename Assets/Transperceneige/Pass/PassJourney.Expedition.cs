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
            HasSettlement = false;
            SettledSuccessfully = false;
            RunSeconds = RunDuration;
            ResumeExpedition();
        }

        void ResumeExpedition()
        {
            clockStopped = false;
            Health = 100;
            clockSample = Time.realtimeSinceStartupAsDouble;
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
