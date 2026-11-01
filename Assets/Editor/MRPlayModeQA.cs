using System;
using System.IO;
using Portfolio.PackFold;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.XR;

[InitializeOnLoad]
public static class MRPlayModeQA
{
    const string Flag="PackFold.MR.PlayModeQA";
    static bool started;static double deadline;static DateTime since;
    static MRPlayModeQA(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        PackFoldMRBuilder.Configure();PackFoldMRBuilder.Verify();EditorSceneManager.OpenScene(PackFoldMRBuilder.Scene);
        SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
        try
        {
            if(!started)
            {
                var session=UnityEngine.Object.FindAnyObjectByType<MRDesignSession>();if(!session)return;
                var manip=UnityEngine.Object.FindAnyObjectByType<MRDesignManipulator>();manip.enabled=false;
                Camera.main.GetComponent<TrackedPoseDriver>().enabled=false;Camera.main.transform.localPosition=new Vector3(0,1.65f,0);manip.Recenter();
                new GameObject("Play mode integration checks").AddComponent<MRDeviceSelfTest>();
                started=true;since=DateTime.UtcNow;deadline=EditorApplication.timeSinceStartup+55;return;
            }
            string report=Path.Combine(Application.persistentDataPath,"DeviceQA","integration_checks.json");
            if(File.Exists(report)&&File.GetLastWriteTimeUtc(report)>since)
            {
                Directory.CreateDirectory("Verification");File.Copy(report,"Verification/playmode_integration_checks.json",true);
                string json=File.ReadAllText(report);Capture();SessionState.SetBool(Flag,false);EditorApplication.isPlaying=false;EditorApplication.Exit(json.Contains("\"passed\": true")?0:1);
            }
            else if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Play mode QA timed out");
        }
        catch(Exception e){Debug.LogException(e);SessionState.SetBool(Flag,false);EditorApplication.isPlaying=false;EditorApplication.Exit(1);}
    }
    static void Capture()
    {
        Camera camera=Camera.main;camera.backgroundColor=new Color(.035f,.055f,.07f,1);camera.transform.SetPositionAndRotation(new Vector3(.02f,1.66f,-.05f),Quaternion.Euler(5,-2,0));
        camera.fieldOfView=70;camera.stereoTargetEye=StereoTargetEyeMask.None;
        var target=new RenderTexture(1600,1100,24){antiAliasing=4};camera.targetTexture=target;camera.Render();RenderTexture.active=target;
        var image=new Texture2D(1600,1100,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1100),0,0);image.Apply();File.WriteAllBytes("Verification/MR_content_editor_preview.png",image.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(image);
        File.WriteAllText("Verification/preview_note.txt","Editor render of MR content; the dark background is not a Quest Passthrough capture.");
    }
}
