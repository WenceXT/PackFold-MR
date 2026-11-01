using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Portfolio.PackFold
{
    // Opt-in integration checks. Synthetic hand frames are reported separately from hardware tracking.
    public class MRDeviceSelfTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var intent=activity.Call<AndroidJavaObject>("getIntent"))
                    if(intent.Call<bool>("getBooleanExtra","packfold_qa",false))new GameObject("Opt-in device integration checks").AddComponent<MRDeviceSelfTest>();
            }catch(Exception e){Debug.LogWarning("QA launch: "+e.Message);}
#endif
        }
        [Serializable] class Report{public bool passed,synthetic_hand_frames=true,real_controller_usability_tested=false;public int checks;public string failure,utc;public string[] results;}
        IEnumerator Start()
        {
            yield return new WaitForSeconds(12);
            var report=new Report{utc=DateTime.UtcNow.ToString("O")};var results=new List<string>();
            var session=FindAnyObjectByType<MRDesignSession>();var original=session.Capture();
            try
            {
                void Check(bool ok,string text){if(!ok)throw new Exception(text);results.Add(text);}
                var panel=FindAnyObjectByType<MRDesignPanel>();var m=FindAnyObjectByType<MRDesignManipulator>();var s=session.studio;
                Button Button(string name)=>panel.GetComponentsInChildren<Button>(true).First(b=>b.name==name);
                Button("HINGED").onClick.Invoke();Check(s.StructureId=="hinged_lid","runtime panel: hinged button");
                Button("TRAY").onClick.Invoke();Check(s.trayMode,"runtime panel: tray button");
                Button("LIFT-OFF").onClick.Invoke();Check(s.twoPieceMode,"runtime panel: lift-off button");
                var slider=panel.GetComponentsInChildren<Slider>(true).First(v=>v.name=="WIDTH slider");slider.value=210;s.Rebuild();session.Commit();Check(s.widthMm==210,"runtime slider changes design");
                Button("UNDO").onClick.Invoke();Check(s.widthMm==180,"runtime undo restores dimension");Button("REDO").onClick.Invoke();Check(s.widthMm==210,"runtime redo restores dimension");
                session.Apply(original);s.targetFold=s.fold=1;s.lidOpen=s.targetLidOpen=0;s.Rebuild();m.Release();
                MRDesignManipulator.Frame Frame(Vector3 p)=>new MRDesignManipulator.Frame{tracked=true,grip=true,position=p,rotation=Quaternion.identity};
                var none=new MRDesignManipulator.Frame();m.Step(none,none);
                var l=Frame(s.transform.TransformPoint(new Vector3(-s.widthMm*.0005f-.01f,-.005f,0)));m.Step(l,none);Check(m.LeftHold==MRDesignManipulator.Hold.Body,"synthetic frame: body selection");
                Vector3 p=s.transform.position;l.position+=Vector3.right*.08f;m.Step(l,none);Check(Vector3.Distance(s.transform.position,p+Vector3.right*.08f)<.002f,"synthetic frame: translation");
                Quaternion q=s.transform.rotation;l.rotation=Quaternion.Euler(0,25,0);m.Step(l,none);Check(Quaternion.Angle(q,s.transform.rotation)>20,"synthetic frame: rotation");m.Step(none,none);session.Apply(original);s.lidOpen=s.targetLidOpen=0;s.fold=s.targetFold=1;s.Rebuild();
                l=Frame(s.transform.TransformPoint(new Vector3(-s.widthMm*.0005f-.01f,-.008f,0)));var r=Frame(s.transform.TransformPoint(new Vector3(s.widthMm*.0005f+.01f,-.008f,0)));
                m.Step(l,r);Check(m.LeftHold==MRDesignManipulator.Hold.Body&&m.RightHold==MRDesignManipulator.Hold.Body,"synthetic frame: two-hand selection");float scale=s.transform.localScale.x;Vector3 mid=(l.position+r.position)*.5f;
                l.position=mid+(l.position-mid)*1.5f;r.position=mid+(r.position-mid)*1.5f;m.Step(l,r);Check(Mathf.Abs(s.transform.localScale.x-scale*1.5f)<.01f,"synthetic frame: two-hand proportional scale");
                l.position=mid+(l.position-mid)*10;r.position=mid+(r.position-mid)*10;m.Step(l,r);Check(s.transform.localScale.x==3,"synthetic frame: scale upper limit");
                m.Step(none,none);Check(m.LeftHold==MRDesignManipulator.Hold.None&&m.RightHold==MRDesignManipulator.Hold.None,"synthetic frame: tracking loss releases both");
                session.Apply(original);s.targetFold=s.fold=1;s.lidOpen=s.targetLidOpen=0;s.Rebuild();r=Frame(s.DetachableLid.TransformPoint(new Vector3(0,.015f,0)));m.Step(none,r);Check(m.RightHold==MRDesignManipulator.Hold.Lid,"synthetic frame: independent cover selection");r.position+=s.transform.up*.3f;m.Step(none,r);Check(s.targetLidOpen>.99f,"synthetic frame: cover lift opens");m.Step(none,none);
                int count=s.RebuildCount;for(int i=0;i<120;i++){s.lidOpen=i/119f;s.ApplyFold(1);}Check(s.RebuildCount==count,"cover animation does not rebuild mesh hierarchy");
                string path=session.Export(Path.Combine(Application.persistentDataPath,"DeviceQA","sample_export"));Check(File.Exists(Path.Combine(path,"base_dieline.svg"))&&File.Exists(Path.Combine(path,"lid_dieline.svg")),"device filesystem: both dielines written");
                var json=File.ReadAllText(Path.Combine(path,"design.json"));s.ImportJson(json);Check(s.twoPieceMode&&s.widthMm==original.width,"device JSON round trip");report.passed=true;
            }
            catch(Exception e){report.failure=e.ToString();Debug.LogException(e);}
            session.Apply(original);FindAnyObjectByType<MRDesignManipulator>().Release();session.Commit("Integration checks finished. Trigger: panel | Grip: box | A: lid | B: fold");
            report.checks=results.Count;report.results=results.ToArray();string folder=Path.Combine(Application.persistentDataPath,"DeviceQA");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"integration_checks.json"),JsonUtility.ToJson(report,true));
            Debug.Log("PACKFOLD_MR_SELFTEST passed="+report.passed+" checks="+report.checks+" failure="+report.failure);
        }
    }
}
