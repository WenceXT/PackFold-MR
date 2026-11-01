using UnityEngine;
namespace Portfolio.PackFold
{
    public class PackagingGuides : MonoBehaviour
    {
        public PackFoldStudio studio;
        public LineRenderer width,depth,height;
        public TextMesh widthText,depthText,heightText;
        private void LateUpdate() {Apply();}
        public void Apply()
        {
            transform.SetPositionAndRotation(studio.BodyRoot.position,studio.BodyRoot.rotation);transform.localScale=studio.modelRoot.localScale;
            float w=studio.widthMm*.001f,d=studio.depthMm*.001f,h=studio.heightMm*.001f;
            width.SetPositions(new[]{new Vector3(-w/2,.002f,-d/2-.023f),new Vector3(w/2,.002f,-d/2-.023f)});
            depth.SetPositions(new[]{new Vector3(w/2+.023f,.002f,-d/2),new Vector3(w/2+.023f,.002f,d/2)});
            height.SetPositions(new[]{new Vector3(-w/2-.02f,0,d/2),new Vector3(-w/2-.02f,h,d/2)});
            widthText.text=studio.widthMm.ToString("F0")+" mm";widthText.transform.localPosition=new Vector3(0,.003f,-d/2-.037f);
            depthText.text=studio.depthMm.ToString("F0")+" mm";depthText.transform.localPosition=new Vector3(w/2+.038f,.003f,0);
            heightText.text=studio.heightMm.ToString("F0")+" mm";heightText.transform.localPosition=new Vector3(-w/2-.035f,h*.55f,d/2);
        }
    }
}
