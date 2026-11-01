using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Portfolio.PackFold
{
    public class PackFoldProView : MonoBehaviour
    {
        public PackFoldStudio studio;
        public Camera view;
        public Transform guides;
        public float yaw=-30,pitch=32,distance=.78f;
        private Vector3 focus=new Vector3(0,.93f,0);
        private GUIStyle title,label,muted,small,button,big;
        private readonly List<string> revisions=new List<string>();
        private int cursor=-1;
        private string last;
        private bool dragging,draggingLid,verify;
        private float dragStartY,dragStartOpening;
        private Vector3 dragOffset;
        private Plane dragPlane;
        private float captureTime=-1;
        private int verifyStage;
        private bool undoPassed;
        private bool closedPassed,openedPassed;
        private float started;
        private void Start()
        {
            started=Time.realtimeSinceStartup;studio.externalUI=true;Remember();verify=Array.IndexOf(Environment.GetCommandLineArgs(),"-portfolioVerify")>=0;
            Application.runInBackground=true;Application.targetFrameRate=60;
        }
        private void Remember()
        {
            string value=Snapshot();
            if(cursor>=0 && value==last) return;
            if(cursor<revisions.Count-1) revisions.RemoveRange(cursor+1,revisions.Count-cursor-1);
            revisions.Add(value);cursor=revisions.Count-1;last=value;
        }
        private string Snapshot()
        {
            return JsonUtility.ToJson(new PackFoldStudio.Design{structure=studio.StructureId,width_mm=studio.widthMm,depth_mm=studio.depthMm,height_mm=studio.heightMm,include_tabs=studio.includeTabs,lid_depth_mm=studio.lidDepthMm,lid_clearance_mm=studio.clearanceMm});
        }
        public void SaveRevision() {Remember();}
        public void Undo() {Recall(-1);}
        public void Redo() {Recall(1);}
        private void Recall(int delta)
        {
            if(cursor+delta<0 || cursor+delta>=revisions.Count) return;cursor+=delta;string value=revisions[cursor];
            float oldFold=studio.targetFold,oldOpen=studio.targetLidOpen;studio.ImportJson(value);studio.targetFold=oldFold;studio.targetLidOpen=oldOpen;last=Snapshot();
            studio.feedback="Restored design #"+(cursor+1).ToString("000")+" (structure, dimensions, flaps and lid fit).";
        }
        private void Update()
        {
            if(guides!=null) guides.gameObject.SetActive(studio.showDimensions);
            if(Input.GetKeyDown(KeyCode.Space)) {if(studio.twoPieceMode) studio.targetLidOpen=studio.targetLidOpen>.5f?0:1;else studio.targetFold=studio.targetFold>.5f?0:1;}
            if(Input.GetKeyDown(KeyCode.F)) {yaw=-30;pitch=32;distance=.78f;}
            float x=Input.mousePosition.x/Screen.width;
            bool overUI=x<.20f || x>.80f;
            if(Input.GetMouseButtonDown(0) && !overUI)
            {
                Ray ray=view.ScreenPointToRay(Input.mousePosition);
                if(Physics.Raycast(ray,out RaycastHit hit,3) && hit.transform.IsChildOf(studio.modelRoot))
                {
                    dragging=true;draggingLid=studio.twoPieceMode&&studio.fold>.96f&&studio.DetachableLid!=null&&hit.transform.IsChildOf(studio.DetachableLid)&&!Input.GetKey(KeyCode.LeftShift);
                    dragStartY=Input.mousePosition.y;dragStartOpening=studio.targetLidOpen;
                    dragPlane=new Plane(Vector3.up,studio.modelRoot.position);if(dragPlane.Raycast(ray,out float enter)) dragOffset=studio.modelRoot.position-ray.GetPoint(enter);
                }
            }
            if(Input.GetMouseButtonUp(0)) {dragging=false;Remember();}
            if(dragging)
            {
                if(draggingLid) studio.targetLidOpen=Mathf.Clamp01(dragStartOpening+(Input.mousePosition.y-dragStartY)/180f);
                else if(Input.GetKey(KeyCode.LeftShift)) studio.modelRoot.Rotate(0,Input.GetAxis("Mouse X")*3,0,Space.World);
                else {Ray ray=view.ScreenPointToRay(Input.mousePosition);if(dragPlane.Raycast(ray,out float enter)) {Vector3 p=ray.GetPoint(enter)+dragOffset;p.x=Mathf.Clamp(p.x,-.18f,.18f);p.z=Mathf.Clamp(p.z,-.18f,.18f);studio.modelRoot.position=p;}}
            }
            if(!overUI)
            {
                if(Input.GetMouseButton(1)) {yaw+=Input.GetAxis("Mouse X")*2.5f;pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*2,12,75);}
                distance=Mathf.Clamp(distance-Input.mouseScrollDelta.y*.035f,.38f,1.6f);
            }
            if(!Input.GetMouseButton(0) && Snapshot()!=last) Remember();
            float sizeFactor=Mathf.Max(studio.widthMm/180,studio.depthMm/120,studio.heightMm/70);
            float framing=distance*Mathf.Lerp(1,Mathf.Clamp(sizeFactor,1,1.7f),.6f)+(studio.twoPieceMode?(1-studio.fold)*.45f:0);
            Vector3 lookAt=focus+Vector3.up*(studio.twoPieceMode?studio.lidOpen*.035f:0);
            Quaternion rotation=Quaternion.Euler(pitch,yaw,0);view.transform.position=lookAt+rotation*new Vector3(0,0,-framing);view.transform.LookAt(lookAt);
            if(verify)
            {
                float elapsed=Time.realtimeSinceStartup-started;
                if(elapsed>.7f && verifyStage==0) {studio.targetLidOpen=0;verifyStage=1;}
                if(elapsed>2.3f && verifyStage==1) {closedPassed=studio.lidOpen<.01f;ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath,"..","runtime_closed.png"));verifyStage=2;}
                if(elapsed>2.8f && verifyStage==2) {studio.targetLidOpen=1;verifyStage=3;}
                if(elapsed>4.6f && verifyStage==3) {openedPassed=studio.lidOpen>.99f;ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath,"..","runtime_open.png"));verifyStage=4;}
                if(elapsed>5.1f && verifyStage==4) {SelectPreset(0);verifyStage=5;}
                if(elapsed>6 && verifyStage==5) {Recall(-1);undoPassed=studio.twoPieceMode && studio.widthMm==180;verifyStage=6;}
                if(elapsed>7 && verifyStage==6) {studio.Export(Path.Combine(Application.dataPath,"..","VerificationExports"));GetComponent<PackFoldAdvisorClient>().RequestRecommendation();verifyStage=7;}
                if(elapsed>9 && captureTime<0) {captureTime=elapsed;ScreenCapture.CaptureScreenshot(Path.Combine(Application.dataPath,"..","runtime_preview.png"));}
                if(elapsed>14)
                {
                    bool api=studio.LastImportedSource!=null&&(studio.LastImportedSource.StartsWith("rule_based")||studio.LastImportedSource.StartsWith("external_ai_provider"));studio.Export(Path.Combine(Application.dataPath,"..","VerificationExports"));
                    bool nets=File.Exists(Path.Combine(Application.dataPath,"..","VerificationExports","base_dieline.svg"))&&File.Exists(Path.Combine(Application.dataPath,"..","VerificationExports","lid_dieline.svg"));
                    File.WriteAllText(Path.Combine(Application.dataPath,"..","runtime_check.json"),"{\"rendered\":true,\"undo_passed\":"+undoPassed.ToString().ToLowerInvariant()+",\"lid_closed_passed\":"+closedPassed.ToString().ToLowerInvariant()+",\"lid_open_passed\":"+openedPassed.ToString().ToLowerInvariant()+",\"two_nets_exported\":"+nets.ToString().ToLowerInvariant()+",\"local_api_passed\":"+api.ToString().ToLowerInvariant()+",\"include_tabs\":"+studio.includeTabs.ToString().ToLowerInvariant()+",\"structure\":\""+studio.StructureId+"\"}");Application.Quit(undoPassed&&api&&closedPassed&&openedPassed&&nets&&studio.includeTabs?0:2);
                }
            }
        }
        private void Styles()
        {
            if(title!=null) return;
            title=new GUIStyle(GUI.skin.label){fontSize=27,fontStyle=FontStyle.Bold,normal={textColor=new Color(.94f,.97f,1)}};
            label=new GUIStyle(GUI.skin.label){fontSize=14,normal={textColor=new Color(.86f,.91f,.96f)},wordWrap=true};
            muted=new GUIStyle(label){fontSize=12,normal={textColor=new Color(.52f,.63f,.71f)}};
            small=new GUIStyle(label){fontSize=11,fontStyle=FontStyle.Bold,normal={textColor=new Color(.31f,.86f,.73f)}};
            big=new GUIStyle(title){fontSize=34};button=new GUIStyle(GUI.skin.button){fontSize=13};
            button.normal.background=ColorTexture(new Color(.10f,.16f,.20f));button.normal.textColor=new Color(.88f,.95f,.96f);
            button.hover.background=ColorTexture(new Color(.12f,.29f,.29f));button.hover.textColor=Color.white;button.active.background=ColorTexture(new Color(.08f,.40f,.34f));button.active.textColor=Color.white;
        }
        private static Texture2D ColorTexture(Color color) {Texture2D t=new Texture2D(1,1);t.SetPixel(0,0,color);t.Apply();return t;}
        private void Rect(float x,float y,float w,float h,Color color) {GUI.color=color;GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);GUI.color=Color.white;}
        public void SelectPreset(int preset)
        {
            studio.SetStructure(preset==2?"open_tray":preset==3?"lift_off_lid":"hinged_lid");
            studio.widthMm=preset==1?240:preset==2?210:180;studio.depthMm=preset==1?160:preset==2?150:120;studio.heightMm=preset==1?90:preset==2?50:70;
            studio.lidDepthMm=28;studio.clearanceMm=.8f;studio.lidOpen=studio.targetLidOpen=1;studio.targetFold=1;studio.Rebuild();studio.ApplyFold(1);Remember();
        }
        private void OnGUI()
        {
            Styles();float scale=Screen.height/900f;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);float screenW=Screen.width/scale,right=screenW-300;
            Rect(0,0,300,900,new Color(.025f,.045f,.065f,.98f));Rect(right,0,300,900,new Color(.025f,.045f,.065f,.98f));Rect(300,0,screenW-600,63,new Color(.025f,.045f,.065f,.93f));
            GUI.Label(new Rect(24,26,260,20),"SPATIAL DESIGN / PACKAGING",small);GUI.Label(new Rect(24,57,260,40),"PackFold Studio",title);
            GUI.Label(new Rect(24,110,260,36),studio.twoPieceMode?"FIELDKIT / LIFT-OFF GIFT BOX":"FIELDKIT / SENSOR EDITION",muted);GUI.Label(new Rect(24,167,250,20),"CONSTRUCTION",small);
            GUI.Label(new Rect(24,204,250,44),Mathf.RoundToInt(studio.fold*100)+"%",big);
            studio.targetFold=GUI.HorizontalSlider(new Rect(24,264,250,20),studio.targetFold,0,1);
            if(GUI.Button(new Rect(24,305,119,37),"UNFOLD",button)) studio.targetFold=0;
            if(GUI.Button(new Rect(155,305,119,37),"FOLD",button)) studio.targetFold=1;
            if(studio.twoPieceMode)
            {
                if(GUI.Button(new Rect(24,355,119,36),"LIFT LID",button)) studio.targetLidOpen=1;
                if(GUI.Button(new Rect(155,355,119,36),"CLOSE LID",button)) studio.targetLidOpen=0;
            }
            GUI.Label(new Rect(24,416,250,20),"STRUCTURE LIBRARY",small);
            if(GUI.Button(new Rect(24,450,250,33),"Hinged sensor kit",button)) SelectPreset(0);
            if(GUI.Button(new Rect(24,490,250,33),"Hinged controller box",button)) SelectPreset(1);
            if(GUI.Button(new Rect(24,530,250,33),"Open presentation tray",button)) SelectPreset(2);
            if(GUI.Button(new Rect(24,570,250,33),"NEW  /  Lift-off gift box",button)) SelectPreset(3);
            GUI.Label(new Rect(24,632,250,20),"DISPLAY",small);
            studio.exploded=GUI.Toggle(new Rect(24,665,250,25),studio.exploded>.5f,"  Exploded assembly")?1:0;
            studio.showDimensions=GUI.Toggle(new Rect(24,699,250,25),studio.showDimensions,"  Dimension guides");
            bool tabs=GUI.Toggle(new Rect(24,733,250,25),studio.includeTabs,"  Glue and tuck flaps");if(tabs!=studio.includeTabs) {studio.includeTabs=tabs;studio.Rebuild();}
            if(GUI.Button(new Rect(24,775,250,33),"RESET OBJECT POSE",button)) {studio.modelRoot.localPosition=Vector3.zero;studio.modelRoot.localRotation=Quaternion.identity;studio.modelRoot.localScale=Vector3.one;}
            GUI.Label(new Rect(24,829,250,70),studio.twoPieceMode?"Drag lid up/down  Open / close\nDrag body  Move   Shift + drag  Rotate\nRight drag  Orbit   Wheel  Zoom\nSpace  Lift / close lid":"Drag box  Move\nShift + drag box  Rotate\nRight drag  Orbit   Wheel  Zoom\nSpace  Fold / unfold",muted);
            GUI.Label(new Rect(328,21,screenW-650,25),"PAPER STRUCTURES  /  INTERACTIVE DESIGN",label);
            GUI.Label(new Rect(right+24,26,250,20),"DESIGN PARAMETERS",small);GUI.Label(new Rect(right+24,60,250,30),"Design #"+(cursor+1).ToString("000"),title);
            GUI.Label(new Rect(right+24,118,250,24),"Width   "+studio.widthMm.ToString("F0")+" mm",label);studio.widthMm=Mathf.Round(GUI.HorizontalSlider(new Rect(right+24,146,250,20),studio.widthMm,60,300));
            GUI.Label(new Rect(right+24,178,250,24),"Depth   "+studio.depthMm.ToString("F0")+" mm",label);studio.depthMm=Mathf.Round(GUI.HorizontalSlider(new Rect(right+24,206,250,20),studio.depthMm,50,240));
            GUI.Label(new Rect(right+24,238,250,24),"Height   "+studio.heightMm.ToString("F0")+" mm",label);studio.heightMm=Mathf.Round(GUI.HorizontalSlider(new Rect(right+24,266,250,20),studio.heightMm,25,150));
            GUI.Label(new Rect(right+24,307,250,24),"Volume   "+studio.VolumeLitres.ToString("F2")+" L",label);GUI.Label(new Rect(right+24,335,250,24),"Net area   "+(studio.TotalBoardAreaMm2/100).ToString("F0")+" cm2",label);
            float next=380;
            if(studio.twoPieceMode)
            {
                GUI.Label(new Rect(right+24,377,250,24),"Lid skirt   "+studio.lidDepthMm.ToString("F0")+" mm",label);studio.lidDepthMm=Mathf.Round(GUI.HorizontalSlider(new Rect(right+24,405,250,20),studio.lidDepthMm,8,Mathf.Min(45,studio.heightMm*.7f)));
                GUI.Label(new Rect(right+24,442,250,24),"Side clearance   "+studio.clearanceMm.ToString("F1")+" mm",label);studio.clearanceMm=Mathf.Round(GUI.HorizontalSlider(new Rect(right+24,470,250,20),studio.clearanceMm,.2f,2.5f)*10)/10;next=513;
            }
            if(GUI.Button(new Rect(right+24,next,119,33),"UNDO",button)) Undo();if(GUI.Button(new Rect(right+155,next,119,33),"REDO",button)) Redo();
            GUI.Label(new Rect(right+24,next+58,250,20),"DESIGN ASSISTANT",small);
            PackFoldAdvisorClient advisor=GetComponent<PackFoldAdvisorClient>();
            advisor.fragile=GUI.Toggle(new Rect(right+24,next+89,119,24),advisor.fragile," Fragile");advisor.reusable=GUI.Toggle(new Rect(right+155,next+89,120,24),advisor.reusable," Reusable");
            advisor.brief=GUI.TextArea(new Rect(right+24,next+120,250,50),advisor.brief,400);
            if(GUI.Button(new Rect(right+24,next+182,250,33),"REQUEST RECOMMENDATION",button)) advisor.RequestRecommendation();
            if(GUI.Button(new Rect(right+24,next+228,250,33),"EXPORT DIELINES + JSON",button)) studio.Export();
            if(GUI.Button(new Rect(right+24,next+274,250,33),"SAVE SCREENSHOT",button)) {string f=Path.Combine(Application.persistentDataPath,"PackFoldExports");Directory.CreateDirectory(f);ScreenCapture.CaptureScreenshot(Path.Combine(f,"design_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".png"));studio.feedback="Screenshot saved to "+f;}
            GUI.Label(new Rect(right+24,840,250,58),"Offline rules by default\nQuest / passthrough not tested\nPrototype fit; verify with real paper",muted);
            Rect(322,804,screenW-644,70,new Color(.025f,.045f,.065f,.94f));GUI.Label(new Rect(338,817,screenW-678,46),studio.feedback,label);
            GUI.matrix=Matrix4x4.identity;
        }
    }
}
