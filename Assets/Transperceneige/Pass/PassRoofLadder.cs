using UnityEngine;

namespace Winterbourne.Pass
{
    // Ordered foot positions. The two middle points lie on the climb line.
    public sealed class PassRoofLadder : MonoBehaviour
    {
        public Transform lowerLanding, lowerRung, upperRung, roofLanding;

        public Transform StopAt(int index)
        {
            switch (index)
            {
                case 0: return lowerLanding;
                case 1: return lowerRung;
                case 2: return upperRung;
                default: return roofLanding;
            }
        }

        public bool IsReady => lowerLanding && lowerRung && upperRung && roofLanding;
    }
}
