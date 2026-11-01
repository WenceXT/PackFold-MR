using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;

namespace Portfolio.PackFold
{
    public class MRDesignManipulator : MonoBehaviour
    {
        public MRDesignSession session;
        public Transform leftHand,rightHand,head,panel;
        public MRControllerVisual leftVisual,rightVisual;
        public GameObject leftPointer,rightPointer;
        public Camera view;
        public string interaction="READY";
        public enum Hold{None,Body,Lid}
        public struct Frame{public bool tracked,grip,primary,secondary;public Vector3 position;public Quaternion rotation;}
        class Hand
        {
            public Hold hold;public bool grip,primary,secondary;public Vector3 offset;public Quaternion rotation;
            public float lidStart,handY;
        }
        readonly Hand left=new Hand(),right=new Hand();
        bool twoHand,initialPlaced;
        readonly MRPlacementGate placementGate=new MRPlacementGate();
        readonly List<XRInputSubsystem> inputSubsystems=new List<XRInputSubsystem>();
        Vector3 startMid,startVector,startPosition;Quaternion startRotation;float startDistance,startScale;
        float nextDeviceLog;
        PackFoldStudio Studio=>session.studio;
        public Hold LeftHold=>left.hold;
        public Hold RightHold=>right.hold;
        void Start()
        {
            if(Application.platform!=RuntimePlatform.Android)return;
            SetContentVisible(false);
            SubsystemManager.GetSubsystems(inputSubsystems);
            foreach(var input in inputSubsystems)input.trackingOriginUpdated+=OriginUpdated;
        }
        void OriginUpdated(XRInputSubsystem input){initialPlaced=false;placementGate.Reset();Release();}
        void OnDestroy(){foreach(var input in inputSubsystems)input.trackingOriginUpdated-=OriginUpdated;}
        void SetContentVisible(bool visible){Studio.gameObject.SetActive(visible);if(panel)panel.gameObject.SetActive(visible);}
        void Update()
        {
            Frame l=Read(XRNode.LeftHand,leftHand),r=Read(XRNode.RightHand,rightHand);
            if(!initialPlaced)
            {
                bool valid=TryGetHeadPose(out Pose pose)&&ARSession.state==ARSessionState.SessionTracking&&Vector3.Distance(head.position,pose.position)<.12f;
                if(placementGate.Ready(valid,pose.position,Time.unscaledTime))
                {
                    Place(pose);initialPlaced=true;SetContentVisible(true);
                    session.ResetHistory("Workspace placed at your viewing height. X places it in front of you again.");
                }
            }
            Step(l,r);
            if(leftPointer&&leftPointer.activeSelf!=l.tracked)leftPointer.SetActive(l.tracked);if(rightPointer&&rightPointer.activeSelf!=r.tracked)rightPointer.SetActive(r.tracked);
            if(leftVisual)leftVisual.SetGrabbed(left.hold!=Hold.None);
            if(rightVisual)rightVisual.SetGrabbed(right.hold!=Hold.None);
            if(Time.unscaledTime>nextDeviceLog){nextDeviceLog=Time.unscaledTime+15;Debug.Log($"PACKFOLD_MR_TRACKING head={initialPlaced} left={l.tracked} right={r.tracked} frame_ms={Time.unscaledDeltaTime*1000:F1}");}
        }
        Frame Read(XRNode node,Transform pose)
        {
            var d=InputDevices.GetDeviceAtXRNode(node);d.TryGetFeatureValue(CommonUsages.isTracked,out bool tracked);d.TryGetFeatureValue(CommonUsages.gripButton,out bool grip);d.TryGetFeatureValue(CommonUsages.primaryButton,out bool p);d.TryGetFeatureValue(CommonUsages.secondaryButton,out bool s);
            return new Frame{tracked=d.isValid&&tracked,grip=grip,primary=p,secondary=s,position=pose.position,rotation=pose.rotation};
        }
        public void Recenter()
        {
            Pose pose=new Pose(head.position,head.rotation);
            if(Application.isPlaying&&Application.platform==RuntimePlatform.Android&&!TryGetHeadPose(out pose))
            {initialPlaced=false;placementGate.Reset();session.status="Waiting for a valid head pose before placing the workspace.";return;}
            Place(pose);
            if(Application.isPlaying){initialPlaced=true;SetContentVisible(true);}
        }
        bool TryGetHeadPose(out Pose pose)
        {
            pose=new Pose(head.position,head.rotation);
            var device=InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if(!device.isValid||!device.TryGetFeatureValue(CommonUsages.isTracked,out bool tracked)||!tracked)return false;
            if(!device.TryGetFeatureValue(CommonUsages.centerEyePosition,out Vector3 position)&&!device.TryGetFeatureValue(CommonUsages.devicePosition,out position))return false;
            if(!device.TryGetFeatureValue(CommonUsages.centerEyeRotation,out Quaternion rotation)&&!device.TryGetFeatureValue(CommonUsages.deviceRotation,out rotation))return false;
            // A zero floor-space pose during initialization is not a valid workspace height.
            if(position.sqrMagnitude<.04f)return false;
            Transform space=head.parent;
            pose=new Pose(space?space.TransformPoint(position):position,space?space.rotation*rotation:rotation);return true;
        }
        void Place(Pose pose)
        {
            Release();Vector3 forward=Vector3.ProjectOnPlane(pose.rotation*Vector3.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.1f)forward=Vector3.forward;
            Quaternion yaw=Quaternion.LookRotation(forward);Vector3 rightAxis=yaw*Vector3.right;
            Studio.transform.SetPositionAndRotation(pose.position+forward*.65f+rightAxis*.21f-Vector3.up*.24f,yaw);Studio.transform.localScale=Vector3.one*1.35f;
            if(panel)panel.SetPositionAndRotation(pose.position+forward*.80f-rightAxis*.25f-Vector3.up*.04f,yaw);
            session.Commit("Workspace placed in front of you. It stays in place as you look around.");
            Debug.Log($"PACKFOLD_MR_PLACED head={pose.position} panel={(panel?panel.position:Vector3.zero)}");
        }
        public void Release(){left.hold=right.hold=Hold.None;twoHand=false;}
        public void Step(Frame l,Frame r)
        {
            bool hadHold=left.hold!=Hold.None||right.hold!=Hold.None;
            if(!l.tracked||!l.grip)left.hold=Hold.None;if(!r.tracked||!r.grip)right.hold=Hold.None;
            if(l.tracked&&l.primary&&!left.primary)Recenter();
            if(l.tracked&&l.secondary&&!left.secondary){Release();session.Undo();}
            if(r.tracked&&r.primary&&!right.primary){Release();session.ToggleLid();}
            if(r.tracked&&r.secondary&&!right.secondary){Release();session.ToggleFold();}
            Begin(left,l,XRNode.LeftHand);Begin(right,r,XRNode.RightHand);
            bool both=left.hold==Hold.Body&&right.hold==Hold.Body;
            if(both)
            {
                Vector3 vector=r.position-l.position,mid=(r.position+l.position)*.5f;float distance=vector.magnitude;
                if(!twoHand){twoHand=true;startDistance=Mathf.Max(distance,.06f);startScale=Studio.transform.localScale.x;startMid=mid;startVector=vector;startPosition=Studio.transform.position;startRotation=Studio.transform.rotation;session.twoHandGrabs++;}
                float scale=Mathf.Clamp(startScale*distance/startDistance,.5f,3);Quaternion delta=vector.sqrMagnitude>.001f?Quaternion.FromToRotation(startVector,vector):Quaternion.identity;
                Studio.transform.localScale=Vector3.one*scale;Studio.transform.rotation=delta*startRotation;
                Studio.transform.position=mid+delta*(startPosition-startMid)*(scale/startScale);interaction="TWO HANDS / SCALE + ROTATE";
            }
            else
            {
                if(twoHand){twoHand=false;ResetOffset(left,l);ResetOffset(right,r);}
                Move(left,l);Move(right,r);
                interaction=left.hold==Hold.Lid||right.hold==Hold.Lid?"LID / LIFT TO OPEN":left.hold==Hold.Body||right.hold==Hold.Body?"GRAB / MOVE + ROTATE":"READY / GRIP BOX OR LID";
            }
            if(hadHold&&left.hold==Hold.None&&right.hold==Hold.None)session.Commit("Placement saved");
            left.grip=l.grip;right.grip=r.grip;left.primary=l.primary;right.primary=r.primary;left.secondary=l.secondary;right.secondary=r.secondary;
        }
        void Begin(Hand hand,Frame f,XRNode node)
        {
            if(!f.tracked||!f.grip||hand.grip||hand.hold!=Hold.None)return;
            Physics.SyncTransforms();
            // Prefer exact collider hits for the separate cover, then the whole packaging bounds.
            Transform cap=Studio.DetachableLid;
            if(node==XRNode.RightHand&&Studio.twoPieceMode&&Studio.fold>.95f&&cap&&Near(cap,f,.035f))
            {hand.hold=Hold.Lid;hand.lidStart=Studio.targetLidOpen;hand.handY=Studio.transform.InverseTransformPoint(f.position).y;session.lidGrabs++;}
            else if(Near(Studio.modelRoot,f,.075f)){hand.hold=Hold.Body;ResetOffset(hand,f);session.grabs++;}
            if(hand.hold!=Hold.None){InputDevices.GetDeviceAtXRNode(node).SendHapticImpulse(0,.25f,.055f);Debug.Log("PACKFOLD_MR_GRAB "+node+" "+hand.hold);}
        }
        bool Near(Transform root,Frame frame,float radius)
        {
            float nearest=float.PositiveInfinity;bool rayHit=false;
            Ray ray=new Ray(frame.position,frame.rotation*Vector3.forward);
            foreach(Collider collider in root.GetComponentsInChildren<Collider>())
            {
                if(!collider.enabled||!collider.gameObject.activeInHierarchy)continue;
                nearest=Mathf.Min(nearest,Vector3.Distance(collider.ClosestPoint(frame.position),frame.position));
                if(collider.Raycast(ray,out RaycastHit hit,.9f))rayHit=true;
            }
            return nearest<radius*Studio.transform.localScale.x||rayHit;
        }
        void ResetOffset(Hand hand,Frame f)
        {if(hand.hold==Hold.Body){hand.offset=Quaternion.Inverse(f.rotation)*(Studio.transform.position-f.position);hand.rotation=Quaternion.Inverse(f.rotation)*Studio.transform.rotation;}}
        void Move(Hand hand,Frame f)
        {
            if(hand.hold==Hold.Body){Studio.transform.SetPositionAndRotation(f.position+f.rotation*hand.offset,f.rotation*hand.rotation);}
            else if(hand.hold==Hold.Lid)
            {
                float delta=Studio.transform.InverseTransformPoint(f.position).y-hand.handY;
                Studio.targetLidOpen=Mathf.Clamp01(hand.lidStart+delta/.17f);
            }
        }
        void OnApplicationFocus(bool focus){if(!focus){Release();left.grip=right.grip=false;}}
    }
}
