using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Portfolio.PackFold
{
    public class PackFoldStudio : MonoBehaviour
    {
        public Transform modelRoot;
        public Material boardMaterial;
        public float widthMm=180, depthMm=120, heightMm=70, fold=1, targetFold=1;
        public string feedback="Desktop prototype | no headset validation yet";
        public bool trayMode;
        public bool twoPieceMode;
        public float lidDepthMm=28,clearanceMm=.8f,lidOpen=1,targetLidOpen=1;
        public Material lidMaterial,linerMaterial,deviceMaterial;
        public bool showContents=true;
        public bool externalUI=true,includeTabs=true;
        public Material creaseMaterial,printMaterial;
        public float exploded;
        public bool showDimensions=true;
        private readonly List<Transform> glueTabs=new List<Transform>();
        private Transform tuckTab;
        private Transform left,right,front,back,lid;
        private Transform bodyRoot,detachableLid,contents;
        private readonly List<Transform> capWalls=new List<Transform>(),capTabs=new List<Transform>();
        private bool lastTwoPiece,lastTabs;
        private float lastLidDepth,lastClearance,nextRebuild;
        public int RebuildCount {get;private set;}
        public string LastImportedSource {get;private set;}
        private float lastW=-1,lastD,lastH;
        private bool lastTray;
        public Transform LidHinge { get { return lid; } }
        public Transform BodyRoot {get {return bodyRoot==null?modelRoot:bodyRoot;}}
        public Transform DetachableLid {get {return detachableLid;}}
        public string StructureId {get {return twoPieceMode?"lift_off_lid":trayMode?"open_tray":"hinged_lid";}}
        public float LidWidthMm {get {return widthMm+2.4f+2*clearanceMm;}}
        public float LidDepthPlanMm {get {return depthMm+2.4f+2*clearanceMm;}}
        public float ClosedLidY {get {return heightMm*.001f+.0012f;}}
        public float TotalBoardAreaMm2 {get {return PackagingDieline.NetArea(widthMm,depthMm,heightMm,trayMode||twoPieceMode,includeTabs)+(twoPieceMode?PackagingDieline.NetArea(LidWidthMm,LidDepthPlanMm,lidDepthMm,true,includeTabs):0);}}
        public void SetStructure(string id)
        {
            if(id!="hinged_lid" && id!="open_tray" && id!="lift_off_lid") throw new ArgumentException("Unsupported structure");
            twoPieceMode=id=="lift_off_lid";trayMode=id=="open_tray";
        }

        public void Rebuild()
        {
            widthMm=Mathf.Clamp(widthMm,60,300); depthMm=Mathf.Clamp(depthMm,50,240); heightMm=Mathf.Clamp(heightMm,25,150);
            clearanceMm=Mathf.Clamp(clearanceMm,.2f,2.5f);lidDepthMm=Mathf.Clamp(lidDepthMm,8,Mathf.Min(45,heightMm*.7f));
            if(twoPieceMode) trayMode=false;
            if(modelRoot==null) { modelRoot=new GameObject("Parametric packaging").transform; modelRoot.SetParent(transform,false); }
            for(int i=modelRoot.childCount-1;i>=0;i--)
            {
                GameObject old=modelRoot.GetChild(i).gameObject;
                old.SetActive(false);
                if(Application.isPlaying) Destroy(old); else DestroyImmediate(old);
            }
            float w=widthMm*.001f,d=depthMm*.001f,h=heightMm*.001f;
            glueTabs.Clear();capWalls.Clear();capTabs.Clear();detachableLid=contents=null;
            bodyRoot=Hinge("Box body assembly",modelRoot,Vector3.zero);
            Face("Base",bodyRoot,new Vector3(0,0,0),new Vector3(w,.0012f,d));
            left=Hinge("Left hinge",bodyRoot,new Vector3(-w/2,0,0));
            Face("Left wall",left,new Vector3(-h/2,0,0),new Vector3(h,.0012f,d));
            right=Hinge("Right hinge",bodyRoot,new Vector3(w/2,0,0));
            Face("Right wall",right,new Vector3(h/2,0,0),new Vector3(h,.0012f,d));
            front=Hinge("Front hinge",bodyRoot,new Vector3(0,0,-d/2));
            Face("Front wall",front,new Vector3(0,0,-h/2),new Vector3(w,.0012f,h));
            back=Hinge("Back hinge",bodyRoot,new Vector3(0,0,d/2));
            Face("Back wall",back,new Vector3(0,0,h/2),new Vector3(w,.0012f,h));
            lid=Hinge("Nested lid hinge",back,new Vector3(0,0,h));
            Face("Lid",lid,new Vector3(0,0,d/2),new Vector3(w,.0012f,d));
            if(includeTabs)
            {
                float g=Mathf.Min(.018f,h*.4f);
                foreach(Transform wall in new[]{front,back})
                {
                    float sign=wall==front?-1:1;
                    foreach(float side in new[]{-1f,1f})
                    {
                        Transform tab=Hinge("Glue flap",wall,new Vector3(side*w/2,0,sign*h/2));
                        Face("Glue area",tab,new Vector3(side*g/2,0,0),new Vector3(g,.001f,h*.85f));glueTabs.Add(tab);
                    }
                }
                tuckTab=Hinge("Tuck flap hinge",lid,new Vector3(0,0,d));Face("Front tuck flap",tuckTab,new Vector3(0,0,g/2),new Vector3(w*.88f,.001f,g));
            }
            else tuckTab=null;
            AddPrint(lid,w,d);
            lid.gameObject.SetActive(!trayMode&&!twoPieceMode);
            if(twoPieceMode) BuildSeparateLid(w,d,h);
            lastW=widthMm; lastD=depthMm; lastH=heightMm; lastTray=trayMode;
            lastTwoPiece=twoPieceMode;lastTabs=includeTabs;lastLidDepth=lidDepthMm;lastClearance=clearanceMm;nextRebuild=Time.unscaledTime+.08f;RebuildCount++;
            ApplyFold(fold);
        }

        private Transform Hinge(string name,Transform parent,Vector3 position)
        {
            Transform t=new GameObject(name).transform; t.SetParent(parent,false); t.localPosition=position; return t;
        }
        private void Face(string name,Transform parent,Vector3 center,Vector3 scale,Material material=null)
        {
            GameObject panel=GameObject.CreatePrimitive(PrimitiveType.Cube); panel.name=name;
            panel.transform.SetParent(parent,false); panel.transform.localPosition=center; panel.transform.localScale=scale;
            panel.GetComponent<Renderer>().sharedMaterial=material==null?boardMaterial:material;
            if(creaseMaterial!=null)
            {
                GameObject edge=new GameObject("Cut edge");edge.transform.SetParent(parent,false);
                LineRenderer line=edge.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.positionCount=4;line.startWidth=line.endWidth=.00045f;line.sharedMaterial=creaseMaterial;
                float y=center.y+scale.y*.52f;
                line.SetPositions(new[]{new Vector3(center.x-scale.x/2,y,center.z-scale.z/2),new Vector3(center.x+scale.x/2,y,center.z-scale.z/2),new Vector3(center.x+scale.x/2,y,center.z+scale.z/2),new Vector3(center.x-scale.x/2,y,center.z+scale.z/2)});
            }
        }
        private void AddPrint(Transform parent,float w,float d)
        {
            if(printMaterial==null) return;
            GameObject stripe=GameObject.CreatePrimitive(PrimitiveType.Cube);stripe.name="Teal printed band";stripe.transform.SetParent(parent,false);
            stripe.transform.localPosition=new Vector3(-w*.31f,-.00085f,d*.5f);stripe.transform.localScale=new Vector3(w*.13f,.00008f,d*.90f);stripe.GetComponent<Renderer>().sharedMaterial=printMaterial;
            TextMesh title=new GameObject("Printed product title").AddComponent<TextMesh>();title.transform.SetParent(parent,false);title.transform.localPosition=new Vector3(w*.06f,-.001f,d*.46f);title.transform.localRotation=Quaternion.Euler(-90,0,0);
            title.text="FIELDKIT\nROBOTICS TOOLS";title.fontSize=96;title.characterSize=.0015f;title.anchor=TextAnchor.MiddleCenter;title.alignment=TextAlignment.Center;title.color=new Color(.06f,.10f,.11f);title.gameObject.AddComponent<PrintedSide>();
            TextMesh caption=new GameObject("Printed SKU").AddComponent<TextMesh>();caption.transform.SetParent(parent,false);caption.transform.localPosition=new Vector3(w*.05f,-.001f,d*.79f);caption.transform.localRotation=Quaternion.Euler(-90,0,0);
            caption.text="SENSOR EDITION  /  MK.02";caption.fontSize=72;caption.characterSize=.00055f;caption.anchor=TextAnchor.MiddleCenter;caption.color=new Color(.1f,.16f,.17f);caption.gameObject.AddComponent<PrintedSide>();
        }
        private void BuildSeparateLid(float w,float d,float h)
        {
            float cw=LidWidthMm*.001f,cd=LidDepthPlanMm*.001f,ch=lidDepthMm*.001f;
            detachableLid=Hinge("Lift-off lid assembly",modelRoot,Vector3.zero);
            Face("Separate lid top",detachableLid,Vector3.zero,new Vector3(cw,.0012f,cd),lidMaterial);
            Vector3[] anchors={new Vector3(-cw/2,0,0),new Vector3(cw/2,0,0),new Vector3(0,0,-cd/2),new Vector3(0,0,cd/2)};
            for(int i=0;i<4;i++)
            {
                Transform wall=Hinge("Lid skirt "+i,detachableLid,anchors[i]);capWalls.Add(wall);
                Vector3 center=i<2?new Vector3((i==0?-1:1)*ch/2,0,0):new Vector3(0,0,(i==2?-1:1)*ch/2);
                Face("Lid skirt panel",wall,center,i<2?new Vector3(ch,.0012f,cd):new Vector3(cw,.0012f,ch),lidMaterial);
                if(includeTabs && i>=2)
                {
                    float g=Mathf.Min(.018f,ch*.4f),sign=i==2?-1:1;
                    foreach(float side in new[]{-1f,1f})
                    {
                        Transform tab=Hinge("Lid glue flap",wall,new Vector3(side*cw/2,0,sign*ch/2));
                        Face("Lid glue area",tab,new Vector3(side*g/2,0,0),new Vector3(g,.001f,ch*.85f),lidMaterial);capTabs.Add(tab);
                    }
                }
            }
            Transform print=Hinge("Lid exterior artwork",detachableLid,Vector3.zero);print.localRotation=Quaternion.Euler(180,0,0);print.localPosition=new Vector3(0,0,cd/2);
            AddPrint(print,cw,cd);
            if(linerMaterial!=null)
            {
                contents=Hinge("Removable sensor insert",bodyRoot,new Vector3(0,.006f,0));
                float iw=w*.84f,id=d*.80f;
                Solid("Insert floor",contents,new Vector3(0,0,0),new Vector3(iw,.004f,id),linerMaterial);
                foreach(float x in new[]{-iw/2,0,iw/2}) Solid("Foam divider",contents,new Vector3(x,.004f,0),new Vector3(.004f,.012f,id),linerMaterial);
                foreach(float z in new[]{-id/2,id/2}) Solid("Foam border",contents,new Vector3(0,.004f,z),new Vector3(iw,.012f,.004f),linerMaterial);
                foreach(float side in new[]{-1f,1f})
                {
                    Transform module=Hinge("Sensor module",contents,new Vector3(side*iw*.25f,.010f,0));
                    Solid("Machined sensor housing",module,Vector3.zero,new Vector3(iw*.30f,Mathf.Min(.018f,h*.25f),id*.58f),deviceMaterial??printMaterial);
                    Solid("Sensor optical window",module,new Vector3(0,.010f,0),new Vector3(iw*.17f,.002f,id*.28f),printMaterial);
                }
                Solid("Pull ribbon",contents,new Vector3(0,.006f,-d*.35f),new Vector3(w*.12f,.001f,d*.32f),printMaterial);
            }
        }
        private Transform Solid(string name,Transform parent,Vector3 center,Vector3 size,Material material)
        {
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=center;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;
        }

        public void ApplyFold(float progress)
        {
            fold=Mathf.Clamp01(progress); if(left==null) return;
            float side=Mathf.Clamp01(fold*1.5f), main=Mathf.Clamp01(fold*1.25f);
            float cover=Mathf.Clamp01((fold-.55f)/.45f);
            left.localRotation=Quaternion.Euler(0,0,-90*side); right.localRotation=Quaternion.Euler(0,0,90*side);
            front.localRotation=Quaternion.Euler(90*main,0,0); back.localRotation=Quaternion.Euler(-90*main,0,0);
            lid.localRotation=Quaternion.Euler(-90*cover,0,0);
            for(int i=0;i<glueTabs.Count;i++) glueTabs[i].localRotation=Quaternion.Euler(0,0,(i%2==0?-90:90)*Mathf.Clamp01(fold*1.5f));
            if(tuckTab!=null) tuckTab.localRotation=Quaternion.Euler(-90*Mathf.Clamp01((fold-.82f)/.18f),0,0);
            float e=exploded*.035f;
            left.localPosition=new Vector3(-widthMm*.0005f-e,0,0);right.localPosition=new Vector3(widthMm*.0005f+e,0,0);
            front.localPosition=new Vector3(0,0,-depthMm*.0005f-e);back.localPosition=new Vector3(0,0,depthMm*.0005f+e);
            bodyRoot.localPosition=Vector3.zero;
            if(detachableLid!=null)
            {
                float s=Mathf.Clamp01(fold*1.35f);
                capWalls[0].localRotation=Quaternion.Euler(0,0,90*s);capWalls[1].localRotation=Quaternion.Euler(0,0,-90*s);
                capWalls[2].localRotation=Quaternion.Euler(-90*s,0,0);capWalls[3].localRotation=Quaternion.Euler(90*s,0,0);
                for(int i=0;i<capTabs.Count;i++) capTabs[i].localRotation=Quaternion.Euler(0,0,(i%2==0?90:-90)*s);
                float layout=(1-fold)*(1-fold)*(widthMm*.001f+heightMm*.001f+lidDepthMm*.001f+.04f);
                bodyRoot.localPosition=Vector3.left*layout*.5f;
                float lift=Mathf.SmoothStep(0,1,Mathf.Clamp01(lidOpen/.55f))*Mathf.Max(.10f,heightMm*.0011f);
                float slide=Mathf.SmoothStep(0,1,Mathf.Clamp01((lidOpen-.45f)/.55f));
                detachableLid.localPosition=new Vector3(layout*.5f+slide*widthMm*.00065f,ClosedLidY*fold+lift*fold+e*2,0);
                detachableLid.localRotation=Quaternion.Euler(0,0,-10*slide*fold);
                if(contents!=null) contents.gameObject.SetActive(showContents&&fold>.96f);
            }
        }
        private void Update()
        {
            if((lastW!=widthMm || lastD!=depthMm || lastH!=heightMm || lastTray!=trayMode || lastTwoPiece!=twoPieceMode || lastTabs!=includeTabs || lastLidDepth!=lidDepthMm || lastClearance!=clearanceMm) && Time.unscaledTime>=nextRebuild) Rebuild();
            lidOpen=Mathf.MoveTowards(lidOpen,targetLidOpen,Time.deltaTime*.8f);
            ApplyFold(Mathf.MoveTowards(fold,targetFold,Time.deltaTime*.55f));
        }
        public float BoardAreaMm2 { get { return widthMm*depthMm*(trayMode?1:2)+2*heightMm*(widthMm+depthMm); } }
        public float VolumeLitres { get { return widthMm*depthMm*heightMm/1000000; } }

        [Serializable] public class Design
        {
            public string schema="packfold.v1",structure,source="manual";
            public float width_mm,depth_mm,height_mm;
            public bool include_tabs=true;
            public float fold_fraction=1,nominal_wall_thickness_mm=1.2f;
            public string material="kraft_cardboard";
            public float lid_depth_mm=28,lid_clearance_mm=.8f,lid_open_fraction=1;
        }
        public string Export(string directory=null)
        {
            directory=directory??Path.Combine(Application.persistentDataPath,"PackFoldExports","design_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(directory);
            Design spec=new Design {structure=StructureId,width_mm=widthMm,depth_mm=depthMm,height_mm=heightMm,include_tabs=includeTabs,fold_fraction=fold,lid_depth_mm=lidDepthMm,lid_clearance_mm=clearanceMm,lid_open_fraction=lidOpen};
            File.WriteAllText(Path.Combine(directory,"design.json"),JsonUtility.ToJson(spec,true));
            if(twoPieceMode)
            {
                string body=PackagingDieline.Create(widthMm,depthMm,heightMm,true,includeTabs).Replace(">BASE<",">BOX BASE<");
                string cap=PackagingDieline.Create(LidWidthMm,LidDepthPlanMm,lidDepthMm,true,includeTabs).Replace(">BASE<",">LID TOP<");
                File.WriteAllText(Path.Combine(directory,"base_dieline.svg"),body);File.WriteAllText(Path.Combine(directory,"lid_dieline.svg"),cap);
                File.WriteAllText(Path.Combine(directory,"dieline.svg"),PackagingDieline.Combine(body,cap));
            }
            else File.WriteAllText(Path.Combine(directory,"dieline.svg"),PackagingDieline.Create(widthMm,depthMm,heightMm,trayMode,includeTabs));
            feedback="Saved "+(twoPieceMode?"base and lid dielines":"dieline")+" + design.json to "+directory; Debug.Log(feedback); return directory;
        }
        public void ImportJson(string json)
        {
            Design spec=JsonUtility.FromJson<Design>(json);
            if(spec==null || spec.schema!="packfold.v1" || (spec.structure!="open_tray" && spec.structure!="hinged_lid" && spec.structure!="lift_off_lid") ||
                float.IsNaN(spec.width_mm) || float.IsInfinity(spec.width_mm) || spec.width_mm<60 || spec.width_mm>300 ||
                float.IsNaN(spec.depth_mm) || float.IsInfinity(spec.depth_mm) || spec.depth_mm<50 || spec.depth_mm>240 ||
                float.IsNaN(spec.height_mm) || float.IsInfinity(spec.height_mm) || spec.height_mm<25 || spec.height_mm>150)
                throw new ArgumentException("Invalid design JSON or dimensions outside supported range.");
            foreach(string key in new[]{"fold_fraction","lid_open_fraction","lid_depth_mm","lid_clearance_mm"})
            {
                if(!json.Contains("\""+key+"\"")) continue;
                float v=key=="fold_fraction"?spec.fold_fraction:key=="lid_open_fraction"?spec.lid_open_fraction:key=="lid_depth_mm"?spec.lid_depth_mm:spec.lid_clearance_mm;
                float min=key=="lid_depth_mm"?8:key=="lid_clearance_mm"?.2f:0,max=key=="lid_depth_mm"?(spec.structure=="lift_off_lid"?Mathf.Min(45,spec.height_mm*.7f):45):key=="lid_clearance_mm"?2.5f:1;
                if(float.IsNaN(v)||float.IsInfinity(v)||v<min||v>max) throw new ArgumentException("Invalid "+key);
            }
            if(json.Contains("\"nominal_wall_thickness_mm\"") && (float.IsNaN(spec.nominal_wall_thickness_mm)||float.IsInfinity(spec.nominal_wall_thickness_mm)||Mathf.Abs(spec.nominal_wall_thickness_mm-1.2f)>.0001f))
                throw new ArgumentException("This model uses 1.2 mm nominal panels.");
            widthMm=spec.width_mm; depthMm=spec.depth_mm; heightMm=spec.height_mm; SetStructure(spec.structure);
            if(json.Contains("\"include_tabs\"")) includeTabs=spec.include_tabs;
            if(json.Contains("\"fold_fraction\"")) targetFold=Mathf.Clamp01(spec.fold_fraction);
            if(json.Contains("\"lid_depth_mm\"")) lidDepthMm=spec.lid_depth_mm;
            if(json.Contains("\"lid_clearance_mm\"")) clearanceMm=spec.lid_clearance_mm;
            if(json.Contains("\"lid_open_fraction\"")) targetLidOpen=spec.lid_open_fraction;
            Rebuild();LastImportedSource=spec.source??"manual";
            feedback=LastImportedSource.StartsWith("rule_based")?"Offline recommendation applied: "+StructureId.Replace('_',' ')+" / "+widthMm.ToString("F0")+" x "+depthMm.ToString("F0")+" x "+heightMm.ToString("F0")+" mm.":"Design imported: "+StructureId.Replace('_',' ')+".";
        }
        public void ImportAdvisorFile()
        {
            string path=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Python_Advisor","recommendation.json"));
            try { ImportJson(File.ReadAllText(path)); } catch(Exception e) { feedback=e.Message; }
        }

        public static string DielineSvg(float w,float d,float h,bool tray)
        {return PackagingDieline.Create(w,d,h,tray,true);}

        private void OnGUI()
        {
            if(externalUI) return;
            GUILayout.BeginArea(new Rect(14,14,375,590),GUI.skin.box);
            GUILayout.Label("PACKFOLD STUDIO | Interactive packaging");
            GUILayout.Label("Parametric model / hinge hierarchy / SVG net");
            GUILayout.Label("Width: "+widthMm.ToString("F0")+" mm"); widthMm=Mathf.Round(GUILayout.HorizontalSlider(widthMm,60,300));
            GUILayout.Label("Depth: "+depthMm.ToString("F0")+" mm"); depthMm=Mathf.Round(GUILayout.HorizontalSlider(depthMm,50,240));
            GUILayout.Label("Height: "+heightMm.ToString("F0")+" mm"); heightMm=Mathf.Round(GUILayout.HorizontalSlider(heightMm,25,150));
            GUILayout.Label("Fold: "+(targetFold*100).ToString("F0")+"%"); targetFold=GUILayout.HorizontalSlider(targetFold,0,1);
            GUILayout.BeginHorizontal(); if(GUILayout.Button("Unfold")) targetFold=0; if(GUILayout.Button("Fold")) targetFold=1; GUILayout.EndHorizontal();
            trayMode=GUILayout.Toggle(trayMode,"Open tray (remove lid)");
            GUILayout.Label("Volume: "+VolumeLitres.ToString("F2")+" L | Board: "+(BoardAreaMm2/100).ToString("F1")+" cm2");
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Rotate -")) modelRoot.Rotate(0,-15,0,Space.World);
            if(GUILayout.Button("Rotate +")) modelRoot.Rotate(0,15,0,Space.World);
            if(GUILayout.Button("Reset pose")) { modelRoot.localPosition=Vector3.zero; modelRoot.localRotation=Quaternion.identity; modelRoot.localScale=Vector3.one; }
            GUILayout.EndHorizontal();
            GUILayout.Label("Preview scale (dimensions stay in mm)"); float scale=GUILayout.HorizontalSlider(modelRoot.localScale.x,.5f,2f); modelRoot.localScale=Vector3.one*scale;
            GUILayout.Label("Move X / Z"); Vector3 p=modelRoot.localPosition; p.x=GUILayout.HorizontalSlider(p.x,-.25f,.25f); p.z=GUILayout.HorizontalSlider(p.z,-.25f,.25f); modelRoot.localPosition=p;
            if(GUILayout.Button("Export SVG + JSON")) Export();
            if(GUILayout.Button("Import Python advisor recommendation")) ImportAdvisorFile();
            if(GUILayout.Button("Request advisor API (optional)"))
            {
                PackFoldAdvisorClient client=GetComponent<PackFoldAdvisorClient>();
                if(client!=null) client.RequestRecommendation();
            }
            GUILayout.Label(feedback);
            GUILayout.Label("XR input bridge included; OpenXR setup and headset test required.");
            GUILayout.EndArea();
        }
    }
}
