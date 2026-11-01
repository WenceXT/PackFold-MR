using System;
using System.Collections;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.Networking;

namespace Portfolio.PackFold
{
    public class PackFoldAdvisorClient : MonoBehaviour
    {
        public PackFoldStudio studio;
        public string endpoint="http://127.0.0.1:8765/recommend";
        public string brief="Protect a compact robotics sensor kit; easy opening and reusable lid.";
        public bool fragile=true,reusable=true;
        private bool pending;
        private Process helper;
        private void Awake()
        {
            #if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Advisor"));
            string python=Path.Combine(directory,"Python","python.exe");
            if(File.Exists(python))
            {
                try
                {
                    TcpListener listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
                    endpoint="http://127.0.0.1:"+port+"/recommend";
                    helper=Process.Start(new ProcessStartInfo{FileName=python,Arguments="\""+Path.Combine(directory,"advisor.py")+"\" --serve --port "+port,WorkingDirectory=directory,UseShellExecute=false,CreateNoWindow=true});
                }
                catch(Exception error) {studio.feedback="Local helper could not start: "+error.Message;}
            }
            #endif
        }
        private void OnApplicationQuit()
        {
            if(helper==null) return;
            try {if(!helper.HasExited) helper.Kill();helper.Dispose();} catch(InvalidOperationException) { }
        }
        [Serializable] private class Request
        {
            public float product_width_mm,product_depth_mm,product_height_mm;
            public bool fragile=true,reusable=true;
            public string brief;
            public string preferred_structure;
        }
        public void RequestRecommendation()
        {
            if(!pending) StartCoroutine(Send());
        }
        private IEnumerator Send()
        {
            pending=true; studio.feedback="Waiting for advisor API...";
            Request spec=new Request {product_width_mm=studio.widthMm-20,product_depth_mm=studio.depthMm-20,
                product_height_mm=studio.heightMm-15,brief=brief,fragile=fragile,reusable=reusable,preferred_structure=studio.StructureId};
            using(UnityWebRequest request=new UnityWebRequest(endpoint,"POST"))
            {
                request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(spec)));
                request.downloadHandler=new DownloadHandlerBuffer(); request.SetRequestHeader("Content-Type","application/json"); request.timeout=25;
                yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success) studio.feedback="Advisor unavailable: "+request.error+". Start Python server first.";
                else
                {
                    try { studio.ImportJson(request.downloadHandler.text); }
                    catch(Exception e) { studio.feedback="Advisor response rejected: "+e.Message; }
                }
            }
            pending=false;
        }
    }
}
