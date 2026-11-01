using UnityEngine;
using UnityEngine.XR;

namespace Portfolio.PackFold
{
    // Official Meta mesh in VrApi model space; parent tracking uses OpenXR grip space.
    public class MRControllerVisual : MonoBehaviour
    {
        public bool left;
        public GameObject modelPrefab;
        Transform model;
        Animator animator;
        public void SetGrabbed(bool value){} // Preserve the official material; grab feedback is haptic.
        public static Matrix4x4 GripToModel()
        {
            // Meta XR Core SDK 207.0.1 OVRInput._gripToVrapiOffsetInverse.
            return Matrix4x4.TRS(new Vector3(0,-.03f,-.04f),new Quaternion(-.5f,0,0,.86603f),Vector3.one).inverse;
        }
        void Awake()
        {
            for(int i=transform.childCount-1;i>=0;i--){var old=transform.GetChild(i).gameObject;old.SetActive(false);Destroy(old);}
            Build();
        }
        public void Build()
        {
            if(!modelPrefab)throw new System.InvalidOperationException("Official Touch Plus prefab is missing");
            model=new GameObject("Meta grip-to-model alignment").transform;model.SetParent(transform,false);
            var offset=GripToModel();model.localPosition=offset.GetPosition();model.localRotation=offset.rotation;
            var instance=Instantiate(modelPrefab,model,false);instance.name=left?"Meta Touch Plus Left":"Meta Touch Plus Right";
            instance.transform.localPosition=Vector3.zero;instance.transform.localRotation=Quaternion.identity;
            animator=instance.GetComponentInChildren<Animator>();
            if(animator){animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;if(Application.isPlaying)animator.Update(0);}
            Debug.Log("PACKFOLD_META_CONTROLLER_READY hand="+(left?"Left":"Right")+" offset="+model.localPosition+" rotation="+model.localEulerAngles);
        }
        void LateUpdate()
        {
            var device=InputDevices.GetDeviceAtXRNode(left?XRNode.LeftHand:XRNode.RightHand);device.TryGetFeatureValue(CommonUsages.isTracked,out bool tracked);
            if(model&&model.gameObject.activeSelf!=tracked)model.gameObject.SetActive(tracked);if(!tracked)return;
            device.TryGetFeatureValue(CommonUsages.trigger,out float index);device.TryGetFeatureValue(CommonUsages.grip,out float grip);device.TryGetFeatureValue(CommonUsages.primary2DAxis,out Vector2 axis);
            device.TryGetFeatureValue(CommonUsages.primaryButton,out bool a);device.TryGetFeatureValue(CommonUsages.secondaryButton,out bool b);device.TryGetFeatureValue(CommonUsages.menuButton,out bool menu);
            Animate(index,grip,axis,a,b,false);if(animator)animator.SetFloat("Button 3",menu?1:0);
        }
        public void Animate(float index,float grip,Vector2 axis,bool a,bool b,bool holding)
        {
            if(!animator)return;animator.SetFloat("Button 1",a?1:0);animator.SetFloat("Button 2",b?1:0);animator.SetFloat("Grip",grip);animator.SetFloat("Trigger",index);animator.SetFloat("Joy X",axis.x);animator.SetFloat("Joy Y",axis.y);
        }
    }
}
