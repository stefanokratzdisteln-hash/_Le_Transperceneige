using UnityEngine;
using UnityEngine.UIElements;

namespace Winterbourne.Pass
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class PassHUD : MonoBehaviour
    {
        public PassJourney journey;
        VisualElement inventory,ending,reticle,waybill,frost;
        Label place,mileage,task,message,reason,result,capacity,help;
        readonly VisualElement[] tiles=new VisualElement[12];
        readonly VisualElement[] glyphs=new VisualElement[12];
        static readonly string[] Skies={"黄昏","白昼","深夜"};
        float nextPaint,frostAmount;
        int selected=-1;

        void OnEnable()
        {
            var root=GetComponent<UIDocument>().rootVisualElement;
            if(root==null || root.Q("passRoot")==null) return;
            if(!journey) journey=FindFirstObjectByType<PassJourney>();
            inventory=root.Q("inventory"); ending=root.Q("ending"); waybill=root.Q("waybill");
            frost=root.Q("frost"); frost.generateVisualContent+=DrawFrost;
            reticle=root.Q("reticle"); place=root.Q<Label>("place"); mileage=root.Q<Label>("mileage");
            task=root.Q<Label>("task"); message=root.Q<Label>("message"); capacity=root.Q<Label>("capacity");
            help=root.Q<Label>("slotHelp"); result=root.Q<Label>("result"); reason=root.Q<Label>("reason");
            var quick=root.Q("quickSlots"); var bag=root.Q("allSlots");
            quick.Clear(); bag.Clear();
            for(int i=0;i<12;i++){
                int index=i<4 ? i : i-4;
                var tile=new VisualElement();
                tile.AddToClassList("slot");
                if(i>=4){
                    tile.AddToClassList("bag-slot");
                    tile.RegisterCallback<ClickEvent>(_=>Choose(index));
                }else tile.pickingMode=PickingMode.Ignore;
                var label=new Label((index+1).ToString());
                label.AddToClassList("slot-number");
                tile.Add(label);
                var glyph=new VisualElement{name="glyph",pickingMode=PickingMode.Ignore};
                glyph.AddToClassList("glyph");
                glyph.generateVisualContent+=ctx=>DrawSatchel(ctx,glyph.contentRect);
                tile.Add(glyph);
                (i<4 ? quick : bag).Add(tile);
                tiles[i]=tile;glyphs[i]=glyph;
            }
            root.Q<Button>("closeBag").clicked+=Close;
            root.Q<Button>("retry").clicked+=Retry;
        }
        void OnDisable()
        {
            var root=GetComponent<UIDocument>().rootVisualElement;
            if(root?.Q<Button>("closeBag") is Button close) close.clicked-=Close;
            if(root?.Q<Button>("retry") is Button retry) retry.clicked-=Retry;
            if(frost!=null) frost.generateVisualContent-=DrawFrost;
        }
        void Close()=>journey.SetInventory(false);
        void Retry()=>journey.Retry();
        void Choose(int i)
        {
            if(!journey.InventoryOpen) return;
            if(selected<0){ if(journey.Slot(i)!=null) selected=i; }
            else{ if(selected!=i) journey.MoveSlot(selected,i); selected=-1; }
        }
        static void DrawSatchel(MeshGenerationContext ctx,Rect r)
        {
            if(r.width<4 || r.height<4) return;
            var p=ctx.painter2D;
            float s=Mathf.Min(r.width/48f,r.height/44f);
            Vector2 V(float x,float y)=>new Vector2((r.width-48*s)*.5f+x*s,(r.height-44*s)*.5f+y*s);
            void Panel(Color color,params Vector2[] points){
                p.fillColor=color;p.BeginPath();p.MoveTo(points[0]);
                for(int i=1;i<points.Length;i++)p.LineTo(points[i]);
                p.ClosePath();p.Fill();
            }
            Color brass=new Color(.88f,.73f,.44f), dark=new Color(.28f,.23f,.16f);
            p.strokeColor=brass;p.lineWidth=2*s;p.BeginPath();p.MoveTo(V(17,9));p.LineTo(V(19,4));
            p.LineTo(V(28,4));p.LineTo(V(31,9));p.Stroke();
            Panel(new Color(.67f,.61f,.43f),V(9,10),V(39,10),V(42,35),V(37,40),V(10,40),V(6,35));
            Panel(new Color(.78f,.72f,.53f),V(8,10),V(39,10),V(36,20),V(24,24),V(11,20));
            foreach(int x in new[]{15,30}){
                Panel(dark,V(x,15),V(x+3,15),V(x+3,39),V(x,39));
                Panel(brass,V(x-1,24),V(x+4,24),V(x+4,28),V(x-1,28));
            }
        }

        void DrawFrost(MeshGenerationContext ctx)
        {
            if(frostAmount<=0) return;
            var r=frost.contentRect;
            var p=ctx.painter2D;
            // Hoarfrost grows from the edges as the cold timer runs out.
            for(int k=0;k<68;k++){
                int edge=k%4;
                float a=Mathf.Repeat(k*.618034f,1), flutter=.48f+.52f*Mathf.Abs(Mathf.Sin(k*5.17f));
                var basePoint=edge==0?new Vector2(0,r.height*a):edge==1?new Vector2(r.width,r.height*a):
                    edge==2?new Vector2(r.width*a,0):new Vector2(r.width*a,r.height);
                var inward=edge==0?Vector2.right:edge==1?Vector2.left:edge==2?Vector2.up:Vector2.down;
                var tangent=new Vector2(inward.y,-inward.x);
                var tip=basePoint+(inward+tangent*Mathf.Sin(k)*.4f)*(14+85*flutter)*frostAmount;
                p.strokeColor=new Color(.80f,.91f,1,.55f*frostAmount);
                p.lineWidth=1.6f*frostAmount;
                void Stem(Vector2 x,Vector2 y){p.BeginPath();p.MoveTo(x);p.LineTo(y);p.Stroke();}
                Stem(basePoint,tip);
                for(int j=1;j<5;j++){
                    var node=Vector2.Lerp(basePoint,tip,j*.17f);
                    var size=(5-j)*3*flutter*frostAmount;
                    Stem(node,node+(inward+tangent)*size);
                    Stem(node,node+(inward-tangent)*size);
                }
            }
        }
        static string Clock(float seconds)
        {
            int s=Mathf.Max(0,Mathf.CeilToInt(seconds));
            return $"{s/60:00}:{s%60:00}";
        }
        static void Show(VisualElement el,bool visible)=>el.EnableInClassList("hidden",!visible);
        void LateUpdate()
        {
            if(!journey || inventory==null) return;
            if(Time.unscaledTime<nextPaint) return;
            nextPaint=Time.unscaledTime+.065f;
            bool bag=journey.InventoryOpen,end=journey.Terminal;
            Show(inventory,bag);Show(ending,end);Show(reticle,!bag&&!end&&journey.walker.HasMouse);
            if(!bag) selected=-1;
            int count=0;
            for(int i=0;i<12;i++){
                int index=i<4?i:i-4; var id=journey.Slot(index);
                tiles[i].EnableInClassList("filled",id!=null);
                tiles[i].EnableInClassList("selected",bag&&selected==index);
                Show(glyphs[i],id!=null);
                if(i>=4 && id!=null) count++;
            }
            if(bag){
                capacity.text=$"已用 {count} / 8   ·   前四格常显";
                help.text=selected<0 ? "点选物品，再点另一格换位。" :
                    journey.ItemName(journey.Slot(selected))+"  ·  请选择目标格";
            }
            float d=journey.Distance;
            float at=-journey.walker.transform.position.z;
            place.text=at<480 ? "雪岭山地" : at<780 ? "山腰隧道" : at<1110 ? "费尔桥站" :
                at<1400 ? "峡谷入口" : "北峡高架桥";
            mileage.text=$"{d/1000:0.00} / 2.12 km  ·  {Skies[journey.Lighting]}";
            float cold=journey.Phase==JourneyPhase.Frozen?1:journey.Phase==JourneyPhase.Freezing?
                Mathf.Clamp01(1-journey.ColdSeconds/8):0;
            if(!Mathf.Approximately(cold,frostAmount)){frostAmount=cold;frost.MarkDirtyRepaint();}
            string line="";
            switch(journey.Phase){
                case JourneyPhase.Dwell: line="停车  "+Clock(journey.StationSeconds)+"    对准车门按 E";break;
                case JourneyPhase.Chase: line=journey.Aboard?"列车正在离站":"对准身边的车门按 E\n按住 Shift 追平，空格跳进门";break;
                case JourneyPhase.Freezing: line="列车已离开   ·   失温  "+Clock(journey.ColdSeconds);break;
                case JourneyPhase.Jumping: line="正在下落"; break;
                case JourneyPhase.UnderCanopy: line="伞已展开   ·   向谷底降落";break;
                case JourneyPhase.Running:
                    if(d>=1400) line="前方断桥  "+Clock(journey.SecondsToBreak)+"\n"+
                        (!journey.HasParachute ? "去 09 号行李架取伞包" : journey.EscapeReady ? "去 09 号尾门跳伞" : "到车尾准备跳伞");
                    else if(d>=880 && d<1095) line="即将停靠   ·   费尔桥站";
                    else if(d<90) line="WASD 移动   ·   Shift 加速\n空格 跳跃   ·   E 开关门";
                    break;
            }
            task.text=line;Show(task,line.Length>0&&!end);
            waybill.EnableInClassList("urgent",d>=1400 || journey.Phase==JourneyPhase.Freezing);
            string hint=string.IsNullOrEmpty(journey.Notice)?journey.ActionPrompt():journey.Notice;
            message.text=hint;Show(message,!bag&&!end&&hint.Length>0);
            if(end){
                bool won=journey.Phase==JourneyPhase.Won;
                result.text=won?"已抵达谷底安全区":"GAME OVER";
                reason.text=won?"伞衣收拢在雪地上。您逃出了雪岭断桥线。":
                    journey.Phase==JourneyPhase.Frozen?"您错过了最后一节车，在站台上失温。":
                    journey.Phase==JourneyPhase.Crashed?"列车驶入炸断的桥跨。下次请从 09 号车尾及时撤离。":
                    "坠入峡谷。先到 09 号行李架拾取跳伞包，再从尾门跃出。";
            }
        }
    }
}
