using UnityEngine;

namespace Portfolio.PackFold
{
    // Tracking availability can precede the first valid head pose or a tracking-origin update.
    public sealed class MRPlacementGate
    {
        float since=-1;int samples;Vector3 previous;
        public void Reset(){since=-1;samples=0;}
        public bool Ready(bool valid,Vector3 position,float time)
        {
            if(!valid||float.IsNaN(position.x)||float.IsNaN(position.y)||float.IsNaN(position.z)||float.IsInfinity(position.x)||float.IsInfinity(position.y)||float.IsInfinity(position.z)){Reset();return false;}
            if(since<0||Vector3.Distance(previous,position)>.15f){since=time;samples=0;}
            previous=position;samples++;
            return samples>=6&&time-since>=.3f;
        }
    }
}
