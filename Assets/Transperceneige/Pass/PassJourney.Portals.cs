using UnityEngine;

namespace Winterbourne.Pass
{
    public sealed partial class PassJourney
    {
        public Transform hubSpawn, sleeperSpawn, stationSpawn;
        public PassPortal hubGate, stationGate, returnGate;
        readonly PassBookcaseCover[] bookcases=new PassBookcaseCover[3];
        enum JourneyLocation { Railway, Hub, Valley, Menu }
        JourneyLocation location;
        Checkpoint initialRide;
        bool stationVisit, completedRideInHub;

        public bool InHub => location == JourneyLocation.Hub;
        public bool InStartMenu => location == JourneyLocation.Menu;
        public bool AtLanding => location == JourneyLocation.Valley;
        public bool VisitingStation => InHub && stationVisit && !MissedTrainInHub;
        public bool MissedTrainInHub { get; private set; }

        void PlaceAt(Transform anchor, Transform support)
        {
            var feet = support ? support.InverseTransformPoint(anchor.position) : anchor.position;
            var facing = support ? Quaternion.Inverse(support.rotation) * anchor.rotation : anchor.rotation;
            walker.Place(support, feet, facing, 0);
            Physics.SyncTransforms();
        }

        void EnterHut(bool visit)
        {
            location = JourneyLocation.Hub;
            Aboard = false;
            stationVisit = visit;
            MissedTrainInHub = false;
            ResetLocomotion();
            walker.externalMotion = false;
            SetInventory(false);
            PlaceAt(hubSpawn, null);
            hubGate.RefreshLabel();
        }

        public void NewGame()
        {
            if (initialRide == null || !hubSpawn) return;
            ResetExpedition();
            completedRideInHub=false;
            saved = initialRide;
            RestoreRide(initialRide);
            if (signal) signal.Stop();
            Started = false;
            notice = ""; noticeItem = null; noticeUntil = -1;
            EnterHut(false);
        }

        public void EndGame()
        {
            if (!InHub || !SettleExpedition()) return;
            location = JourneyLocation.Menu;
            Started = false;
            ResetLocomotion();
            walker.externalMotion = true;
            SetInventory(false);
            if (signal) signal.Stop();
        }

        bool CanReturnToStation => Phase == JourneyPhase.Dwell ||
            (Phase == JourneyPhase.Chase && Distance < PlatformEnd + TrainLength);

        bool TryLookAtCover(out PassBookcaseCover cover)
        {
            var gate=InHub ? hubGate : AtLanding ? returnGate : !Aboard && CanReturnToStation ? stationGate : null;
            cover=gate ? gate.cover : null;
            return cover && cover.InSight(walker.eyeCamera,walker.interactionDistance,gate.threshold);
        }

        void PollPortals()
        {
            if (InventoryOpen || Terminal || !body.enabled || walker.externalMotion) return;
            if (InHub) TryEnterPortal(hubGate);
            else if (AtLanding) TryEnterPortal(returnGate);
            else if (!Aboard && CanReturnToStation) TryEnterPortal(stationGate);
        }

        public bool TryEnterPortal(PassPortal gate)
        {
            if (!gate || !gate.Contains(body) || InventoryOpen || InStartMenu || Terminal || walker.externalMotion)
                return false;
            if (InHub && gate == hubGate)
            {
                if (MissedTrainInHub || (stationVisit && !CanReturnToStation)) return false;
                if (stationVisit)
                {
                    location = JourneyLocation.Railway;
                    stationVisit = false;
                    Aboard = false;
                    PlaceAt(stationSpawn, null);
                    Tell("station.returned");
                }
                else
                {
                    if(completedRideInHub)
                    {
                        saved=initialRide;
                        RestoreRide(initialRide);
                        completedRideInHub=false;
                    }
                    else
                    {
                        location=JourneyLocation.Railway;
                        Aboard=true;
                        PlaceAt(sleeperSpawn,consist);
                    }
                    Started = true;
                    noticeUntil = -1;
                }
                return true;
            }
            if (!InHub && !AtLanding && !Aboard && gate == stationGate && CanReturnToStation)
            {
                EnterHut(true);
                Tell("hub.returned");
                return true;
            }
            if (AtLanding && gate == returnGate)
            {
                Started=false;
                completedRideInHub=true;
                returnGate.gameObject.SetActive(false);
                EnterHut(false);
                Tell("hub.returned");
                return true;
            }
            return false;
        }

    }
}
