using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Winterbourne.Pass;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif
using Object = UnityEngine.Object;

namespace Winterbourne.Editor
{
    internal static partial class PassExtractionReview
    {
        // A regular Game view renders the player and overlay together into this
        // texture. Copy its complete frame, after layout and UI rendering.
        static RenderTexture gameFrame;
        const BindingFlags Inspector = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static string PortraitFolder => FleetImport.Argument("-portraitDir");

        static IEnumerator PrepareNativeGameView()
        {
            if(gameFrame) yield break;
            Check(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline,"Portraits require the project's URP renderer");
            ShaderUtil.allowAsyncCompilation=false;
            var editor=typeof(EditorWindow).Assembly;
            var view=EditorWindow.GetWindow(editor.GetType("UnityEditor.GameView"));
            view.Focus();
            var sizesType=editor.GetType("UnityEditor.GameViewSizes");
            var sizes=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var group=sizesType.GetMethod("GetGroup",Inspector).Invoke(sizes,new object[]{0});
            var groupType=group.GetType();
            var find=groupType.GetMethod("GetGameViewSize",Inspector);
            int count=(int)groupType.GetMethod("GetBuiltinCount",Inspector).Invoke(group,null)
                     +(int)groupType.GetMethod("GetCustomCount",Inspector).Invoke(group,null);
            int index=0;
            for(;index<count;index++)
            {
                var entry=find.Invoke(group,new object[]{index});
                var t=entry.GetType();
                if((int)t.GetProperty("width",Inspector).GetValue(entry)==2560 &&
                   (int)t.GetProperty("height",Inspector).GetValue(entry)==1440) break;
            }
            if(index==count)
            {
                var sizeType=editor.GetType("UnityEditor.GameViewSize");
                var kind=Enum.Parse(editor.GetType("UnityEditor.GameViewSizeType"),"FixedResolution");
                var entry=Activator.CreateInstance(sizeType,Inspector,null,new object[]{kind,2560,1440,"Winterbourne 1440p"},null);
                groupType.GetMethod("AddCustomSize",Inspector).Invoke(group,new[]{entry});
            }
            view.GetType().GetProperty("selectedSizeIndex",Inspector).SetValue(view,index);
            for(int i=0;i<15;i++)yield return null;
            for(var type=view.GetType();type!=null && !gameFrame;type=type.BaseType)
            {
                foreach(var field in type.GetFields(Inspector))
                    if(typeof(RenderTexture).IsAssignableFrom(field.FieldType) &&
                       field.GetValue(view) is RenderTexture rt && rt.width>=1920) { gameFrame=rt; break; }
            }
            Check(gameFrame,"No native Game view render target at the requested resolution");
        }

        static IEnumerator CabinetPortraits()
        {
            yield return PrepareNativeGameView();
#if ENABLE_INPUT_SYSTEM
            var cameraChoice=walker.GetComponent<PassDetectivePlayer>();
            if(cameraChoice && cameraChoice.PrefersThirdPerson && keyboard!=null)
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.V));
                yield return null;yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                yield return null;
                Check(!cameraChoice.PrefersThirdPerson,"V did not select the standard first-person portrait view");
            }
