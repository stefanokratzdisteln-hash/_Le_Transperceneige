using System;
using UnityEngine;

namespace Winterbourne.Pass
{
    // Both hand meshes have their origins at the bearing. The markers describe
    // the dial plane in the imported model, including the FBX axis conversion.
    public sealed class PassMysteryClock : MonoBehaviour
    {
        [SerializeField] Transform hourHand;
        [SerializeField] Transform minuteHand;
        [SerializeField] Vector3 rotationAxis;
        [SerializeField] Quaternion hourAtTen;
        [SerializeField] Quaternion minuteAtTenPast;

        public void Bind(Transform importedModel)
        {
            Transform twelve = null;
            Transform three = null;
            foreach (Transform piece in importedModel.GetComponentsInChildren<Transform>(true))
            {
                switch (piece.name)
                {
                    case "HourHand": hourHand = piece; break;
                    case "MinuteHand": minuteHand = piece; break;
                    case "DialTwelve": twelve = piece; break;
                    case "DialThree": three = piece; break;
                }
            }
            if (!hourHand || !minuteHand || !twelve || !three || hourHand.parent != minuteHand.parent)
                throw new InvalidOperationException("Frostbridge clock requires its two hands and dial markers.");
            var parent = hourHand.parent;
            Vector3 up = parent.InverseTransformPoint(twelve.position) - hourHand.localPosition;
            Vector3 right = parent.InverseTransformPoint(three.position) - hourHand.localPosition;
            rotationAxis = Vector3.Cross(up, right).normalized;
            hourAtTen = hourHand.localRotation;
            minuteAtTenPast = minuteHand.localRotation;
        }

        void Update()
        {
            if (!hourHand || !minuteHand) return;
            double seconds = Time.timeAsDouble;
            // Minute hand advances 6 degrees per minute. The hour hand travels
            // half a degree in the same interval. Start at 10:10 on the dial.
            hourHand.localRotation = Quaternion.AngleAxis(5f + (float)(seconds % 43200 / 120), rotationAxis) * hourAtTen;
            minuteHand.localRotation = Quaternion.AngleAxis((float)(seconds % 3600 / 10), rotationAxis) * minuteAtTenPast;
        }
    }
}
