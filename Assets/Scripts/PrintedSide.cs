using UnityEngine;
namespace Portfolio.PackFold
{
    public class PrintedSide : MonoBehaviour
    {
        public void Apply(Camera camera)
        {if(camera!=null) GetComponent<Renderer>().enabled=Vector3.Dot(-transform.forward,camera.transform.position-transform.position)>0;}
        private void LateUpdate() {Apply(Camera.main);}
    }
}