#endif
            Directory.CreateDirectory(PortraitFolder);
            string[] names={"05 luggage recess middle shelf","09 guard's writing desk"};
            string[] ids={"singing_bird_box","rose_egg"};
            foreach(int i in new[]{0,1})
            {
                bool HasSite()=>Object.FindObjectsByType<PassPickup>(FindObjectsSortMode.None).Any(p=>p.siteId==names[i]);
                route.NewGame();
                for(int n=0;n<160&&!HasSite();n++)route.NewGame();
                Check(HasSite(),"Could not roll "+names[i]);
                yield return EnterGate(route.hubGate,()=>route.Aboard);
                yield return Collect(ids[i]);
                Check(route.CarriedValue==PassLootCatalogue.Value(ids[i]),"Incorrect photographed pickup");
            }
        }

        static void WriteGameFrame(string file,bool still)
        {
            int width=still?gameFrame.width:1280;
            int height=still?gameFrame.height:720;
            var original=RenderTexture.active;
            var tmp=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32);
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                if(SystemInfo.graphicsUVStartsAtTop)
                    Graphics.Blit(gameFrame,tmp,new Vector2(1,-1),new Vector2(0,1));
                else Graphics.Blit(gameFrame,tmp);
                RenderTexture.active=tmp;
                image.ReadPixels(new Rect(0,0,width,height),0,0);
                File.WriteAllBytes(file,still?image.EncodeToPNG():image.EncodeToJPG(95));
            }
            finally
            {
                RenderTexture.active=original;
                RenderTexture.ReleaseTemporary(tmp);
                Object.DestroyImmediate(image);
            }
        }

        static IEnumerator PhotographArticle(PassPickup item)
        {
            bool bird=item.itemId=="singing_bird_box";
            var name=bird?"SingingBirdBox":"WinterRoseEgg";
            var shape=item.GetComponent<Collider>();
            var frame=route.consist;
            var car=frame.Find(bird?"Car05":"Car09");
            string dir=Path.Combine(PortraitFolder,name);
            Directory.CreateDirectory(dir);
            walker.ResumeLook();
            Vector3 Target()=>shape.bounds.center;
            bool Clear(Vector3 eye)
            {
                Vector3 ray=Target()-eye;
                return Physics.Raycast(eye,ray.normalized,out var hit,ray.magnitude+.02f,
                    Physics.DefaultRaycastLayers,QueryTriggerInteraction.Collide) &&
                    hit.collider.GetComponentInParent<PassPickup>()==item;
            }
            IEnumerator Settle()
            {
                var until=Time.realtimeSinceStartupAsDouble+.40;
                while(Time.realtimeSinceStartupAsDouble<until){Aim(Target());yield return null;}
                Check(Clear(Eye.position),"Display article is occluded");
                Check(route.ActionPrompt().Contains("F"),"Missing actual pickup prompt");
            }
            yield return Settle();
            WriteGameFrame(Path.Combine(PortraitFolder,name+"Approach.png"),true);
            walker.SetCrouched(true);
            for(int i=0;i<18;i++){Step(Vector3.zero);yield return null;}
            // Mark the same patch of accessible floor in both asset revisions.
            // The camera stays on the regular player; walk the last centimetres
            // off the navigation grid so a different birth compartment cannot
            // change the comparison angle.
            var close=bird?new Vector3(1.095f,1.405f,9.46f):new Vector3(-.52f,1.405f,4.49f);
            yield return WalkPoint(frame,frame.InverseTransformPoint(car.TransformPoint(close)),name+" close mark",.145f);
            for(int i=0;i<240;i++)
            {
                Vector3 delta=Horizontal(car.TransformPoint(close)-Feet);
                if(delta.magnitude<.012f) break;
                Step(Vector3.ClampMagnitude(delta*5f,1));
                if(i%6==0)yield return null;
            }
            Check(Horizontal(car.TransformPoint(close)-Feet).magnitude<.05f,"Could not stand on the close comparison mark");
            yield return Settle();
            WriteGameFrame(Path.Combine(PortraitFolder,name+"Close.png"),true);
            Debug.Log($"ANTIQUES PHOTO: {name}, eye {car.InverseTransformPoint(Eye.position):F3}, distance {Vector3.Distance(Eye.position,Target()):F3}m; {route.ActionPrompt()}");
            Vector3 start=car.InverseTransformPoint(Feet);
            float furthest=0;
            for(int i=0;i<100;i++)
            {
                var heading=car.forward*(i<12?0:i<38?-.23f:i<81?.23f:0f);
                if(!Clear(Eye.position+heading*.06f))heading=Vector3.zero;
                Step(heading);Aim(Target());
                furthest=Mathf.Max(furthest,Vector3.Distance(start,car.InverseTransformPoint(Feet)));
                yield return null;
                WriteGameFrame(Path.Combine(dir,i.ToString("D4")+".jpg"),false);
            }
            Debug.Log($"ANTIQUES TRACK: {name}, lateral excursion {furthest:F3}m");
            yield return Settle();
        }
    }
}
