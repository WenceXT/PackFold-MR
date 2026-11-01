using UnityEngine;
using UnityEngine.XR;

namespace Portfolio.PackFold
{
    // Optional controller pose/button adapter. A configured OpenXR loader is required.
    // This is NOT passthrough or hand tracking. It is inactive in desktop mode.
    public class QuestInputBridge : MonoBehaviour
    {
        public PackFoldStudio studio;
        public Transform trackingOrigin;
        public Camera viewCamera;
        private bool wasGrip,wasButton;
        private Vector3 offset;
        private Quaternion relativeRotation;
        private void Update()
        {
            if(studio==null || studio.modelRoot==null || !XRSettings.isDeviceActive) return;
            InputDevice head=InputDevices.GetDeviceAtXRNode(XRNode.Head);
            InputDevice hand=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            Vector3 position; Quaternion rotation;
            if(head.TryGetFeatureValue(CommonUsages.devicePosition,out position) && head.TryGetFeatureValue(CommonUsages.deviceRotation,out rotation))
            { viewCamera.transform.position=trackingOrigin.TransformPoint(position); viewCamera.transform.rotation=trackingOrigin.rotation*rotation; }
            bool tracked;
            if(!hand.TryGetFeatureValue(CommonUsages.isTracked,out tracked) || !tracked ||
                !hand.TryGetFeatureValue(CommonUsages.devicePosition,out position) || !hand.TryGetFeatureValue(CommonUsages.deviceRotation,out rotation))
            { wasGrip=false; return; }
            Vector3 handWorld=trackingOrigin.TransformPoint(position); Quaternion handRotation=trackingOrigin.rotation*rotation;
            bool grip; hand.TryGetFeatureValue(CommonUsages.gripButton,out grip);
            if(grip && !wasGrip && Vector3.Distance(handWorld,studio.modelRoot.position)<.45f)
            { offset=Quaternion.Inverse(handRotation)*(studio.modelRoot.position-handWorld); relativeRotation=Quaternion.Inverse(handRotation)*studio.modelRoot.rotation; wasGrip=true; }
            if(!grip) wasGrip=false;
            if(wasGrip) { studio.modelRoot.position=handWorld+handRotation*offset; studio.modelRoot.rotation=handRotation*relativeRotation; }
            Vector2 axis; if(hand.TryGetFeatureValue(CommonUsages.primary2DAxis,out axis) && Mathf.Abs(axis.y)>.15f)
                studio.targetFold=Mathf.Clamp01(studio.targetFold+axis.y*Time.deltaTime*.5f);
            bool button; hand.TryGetFeatureValue(CommonUsages.primaryButton,out button);
            if(button && !wasButton) studio.targetFold=studio.targetFold>.5f?0:1;
            wasButton=button;
        }
    }
}
