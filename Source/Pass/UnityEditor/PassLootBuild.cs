using System.Collections.Generic;
using UnityEngine;
using Winterbourne.Pass;

namespace Winterbourne.Editor
{
    // Local positions are in rail-height metres on each vehicle and in floor-height
    // metres in the station. Each group rolls one article across its plausible sites.
    public static class PassLootBuild
    {
        public static void Build(GameObject[] cars,Transform house)
        {
            using var fittings=new PassLootFixtures();
            var sites=new HashSet<string>();
            var types=new HashSet<string>();
            Transform Car(int number)=>cars[number-1].transform;
            Vector3 P(float x,float y,float z)=>new Vector3(x,y,z);

            PassPickup Put(Transform parent,string id,string site,Vector3 point,float yaw=0)
            {
                PassBuild.Check(PassLootCatalogue.Contains(id),"Unknown catalogue article "+id);
                PassBuild.Check(sites.Add(site),"Duplicate physical search site "+site);
                types.Add(id);
                return PassLootFixtures.Item(parent,id,site,id,point,yaw);
            }

            void Case(Transform parent,string id,string site,Vector3 floorOrCounter,float yaw,
                string kind,bool locked=false)
            {
                var inside=fittings.Case(parent,site+" case",floorOrCounter,yaw,kind,locked);
                Put(inside,id,site,Vector3.zero);
            }

            void Watch(string site,float shelfZ,float doorZ)
            {
                var inside=fittings.Case(Car(7),site+" velvet box",P(-1.245f,2.591f,shelfZ),-90,"watch");
                var item=Put(inside,"pocket_watch",site,Vector3.zero);
                var arrival=new GameObject(site+" bedside arrival").transform;
                arrival.SetParent(Car(7),false);
                arrival.localPosition=P(-.13f,1.405f,doorZ);
                arrival.localRotation=Quaternion.LookRotation(Vector3.left);
                item.bedsideArrival=arrival;
            }
            Watch("07 first cabin watch", -6.30f,-5.21f);
            Watch("07 second cabin watch",-3.96f,-3.67f);
            Watch("07 fourth cabin watch",6.30f,5.21f);

            // The large pieces sit upright. Neither cab can spare its narrow
            // doorway for a trunk, so use the worktop or the outer wall footprint.
            Put(Car(1),"astronomical_globe","01 instrument table",P(.95f,2.240f,5.70f),-90);
            Case(Car(1),"astronomical_globe","01 driver's private cabinet",P(-1.05f,1.385f,6.64f),-90,"cabinet",true);
            Put(Car(2),"astrolabe","02 front tool room",P(.95f,2.240f,-3.67f),-90);
            Case(Car(2),"astrolabe","02 water service desk",P(.98f,2.240f,3.69f),-90,"trunk");

            Case(Car(3),"diptych_dial","03 dispatch drawer",P(-1.93f,2.287f,-8.82f),-90,"drawer");
            Put(Car(3),"diptych_dial","03 artificer's bench",P(-.86f,2.282f,4.11f),85);
            Case(Car(3),"silver_nef","03 bonded goods cabinet",P(-1.96f,2.287f,10.63f),-90,"cabinet",true);
            Put(Car(3),"cloisonne_vase","03 lower lamp-room shelf",P(1.56f,1.544f,-10.13f),-77);

            // The writing island is separate from the four enclosed compartments.
            Case(Car(4),"intaglio_seal","04 correspondence drawer",P(-.71f,2.207f,-9.05f),-90,"drawer");
            Put(Car(4),"intaglio_seal","04 second writing table",P(-1.21f,2.267f,-1.55f),100);
            Put(Car(4),"intaglio_seal","04 fourth writing table",P(-1.21f,2.267f,5.13f),74);
            var dispatch=fittings.Case(Car(4),"04 signed valise",P(-.74f,2.207f,10.28f),-90,"trunk");
            Put(dispatch,"cameo","04 dispatch valise brooch compartment",P(.13f,0,0));
            Put(dispatch,"jewelled_fan","04 dispatch valise fan compartment",P(-.08f,0,0));
            Put(Car(4),"cameo","04 first writing table",P(-1.20f,2.267f,-5.14f),78);

            Case(Car(5),"singing_bird_box","05 reading desk",P(-.70f,2.207f,-9.06f),-90,"trunk");
            Case(Car(5),"singing_bird_box","05 passenger luggage sideboard",P(-1.95f,2.287f,10.46f),-90,"trunk");
            Put(Car(5),"singing_bird_box","05 luggage recess middle shelf",P(1.48f,2.113f,9.38f),76);
            Put(Car(5),"jewelled_fan","05 low luggage shelf",P(1.54f,1.544f,10.13f),-90);
            Put(Car(5),"silver_nef","05 forwarding room upper shelf",P(1.54f,2.664f,9.68f),-90);

            // The silver pantry's new bay can accommodate a cabinet in either
            // fit-out. Use its named counter to keep the case on the actual top.
            BoxCollider pantry=null;
            foreach(var surface in Car(6).GetComponentsInChildren<BoxCollider>(true))
                if(surface.name=="SILVER PANTRY sideboard" || surface.name=="SILVER PANTRY tabletop") pantry=surface;
            PassBuild.Check(pantry,"06 silver pantry has no supporting furniture");
            var top=pantry.transform.TransformPoint(pantry.center+Vector3.up*(pantry.size.y*.5f));
            var plate=Car(6).InverseTransformPoint(top);
            plate.y+=
                (pantry.name.EndsWith("sideboard")?.024f:.003f);
            plate.z+=.27f;
            Case(Car(6),"crystal_cup","06 plate-room cabinet",plate,-90,"cabinet");
            Put(Car(6),"crystal_cup","06 silver service buffet",P(-1.07f,2.303f,6.89f),90);
            Put(Car(6),"crystal_cup","06 private dinner table",P(-.69f,2.208f,10.32f),60);
            Put(Car(6),"cloisonne_vase","06 private dining centrepiece",P(-.77f,2.208f,9.72f),-22);

            // The service sideboard and the standalone reading table are away from
            // the spawn shelves; none of the three bedside arrivals is obstructed.
            Case(Car(7),"scent_flacon","07 dressing case",P(-1.95f,2.287f,10.62f),-90,"drawer");
            Put(Car(7),"jewelled_fan","07 private drawing table",P(-.70f,2.207f,-9.04f),86);

            Case(Car(8),"pearl_aigrette","08 berth luggage valise",P(-1.96f,2.287f,-8.77f),-90,"trunk");
            var sleeperTrunk=fittings.Case(Car(8),"08 second sleeper's trunk",P(-1.10f,1.841f,-1.31f),-90,"trunk");
            Put(sleeperTrunk,"pearl_aigrette","08 second sleeper's jewellery cushion",P(-.088f,0,0));
            Put(sleeperTrunk,"cameo","08 second sleeper's velvet insert",P(.115f,0,0));
            Put(Car(8),"pearl_aigrette","08 linen exchange high shelf",P(1.55f,2.664f,10.01f),-90);
            Put(Car(8),"scent_flacon","08 linen exchange shelf",P(1.55f,2.113f,9.48f),-90);

            Case(Car(9),"rose_egg","09 bonded cabinet",P(-1.95f,2.287f,-8.72f),-90,"cabinet",true);
            Case(Car(9),"rose_egg","09 sorting room dispatch trunk",P(-1.94f,2.287f,10.66f),-90,"trunk");
            Put(Car(9),"rose_egg","09 guard's writing desk",P(-.94f,2.234f,4.55f),-42);
            Put(Car(9),"silver_nef","09 consigned silver shelf",P(1.55f,2.114f,-9.95f),-90);

            // Passenger-facing side of the parcels office: the wall of tagged
            // cargo has its own full-height collider. The new cases stand outside
            // it; keep the open floor between the two platform doors and stair clear.
            var arrivals=fittings.Case(house,"FB incoming registered case",P(-5.52f,.932f,-8.99f),180,"trunk");
            Put(arrivals,"portrait_snuffbox","FB incoming parcel velvet tray",P(-.10f,0,0));
            Put(arrivals,"scent_flacon","FB incoming parcel scent tray",P(.12f,0,0));
            Case(house,"portrait_snuffbox","FB left-luggage drawer",P(-7.24f,.017f,-5.17f),-90,"drawer");
            Case(house,"portrait_snuffbox","FB parcels docket case",P(-5.97f,.017f,-8.31f),180,"trunk");
            Case(house,"emerald_tiara","FB registered coronet cabinet",P(-6.76f,.017f,-7.77f),-90,"cabinet",true);
            Case(house,"emerald_tiara","FB overnight jewellery dispatch",P(-3.92f,.017f,-9.25f),180,"cabinet",true);
            Case(house,"emerald_tiara","FB unclaimed luggage valise",P(-8.10f,.017f,-4.89f),-90,"trunk",true);
            Put(house,"cloisonne_vase","FB stationmaster's desk corner",P(-1.88f,4.239f,-5.54f),36);
            PassBuild.Check(sites.Count>=36 && types.Count==16,"Incomplete searchable luggage layout");

            // Additional deposits have their own instance identities. Original
            // high-value route manifests still roll at their established sites.
            void Parcel(Transform room,string site,Vector3 pos,float angle,string article)
            {
                var tray=fittings.Case(room,site,pos,angle,"parcelChest",true);
                PassLootFixtures.Item(tray,article,site+" parcel",string.Empty,Vector3.zero);
            }
            void Treasury(Transform room,string site,Vector3 pos,float angle,string ordinary)
            {
                var tray=fittings.Case(room,site,pos,angle,"treasuryChest",true);
                foreach(var article in PassLootCatalogue.GrandTreasures)
                {
                    var option=PassLootFixtures.Item(tray,article,site+" "+article,PassLootCatalogue.TreasuryGroup,Vector3.zero);
                    SeatInVelvetBay(option);
                    option.gameObject.SetActive(false);
                }
                PassLootFixtures.Item(tray,ordinary,site+" registered parcel",PassLootCatalogue.TreasuryGroup,Vector3.zero);
            }
            Parcel(Car(3),"03 linen dispatch chest",P(-1.98f,1.382f,-10.97f),-90,"scent_flacon");
            Parcel(Car(5),"05 forwarded passenger chest",P(-1.99f,1.382f,8.45f),-90,"diptych_dial");
            Parcel(house,"FB parcels office chest",P(-6.05f,.017f,-4.51f),0,"jewelled_fan");
            Treasury(Car(4),"04 drawing room treasury",P(2.01f,1.382f,10.65f),90,"silver_nef");
            Treasury(Car(9),"09 bonded freight treasury",P(-2.01f,1.382f,-11.03f),-90,"cloisonne_vase");
            Treasury(house,"FB parcels office treasury",P(-4.19f,.017f,-4.53f),0,"diptych_dial");
        }

