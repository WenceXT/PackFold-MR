using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Portfolio.PackFold;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.Meta;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
using UnityEngine.XR.OpenXR.Features.CompositionLayers;
using Unity.XR.CoreUtils;

public static class PackFoldMRBuilder
{
    public const string Scene="Assets/Scenes/PackFold_MR.unity";
    static Material Material(string name)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/StudioMaterials/"+name+".mat");
    [MenuItem("Tools/PackFold MR/Create Quest Scene")]
    public static void Configure()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        PlayerSettings.companyName="RobotLab Portfolio";PlayerSettings.productName="PackFold MR";PlayerSettings.bundleVersion="0.5";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.robotlab.packfoldmr");
        PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel32;PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevel34;
        PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;PlayerSettings.Android.bundleVersionCode=9;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android,ManagedStrippingLevel.Low);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan});
        PlayerSettings.Android.androidTVCompatibility=false;PlayerSettings.Android.preferredInstallLocation=AndroidPreferredInstallLocation.Auto;
        PlayerSettings.runInBackground=true;
        var icon=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/QuestIcon.png");if(icon)PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown,new[]{icon});
        SerializedObject player=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        var active=player.FindProperty("activeInputHandler");if(active!=null){active.intValue=1;player.ApplyModifiedPropertiesWithoutUndo();}
        QualitySettings.antiAliasing=4;QualitySettings.shadows=ShadowQuality.Disable;QualitySettings.pixelLightCount=2;QualitySettings.vSyncCount=0;
        Directory.CreateDirectory("Assets/XR");Directory.CreateDirectory("Assets/ControllerModels");AssetDatabase.Refresh();
        XRGeneralSettingsPerBuildTarget perTarget;
        if(!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey,out perTarget))
        {perTarget=ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();AssetDatabase.CreateAsset(perTarget,"Assets/XR/XRGeneralSettingsPerBuildTarget.asset");EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey,perTarget,true);}
        if(!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        var general=perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);general.InitManagerOnStart=true;general.Manager.automaticLoading=true;general.Manager.automaticRunning=true;
        if(!XRPackageMetadataStore.AssignLoader(general.Manager,"UnityEngine.XR.OpenXR.OpenXRLoader",BuildTargetGroup.Android))throw new Exception("OpenXR loader assignment failed");
        var xr=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        if(xr==null)throw new Exception("Android OpenXR settings missing");
        UnityEditor.XR.OpenXR.Features.FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
        xr.renderMode=OpenXRSettings.RenderMode.SinglePassInstanced;xr.latencyOptimization=OpenXRSettings.LatencyOptimization.PrioritizeInputPolling;
        foreach(var feature in xr.GetFeatures<OpenXRFeature>())
        {
            feature.enabled=feature is MetaQuestFeature||feature is ARCameraFeature||feature is ARSessionFeature||feature is OculusTouchControllerProfile||feature is OpenXRCompositionLayersFeature||(feature.GetType().Name=="DisplayUtilitiesFeature"||feature.GetType().Name=="OpenXRLifeCycleFeature");
            Debug.Log("MR_FEATURE "+feature.GetType().Name+"="+feature.enabled);
        }
        var cameraFeature=xr.GetFeature<ARCameraFeature>();cameraFeature.cameraImageSupportEnabled=false;
        EditorUtility.SetDirty(xr);EditorUtility.SetDirty(perTarget);EditorUtility.SetDirty(general);EditorUtility.SetDirty(general.Manager);

        var actions=CreateActions();
        var originObject=new GameObject("XR Origin");var origin=originObject.AddComponent<XROrigin>();
        var offset=new GameObject("Camera Offset");offset.transform.SetParent(originObject.transform,false);origin.CameraFloorOffsetObject=offset;origin.RequestedTrackingOriginMode=XROrigin.TrackingOriginMode.Floor;
        var camObject=new GameObject("Main Camera");camObject.tag="MainCamera";camObject.transform.SetParent(offset.transform,false);
        var camera=camObject.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.nearClipPlane=.025f;camera.farClipPlane=20;camera.allowHDR=false;camera.allowMSAA=true;camObject.AddComponent<AudioListener>();
        Pose(camObject,actions,"Head");origin.Camera=camera;
        var arCamera=camObject.AddComponent<ARCameraManager>();new GameObject("AR Session",typeof(ARSession),typeof(ARInputManager));
        var actionManager=originObject.AddComponent<InputActionManager>();actionManager.actionAssets=new List<InputActionAsset>{actions};
        var interactionManager=new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
        var left=Controller(offset.transform,actions,"Left",interactionManager,out MRControllerVisual leftVisual);
        var right=Controller(offset.transform,actions,"Right",interactionManager,out MRControllerVisual rightVisual);
        var events=new GameObject("XR UI EventSystem",typeof(EventSystem),typeof(XRUIInputModule));var ui=events.GetComponent<XRUIInputModule>();ui.enableXRInput=true;ui.enableMouseInput=false;ui.enableTouchInput=false;

        var model=new GameObject("Spatial packaging");var studio=model.AddComponent<PackFoldStudio>();studio.externalUI=true;
        studio.boardMaterial=Material("Natural kraft");studio.lidMaterial=Material("Ivory gift lid");studio.creaseMaterial=Material("Cut edges");studio.printMaterial=Material("Printed teal");studio.linerMaterial=Material("Dark teal foam");studio.deviceMaterial=Material("Sensor aluminium");
        studio.SetStructure("lift_off_lid");studio.fold=studio.targetFold=1;studio.lidOpen=studio.targetLidOpen=0;studio.Rebuild();
        var workspace=new GameObject("MR workspace");var session=workspace.AddComponent<MRDesignSession>();session.studio=studio;
        var manip=workspace.AddComponent<MRDesignManipulator>();manip.session=session;manip.head=camera.transform;manip.view=camera;manip.leftHand=left;manip.rightHand=right;manip.leftVisual=leftVisual;manip.rightVisual=rightVisual;manip.leftPointer=offset.transform.Find("Left UI pointer").gameObject;manip.rightPointer=offset.transform.Find("Right UI pointer").gameObject;
        var panelObject=new GameObject("Spatial design panel",typeof(RectTransform),typeof(Canvas));var panel=panelObject.AddComponent<MRDesignPanel>();panel.session=session;panel.manipulator=manip;panel.view=camera;panel.Build();manip.panel=panel.transform;
        camera.transform.position=new Vector3(0,1.65f,0);manip.Recenter();camera.transform.localPosition=Vector3.zero;
        var diagnostics=workspace.AddComponent<MRRuntimeDiagnostics>();diagnostics.session=session;diagnostics.arCamera=arCamera;
        CreateGuides(studio);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.68f,.70f);RenderSettings.skybox=null;
        var key=new GameObject("Product lighting").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1;key.transform.rotation=Quaternion.Euler(48,-32,0);key.shadows=LightShadows.None;
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Scene);EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Scene,true)};AssetDatabase.SaveAssets();
        Debug.Log("PACKFOLD_MR_SCENE_READY");
    }
    public static void PreviewControllers()
    {
        var stage=new GameObject("Controller preview stage");stage.transform.position=new Vector3(6,6,6);
        var originals=UnityEngine.Object.FindObjectsByType<MRControllerVisual>(FindObjectsSortMode.None);
        foreach(var original in originals)
        {
            var o=new GameObject(original.left?"Left preview":"Right preview");o.transform.SetParent(stage.transform,false);o.transform.localPosition=new Vector3(original.left?-.046f:.046f,0,0);o.transform.localRotation=Quaternion.Euler(-60,original.left?-12:12,original.left?-8:8);
            var visual=o.AddComponent<MRControllerVisual>();visual.left=original.left;visual.modelPrefab=original.modelPrefab;visual.Build();visual.Animate(.15f,.1f,new Vector2(.2f,.1f),false,false,false);
        }
        foreach(var t in stage.GetComponentsInChildren<Transform>())t.gameObject.layer=30;
        var cameraObject=new GameObject("Controller preview camera");var camera=cameraObject.AddComponent<Camera>();camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.055f,.07f);camera.fieldOfView=38;camera.nearClipPlane=.005f;
        camera.transform.position=stage.transform.position+new Vector3(.12f,.13f,-.26f);camera.transform.LookAt(stage.transform.position+new Vector3(0,-.008f,0));
        var target=new RenderTexture(1400,1100,24){antiAliasing=4};camera.targetTexture=target;camera.Render();RenderTexture.active=target;
        var image=new Texture2D(1400,1100,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1400,1100),0,0);image.Apply();Directory.CreateDirectory("Verification");File.WriteAllBytes("Verification/Controllers_v043.png",image.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(stage);UnityEngine.Object.DestroyImmediate(cameraObject);
    }
    static InputActionAsset CreateActions()
    {
        string path="Assets/XR/PackFold.inputactions";var asset=ScriptableObject.CreateInstance<InputActionAsset>();
        foreach(string hand in new[]{"Head","Left","Right"})
        {
            var map=asset.AddActionMap(hand);string device=hand=="Head"?"<XRHMD>":"<XRController>{"+hand+"Hand}";
            map.AddAction("Position",InputActionType.Value,device+(hand=="Head"?"/centerEyePosition":"/devicePosition"),expectedControlLayout:"Vector3");
            map.AddAction("Rotation",InputActionType.Value,device+(hand=="Head"?"/centerEyeRotation":"/deviceRotation"),expectedControlLayout:"Quaternion");
            map.AddAction("Tracking",InputActionType.Value,device+"/trackingState",expectedControlLayout:"Integer");
            if(hand!="Head")
            {
                map.AddAction("Aim Position",InputActionType.Value,device+"/pointerPosition",expectedControlLayout:"Vector3");map.AddAction("Aim Rotation",InputActionType.Value,device+"/pointerRotation",expectedControlLayout:"Quaternion");
                map.AddAction("UI Press",InputActionType.Button,device+"/triggerPressed");map.AddAction("UI Value",InputActionType.Value,device+"/trigger",expectedControlLayout:"Axis");
            }
        }
        File.WriteAllText(path,asset.ToJson());UnityEngine.Object.DestroyImmediate(asset);AssetDatabase.ImportAsset(path);return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
    }
    static InputActionReference Reference(InputActionAsset asset,string path)
    {
        var action=asset.FindAction(path,true);
        var reference=AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(asset)).OfType<InputActionReference>().FirstOrDefault(r=>r.action.id==action.id);
        if(reference!=null)return reference;
        string destination="Assets/XR/"+path.Replace('/','_').Replace(' ','_')+".asset";
        reference=AssetDatabase.LoadAssetAtPath<InputActionReference>(destination);
        if(reference==null){reference=InputActionReference.Create(action);AssetDatabase.CreateAsset(reference,destination);}else reference.Set(action);
        return reference;
    }
    static void Pose(GameObject o,InputActionAsset actions,string hand,bool aim=false)
    {
        var driver=o.AddComponent<TrackedPoseDriver>();driver.positionInput=new InputActionProperty(Reference(actions,hand+(aim?"/Aim Position":"/Position")));
        driver.rotationInput=new InputActionProperty(Reference(actions,hand+(aim?"/Aim Rotation":"/Rotation")));
        driver.trackingStateInput=new InputActionProperty(Reference(actions,hand+"/Tracking"));driver.updateType=TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
    }
    static Transform Controller(Transform parent,InputActionAsset actions,string hand,XRInteractionManager manager,out MRControllerVisual visual)
    {
        var grip=new GameObject(hand+" grip pose");grip.transform.SetParent(parent,false);Pose(grip,actions,hand);
        var visualObject=new GameObject(hand+" designed controller");visualObject.transform.SetParent(grip.transform,false);
        visual=visualObject.AddComponent<MRControllerVisual>();visual.left=hand=="Left";
        visual.modelPrefab=PrepareOfficialController(hand);visual.Build();
        var pointer=new GameObject(hand+" UI pointer");pointer.SetActive(false);pointer.transform.SetParent(parent,false);Pose(pointer,actions,hand,true);
        // XRI also uses raycastMask for UGUI graphics, so zero disables UI hits too.
        // UI-only raycasts keep packaging colliders out of the panel input path.
        var ray=pointer.AddComponent<XRRayInteractor>();ray.interactionManager=manager;ray.interactionLayers=0;ray.raycastMask=1<<LayerMask.NameToLayer("UI");ray.enableUIInteraction=true;ray.maxRaycastDistance=3;ray.lineType=XRRayInteractor.LineType.StraightLine;
        ray.uiPressInput=new XRInputButtonReader{inputSourceMode=XRInputButtonReader.InputSourceMode.InputActionReference,inputActionReferencePerformed=Reference(actions,hand+"/UI Press"),inputActionReferenceValue=Reference(actions,hand+"/UI Value")};
        string materialPath="Assets/StudioMaterials/XR pointer.mat";
        var pointerMaterial=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(!pointerMaterial){pointerMaterial=new Material(Shader.Find("PackFold/XR Pointer"));AssetDatabase.CreateAsset(pointerMaterial,materialPath);}
        pointerMaterial.enableInstancing=true;
        var renderer=pointer.AddComponent<LineRenderer>();renderer.sharedMaterial=pointerMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.generateLightingData=false;
        ray.blendVisualLinePoints=true;
        var line=pointer.AddComponent<XRInteractorLineVisual>();line.smoothMovement=false;line.overrideInteractorLineLength=true;line.lineLength=1.8f;line.lineWidth=.0015f;line.invalidColorGradient=Gradient(new Color(.22f,.58f,.58f,.6f));line.validColorGradient=Gradient(new Color(.35f,1,.77f,.95f));pointer.SetActive(true);
        return grip.transform;
    }
    [Serializable] class MetaModelSettings{public string hand;public long[] hidden_ids;}
    [Serializable] class MetaPrefabSettings{public MetaModelSettings[] models;}
    static GameObject PrepareOfficialController(string hand)
    {
        const string root="Assets/ThirdParty/MetaTouchPlus/";
        string modelPath=root+"Meshes/MetaQuestTouchPlus/MetaQuestTouchPlus_"+hand+".fbx";
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if(!asset)throw new Exception("Official Meta controller model missing: "+modelPath);
        var settings=JsonUtility.FromJson<MetaPrefabSettings>(File.ReadAllText(root+"PrefabSettings.json")).models.First(x=>x.hand==hand);
        var hidden=new HashSet<long>(settings.hidden_ids);var hiddenPaths=new List<string>();
        foreach(var t in asset.GetComponentsInChildren<Transform>(true))
        {
            if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(t.gameObject,out string guid,out long id)&&hidden.Contains(id))hiddenPaths.Add(AnimationUtility.CalculateTransformPath(t,asset.transform));
        }
        var instance=UnityEngine.Object.Instantiate(asset);instance.name="MetaTouchPlus_"+hand+"_Official";
        instance.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        foreach(string path in hiddenPaths){var child=instance.transform.Find(path);if(child)child.gameObject.SetActive(false);}
        string textureRoot=root+"Textures/MetaQuestTouchPlus/";
        string normalPath=textureRoot+"MetaQuestTouchPlus_Normals.png";
        var normal=AssetImporter.GetAtPath(normalPath) as TextureImporter;
        if(normal!=null&&normal.textureType!=TextureImporterType.NormalMap){normal.textureType=TextureImporterType.NormalMap;normal.SaveAndReimport();}
        var material=ControllerMaterial("Meta Touch Plus "+hand,Color.white,0,.35f);
        material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(textureRoot+"MetaQuestTouchPlus_"+hand+"_BaseColor_AO_AlphaRoughness.png");
        material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));material.EnableKeyword("_NORMALMAP");
        foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
        {renderer.sharedMaterials=Enumerable.Repeat(material,Mathf.Max(1,renderer.sharedMaterials.Length)).ToArray();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;}
        var animator=instance.GetComponent<Animator>();if(!animator)animator=instance.AddComponent<Animator>();
        animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(root+"Meshes/MetaQuestTouchPlus/Animation/MetaQuestTouchPlus"+hand+"Animation.controller");animator.applyRootMotion=false;
        string destination="Assets/ControllerModels/MetaTouchPlus_"+hand+"_Official.prefab";
        var prefab=PrefabUtility.SaveAsPrefabAsset(instance,destination);UnityEngine.Object.DestroyImmediate(instance);return prefab;
    }
    static Material ControllerMaterial(string name,Color color,float metal,float gloss)
    {
        string path="Assets/StudioMaterials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(material,path);}
        material.color=color;material.SetFloat("_Metallic",metal);material.SetFloat("_Glossiness",gloss);material.enableInstancing=true;return material;
    }
    static Gradient Gradient(Color c){var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(c,0),new GradientColorKey(c,1)},new[]{new GradientAlphaKey(c.a,0),new GradientAlphaKey(c.a*.25f,1)});return g;}
    static void CreateGuides(PackFoldStudio s)
    {
        var root=new GameObject("Packaging dimensions");root.transform.SetParent(s.transform,false);var guides=root.AddComponent<PackagingGuides>();guides.studio=s;
        LineRenderer Line(string name){var o=new GameObject(name);o.transform.SetParent(root.transform,false);var l=o.AddComponent<LineRenderer>();l.positionCount=2;l.useWorldSpace=false;l.startWidth=l.endWidth=.0007f;l.sharedMaterial=Material("Dimension ink");if(!l.sharedMaterial)l.sharedMaterial=Material("Printed teal");return l;}
        TextMesh Text(string name,Quaternion q){var o=new GameObject(name);o.transform.SetParent(root.transform,false);o.transform.localRotation=q;var t=o.AddComponent<TextMesh>();t.fontSize=64;t.characterSize=.0014f;t.anchor=TextAnchor.MiddleCenter;t.color=new Color(.3f,.95f,.8f);return t;}
        guides.width=Line("Width");guides.depth=Line("Depth");guides.height=Line("Height");guides.widthText=Text("Width label",Quaternion.Euler(90,0,0));guides.depthText=Text("Depth label",Quaternion.Euler(90,0,90));guides.heightText=Text("Height label",Quaternion.identity);guides.Apply();
    }
    [MenuItem("Tools/PackFold MR/Build APK")]
    public static void BuildAndroid()
    {
        string tools=Environment.GetEnvironmentVariable("PACKFOLD_ANDROID_TOOLS");
        if(!string.IsNullOrEmpty(tools))
        {
            UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath=Path.Combine(tools,"SDK");
            UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath=Path.Combine(tools,"NDK","android-ndk-r27c");
            UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath=Path.Combine(tools,"JDK");
        }
        Configure();Verify();Directory.CreateDirectory("Build");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},target=BuildTarget.Android,locationPathName="Build/PackFold_MR_Quest3_v0.5.apk",options=BuildOptions.None});
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Quest build failed: "+report.summary.result);
        using(var apk=System.IO.Compression.ZipFile.OpenRead("Build/PackFold_MR_Quest3_v0.5.apk"))
            if(apk.GetEntry("lib/arm64-v8a/libUnityARFoundationMeta.so")==null)throw new Exception("APK is missing the Meta native passthrough library");
        Debug.Log("PACKFOLD_MR_APK_SUCCEEDED bytes="+report.summary.totalSize);
    }
    public static void Verify()
    {
        var session=UnityEngine.Object.FindFirstObjectByType<MRDesignSession>();var s=session.studio;var m=session.GetComponent<MRDesignManipulator>();
        if(!session)throw new Exception("MR scene missing");var original=session.Capture();int checks=0;
        void Check(bool ok,string text){if(!ok)throw new Exception("MR verification: "+text);checks++;}
        Check(OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android).GetFeatures<OpenXRFeature>().Any(f=>f.GetType().Name=="OpenXRLifeCycleFeature"&&f.enabled),"Meta native lifecycle enabled");
        Check(Camera.main.backgroundColor.a==0,"Transparent background");Check(Camera.main.GetComponent<ARCameraManager>()!=null,"AR passthrough camera");Check(UnityEngine.Object.FindFirstObjectByType<ARSession>()!=null,"AR session");
        Check(UnityEngine.Object.FindObjectsByType<XRRayInteractor>(FindObjectsSortMode.None).Length==2,"Two native XR UI rays");Check(UnityEngine.Object.FindFirstObjectByType<MRDesignPanel>().GetComponent<Canvas>().renderMode==RenderMode.WorldSpace,"World space UI");
        foreach(var ray in UnityEngine.Object.FindObjectsByType<XRRayInteractor>(FindObjectsSortMode.None))Check(ray.GetComponent<LineRenderer>().sharedMaterial.shader.name=="PackFold/XR Pointer"&&ray.GetComponent<LineRenderer>().sharedMaterial.enableInstancing,"Explicit stereo ray shader");
        foreach(var ray in UnityEngine.Object.FindObjectsByType<XRRayInteractor>(FindObjectsSortMode.None))Check((ray.raycastMask.value&(1<<LayerMask.NameToLayer("UI")))!=0,"UI layer included in controller raycasts");
        Check(UnityEngine.Object.FindFirstObjectByType<MRDesignPanel>().GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(g=>g.gameObject.layer==LayerMask.NameToLayer("UI")),"All panel graphics use UI layer");
        foreach(var visual in UnityEngine.Object.FindObjectsByType<MRControllerVisual>(FindObjectsSortMode.None))
        {
            Check(visual.modelPrefab&&visual.modelPrefab.name.StartsWith("MetaTouchPlus_"),"Official Touch Plus model assigned");
            Check(Quaternion.Angle(visual.transform.GetChild(0).localRotation,Quaternion.Euler(60,0,0))<.01f,"Official OpenXR grip-to-model rotation");
        }
        Check(Vector3.Distance(MRControllerVisual.GripToModel().GetPosition(),new Vector3(0,-.019641f,.045981f))<.0001f,"Official grip-to-model translation");
        var gate=new MRPlacementGate();Check(!gate.Ready(false,Vector3.zero,0),"Tracking flag without valid pose does not place workspace");
        bool ready=false;for(int i=0;i<5;i++)ready=gate.Ready(true,new Vector3(0,.85f,0),i*.02f);Check(!ready,"No placement during first pose samples");
        ready=gate.Ready(true,new Vector3(0,.85f,0),.4f);Check(ready,"Stable seated head pose is accepted");
        Check(!gate.Ready(true,new Vector3(0,1.65f,0),.42f),"Tracking-origin jump restarts placement delay");
        Check(gate.Ready(true,new Vector3(0,1.65f,0),.8f)==false,"Minimum sample count after origin jump");
        for(int i=0;i<6;i++)ready=gate.Ready(true,new Vector3(0,1.65f,0),.85f+i*.02f);Check(ready,"Stable standing head pose is accepted");
        Check(!gate.Ready(true,new Vector3(float.NaN,0,0),1.1f),"Invalid head pose rejected");
        var savedHead=m.head.position;var savedPanel=m.panel.position;var savedPanelRotation=m.panel.rotation;m.head.position=new Vector3(.2f,.85f,-.1f);m.Recenter();Check(Mathf.Abs(m.panel.position.y-(m.head.position.y-.04f))<.001f,"Panel placed at seated viewing height");
        Check(Mathf.Abs(Vector3.Dot(m.panel.position-m.head.position,m.head.forward)-.80f)<.001f,"Closer 80 cm panel placement");Check(Mathf.Abs(Vector3.Dot(s.transform.position-m.head.position,m.head.forward)-.65f)<.001f,"Closer 65 cm model placement");
        m.head.position=savedHead;m.panel.SetPositionAndRotation(savedPanel,savedPanelRotation);
        for(int i=0;i<3;i++){session.Preset(i);Check(s.StructureId==(i==0?"lift_off_lid":i==1?"hinged_lid":"open_tray"),"Structure "+i);}
        session.Preset(0);float w=s.widthMm;s.widthMm=220;session.Commit();session.Undo();Check(s.widthMm==w,"Full revision undo");session.Redo();Check(s.widthMm==220,"Full revision redo");
        session.Apply(original);session.Commit();
        var frame=new MRDesignManipulator.Frame{tracked=true,grip=true,position=s.transform.position+new Vector3(0,-.015f,0),rotation=Quaternion.identity};var empty=new MRDesignManipulator.Frame();
        m.Step(frame,empty);Check(m.LeftHold==MRDesignManipulator.Hold.Body,"Near grip selects box");Vector3 before=s.transform.position;frame.position+=Vector3.right*.1f;m.Step(frame,empty);Check(Vector3.Distance(s.transform.position,before+Vector3.right*.1f)<.001f,"One hand translation");
        m.Step(empty,empty);Check(m.LeftHold==MRDesignManipulator.Hold.None,"Tracking loss releases");
        session.Apply(original);session.Commit();string folder=session.Export(Path.GetFullPath("Verification/EditorExport"));Check(folder!=null&&File.Exists(Path.Combine(folder,"base_dieline.svg"))&&File.Exists(Path.Combine(folder,"lid_dieline.svg")),"Two piece exports");
        session.Apply(original);m.Release();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Scene);
        PreviewControllers();Directory.CreateDirectory("Verification");File.WriteAllText("Verification/mr_editor_checks.json","{\"passed\":true,\"checks\":"+checks+",\"device_tested\":false}");Debug.Log("PACKFOLD_MR_EDITOR_VERIFIED "+checks);
    }
}
