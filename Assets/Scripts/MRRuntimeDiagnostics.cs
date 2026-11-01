using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;

namespace Portfolio.PackFold
{
    public class MRRuntimeDiagnostics : MonoBehaviour
    {
        public MRDesignSession session;
        public ARCameraManager arCamera;
        float timer;int frames;float seconds;bool logged;
        IEnumerator Start()
        {
            Application.targetFrameRate=72;QualitySettings.vSyncCount=0;Screen.sleepTimeout=SleepTimeout.NeverSleep;
            ARSession.stateChanged+=OnSession;
            Debug.Log("PACKFOLD_MR_BOOT "+Application.version+" device="+SystemInfo.deviceModel+" graphics="+SystemInfo.graphicsDeviceType);
            yield return new WaitForSeconds(8);
            var displays=new List<XRDisplaySubsystem>();SubsystemManager.GetSubsystems(displays);
            bool running=displays.Exists(d=>d.running);
            string line="PACKFOLD_MR_READY xr="+running+" ar="+ARSession.state+" camera="+(arCamera.subsystem!=null&&arCamera.subsystem.running)+" structures=3 ui=worldspace";
            Debug.Log(line);File.WriteAllText(Path.Combine(Application.persistentDataPath,"device_validation.txt"),DateTime.UtcNow.ToString("O")+"\n"+line+"\n"+SystemInfo.graphicsDeviceName);
            if(running){session.status="Quest runtime ready. Trigger: panel | Grip: box | A: lid | B: fold";foreach(var d in displays)if(d.running)d.TryGetDisplayRefreshRate(out float rate);}
            else session.status="XR display has not started. Check device runtime logs.";
        }
        void OnSession(ARSessionStateChangedEventArgs args){Debug.Log("PACKFOLD_MR_AR "+args.state);}
        void Update(){frames++;seconds+=Time.unscaledDeltaTime;timer+=Time.unscaledDeltaTime;if(timer>=20){Debug.Log($"PACKFOLD_MR_PERF fps={frames/seconds:F1} frames={frames} seconds={seconds:F1} memory_mb={UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()/1048576}");timer=0;frames=0;seconds=0;}}
        void OnDestroy(){ARSession.stateChanged-=OnSession;}
    }
}
