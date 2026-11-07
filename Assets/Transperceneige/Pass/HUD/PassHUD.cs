using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using static Winterbourne.Pass.PassText;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
        readonly Label[] slotPrices=new Label[8];
        readonly Label[] slotNames=new Label[8];
        readonly Dictionary<string,Texture2D> lootIcons=new Dictionary<string,Texture2D>();
        VisualElement searchSheet,searchRows,lockTrack,lockPin;
        Label inventoryValue,searchTitle,searchHelp;
        Button takeOne,takeAll,closeBag;
        int hoveredSlot=-1,paintedLoot=-1;
        bool redrawText;
        VisualElement root,roomActions,titleScreen,kit,vitals,healthFill,runBoard;
        Label shortcuts,healthNumber,bagValue,runClock,runTarget,settlement,settledValue;
        VisualElement deployPrompt;
        Label deployKey,deployAction,impactLoss,impactResult;
        int previousHealth=-1;
        float showLossUntil;
        float nextPaint,frostAmount;
        int selected=-1;

        void OnEnable()
        {
            root=GetComponent<UIDocument>().rootVisualElement;
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
                    tile.RegisterCallback<PointerEnterEvent>(_=>hoveredSlot=index);
                    tile.RegisterCallback<PointerLeaveEvent>(_=>{ if(hoveredSlot==index) hoveredSlot=-1; });
                }else tile.pickingMode=PickingMode.Ignore;
                var label=new Label((index+1).ToString());
                label.AddToClassList("slot-number");
                tile.Add(label);
                var glyph=new VisualElement{name="glyph",pickingMode=PickingMode.Ignore};
                glyph.AddToClassList("glyph");
                glyph.generateVisualContent+=ctx=>{
                    if(journey && journey.Slot(index)=="parachute") DrawSatchel(ctx,glyph.contentRect);
                };
                tile.Add(glyph);
                if(i>=4){
                    var caption=new Label(){pickingMode=PickingMode.Ignore};
                    caption.AddToClassList("slot-caption"); tile.Add(caption); slotNames[index]=caption;
                    var price=new Label(){pickingMode=PickingMode.Ignore};
                    price.AddToClassList("slot-price"); tile.Add(price); slotPrices[index]=price;
                }
                (i<4 ? quick : bag).Add(tile);
                tiles[i]=tile;glyphs[i]=glyph;
            }
            closeBag=root.Q<Button>("closeBag"); closeBag.clicked+=Close;
            searchSheet=root.Q("searchSheet"); searchRows=root.Q("searchRows");
            searchTitle=root.Q<Label>("searchTitle"); searchHelp=root.Q<Label>("searchHelp");
            lockTrack=root.Q("lockTrack");lockPin=root.Q("lockPin");
            inventoryValue=root.Q<Label>("inventoryValue");
            takeOne=root.Q<Button>("takeOne");takeAll=root.Q<Button>("takeAll");
            takeOne.clicked+=TakeOne;takeAll.clicked+=TakeAll;
            root.Q<Button>("retry").clicked+=Retry;
            root.Q<Button>("roomLanguage").clicked+=ToggleLanguage;
            root.Q<Button>("bagBadge").clicked+=OpenBag;
            root.Q<Button>("bagLanguage").clicked+=ToggleLanguage;
            root.Q<Button>("titleLanguage").clicked+=ToggleLanguage;
            root.Q<Button>("endGame").clicked+=EndGame;
            root.Q<Button>("newGame").clicked+=NewGame;
            root.Q<Button>("quitGame").clicked+=Quit;
            roomActions=root.Q("roomActions"); titleScreen=root.Q("titleScreen"); kit=root.Q("kit");
            vitals=root.Q("vitals"); healthFill=root.Q("healthFill"); runBoard=root.Q("runBoard");
            deployPrompt=root.Q("deployPrompt"); deployKey=root.Q<Label>("deployKey");
            deployAction=root.Q<Label>("deployAction"); impactLoss=root.Q<Label>("impactLoss");
            impactResult=root.Q<Label>("impactResult");
            healthNumber=root.Q<Label>("healthNumber"); bagValue=root.Q<Label>("bagValue");
            runClock=root.Q<Label>("runClock"); runTarget=root.Q<Label>("runTarget");
            settlement=root.Q<Label>("settlement"); settledValue=root.Q<Label>("settledValue");
            foreach(var name in new[]{"bagDrop","targetDrop"}){
                var drop=root.Q(name);
                drop.generateVisualContent+=ctx=>DrawValue(ctx,drop.contentRect);
            }
            var badge=root.Q("bagDrawing");
            badge.generateVisualContent+=ctx=>DrawSatchel(ctx,badge.contentRect);
            shortcuts=root.Q<Label>("shortcuts");
            if(journey && journey.walker) journey.walker.pointerOverControl=OverButton;
            Changed+=ApplyLanguage;
            ApplyLanguage();
        }
        void OnDisable()
        {
            var root=GetComponent<UIDocument>().rootVisualElement;
            if(root?.Q<Button>("closeBag") is Button close) close.clicked-=Close;
            if(takeOne!=null) takeOne.clicked-=TakeOne;
            if(takeAll!=null) takeAll.clicked-=TakeAll;
            if(root?.Q<Button>("retry") is Button retry) retry.clicked-=Retry;
            if(root?.Q<Button>("roomLanguage") is Button room) room.clicked-=ToggleLanguage;
            if(root?.Q<Button>("bagBadge") is Button badge) badge.clicked-=OpenBag;
            if(root?.Q<Button>("bagLanguage") is Button bagLang) bagLang.clicked-=ToggleLanguage;
            if(root?.Q<Button>("titleLanguage") is Button titleLang) titleLang.clicked-=ToggleLanguage;
            if(root?.Q<Button>("endGame") is Button end) end.clicked-=EndGame;
            if(root?.Q<Button>("newGame") is Button play) play.clicked-=NewGame;
            if(root?.Q<Button>("quitGame") is Button quit) quit.clicked-=Quit;
            if(journey && journey.walker) journey.walker.pointerOverControl=null;
            Changed-=ApplyLanguage;
            if(frost!=null) frost.generateVisualContent-=DrawFrost;
        }
        bool OverButton()
        {
            if (root?.panel == null) return false;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return false;
            Vector2 p=Mouse.current.position.ReadValue();
#elif ENABLE_LEGACY_INPUT_MANAGER
            Vector2 p=Input.mousePosition;
#endif
#if ENABLE_INPUT_SYSTEM || ENABLE_LEGACY_INPUT_MANAGER
            p.y=Screen.height-p.y;
            var hit=root.panel.Pick(RuntimePanelUtils.ScreenToPanel(root.panel,p));
            return hit is Button || hit?.GetFirstAncestorOfType<Button>() != null;
#else
            return false;
#endif
        }
        void ApplyLanguage()
        {
            root.Q<Label>("inventoryTitle").text=Get("inventory.title");
            root.Q<Label>("gameTitle").text=Get("game.title");
            root.Q<Button>("closeBag").text=Get("inventory.close");
            root.Q<Button>("retry").text=Get("controls.retry");
            root.Q<Button>("endGame").text=Get("menu.end");
            root.Q<Button>("newGame").text=Get("menu.new");
            root.Q<Button>("quitGame").text=Get("menu.quit");
            root.Q<Label>("clockCaption").text=Get("run.clock");
            root.Q<Label>("targetCaption").text=Get("run.target");
            redrawText=true;nextPaint=0;
        }
        void ToggleLanguage()=>SetChinese(!Chinese);
        void EndGame()=>journey.EndGame();
        void NewGame()=>journey.NewGame();
        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
        void Close()=>journey.SetInventory(false);
        void OpenBag()=>journey.SetInventory(true);
        void TakeOne()=>journey.UseFocused();
        void TakeAll()=>journey.TakeAllFocused();
        void Retry(){ if(journey.RunExpired) journey.NewGame(); else journey.Retry(); }
        void Choose(int i)
        {
            if(!journey.InventoryOpen) return;
            if(journey.AwaitingSwap){journey.ExchangeSlot(i);selected=-1;return;}
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

        static void DrawValue(MeshGenerationContext ctx,Rect r)
        {
            if(r.width<1 || r.height<1) return;
            var p=ctx.painter2D;
            Vector2 At(float x,float y)=>new Vector2(r.x+x*r.width,r.y+y*r.height);
            p.fillColor=new Color(.94f,.76f,.39f);
            p.BeginPath();p.MoveTo(At(.55f,.02f));
            p.BezierCurveTo(At(.49f,.31f),At(.10f,.55f),At(.13f,.72f));
            p.BezierCurveTo(At(.14f,1.08f),At(.87f,1.1f),At(.88f,.69f));
            p.BezierCurveTo(At(.91f,.47f),At(.66f,.22f),At(.55f,.02f));p.ClosePath();p.Fill();
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

        void RefreshSearch(bool dirty)
        {
            bool search=journey.SearchOpen;
            Show(searchSheet,search);
            closeBag.text=Get(search?"search.close":"inventory.close");
            if(!search) return;
            bool locked=journey.PickingLock;
            Show(lockTrack,locked);Show(searchRows,!locked);Show(takeAll,!locked);
            if(locked)lockPin.style.left=Length.Percent(journey.LockPosition*100);
            takeOne.text=Get(locked?"search.turn":"search.one");
            takeAll.text=Get("search.all");
            takeOne.SetEnabled(locked || journey.SearchCount>0);
            takeAll.SetEnabled(journey.SearchCount>0);
            searchTitle.text=Get(journey.OpenContainer?journey.OpenContainer.labelKey:"search.offer");
            string notice=journey.Notice;
            searchHelp.text=locked?Get("search.timing"):journey.AwaitingSwap?Get("search.exchange"):
                !string.IsNullOrEmpty(notice)?notice:Get(journey.SearchCount==0?"search.empty":"search.select");
            if(!dirty) return;
            searchRows.Clear();
            for(int i=0;i<journey.SearchCount;i++)
            {
                int at=i;
                var item=journey.SearchItem(i);
                var row=new Button(()=>journey.SelectSearchItem(at));
                var icon=LootIcon(item.itemId);
                if(icon){
                    var picture=new VisualElement{pickingMode=PickingMode.Ignore};
                    picture.AddToClassList("search-icon");
                    picture.style.backgroundImage=new StyleBackground(icon);
                    row.Add(picture);
                }
                var title=new Label(journey.ItemName(item.itemId)){pickingMode=PickingMode.Ignore};
                title.AddToClassList("search-name"); row.Add(title);
                var price=new Label(PassLootCatalogue.Format(PassLootCatalogue.Value(item.itemId))){pickingMode=PickingMode.Ignore};
                price.AddToClassList("search-price"); row.Add(price);
                row.AddToClassList("search-row");
                row.EnableInClassList("selected",i==journey.SelectedSearchIndex);
                searchRows.Add(row);
            }
        }

        Texture2D LootIcon(string id)
        {
            if(id==null || id=="parachute") return null;
            if(!lootIcons.TryGetValue(id,out var icon))
                lootIcons[id]=icon=Resources.Load<Texture2D>("WinterbourneLootIcons/"+id);
            return icon;
        }
        void LateUpdate()
        {
            if(!journey || inventory==null) return;
            bool dirty=paintedLoot!=journey.LootRevision || redrawText;
            if(Time.unscaledTime<nextPaint && !dirty && !journey.PickingLock) return;
            paintedLoot=journey.LootRevision;redrawText=false;
            nextPaint=Time.unscaledTime+.065f;
            bool bag=journey.InventoryOpen, end=journey.Terminal, menu=journey.InStartMenu;
            bool hub=journey.InHub;
            Show(titleScreen,menu); Show(roomActions,hub&&journey.InsideCottage&&!bag&&!menu&&!end);
            Show(inventory,bag&&!menu); Show(ending,end&&!menu);
            Show(waybill,!menu&&!bag&&!end);
            Show(reticle,!menu&&!bag&&!end&&journey.walker.HasMouse);
            Show(kit,!bag&&!menu&&!end); Show(vitals,!bag&&!menu&&!end);
            Show(runBoard,!menu&&!end);
            string value=journey.CarriedValue.ToString("N0",System.Globalization.CultureInfo.InvariantCulture);
            string target=PassJourney.TargetValue.ToString("N0",System.Globalization.CultureInfo.InvariantCulture);
            bagValue.text=value; bagValue.tooltip=Get("run.value");
            runTarget.text=value+" / "+target; runClock.text=Clock(journey.RunSeconds);
            runClock.EnableInClassList("last-minute",journey.RunSeconds<=60);
            healthNumber.text=journey.Health.ToString();vitals.tooltip=Get("run.health");
            healthFill.style.width=Length.Percent(journey.Health);
            if(previousHealth>journey.Health && journey.LastImpactDamage>0){
                showLossUntil=Time.unscaledTime+2.6f;
                impactLoss.text="-"+journey.LastImpactDamage;
            }
            if(journey.Health>previousHealth) showLossUntil=0;
            previousHealth=journey.Health;
            Show(impactLoss,Time.unscaledTime<showLossUntil);
            Show(settlement,menu&&journey.HasSettlement); Show(settledValue,menu&&journey.HasSettlement);
            if(menu&&journey.HasSettlement){
                settlement.text=Get(journey.SettledSuccessfully?"run.success":"run.shortfall");
                settledValue.text=string.Format(Get("run.settled"),value,target);
            }
            Show(shortcuts,!menu&&!bag&&!end);
            shortcuts.text=Get(hub?"hub.controls":journey.OnRoof?"roof.controls":"controls.shortcuts");
            if(!bag){ selected=-1;hoveredSlot=-1; }
            RefreshSearch(dirty);
            int count=0;
            for(int i=0;i<12;i++){
                int index=i<4?i:i-4;
                var id=journey.Slot(index);
                tiles[i].EnableInClassList("filled",id!=null);
                tiles[i].EnableInClassList("selected",bag ? selected==index : i<4&&journey.QuickSlot==index);
                Show(glyphs[i],id!=null);
                if(dirty) {
                    var icon=LootIcon(id);
                    glyphs[i].style.backgroundImage=icon?new StyleBackground(icon):new StyleBackground(StyleKeyword.None);
                    glyphs[i].MarkDirtyRepaint();
                }
                if(i>=4){
                    tiles[i].tooltip=id==null?Get("inventory.empty"):journey.ItemName(id);
                    slotNames[index].text=id==null?"":journey.ItemName(id);
                    slotPrices[index].text=id==null?"":PassLootCatalogue.Format(PassLootCatalogue.Value(id));
                    if(id!=null) count++;
                }
            }
            if(bag){
                inventoryValue.text=Get("run.value")+"    "+value;
                capacity.text=string.Format(Get("inventory.capacity"),count);
                var hover=hoveredSlot>=0?journey.Slot(hoveredSlot):null;
                help.text=journey.AwaitingSwap?Get("search.exchange"):selected>=0?
                    string.Format(Get("inventory.move"),journey.ItemName(journey.Slot(selected))):
                    hover!=null?journey.ItemName(hover):Get("inventory.choose");
            }
            float at=-journey.walker.transform.position.z;
            string area=at<480?"mountain":at<780?"tunnel":at<1110?"fellbridge":at<1400?"gorge":"viaduct";
            place.text=Get(hub?"hub.name":journey.AtLanding?"landing.name":area);
            mileage.text=$"{journey.Distance/1000:0.000} / 2.120 km";
            Show(mileage,!hub&&!journey.AtLanding);
            float cold=journey.Phase==JourneyPhase.Frozen?1:journey.Phase==JourneyPhase.Freezing?
                Mathf.Clamp01(1-journey.ColdSeconds/8):0;
            if(!Mathf.Approximately(cold,frostAmount)){frostAmount=cold;frost.MarkDirtyRepaint();}
            string line=RouteInstruction();
            task.text=line;Show(task,line.Length>0&&!end);
            waybill.EnableInClassList("urgent",!hub&&!journey.AtLanding&&
                (journey.TunnelAhead || journey.InRoofTunnel || journey.Distance>=1400 || journey.Phase==JourneyPhase.Freezing));
            bool freefall=journey.Phase==JourneyPhase.Jumping;
            Show(deployPrompt,freefall&&!menu&&!end);
            if(freefall){
                Show(deployKey,journey.HasParachute);
                deployKey.text=Get("flight.space");
                deployAction.text=Get(journey.HasParachute?"flight.pull":"flight.noPack");
            }
            string hint=string.IsNullOrEmpty(journey.Notice)?journey.ActionPrompt():journey.Notice;
            message.text=hint;Show(message,!freefall&&!menu&&!bag&&!end&&!string.IsNullOrEmpty(hint));
            if(end){
                result.text=Get("ending.failed");
                root.Q<Button>("retry").text=Get(journey.RunExpired?"menu.new":"controls.retry");
                reason.text=Get(FailureReason());
                bool fatalImpact=journey.Phase==JourneyPhase.Fell && journey.LastImpactDamage>0;
                Show(impactResult,fatalImpact);
                if(fatalImpact) impactResult.text=string.Format(Get("flight.impact"),journey.LastImpactDamage,journey.Health);
            }
        }

        string FailureReason()
        {
            if(journey.RunExpired) return "run.timeout";
            if(journey.Phase==JourneyPhase.Frozen) return "ending.frozen";
            if(journey.Phase==JourneyPhase.Crashed) return "ending.crashed";
            if(journey.Phase==JourneyPhase.TunnelImpact) return "roof.impact";
            if(journey.CanopyWasDeployed) return "ending.lateCanopy";
            return journey.HasParachute?"ending.packedImpact":"ending.fell";
        }

        string RouteInstruction()
        {
            if(journey.InHub){
                if(journey.MissedTrainInHub) return Get("hub.done");
                if(journey.VisitingStation) return journey.Phase==JourneyPhase.Dwell
                    ? string.Format(Get("hub.wait"),Clock(journey.StationSeconds)) : Get("hub.chase");
                if(!journey.InsideCottage) return Get("cottage.outside");
                return Get("hub.ready");
            }
            if(journey.AtLanding) return Get("landing.safe");
            if(journey.InRoofTunnel) return Get("roof.inside");
            if(journey.TunnelAhead) return Get(journey.walker.IsCrouched?"roof.inside":"roof.warning");
            float d=journey.Distance;
            switch(journey.Phase){
                case JourneyPhase.Dwell: return string.Format(Get("dwell"),Clock(journey.StationSeconds));
                case JourneyPhase.Chase: return Get(journey.Aboard?"chase.on":"chase.off");
                case JourneyPhase.Freezing: return string.Format(Get("freezing"),Clock(journey.ColdSeconds));
                case JourneyPhase.Jumping: return Get("falling");
                case JourneyPhase.UnderCanopy: return Get("descending");
                case JourneyPhase.Running:
                    if(d>=1400) return string.Format(Get("bridge.time"),Clock(journey.SecondsToBreak))+"\n"+
                        Get(!journey.HasParachute?"bridge.pack":journey.EscapeReady?"bridge.ready":"bridge.wait");
                    if(d>=880&&d<1095) return Get("station.approach");
                    if(d<90) return Get("controls.walk");
                    break;
            }
            return "";
        }
    }
}
