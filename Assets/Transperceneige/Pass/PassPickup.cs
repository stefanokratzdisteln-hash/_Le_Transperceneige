using UnityEngine;

namespace Winterbourne.Pass
{
    public sealed class PassPickup : MonoBehaviour
    {
        public string itemId = "parachute";
        public string label = "帆布跳伞包";
        [Tooltip("Unique, persistent identity of this authored pickup position.")]
        public string siteId;
        [Tooltip("One candidate in each nonempty group is chosen per new expedition.")]
        public string rollGroup;
        [Tooltip("Optional safe bedside arrival for this opening watch candidate.")]
        public Transform bedsideArrival;
        [System.NonSerialized] internal int currentSite = -1;
    }
}
