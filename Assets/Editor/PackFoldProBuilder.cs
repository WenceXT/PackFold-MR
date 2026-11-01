using System;
using System.IO;
using System.Xml;
using Portfolio.PackFold;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class PackFoldProBuilder
{
    private const string Scene="Assets/Scenes/PackFold_Studio.unity";
    private static Material Mat(string name,Color color,float metal=0,float gloss=.3f)
    {
        string path="Assets/StudioMaterials/"+name+".mat";Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null) {material=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(material,path);}material.color=color;material.SetFloat("_Metallic",metal);material.SetFloat("_Glossiness",gloss);return material;
    }
    private static Transform Shape(string name,Transform parent,Vector3 p,Vector3 scale,Material material,PrimitiveType type=PrimitiveType.Cube)
    {
        GameObject g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=material;return g.transform;
    }
    [MenuItem("Tools/PackFold/Build Detailed Studio")]
    public static void Build()
    {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach(string folder in new[]{"Assets/Scenes","Assets/StudioMaterials"}) Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.companyName="RobotLab Portfolio";PlayerSettings.productName="PackFold Studio";
        PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        QualitySettings.antiAliasing=4;QualitySettings.shadowDistance=10;QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.High;
        Material paper=Mat("Natural kraft",new Color(.75f,.70f,.62f),0,.18f);paper.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Cardboard004_1K-JPG_Color.jpg");
        TextureImporter normalImporter=AssetImporter.GetAtPath("Assets/Textures/Cardboard004_1K-JPG_NormalGL.jpg") as TextureImporter;
        if(normalImporter!=null && normalImporter.textureType!=TextureImporterType.NormalMap) {normalImporter.textureType=TextureImporterType.NormalMap;normalImporter.SaveAndReimport();}
        paper.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Cardboard004_1K-JPG_NormalGL.jpg"));paper.SetFloat("_BumpScale",.35f);paper.EnableKeyword("_NORMALMAP");
        Material teal=Mat("Printed teal",new Color(.025f,.34f,.30f),0,.22f);Material dark=Mat("Studio graphite",new Color(.055f,.075f,.085f),.15f,.3f);
        Material surface=Mat("Warm studio surface",new Color(.61f,.64f,.64f),.05f,.25f);Material white=Mat("Warm white",new Color(.82f,.83f,.79f));
        Material line=AssetDatabase.LoadAssetAtPath<Material>("Assets/StudioMaterials/Cut edges.mat");
        if(line==null) {line=new Material(Shader.Find("Unlit/Color"));line.color=new Color(.24f,.15f,.08f);AssetDatabase.CreateAsset(line,"Assets/StudioMaterials/Cut edges.mat");}
        Material guide=AssetDatabase.LoadAssetAtPath<Material>("Assets/StudioMaterials/Dimension ink.mat");
        if(guide==null) {guide=new Material(Shader.Find("Unlit/Color"));guide.color=new Color(.05f,.55f,.48f);AssetDatabase.CreateAsset(guide,"Assets/StudioMaterials/Dimension ink.mat");}
        Transform environment=new GameObject("Packaging design studio").transform;
        Shape("Studio desk",environment,new Vector3(0,.84f,0),new Vector3(2,.08f,1.4f),dark);
        Shape("Cutting mat",environment,new Vector3(0,.884f,0),new Vector3(.85f,.006f,.75f),surface);
        for(int i=-8;i<=8;i++)
        {
            Shape("Mat grid",environment,new Vector3(i*.05f,.888f,0),new Vector3(.00045f,.0002f,.72f),white);
            Shape("Mat grid",environment,new Vector3(0,.888f,i*.04f),new Vector3(.82f,.0002f,.00045f),white);
        }
        Shape("Presentation plinth",environment,new Vector3(0,.895f,0),new Vector3(.48f,.007f,.48f),white,PrimitiveType.Cylinder);
        Shape("Studio background",environment,new Vector3(0,1.4f,.8f),new Vector3(4,2,.05f),dark);
        Shape("Drafting ruler",environment,new Vector3(.32f,.897f,.05f),new Vector3(.025f,.002f,.31f),teal);
        for(int i=0;i<30;i++) Shape("Ruler tick",environment,new Vector3(.312f,.899f,-.09f+i*.01f),new Vector3(i%5==0?.012f:.006f,.0003f,.0005f),white);
        Transform knife=Shape("Craft knife",environment,new Vector3(-.29f,.9f,.18f),new Vector3(.018f,.013f,.15f),teal);knife.localRotation=Quaternion.Euler(0,-18,0);
        Shape("Knife steel tip",knife,new Vector3(0,0,.55f),new Vector3(.65f,.3f,.30f),white);
        GameObject root=new GameObject("PackFold Studio");root.transform.position=new Vector3(0,.91f,0);
        PackFoldStudio studio=root.AddComponent<PackFoldStudio>();studio.externalUI=true;studio.boardMaterial=paper;studio.creaseMaterial=line;studio.printMaterial=teal;
        studio.lidMaterial=Mat("Ivory gift lid",new Color(.88f,.85f,.77f),0,.22f);studio.lidMaterial.SetTexture("_BumpMap",paper.GetTexture("_BumpMap"));studio.lidMaterial.EnableKeyword("_NORMALMAP");studio.lidMaterial.SetFloat("_BumpScale",.18f);
        studio.linerMaterial=Mat("Dark teal foam",new Color(.025f,.075f,.07f),0,.1f);studio.deviceMaterial=Mat("Sensor aluminium",new Color(.34f,.41f,.44f),.7f,.4f);
        studio.SetStructure("lift_off_lid");studio.Rebuild();studio.feedback="New: lift-off gift box. Drag the lid up/down or use LIFT LID / CLOSE LID.";
        PackFoldAdvisorClient client=root.AddComponent<PackFoldAdvisorClient>();client.studio=studio;
        GameObject cameraObject=new GameObject("Main Camera");Camera camera=cameraObject.AddComponent<Camera>();cameraObject.tag="MainCamera";cameraObject.AddComponent<AudioListener>();camera.fieldOfView=40;camera.nearClipPlane=.01f;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.16f,.18f);camera.rect=new Rect(.1875f,0,.625f,1);camera.transform.position=new Vector3(.32f,1.38f,-.60f);camera.transform.LookAt(new Vector3(0,.93f,0));
        PackFoldProView view=root.AddComponent<PackFoldProView>();view.studio=studio;view.view=camera;
        Transform guides=new GameObject("Dimension guides").transform;guides.SetParent(root.transform,false);view.guides=guides;
        PackagingGuides guideController=guides.gameObject.AddComponent<PackagingGuides>();guideController.studio=studio;
        guideController.width=GuideLine("Width",guides,guide);guideController.depth=GuideLine("Depth",guides,guide);guideController.height=GuideLine("Height",guides,guide);
        guideController.widthText=GuideText("Width value",guides,Quaternion.Euler(90,0,0));guideController.depthText=GuideText("Depth value",guides,Quaternion.Euler(90,0,90));guideController.heightText=GuideText("Height value",guides,Quaternion.identity);
        guideController.Apply();
        GameObject origin=new GameObject("Optional XR origin");QuestInputBridge xr=root.AddComponent<QuestInputBridge>();xr.studio=studio;xr.viewCamera=camera;xr.trackingOrigin=origin.transform;
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.35f,.40f,.43f);RenderSettings.ambientEquatorColor=new Color(.18f,.21f,.21f);RenderSettings.ambientGroundColor=new Color(.10f,.12f,.13f);
        Light key=new GameObject("Soft product key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.1f;key.color=new Color(1,.95f,.88f);key.transform.rotation=Quaternion.Euler(50,-35,0);key.shadows=LightShadows.Soft;key.shadowBias=.005f;
        Light fill=new GameObject("Studio fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.35f;fill.transform.rotation=Quaternion.Euler(35,140,0);fill.color=new Color(.75f,.88f,1);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Scene);EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Scene,true)};AssetDatabase.SaveAssets();
    }
    private static LineRenderer GuideLine(string name,Transform parent,Material material)
    {GameObject g=new GameObject(name);g.transform.SetParent(parent,false);LineRenderer line=g.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=2;line.startWidth=line.endWidth=.0006f;line.sharedMaterial=material;return line;}
    private static TextMesh GuideText(string name,Transform parent,Quaternion rotation)
    {GameObject g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localRotation=rotation;TextMesh text=g.AddComponent<TextMesh>();text.characterSize=.0007f;text.fontSize=96;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(.02f,.32f,.28f);return text;}
    public static void VerifyAndRender()
    {
        Build();PackFoldStudio studio=UnityEngine.Object.FindFirstObjectByType<PackFoldStudio>();PackFoldProView view=studio.GetComponent<PackFoldProView>();
        Directory.CreateDirectory("Verification");
        int fitCases=0;
        foreach(Vector3 size in new[]{new Vector3(60,50,25),new Vector3(180,120,70),new Vector3(300,240,150)})
        foreach(float clearance in new[]{.2f,.8f,2.5f})
        {
            studio.widthMm=size.x;studio.depthMm=size.y;studio.heightMm=size.z;studio.clearanceMm=clearance;studio.SetStructure("lift_off_lid");studio.lidOpen=0;studio.Rebuild();studio.ApplyFold(1);
            Require(Vector3.Distance(studio.DetachableLid.localPosition,new Vector3(0,studio.ClosedLidY,0))<.0001f,"Closed cap center");
            float measured=(studio.LidWidthMm*.001f/2-.0006f)-(studio.widthMm*.001f/2+.0006f);
            Require(Mathf.Abs(measured-clearance*.001f)<.000001f,"Specified cap/body clearance");
            Bounds closed=MeshBounds(studio.DetachableLid);
            Require(closed.max.y>=studio.transform.position.y+size.z*.001f,"Cap covers body rim");
            studio.lidOpen=1;studio.ApplyFold(1);Bounds opened=MeshBounds(studio.DetachableLid);
            Require(opened.min.y>studio.transform.position.y+size.z*.001f+.01f,"Opened cap clears rim");
            studio.ApplyFold(0);Bounds body=MeshBounds(studio.BodyRoot),lid=MeshBounds(studio.DetachableLid);
            Require(body.max.x+.01f<lid.min.x,"Flat body and cap nets are separated");fitCases++;
        }
        studio.widthMm=180;studio.depthMm=120;studio.heightMm=70;studio.lidDepthMm=28;studio.clearanceMm=.8f;studio.includeTabs=true;studio.exploded=0;studio.SetStructure("lift_off_lid");studio.Rebuild();studio.ApplyFold(1);
        int rebuilds=studio.RebuildCount;
        for(int i=0;i<240;i++) {studio.lidOpen=(i%120)/119f;studio.ApplyFold(1);}
        Require(studio.RebuildCount==rebuilds,"Lid animation does not rebuild geometry");
        view.SaveRevision();studio.widthMm=210;studio.clearanceMm=1.5f;studio.lidDepthMm=20;studio.includeTabs=false;studio.Rebuild();view.SaveRevision();view.SelectPreset(0);view.Undo();
        Require(studio.twoPieceMode&&studio.widthMm==210&&!studio.includeTabs&&Mathf.Abs(studio.clearanceMm-1.5f)<.001f&&studio.lidDepthMm==20,"Undo restores structure, flaps, dimensions and lid fit");
        view.Redo();Require(studio.StructureId=="hinged_lid","Redo restores subsequent box style");
        bool rejected=false;try {studio.ImportJson("{\"schema\":\"packfold.v1\",\"structure\":\"lift_off_lid\",\"width_mm\":180,\"depth_mm\":120,\"height_mm\":70,\"lid_clearance_mm\":-1}");} catch(ArgumentException) {rejected=true;}Require(rejected,"Invalid optional lid parameter rejected");
        view.SelectPreset(3);studio.includeTabs=true;studio.Rebuild();studio.lidOpen=0;studio.ApplyFold(1);string folder=studio.Export(Path.GetFullPath("Verification"));
        foreach(string name in new[]{"base_dieline.svg","lid_dieline.svg","dieline.svg"}) {XmlDocument xml=new XmlDocument();xml.Load(Path.Combine(folder,name));}
        string json=File.ReadAllText(Path.Combine(folder,"design.json"));studio.SetStructure("open_tray");studio.ImportJson(json);Require(studio.twoPieceMode,"Two-piece JSON round trip");
        Camera camera=Camera.main;camera.rect=new Rect(0,0,1,1);
        Capture(studio,camera,1,0,"gift_closed");Capture(studio,camera,1,1,"gift_open");Capture(studio,camera,0,0,"gift_flat");
        view.SelectPreset(0);Capture(studio,camera,1,0,"hinged_closed");view.SelectPreset(2);Capture(studio,camera,1,0,"open_tray");
        view.SelectPreset(3);studio.includeTabs=true;studio.Rebuild();studio.lidOpen=studio.targetLidOpen=1;studio.ApplyFold(1);camera.rect=new Rect(.1875f,0,.625f,1);camera.transform.position=new Vector3(.32f,1.38f,-.60f);camera.transform.LookAt(new Vector3(0,.96f,0));
        studio.feedback="New: lift-off gift box. Drag the lid up/down or use LIFT LID / CLOSE LID.";
        File.WriteAllText("Verification/result_v3.json","{\"status\":\"passed\",\"fit_cases\":"+fitCases+",\"animation_frames_without_rebuild\":240,\"checks\":\"body-lid clearance, separated nets, open clearance, full design undo-redo, JSON round trip and rejection, three SVG exports\",\"quest_tested\":false}");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Scene);Debug.Log("PACKFOLD_V3_VERIFIED");
    }
    private static Bounds MeshBounds(Transform parent)
    {
        Bounds result=new Bounds(parent.position,Vector3.zero);bool found=false;
        foreach(MeshFilter filter in parent.GetComponentsInChildren<MeshFilter>())
        {
            if(filter.GetComponent<TextMesh>()!=null) continue;
            Bounds b=filter.sharedMesh.bounds;
            for(int i=0;i<8;i++)
            {
                Vector3 local=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));Vector3 world=filter.transform.TransformPoint(local);
                if(!found) {result=new Bounds(world,Vector3.zero);found=true;} else result.Encapsulate(world);
            }
        }
        return result;
    }
    private static void Require(bool condition,string message) {if(!condition) throw new Exception("Verification: "+message);}
    private static void Capture(PackFoldStudio studio,Camera camera,float fold,float open,string name)
    {
        studio.lidOpen=open;studio.ApplyFold(fold);float distance=fold<.5f?1.2f:.78f;Vector3 focus=new Vector3(0,.93f+(open>.5f?.035f:0),0);
        camera.transform.position=focus+Quaternion.Euler(32,-30,0)*new Vector3(0,0,-distance);camera.transform.LookAt(focus);
        foreach(PrintedSide side in UnityEngine.Object.FindObjectsByType<PrintedSide>(FindObjectsSortMode.None)) side.Apply(camera);
        UnityEngine.Object.FindFirstObjectByType<PackagingGuides>().Apply();
        RenderTexture target=new RenderTexture(1600,1000,24){antiAliasing=4};camera.targetTexture=target;camera.Render();RenderTexture.active=target;
        Texture2D image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes("Verification/"+name+".png",image.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);
    }
    public static void BuildWindows()
    {
        VerifyAndRender();Directory.CreateDirectory("Build/PackFold_Studio");BuildReport report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName="Build/PackFold_Studio/PackFold_Studio.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Windows build failed: "+report.summary.result);Debug.Log("PACKFOLD_WINDOWS_BUILD_SUCCEEDED");
        string helper="Build/PackFold_Studio/Advisor";Directory.CreateDirectory(helper);File.Copy("Python_Advisor/advisor.py",Path.Combine(helper,"advisor.py"),true);
        string runtime=Path.GetFullPath("PortableRuntime/Python");
        if(!Directory.Exists(runtime)) throw new Exception("Bundled Python runtime missing: PortableRuntime/Python");
        foreach(string file in Directory.GetFiles(runtime,"*",SearchOption.AllDirectories))
        {
            string relative=file.Substring(runtime.Length).TrimStart(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            string target=Path.Combine(helper,"Python",relative);Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(file,target,true);
        }
    }
}
