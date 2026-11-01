using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Portfolio.PackFold
{
    // A revision is a complete design and presentation pose. Preview scale never changes millimetres.
    public class MRDesignSession : MonoBehaviour
    {
        public PackFoldStudio studio;
        public string status="Point at a control and pull the trigger. Grip the box to move it.";
        public int revisions, grabs, lidGrabs, twoHandGrabs;
        public event Action Changed;
        [Serializable] public class Revision
        {
            public string structure;
            public float width,depth,height,lidDepth,clearance,fold,open,exploded;
            public bool tabs,contents,dimensions;
            public Vector3 position,scale;
            public Quaternion rotation;
        }
        readonly List<string> history=new List<string>();
        int cursor=-1;
        float saveAt;
        public bool CanUndo=>cursor>0;
        public bool CanRedo=>cursor<history.Count-1;
        public void ResetHistory(string message){history.Clear();cursor=-1;Commit(message);}
        public Revision Capture()=>new Revision{structure=studio.StructureId,width=studio.widthMm,depth=studio.depthMm,height=studio.heightMm,lidDepth=studio.lidDepthMm,clearance=studio.clearanceMm,fold=studio.targetFold,open=studio.targetLidOpen,exploded=studio.exploded,tabs=studio.includeTabs,contents=studio.showContents,dimensions=studio.showDimensions,position=studio.transform.position,rotation=studio.transform.rotation,scale=studio.transform.localScale};
        void Start()
        {
            string path=Path.Combine(Application.persistentDataPath,"last_design_mr.json");
            if(File.Exists(path))try{Apply(JsonUtility.FromJson<Revision>(File.ReadAllText(path)));status="Previous design restored. X places it in front of you.";}catch(Exception e){Debug.LogWarning("MR restore: "+e.Message);}
            Commit("Session ready");
        }
        public void Apply(Revision r)
        {
            if(r==null||!IsFinite(r.width)||!IsFinite(r.depth)||!IsFinite(r.height))throw new ArgumentException("Invalid revision");
            studio.SetStructure(r.structure);studio.widthMm=r.width;studio.depthMm=r.depth;studio.heightMm=r.height;studio.lidDepthMm=r.lidDepth;studio.clearanceMm=r.clearance;
            studio.targetFold=studio.fold=Mathf.Clamp01(r.fold);studio.targetLidOpen=studio.lidOpen=Mathf.Clamp01(r.open);studio.exploded=r.exploded;
            studio.includeTabs=r.tabs;studio.showContents=r.contents;studio.showDimensions=r.dimensions;
            if(IsFinite(r.position.x)&&IsFinite(r.position.y)&&IsFinite(r.position.z))studio.transform.SetPositionAndRotation(r.position,r.rotation);
            studio.transform.localScale=Vector3.one*Mathf.Clamp(r.scale.x,.5f,3);
            studio.Rebuild();Changed?.Invoke();
        }
        static bool IsFinite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
        public void Commit(string message=null)
        {
            string json=JsonUtility.ToJson(Capture());
            if(cursor<0||history[cursor]!=json)
            {
                if(cursor<history.Count-1)history.RemoveRange(cursor+1,history.Count-cursor-1);
                history.Add(json);if(history.Count>80)history.RemoveAt(0);cursor=history.Count-1;revisions++;
                saveAt=Time.unscaledTime+1;
            }
            if(message!=null)status=message;Changed?.Invoke();
        }
        public void Undo(){if(CanUndo){Apply(JsonUtility.FromJson<Revision>(history[--cursor]));status="Undid one design revision";saveAt=Time.unscaledTime+1;}}
        public void Redo(){if(CanRedo){Apply(JsonUtility.FromJson<Revision>(history[++cursor]));status="Restored next design revision";saveAt=Time.unscaledTime+1;}}
        public void Preset(int n)
        {
            studio.SetStructure(n==0?"lift_off_lid":n==1?"hinged_lid":"open_tray");
            studio.widthMm=n==1?220:180;studio.depthMm=n==1?150:120;studio.heightMm=n==2?45:70;
            studio.lidDepthMm=28;studio.clearanceMm=.8f;studio.targetFold=1;studio.targetLidOpen=0;studio.exploded=0;studio.Rebuild();Commit("Structure changed; dimensions remain editable");
        }
        public void ToggleLid(){if(studio.trayMode){status="The open tray has no cover";return;}if(studio.twoPieceMode)studio.targetLidOpen=studio.targetLidOpen>.5f?0:1;else studio.targetFold=studio.targetFold>.8f?.72f:1;Commit("Cover animation");}
        public void ToggleFold(){studio.targetFold=studio.targetFold>.5f?0:1;Commit("Assembly folding / unfolding");}
        public string Export(string destination=null)
        {
            try
            {
                string folder=studio.Export(destination);File.WriteAllText(Path.Combine(folder,"mr_session.json"),JsonUtility.ToJson(new SessionRecord{utc=DateTime.UtcNow.ToString("O"),design=Capture(),revisions=revisions,body_grabs=grabs,lid_grabs=lidGrabs,two_hand_grabs=twoHandGrabs},true));
                status="Saved SVG + JSON on this headset. Use the supplied PC export tool.";Debug.Log("PACKFOLD_MR_EXPORT "+folder);return folder;
            }catch(Exception e){status="Export failed: "+e.Message;Debug.LogException(e);return null;}
        }
        [Serializable] public class SessionRecord{public string utc;public Revision design;public int revisions,body_grabs,lid_grabs,two_hand_grabs;}
        public void Recommend(int n)
        {
            // Transparent offline template assistant. No language model or cloud call is implied.
            float[] w={145,110,190},d={90,85,130},h={38,68,55};
            n=Mathf.Clamp(n,0,2);studio.widthMm=w[n]+20;studio.depthMm=d[n]+20;studio.heightMm=h[n]+12;
            studio.SetStructure(n==1?"lift_off_lid":"hinged_lid");studio.targetFold=1;studio.targetLidOpen=0;studio.Rebuild();
            Commit("OFFLINE RULES: item envelope + 10 mm side / 12 mm height allowance. Check actual fit.");
        }
        void Update(){if(saveAt>0&&Time.unscaledTime>=saveAt){saveAt=0;Save();}}
        void Save(){try{File.WriteAllText(Path.Combine(Application.persistentDataPath,"last_design_mr.json"),JsonUtility.ToJson(Capture(),true));}catch(Exception e){Debug.LogWarning(e.Message);}}
        void OnApplicationPause(bool paused){if(paused&&cursor>=0)Save();}
        void OnApplicationQuit(){if(cursor>=0)Save();}
    }
}