        // The fixed lock escutcheon splits the front into two narrow openings.
        // Align the instrument neck into the deep case and bed its lower bout
        // on the velvet. Preserve real scale and keep both leaves clear.
        static void SeatInVelvetBay(PassPickup article)
        {
            var holder=article.transform;
            var vertices=new List<Vector3>();
            foreach(var filter in holder.GetComponentsInChildren<MeshFilter>())
            {
                var toHolder=holder.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                foreach(var point in filter.sharedMesh.vertices)
                    vertices.Add(toHolder.MultiplyPoint3x4(point));
            }
            Quaternion pose=Quaternion.identity;
            if(article.itemId=="touring_violin" || article.itemId=="falcon_dagger")
            {
                float angle=article.itemId=="touring_violin"?60:48;
                float radians=angle*Mathf.Deg2Rad;
                pose=Quaternion.LookRotation(Vector3.left,new Vector3(0,Mathf.Cos(radians),-Mathf.Sin(radians)));
                if(article.itemId=="touring_violin" && NarrowEndIsNegative(vertices))
                    pose*=Quaternion.Euler(0,180,0);
            }
            else if(article.itemId=="gilt_ephemeris")
                pose=Quaternion.Euler(-55,0,0);
            var bounds=new Bounds(pose*vertices[0],Vector3.zero);
            foreach(var point in vertices) bounds.Encapsulate(pose*point);
            PassBuild.Check(bounds.size.x<.281f && bounds.size.y<.552f && bounds.size.z<.40f,
                article.itemId+" does not fit the treasury's velvet bay: "+bounds.size.ToString("F3"));
            holder.SetLocalPositionAndRotation(new Vector3(-bounds.center.x,.008f-bounds.min.y,-bounds.center.z),pose);
        }

        static bool NarrowEndIsNegative(List<Vector3> vertices)
        {
            var bounds=new Bounds(vertices[0],Vector3.zero);
            foreach(var p in vertices)bounds.Encapsulate(p);
            var negative=new Bounds();var positive=new Bounds();
            bool left=false,right=false;
            foreach(var point in vertices)
            {
                if(point.x<bounds.min.x+.15f*bounds.size.x)
                {
                    if(left) negative.Encapsulate(point); else negative=new Bounds(point,Vector3.zero);
                    left=true;
                }
                if(point.x>bounds.max.x-.15f*bounds.size.x)
                {
                    if(right) positive.Encapsulate(point); else positive=new Bounds(point,Vector3.zero);
                    right=true;
                }
            }
            return negative.size.z<positive.size.z;
        }
    }
}
