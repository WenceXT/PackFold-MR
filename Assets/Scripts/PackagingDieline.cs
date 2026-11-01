using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;
using UnityEngine;

namespace Portfolio.PackFold
{
    public static class PackagingDieline
    {
        public static float TabWidth(float height) {return Mathf.Min(18,height*.4f);}
        public static float NetArea(float w,float d,float h,bool tray,bool tabs)
        {return w*d*(tray?1:2)+2*h*(w+d)+(tabs?4*TabWidth(h)*h*.85f+(tray?0:w*.88f*TabWidth(h)):0);}
        private static int Owner(List<Rect> rectangles,float x,float y)
        {for(int i=0;i<rectangles.Count;i++) if(rectangles[i].Contains(new Vector2(x,y))) return i;return -1;}
        public static string Create(float w,float d,float h,bool tray,bool tabs)
        {
            var panels=new List<Rect>{new Rect(h,h,w,d),new Rect(0,h,h,d),new Rect(h+w,h,h,d),new Rect(h,0,w,h),new Rect(h,h+d,w,h)};
            if(!tray) panels.Add(new Rect(h,2*h+d,w,d));
            float g=TabWidth(h);
            if(tabs)
            {
                foreach(float y in new[]{h*.075f,h+d+h*.075f}) {panels.Add(new Rect(h-g,y,g,h*.85f));panels.Add(new Rect(h+w,y,g,h*.85f));}
                if(!tray) panels.Add(new Rect(h+w*.06f,2*h+2*d,w*.88f,g));
            }
            SortedSet<float> xs=new SortedSet<float>(),ys=new SortedSet<float>();foreach(Rect r in panels) {xs.Add(r.xMin);xs.Add(r.xMax);ys.Add(r.yMin);ys.Add(r.yMax);}
            float[] x=new List<float>(xs).ToArray(),yValues=new List<float>(ys).ToArray();
            float blankW=w+2*h,blankH=(tray?d+2*h:2*d+2*h)+(tabs&&!tray?g:0);
            StringBuilder s=new StringBuilder();
            s.AppendFormat(CultureInfo.InvariantCulture,"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{0}mm\" height=\"{1}mm\" viewBox=\"-20 -22 {0} {1}\">",blankW+40,blankH+52);
            s.Append("<rect x=\"-20\" y=\"-22\" width=\"100%\" height=\"100%\" fill=\"white\"/>");
            for(int i=0;i<panels.Count;i++)
            {
                Rect r=panels[i];s.AppendFormat(CultureInfo.InvariantCulture,"<rect x=\"{0}\" y=\"{1}\" width=\"{2}\" height=\"{3}\" fill=\"{4}\"/>",r.x,r.y,r.width,r.height,i>= (tray?5:6)?"#e1f2ed":"#faf4e7");
            }
            for(int i=0;i<x.Length;i++) for(int j=0;j<yValues.Length-1;j++)
            {
                float middle=(yValues[j]+yValues[j+1])*.5f;int a=Owner(panels,x[i]-.001f,middle),b=Owner(panels,x[i]+.001f,middle);
                if(a!=b && (a>=0 || b>=0)) Line(s,x[i],yValues[j],x[i],yValues[j+1],a>=0&&b>=0);
            }
            for(int j=0;j<yValues.Length;j++) for(int i=0;i<x.Length-1;i++)
            {
                float middle=(x[i]+x[i+1])*.5f;int a=Owner(panels,middle,yValues[j]-.001f),b=Owner(panels,middle,yValues[j]+.001f);
                if(a!=b && (a>=0 || b>=0)) Line(s,x[i],yValues[j],x[i+1],yValues[j],a>=0&&b>=0);
            }
            string[] labels={"BASE","LEFT","RIGHT","FRONT","BACK","LID"};
            for(int i=0;i<(tray?5:6);i++) {Rect r=panels[i];s.AppendFormat(CultureInfo.InvariantCulture,"<text x=\"{0}\" y=\"{1}\" text-anchor=\"middle\" font-family=\"sans-serif\" font-size=\"4\" fill=\"#6d685f\">{2}</text>",r.center.x,r.center.y,labels[i]);}
            s.AppendFormat(CultureInfo.InvariantCulture,"<text x=\"0\" y=\"{0}\" font-family=\"sans-serif\" font-size=\"3.4\">PACKFOLD / {1} x {2} x {3} mm / solid: cut / dashed: fold / green: glue area</text>",blankH+10,w,d,h);
            s.AppendFormat(CultureInfo.InvariantCulture,"<text x=\"0\" y=\"{0}\" font-family=\"sans-serif\" font-size=\"3\">Concept template: physical fit, paper thickness and material testing required.</text></svg>",blankH+16);
            return s.ToString();
        }
        private static void Line(StringBuilder s,float x,float y,float u,float v,bool fold)
        {
            s.AppendFormat(CultureInfo.InvariantCulture,"<line x1=\"{0}\" y1=\"{1}\" x2=\"{2}\" y2=\"{3}\" stroke=\"{4}\" stroke-width=\"{5}\" {6}/>",x,y,u,v,fold?"#007b82":"#292727",fold?.2f:.28f,fold?"stroke-dasharray=\"3 1.5\"":"");
        }
        public static string Combine(string body,string cap)
        {
            XmlDocument a=new XmlDocument(),b=new XmlDocument();a.LoadXml(body);b.LoadXml(cap);
            float aw=float.Parse(a.DocumentElement.GetAttribute("width").Replace("mm",""),CultureInfo.InvariantCulture);
            float ah=float.Parse(a.DocumentElement.GetAttribute("height").Replace("mm",""),CultureInfo.InvariantCulture);
            float bw=float.Parse(b.DocumentElement.GetAttribute("width").Replace("mm",""),CultureInfo.InvariantCulture);
            float bh=float.Parse(b.DocumentElement.GetAttribute("height").Replace("mm",""),CultureInfo.InvariantCulture);
            a.DocumentElement.SetAttribute("x","0");a.DocumentElement.SetAttribute("y","10");
            b.DocumentElement.SetAttribute("x",(aw+12).ToString(CultureInfo.InvariantCulture));b.DocumentElement.SetAttribute("y","10");
            a.DocumentElement.SetAttribute("width",aw.ToString(CultureInfo.InvariantCulture));a.DocumentElement.SetAttribute("height",ah.ToString(CultureInfo.InvariantCulture));
            b.DocumentElement.SetAttribute("width",bw.ToString(CultureInfo.InvariantCulture));b.DocumentElement.SetAttribute("height",bh.ToString(CultureInfo.InvariantCulture));
            return string.Format(CultureInfo.InvariantCulture,"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{0}mm\" height=\"{1}mm\" viewBox=\"0 0 {0} {1}\"><title>Lift-off box: separate body and lid nets</title>{2}{3}</svg>",aw+bw+12,Mathf.Max(ah,bh)+10,a.DocumentElement.OuterXml,b.DocumentElement.OuterXml);
        }
    }
}
