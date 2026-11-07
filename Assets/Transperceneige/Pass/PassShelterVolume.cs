using UnityEngine;

namespace Winterbourne.Pass
{
    // Assign to indoor weather trigger boxes. The emitter kills only flakes inside
    // these volumes, so the storm remains visible through the station and depot windows.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PassShelterVolume : MonoBehaviour { }
}
