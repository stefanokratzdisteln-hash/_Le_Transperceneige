using UnityEngine;
using static Winterbourne.Editor.StationFabrication;

namespace Winterbourne.Editor
{
    public static partial class PassFittings
    {
        static void MakeGangways(GameObject[] cars, Finishes finishes)
        {
            for (int i = 0; i < cars.Length - 1; i++)
            {
                Transform leading = cars[i].transform;
                Transform socket = Find(leading, "Socket_B");
                Transform next = Find(cars[i + 1].transform, "Socket_A");
                PassBuild.Check(Vector3.Distance(socket.position, next.position) < .014f, "Misaligned gangway " + i);
                Vector3 seam = leading.InverseTransformPoint((socket.position + next.position) * .5f);
                seam.y = 1.396f;
                Transform joint = Child(leading, $"Gangway {i+1:00}-{i+2:00}", seam).transform;
                // At the cab and tender the end balconies have no bellows. Cover their full
                // exposed length. The passenger stock already has enclosed bellows on either end.
                bool open = i < 2;
                float halfWidth = i == 0 ? .715f : i == 1 ? .675f : .658f;
                float length = open ? 1.46f : .82f;
                Vector3 tread = new Vector3(halfWidth * 2f + .035f, .066f, length);
                var mesh = new StationFabrication();
                mesh.Box(finishes.soot, Vector3.down * .033f, tread);
                Box(joint, "Joint tread", Vector3.down * .033f, tread);
                for (float z = -.5f * length + .066f; z < .5f * length; z += .103f)
                    mesh.Box(finishes.pine, new Vector3(0, .0015f, z), new Vector3(2 * halfWidth - .10f, .003f, .015f));

                for (int hand = -1; hand <= 1; hand += 2)
                {
                    float x = hand * (halfWidth + .038f);
                    float guardHeight = open ? 1.65f : 2.10f;
                    Vector3 guard = new Vector3(x, guardHeight * .5f - .008f, 0);
                    Box(joint, hand < 0 ? "Left joint guard" : "Right joint guard", guard,
                        new Vector3(.075f, guardHeight, length));
                    if (open)
                    {
                        mesh.Box(finishes.soot, new Vector3(x, .084f, 0), new Vector3(.062f, .168f, length));
                        for (int end = -1; end <= 1; end += 2)
                        {
                            float z = end * (length / 2 - .045f);
                            mesh.Ring(finishes.pine, new Vector3(x, .12f, z), new Vector3(x, guardHeight, z), .027f, .024f);
                        }
                        foreach (float height in new[] { .51f, 1.08f, guardHeight })
                            mesh.Ring(finishes.pine, new Vector3(x, height, -length * .49f),
                                new Vector3(x, height, length * .49f), .024f, .024f);
                    }
                    else
                    {
                        mesh.Box(finishes.soot, guard, new Vector3(.064f, guardHeight, length));
                        for (int fold = -2; fold <= 2; fold++)
                            mesh.Box(finishes.pine, guard + Vector3.forward * (fold * .126f),
                                new Vector3(.087f, guardHeight, .036f));
                    }
                }
                if (!open)
                {
                    var roof = new Vector3(2f * halfWidth + .12f, .065f, length);
                    mesh.Box(finishes.soot, new Vector3(0, 2.142f, 0), roof);
                    Box(joint, "Joint headlining", new Vector3(0, 2.142f, 0), roof);
                }
                mesh.Finish(joint, i == 0 ? "Cab fall plate and rails" :
                    i == 1 ? "Tender fall plate and rails" : "Sealed gangway sleeve and tread");
            }
        }
    }
}
