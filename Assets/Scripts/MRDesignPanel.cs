using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Portfolio.PackFold
{
    public class MRDesignPanel : MonoBehaviour
    {
        public MRDesignSession session;
        public MRDesignManipulator manipulator;
        public Camera view;
        Text metrics,status,mode;
        GameObject designPage,fitPage;
        Font font;
        readonly Color ink=new Color(.89f,.94f,.93f),muted=new Color(.57f,.69f,.70f),teal=new Color(.10f,.53f,.46f),dark=new Color(.035f,.065f,.075f,.97f);
        bool syncing;
        Slider width,depth,height,fold,lid,skirt,clearance,scale;
        Text widthLabel,depthLabel,heightLabel,foldLabel,lidLabel,skirtLabel,clearanceLabel,scaleLabel;
        float dirtyAt;
        public void Build()
        {
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas=GetComponent<Canvas>()??gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=view;
            var rt=GetComponent<RectTransform>();rt.sizeDelta=new Vector2(640,900);transform.localScale=Vector3.one*.00065f;
            if(!GetComponent<TrackedDeviceGraphicRaycaster>())gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            var bg=gameObject.GetComponent<Image>()??gameObject.AddComponent<Image>();bg.color=dark;
            TextAt(transform,"PACKFOLD  /  MR",28,28,584,44,31,ink);
            TextAt(transform,"AIM AT PANEL · TRIGGER: CLICK / HOLD + DRAG",28,76,584,24,16,muted);
            ButtonAt(transform,"DESIGN",28,112,280,42,()=>{designPage.SetActive(true);fitPage.SetActive(false);});
            ButtonAt(transform,"FIT + ASSIST",320,112,292,42,()=>{designPage.SetActive(false);fitPage.SetActive(true);});
            designPage=Group("Design",transform);fitPage=Group("Fit & assistant",transform);
            ButtonAt(designPage.transform,"LIFT-OFF",28,174,186,46,()=>Preset(0));ButtonAt(designPage.transform,"HINGED",226,174,186,46,()=>Preset(1));ButtonAt(designPage.transform,"TRAY",424,174,188,46,()=>Preset(2));
            width=SliderAt(designPage.transform,244,"WIDTH",60,300,v=>session.studio.widthMm=Mathf.Round(v),out widthLabel);
            depth=SliderAt(designPage.transform,313,"DEPTH",50,240,v=>session.studio.depthMm=Mathf.Round(v),out depthLabel);
            height=SliderAt(designPage.transform,382,"HEIGHT",25,150,v=>session.studio.heightMm=Mathf.Round(v),out heightLabel);
            fold=SliderAt(designPage.transform,451,"ASSEMBLY",0,1,v=>session.studio.targetFold=v,out foldLabel);
            lid=SliderAt(designPage.transform,520,"LIFT-OFF COVER",0,1,v=>session.studio.targetLidOpen=v,out lidLabel);
            ButtonAt(designPage.transform,"OPEN / CLOSE",28,593,186,48,()=>{Release();session.ToggleLid();});
            ButtonAt(designPage.transform,"FOLD / FLAT",226,593,186,48,()=>{Release();session.ToggleFold();});
            ButtonAt(designPage.transform,"RECENTER",424,593,188,48,()=>manipulator.Recenter());
            ButtonAt(designPage.transform,"UNDO",28,655,186,48,()=>{Release();session.Undo();});ButtonAt(designPage.transform,"REDO",226,655,186,48,()=>{Release();session.Redo();});ButtonAt(designPage.transform,"EXPORT",424,655,188,48,()=>session.Export());
            metrics=TextAt(designPage.transform,"",28,722,584,72,20,ink);
            TextAt(fitPage.transform,"LID FIT    /    1.2 mm nominal board",28,177,584,34,22,ink);
            skirt=SliderAt(fitPage.transform,235,"COVER SKIRT",8,45,v=>session.studio.lidDepthMm=v,out skirtLabel);
            clearance=SliderAt(fitPage.transform,304,"SIDE CLEARANCE",.2f,2.5f,v=>session.studio.clearanceMm=v,out clearanceLabel);
            scale=SliderAt(fitPage.transform,373,"PREVIEW SCALE",.5f,3,v=>session.studio.transform.localScale=Vector3.one*v,out scaleLabel);
            ButtonAt(fitPage.transform,"GLUE FLAPS",28,447,280,48,()=>{session.studio.includeTabs=!session.studio.includeTabs;session.studio.Rebuild();session.Commit("Glue flaps toggled");});
            ButtonAt(fitPage.transform,"SENSOR INSERT",320,447,292,48,()=>{session.studio.showContents=!session.studio.showContents;session.Commit("Sensor insert toggled");});
            TextAt(fitPage.transform,"OFFLINE STRUCTURE ASSISTANT",28,526,584,30,22,ink);
            TextAt(fitPage.transform,"Template rules · no cloud / generative AI\nItem envelope + protective clearance",28,567,584,58,18,muted);
            ButtonAt(fitPage.transform,"SENSOR KIT",28,642,186,48,()=>Recommend(0));ButtonAt(fitPage.transform,"CAMERA",226,642,186,48,()=>Recommend(1));ButtonAt(fitPage.transform,"CONTROLLER",424,642,188,48,()=>Recommend(2));
            TextAt(fitPage.transform,"Preview scale changes viewing size only.\nExported dimensions stay in millimetres.\nCheck paper thickness and real manufacturing fit.",28,717,584,78,18,muted);
            mode=TextAt(transform,"",28,808,584,25,17,new Color(.38f,.83f,.72f));
            status=TextAt(transform,"",28,845,584,44,16,muted);
            fitPage.SetActive(false);
            // Children are recreated at runtime; layer assignment must be recreated too.
            foreach(var child in GetComponentsInChildren<Transform>(true))child.gameObject.layer=LayerMask.NameToLayer("UI");
        }
        void Awake()
        {
            // Runtime listeners and UI references are rebuilt after scene deserialization.
            for(int i=transform.childCount-1;i>=0;i--){var child=transform.GetChild(i).gameObject;child.SetActive(false);Destroy(child);}
            Build();
        }
        void Release(){manipulator.Release();}
        void Preset(int n){Release();session.Preset(n);}
        void Recommend(int n){Release();session.Recommend(n);}
        GameObject Group(string name,Transform parent){var o=new GameObject(name,typeof(RectTransform));o.transform.SetParent(parent,false);var r=(RectTransform)o.transform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.sizeDelta=Vector2.zero;return o;}
        RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
        {var o=new GameObject(name,typeof(RectTransform));o.transform.SetParent(parent,false);var r=(RectTransform)o.transform;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
        Text TextAt(Transform parent,string value,float x,float y,float w,float h,int size,Color color)
        {var r=Rect(value,parent,x,y,w,h);var t=r.gameObject.AddComponent<Text>();t.text=value;t.font=font;t.fontSize=size;t.color=color;t.raycastTarget=false;t.verticalOverflow=VerticalWrapMode.Truncate;t.horizontalOverflow=HorizontalWrapMode.Wrap;return t;}
        void ButtonAt(Transform parent,string title,float x,float y,float w,float h,Action action)
        {
            var r=Rect(title,parent,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.color=teal;var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var colors=button.colors;colors.highlightedColor=new Color(1.25f,1.3f,1.3f);colors.pressedColor=new Color(.7f,.8f,.8f);button.colors=colors;button.navigation=new Navigation{mode=Navigation.Mode.None};
            button.onClick.AddListener(()=>{if(dirtyAt>0){dirtyAt=0;session.Commit();}action();Debug.Log("PACKFOLD_MR_UI "+title);});var text=TextAt(r,title,5,0,w-10,h,19,ink);text.alignment=TextAnchor.MiddleCenter;
        }
        Slider SliderAt(Transform parent,float y,string title,float min,float max,Action<float> change,out Text label)
        {
            label=TextAt(parent,title,28,y,584,26,20,ink);
            var r=Rect(title+" slider",parent,28,y+27,584,34);r.gameObject.AddComponent<Image>().color=new Color(.10f,.17f,.18f);
            var slider=r.gameObject.AddComponent<Slider>();slider.minValue=min;slider.maxValue=max;slider.navigation=new Navigation{mode=Navigation.Mode.None};
            var fill=Rect("Fill",r,0,12,584,10);fill.anchorMin=new Vector2(0,.35f);fill.anchorMax=new Vector2(1,.65f);fill.offsetMin=fill.offsetMax=Vector2.zero;fill.gameObject.AddComponent<Image>().color=teal;slider.fillRect=fill;
            var handleArea=Rect("Handle area",r,15,0,554,34);
            var handle=Rect("Handle",handleArea,0,0,30,34);handle.anchorMin=handle.anchorMax=handle.pivot=new Vector2(.5f,.5f);handle.anchoredPosition=Vector2.zero;var img=handle.gameObject.AddComponent<Image>();img.color=new Color(.68f,.89f,.81f);slider.handleRect=handle;slider.targetGraphic=img;
            slider.onValueChanged.AddListener(v=>{if(syncing)return;Release();change(v);dirtyAt=Time.unscaledTime+.6f;});return slider;
        }
        void LateUpdate()
        {
            if(!width||!session)return;var s=session.studio;syncing=true;
            width.SetValueWithoutNotify(s.widthMm);depth.SetValueWithoutNotify(s.depthMm);height.SetValueWithoutNotify(s.heightMm);fold.SetValueWithoutNotify(s.targetFold);lid.SetValueWithoutNotify(s.targetLidOpen);
            skirt.maxValue=Mathf.Min(45,s.heightMm*.7f);skirt.SetValueWithoutNotify(s.lidDepthMm);clearance.SetValueWithoutNotify(s.clearanceMm);scale.SetValueWithoutNotify(s.transform.localScale.x);
            widthLabel.text=$"WIDTH    {s.widthMm:F0} mm";depthLabel.text=$"DEPTH    {s.depthMm:F0} mm";heightLabel.text=$"HEIGHT    {s.heightMm:F0} mm";
            foldLabel.text=$"ASSEMBLY    {s.targetFold*100:F0}%";lidLabel.text=s.twoPieceMode?$"LIFT-OFF COVER    {s.targetLidOpen*100:F0}%":"LIFT-OFF COVER    available on lift-off structure";lid.interactable=s.twoPieceMode;
            skirt.interactable=clearance.interactable=s.twoPieceMode;skirtLabel.text=$"COVER SKIRT    {s.lidDepthMm:F1} mm";clearanceLabel.text=$"SIDE CLEARANCE    {s.clearanceMm:F1} mm";scaleLabel.text=$"PREVIEW SCALE    {s.transform.localScale.x:F2} x";
            metrics.text=$"{s.StructureId.Replace('_',' ').ToUpperInvariant()}   /   {s.widthMm*s.depthMm*s.heightMm/1000000:F2} L\nBOARD  {s.TotalBoardAreaMm2/100:F0} cm²   ·   FLAPS {(s.includeTabs?"ON":"OFF")}   ·   SCALE {s.transform.localScale.x:F2}x";
            mode.text=manipulator.interaction+"    |    X RECENTER · Y UNDO";
            status.text=session.status;syncing=false;
            if(dirtyAt>0&&Time.unscaledTime>=dirtyAt){dirtyAt=0;session.Commit("Design updated");}
        }
    }
}
