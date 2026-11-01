using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Portfolio.PackFold;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Exercises the real XR ray -> UI input module -> UGUI event route, without a headset.
// Only the tracked poses and trigger reader are synthetic; button callbacks are never invoked directly.
[InitializeOnLoad]
public static class MRUIRayQA
{
    const string Flag="PackFold.MR.UIRayQA";
    static Queue<Action> steps;
    static List<string> results;
    static double next,deadline;
    static MRDesignPanel panel;
    static MRDesignSession session;
    static XRRayInteractor left,right,active;
    static string expected;
    static float sliderStart;
    [Serializable] class Report { public bool passed; public bool synthetic_poses_and_trigger=true; public bool hardware_tested=false; public string failure; public string[] checks; }
    static MRUIRayQA(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        PackFoldMRBuilder.Configure();PackFoldMRBuilder.Verify();EditorSceneManager.OpenScene(PackFoldMRBuilder.Scene);
        SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;
    }
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);results.Add(text);Debug.Log("PACKFOLD_UI_RAY_CHECK "+text);}
    static void Setup()
    {
        results=new List<string>();steps=new Queue<Action>();
        panel=UnityEngine.Object.FindAnyObjectByType<MRDesignPanel>();session=panel.session;
        var manip=panel.manipulator;manip.enabled=false;
        foreach(var pose in UnityEngine.Object.FindObjectsByType<TrackedPoseDriver>(FindObjectsSortMode.None))pose.enabled=false;
        Camera.main.transform.SetPositionAndRotation(new Vector3(0,1.65f,0),Quaternion.identity);manip.Recenter();
        left=manip.leftPointer.GetComponent<XRRayInteractor>();right=manip.rightPointer.GetComponent<XRRayInteractor>();
        foreach(var ray in new[]{left,right}){ray.uiPressInput.inputSourceMode=XRInputButtonReader.InputSourceMode.ManualValue;ray.uiPressInput.manualPerformed=false;ray.gameObject.SetActive(false);}
        void Add(Action step)=>steps.Enqueue(step);
        void Click(XRRayInteractor ray,string name,Func<bool> effect,string description)
        {
            Add(()=>Aim(ray,name));Add(()=>Hover(name));Add(()=>Press(true));Add(()=>Check(active.TryGetUIModel(out var model)&&model.select,"Trigger reaches tracked UI model: "+name));Add(()=>Press(false));Add(()=>Check(effect(),description));
        }
        Click(left,"HINGED",()=>session.studio.StructureId=="hinged_lid","Left ray clicks hinged preset through UI events");
        Click(right,"TRAY",()=>session.studio.trayMode,"Right ray clicks tray preset through UI events");
        Add(()=>{sliderStart=session.studio.widthMm;Aim(right,"WIDTH slider",.25f);});Add(()=>Hover("WIDTH slider"));Add(()=>Press(true));
        Add(()=>Check(Mathf.Abs(session.studio.widthMm-sliderStart)>5,"Ray pointer-down changes width slider"));
        Add(()=>Aim(right,"WIDTH slider",.8f));Add(()=>Check(session.studio.widthMm>220,"Held ray drag changes width continuously"));Add(()=>Press(false));
        Click(left,"FIT + ASSIST",()=>panel.GetComponentsInChildren<Slider>().Any(s=>s.name=="PREVIEW SCALE slider"),"Left ray switches panel page");
        Click(right,"DESIGN",()=>panel.GetComponentsInChildren<Slider>().Any(s=>s.name=="WIDTH slider"),"Right ray returns to design page");
        Add(()=>{session.Preset(0);session.studio.widthMm=180;session.studio.depthMm=120;session.studio.heightMm=70;session.Commit("UI ray checks finished");});
        next=EditorApplication.timeSinceStartup+.6;deadline=EditorApplication.timeSinceStartup+30;
    }
    static void Aim(XRRayInteractor ray,string name,float fraction=.5f)
    {
        if(active!=ray){if(active)active.gameObject.SetActive(false);active=ray;ray.gameObject.SetActive(true);}
        var target=panel.GetComponentsInChildren<RectTransform>().First(t=>t.name==name);
        Vector3 point=target.TransformPoint(new Vector3(Mathf.Lerp(target.rect.xMin,target.rect.xMax,fraction),target.rect.center.y,0));
        Vector3 origin=Camera.main.transform.position+new Vector3(ray==left?-.13f:.13f,-.12f,0);
        ray.transform.SetPositionAndRotation(origin,Quaternion.LookRotation(point-origin));expected=name;Canvas.ForceUpdateCanvases();
    }
    static void Hover(string name)
    {
        bool hit=active.TryGetCurrentUIRaycastResult(out var result);
        bool match=hit&&result.gameObject&&result.gameObject.GetComponentsInParent<RectTransform>().Any(t=>t.name==name);
        Check(match,"Ray hits "+name+" (actual: "+(hit&&result.gameObject?result.gameObject.name:"none")+")");
    }
    static void Press(bool pressed){active.uiPressInput.manualPerformed=pressed;active.uiPressInput.manualValue=pressed?1:0;}
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        try
        {
            if(steps==null){Setup();return;}
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("UI ray QA timeout at "+expected);
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.15;
            if(steps.Count>0)steps.Dequeue()();else Finish(true,null);
        }
        catch(Exception e){Debug.LogException(e);Finish(false,e.ToString());}
    }
    static void Finish(bool passed,string failure)
    {
        Directory.CreateDirectory("Verification");File.WriteAllText("Verification/ui_ray_checks.json",JsonUtility.ToJson(new Report{passed=passed,failure=failure,checks=results?.ToArray()},true));
        Debug.Log("PACKFOLD_UI_RAY_QA passed="+passed);SessionState.SetBool(Flag,false);EditorApplication.isPlaying=false;EditorApplication.Exit(passed?0:1);
    }
}
