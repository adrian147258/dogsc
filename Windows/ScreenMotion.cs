// Adapted from RecorderCore/ScreenMotion.swift and ScreenProjection.swift.
// MIT License, Copyright (c) 2026 神烦老狗.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;

namespace DogSC.Replica {
 public class ScreenMotionState {
  public double X=.5,Y=.5,Scale=1,RotationX,RotationY,RotationZ,Perspective=.65;
  public ScreenMotionState Copy(){return (ScreenMotionState)MemberwiseClone();}
 }
 public class ScreenMotionClip {
  public string Id=Guid.NewGuid().ToString("N");public double Start,End=3,Transition=.7,Return=.7;public string Easing="spring";
  public double? AnimationStart,AnimationEnd;
  public ScreenMotionState Target=new ScreenMotionState {Scale=.85,RotationX=12,RotationY=-18};
 }
 static class ScreenMotionModels {
  static void State(ScreenMotionState s){if(s==null||new[]{s.X,s.Y,s.Scale,s.RotationX,s.RotationY,s.RotationZ,s.Perspective}.Any(v=>!EditDocument.Finite(v)))throw new Exception("屏幕 3D 参数无效。");
   s.X=EditDocument.Clamp(s.X,0,1);s.Y=EditDocument.Clamp(s.Y,0,1);s.Scale=EditDocument.Clamp(s.Scale,.25,4);s.RotationX=EditDocument.Clamp(s.RotationX,-60,60);s.RotationY=EditDocument.Clamp(s.RotationY,-60,60);s.RotationZ=EditDocument.Clamp(s.RotationZ,-30,30);s.Perspective=EditDocument.Clamp(s.Perspective,0,2);
  }
  public static void Validate(EditDocument d){if(d.ScreenMotions==null)d.ScreenMotions=new List<ScreenMotionClip>();if(d.ScreenBase!=null)State(d.ScreenBase);double end=0;var ids=new HashSet<string>();
   foreach(var c in d.ScreenMotions.OrderBy(c=>c==null?0:c.Start)){if(c==null||string.IsNullOrEmpty(c.Id)||!ids.Add(c.Id)||new[]{c.Start,c.End,c.Transition,c.Return}.Any(v=>!EditDocument.Finite(v))||c.Start<end-.0001||c.Start<0||c.End<=c.Start||c.End>d.Length+.01||c.Transition<0||c.Return<0||c.Transition>5||c.Return>5)throw new Exception("屏幕 3D 片段重叠或时间范围无效。");if(c.AnimationStart.HasValue&&(!EditDocument.Finite(c.AnimationStart.Value)||c.AnimationStart.Value<0||c.AnimationStart.Value>c.Start)||c.AnimationEnd.HasValue&&(!EditDocument.Finite(c.AnimationEnd.Value)||c.AnimationEnd.Value<c.End))throw new Exception("屏幕 3D 动画范围无效。");State(c.Target);end=c.End;}
  }
  public static ScreenMotionState Base(EditDocument d){return d.ScreenBase??new ScreenMotionState {X=d.Style.X,Y=d.Style.Y};}
  public static ScreenMotionState At(EditDocument d,double time){var b=Base(d);var c=(d.ScreenMotions??new List<ScreenMotionClip>()).FirstOrDefault(m=>time>=m.Start&&time<m.End);if(c==null)return b;
   double start=c.AnimationStart??c.Start,end=c.AnimationEnd??c.End,length=end-start,enter=Math.Min(c.Transition,length/2),exit=Math.Min(c.Return,length/2),weight=1;
   if(enter>0&&time<start+enter)weight=MotionEvaluator.Ease((time-start)/enter,c.Easing);
   if(exit>0&&time>end-exit)weight=1-MotionEvaluator.Ease((time-end+exit)/exit,c.Easing);
   return new ScreenMotionState {X=b.X+(c.Target.X-b.X)*weight,Y=b.Y+(c.Target.Y-b.Y)*weight,Scale=b.Scale+(c.Target.Scale-b.Scale)*weight,RotationX=b.RotationX+(c.Target.RotationX-b.RotationX)*weight,RotationY=b.RotationY+(c.Target.RotationY-b.RotationY)*weight,RotationZ=b.RotationZ+(c.Target.RotationZ-b.RotationZ)*weight,Perspective=b.Perspective+(c.Target.Perspective-b.Perspective)*weight};
  }
 }
 // One perspective pass over a transparent screen card. Background and stickers stay in canvas space.
 public sealed class ScreenProjection {
  readonly double[] matrix,inverse;
  static double[] Invert(double[] m){double a=m[4]*m[8]-m[5]*m[7],b=m[5]*m[6]-m[3]*m[8],c=m[3]*m[7]-m[4]*m[6],det=m[0]*a+m[1]*b+m[2]*c;if(Math.Abs(det)<1e-12)throw new Exception("屏幕 3D 投影不可逆。");return new[]{a/det,(m[2]*m[7]-m[1]*m[8])/det,(m[1]*m[5]-m[2]*m[4])/det,b/det,(m[0]*m[8]-m[2]*m[6])/det,(m[2]*m[3]-m[0]*m[5])/det,c/det,(m[1]*m[6]-m[0]*m[7])/det,(m[0]*m[4]-m[1]*m[3])/det};}
  public ScreenProjection(RectangleF frame,ScreenMotionState s,int width,int height){double rx=s.RotationX*Math.PI/180,ry=s.RotationY*Math.PI/180,rz=s.RotationZ*Math.PI/180;
   double sx=Math.Sin(rx),cx=Math.Cos(rx),sy=Math.Sin(ry),cy=Math.Cos(ry),sz=Math.Sin(rz),cz=Math.Cos(rz),scale=s.Scale;
   double px=frame.X+frame.Width*s.X,py=frame.Y+frame.Height*s.Y,u=width*s.X,v=height*s.Y,distance=s.Perspective>0?Math.Max(frame.Width,frame.Height)*scale*2/s.Perspective:double.PositiveInfinity;
   Func<double,double,PointF> point=(x,y)=>{x=(x-px)*scale;y=(y-py)*scale;double xx=x*cy+y*sx*sy,yy=y*cx,z=-x*sy+y*sx*cy,f=double.IsInfinity(distance)?1:distance/Math.Max(distance-z,distance*.12);return new PointF((float)(u+(xx*cz-yy*sz)*f),(float)(v+(xx*sz+yy*cz)*f));};
   var tl=point(frame.X,frame.Y);var tr=point(frame.Right,frame.Y);var br=point(frame.Right,frame.Bottom);var bl=point(frame.X,frame.Bottom);
   double dx1=tr.X-br.X,dx2=bl.X-br.X,dx3=tl.X-tr.X+br.X-bl.X,dy1=tr.Y-br.Y,dy2=bl.Y-br.Y,dy3=tl.Y-tr.Y+br.Y-bl.Y,g=0,h=0;
   if(Math.Abs(dx3)+Math.Abs(dy3)>.00001){double denominator=dx1*dy2-dx2*dy1;if(Math.Abs(denominator)<1e-10)throw new Exception("屏幕 3D 投影不可逆。");g=(dx3*dy2-dx2*dy3)/denominator;h=(dx1*dy3-dx3*dy1)/denominator;}
   double a=(tr.X-tl.X+g*tr.X)/frame.Width,b=(bl.X-tl.X+h*bl.X)/frame.Height,d=(tr.Y-tl.Y+g*tr.Y)/frame.Width,e=(bl.Y-tl.Y+h*bl.Y)/frame.Height,gg=g/frame.Width,hh=h/frame.Height;
   matrix=new[]{a,b,tl.X-a*frame.X-b*frame.Y,d,e,tl.Y-d*frame.X-e*frame.Y,gg,hh,1-gg*frame.X-hh*frame.Y};inverse=Invert(matrix);
  }
  static PointF Map(double[] m,double x,double y){double z=m[6]*x+m[7]*y+m[8];return new PointF((float)((m[0]*x+m[1]*y+m[2])/z),(float)((m[3]*x+m[4]*y+m[5])/z));}
  public PointF Project(double x,double y){return Map(matrix,x,y);}
  public PointF Unproject(double x,double y){return Map(inverse,x,y);}
  public bool Identity(RectangleF frame){var p=Project(frame.X,frame.Y);var q=Project(frame.Right,frame.Bottom);var r=Project(frame.Right,frame.Y);return Math.Abs(p.X-frame.X)+Math.Abs(p.Y-frame.Y)+Math.Abs(q.X-frame.Right)+Math.Abs(q.Y-frame.Bottom)+Math.Abs(r.X-frame.Right)+Math.Abs(r.Y-frame.Y)<.001;}
  public Bitmap Warp(Bitmap source){return Warp(source,new Rectangle(0,0,source.Width,source.Height),source.Width,source.Height);}
  public unsafe Bitmap Warp(Bitmap source,Rectangle bounds,int w,int h){int sw=source.Width,sh=source.Height;var result=new Bitmap(w,h,PixelFormat.Format32bppPArgb);var src=source.LockBits(new Rectangle(0,0,sw,sh),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);var dst=result.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
   try{IntPtr read=src.Scan0,write=dst.Scan0;int ss=src.Stride,ds=dst.Stride;var m=inverse;
    Parallel.For(0,h,new ParallelOptions {MaxDegreeOfParallelism=4},y=>{byte* row=(byte*)write+y*ds;double xx=m[1]*(y+.5)+m[2]+m[0]*.5,yy=m[4]*(y+.5)+m[5]+m[3]*.5,zz=m[7]*(y+.5)+m[8]+m[6]*.5;
     for(int x=0;x<w;x++,xx+=m[0],yy+=m[3],zz+=m[6]){double u=xx/zz-.5-bounds.X,v=yy/zz-.5-bounds.Y;if(!EditDocument.Finite(u)||!EditDocument.Finite(v)||u<0||v<0||u>=sw-1||v>=sh-1)continue;int ix=(int)u,iy=(int)v;double fx=u-ix,fy=v-iy,aa=(1-fx)*(1-fy),bb=fx*(1-fy),cc=(1-fx)*fy,dd=fx*fy;byte* p=(byte*)read+iy*ss+ix*4;byte* q=p+ss;byte* target=row+x*4;for(int channel=0;channel<4;channel++)target[channel]=(byte)(p[channel]*aa+p[channel+4]*bb+q[channel]*cc+q[channel+4]*dd+.5);}
    });
   }catch{result.UnlockBits(dst);source.UnlockBits(src);result.Dispose();throw;}result.UnlockBits(dst);source.UnlockBits(src);return result;
  }
 }
}
