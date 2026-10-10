// Adapted from DogSC CursorRenderGeometry, OverlayTimeline and SharedFrameOverlayRenderer.
// MIT License, Copyright (c) 2026 神烦老狗.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using DogSC.Windows;

namespace DogSC.Replica {
 public class CursorSettings {
  public string Asset="automatic",ClickEffect="ripple",ClickColor="#4A90E2";
  public double Size=1.25,IdleDelay=1.2,ClickOpacity=.8,ClickScale=1,Smoothing=.65,TimeOffset=0,TiltStrength=2;
  public bool HideWhenIdle=false,Tilt=false;
 }
 public class OverlayClip {
  public string Id=Guid.NewGuid().ToString("N"),Kind="blur",Name="柔化",ImageData="";
  public int ImageWidth=1,ImageHeight=1;
  public double Start,End=3,X=.35,Y=.39,Width=.30,Height=.22;
  public double Intensity=.55,Dimming=.22,Corner=.08,Rotation,Opacity=1,Border,Shadow=.28,ShadowRadius=24,ShadowX=0,ShadowY=10,BackdropBlur=0;public bool HideScreen;
  public string BorderColor="#FFFFFF",Animation="none",ExitAnimation="auto",Curve="smooth",Transition="none";
  public double Enter=.3,Exit=.3;
  public bool Contains(double time){return time>=Start&&time<End;}
  public static OverlayClip Image(string path,double start,double end){using(var source=System.Drawing.Image.FromFile(path))using(var bitmap=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(bitmap))using(var bytes=new MemoryStream()){g.Clear(Color.Transparent);g.DrawImageUnscaled(source,0,0);bitmap.Save(bytes,ImageFormat.Png);return new OverlayClip {Kind="sticker",Name=Path.GetFileNameWithoutExtension(path),ImageData=Convert.ToBase64String(bytes.ToArray()),ImageWidth=source.Width,ImageHeight=source.Height,Start=start,End=end,X=.5,Y=.5,Width=.38,Corner=18,Animation="pop",Curve="swift",Enter=.7,Exit=.22,BackdropBlur=20};}}
 }
 public class CursorPose {public double X,Y,Angle,Opacity=1;public string Asset="arrow";public bool Visible;}
 static class NativeCursor {
  [StructLayout(LayoutKind.Sequential)]struct Info{public int Size,Flags;public IntPtr Handle;public Native.Point Position;}
  [DllImport("user32.dll")]static extern bool GetCursorInfo(ref Info info);
  public static PointerSample Sample(Rectangle area,double time,bool click){var info=new Info {Size=Marshal.SizeOf(typeof(Info))};if(!GetCursorInfo(ref info))return new PointerSample {Time=time,Visible=false};bool inside=area.Contains(info.Position.X,info.Position.Y);string kind="arrow";var handle=info.Handle;
   if(handle==System.Windows.Forms.Cursors.IBeam.Handle)kind="ibeam";else if(handle==System.Windows.Forms.Cursors.Hand.Handle)kind="hand";else if(handle==System.Windows.Forms.Cursors.Cross.Handle)kind="crosshair";else if(handle==System.Windows.Forms.Cursors.No.Handle)kind="notAllowed";
   return new PointerSample {Time=time,X=EditDocument.Clamp((double)(info.Position.X-area.X)/area.Width,0,1),Y=EditDocument.Clamp((double)(info.Position.Y-area.Y)/area.Height,0,1),Visible=inside&&(info.Flags&1)!=0,Click=inside&&click,CursorKind=kind};
  }
 }
 public partial class MotionEvaluator:IDisposable {
  readonly Dictionary<string,Bitmap> images=new Dictionary<string,Bitmap>();
  public Bitmap StickerImage(OverlayClip clip){Bitmap bitmap;if(images.TryGetValue(clip.Id,out bitmap))return bitmap;try{using(var bytes=new MemoryStream(Convert.FromBase64String(clip.ImageData)))using(var image=Image.FromStream(bytes)){bitmap=new Bitmap(image.Width,image.Height,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(bitmap))g.DrawImageUnscaled(image,0,0);images[clip.Id]=bitmap;return bitmap;}}catch{return null;}}
  public CursorPose CursorAt(double time){var pose=new CursorPose();if(!doc.IndependentCursor||pointers.Count==0)return pose;var c=doc.Cursor;double t=time-c.TimeOffset;if(t<0||t>=doc.Length)return pose;int lo=0,hi=pointers.Count;while(lo<hi){int mid=(lo+hi)/2;if(pointers[mid].Time<=t)lo=mid+1;else hi=mid;}int index=lo-1;if(index<0)return pose;var point=pointers[index];if(!point.Visible)return pose;
   pose.Visible=c.Asset!="hidden";pose.X=point.X;pose.Y=point.Y;pose.Asset=c.Asset=="automatic"?(point.CursorKind??"arrow"):c.Asset;
   // Interpolate only within an uninterrupted pointer segment.
   if(index+1<pointers.Count&&pointers[index+1].Segment==point.Segment&&pointers[index+1].Visible&&pointers[index+1].Time-point.Time>0&&pointers[index+1].Time-point.Time<.1){var next=pointers[index+1];double f=EditDocument.Clamp((t-point.Time)/(next.Time-point.Time),0,1);pose.X+=(next.X-point.X)*f;pose.Y+=(next.Y-point.Y)*f;}
   if(c.Smoothing>.001){double sx=pose.X,sy=pose.Y,weight=1,window=.025+c.Smoothing*.14;for(int i=index;i>=0;i--){var p=pointers[i];double age=t-p.Time;if(age>window*4||!p.Visible||p.Segment!=point.Segment)break;double w=Math.Exp(-age/window);sx+=p.X*w;sy+=p.Y*w;weight+=w;}pose.X=sx/weight;pose.Y=sy/weight;}
   double lastMove=point.Time;if(!point.Click)for(int i=index-1;i>=0;i--){var p=pointers[i];if(p.Segment!=point.Segment||!p.Visible||p.Click||Math.Abs(p.X-point.X)+Math.Abs(p.Y-point.Y)>.001){lastMove=pointers[Math.Min(i+1,index)].Time;break;}lastMove=p.Time;}
   if(c.HideWhenIdle){double idle=t-lastMove;pose.Opacity=1-EditDocument.Clamp((idle-c.IdleDelay)/.15,0,1);if(pose.Opacity<.001)pose.Visible=false;}
   if(c.Tilt&&index>0){var previous=pointers[index-1];double dt=point.Time-previous.Time;if(dt>.001&&dt<.2)pose.Angle=EditDocument.Clamp((point.X-previous.X)/dt*18*c.TiltStrength,-28,28);}
   return pose;
  }
  public IEnumerable<PointerSample> RecentClicks(double time,double duration){double t=time-doc.Cursor.TimeOffset;return pointers.Where(p=>p.Click&&p.Visible&&t>=p.Time&&t-p.Time<duration);}
  public void Dispose(){foreach(var image in images.Values)image.Dispose();images.Clear();}
 }
 static class EffectModels {
  public static void Validate(EditDocument doc){if(doc.Cursor==null)doc.Cursor=new CursorSettings();if(doc.Overlays==null)doc.Overlays=new List<OverlayClip>();var c=doc.Cursor;
   double[] values={c.Size,c.IdleDelay,c.ClickOpacity,c.ClickScale,c.Smoothing,c.TimeOffset,c.TiltStrength};if(values.Any(v=>!EditDocument.Finite(v)))throw new Exception("光标设置无效");
   c.Size=EditDocument.Clamp(c.Size,.25,6);c.IdleDelay=EditDocument.Clamp(c.IdleDelay,.2,8);c.ClickOpacity=EditDocument.Clamp(c.ClickOpacity,.05,1);c.ClickScale=EditDocument.Clamp(c.ClickScale,.4,3);c.Smoothing=EditDocument.Clamp(c.Smoothing,0,1);c.TimeOffset=EditDocument.Clamp(c.TimeOffset,-2,2);c.TiltStrength=EditDocument.Clamp(c.TiltStrength,2,4);
   var ids=new HashSet<string>();foreach(var o in doc.Overlays){if(o==null||string.IsNullOrEmpty(o.Id)||!ids.Add(o.Id))throw new Exception("效果片段标识无效");double[] numbers={o.Start,o.End,o.X,o.Y,o.Width,o.Height,o.Intensity,o.Dimming,o.Corner,o.Rotation,o.Opacity,o.Border,o.Shadow,o.ShadowRadius,o.ShadowX,o.ShadowY,o.Enter,o.Exit,o.BackdropBlur};if(numbers.Any(v=>!EditDocument.Finite(v))||o.Start<0||o.End<=o.Start||o.End>doc.Length+.01)throw new Exception("效果片段范围或参数无效");
    if(o.Kind!="sticker"&&o.Kind!="blur"&&o.Kind!="spotlight")throw new Exception("未知效果类型");o.Width=EditDocument.Clamp(o.Width,o.Kind=="sticker"?.02:.01,o.Kind=="sticker"?1.5:1);o.Height=EditDocument.Clamp(o.Height,.01,1);o.X=EditDocument.Clamp(o.X,0,o.Kind=="sticker"?1:1-o.Width);o.Y=EditDocument.Clamp(o.Y,0,o.Kind=="sticker"?1:1-o.Height);o.Opacity=EditDocument.Clamp(o.Opacity,0,1);o.Intensity=EditDocument.Clamp(o.Intensity,0,1);o.Dimming=EditDocument.Clamp(o.Dimming,0,.75);o.Corner=EditDocument.Clamp(o.Corner,0,o.Kind=="sticker"?160:.5);o.Border=EditDocument.Clamp(o.Border,0,40);o.Shadow=EditDocument.Clamp(o.Shadow,0,1);o.ShadowRadius=EditDocument.Clamp(o.ShadowRadius,0,80);o.BackdropBlur=EditDocument.Clamp(o.BackdropBlur,0,60);o.Enter=EditDocument.Clamp(o.Enter,0,2);o.Exit=EditDocument.Clamp(o.Exit,0,2);if(o.Kind=="sticker"&&(o.ImageWidth<1||o.ImageHeight<1||string.IsNullOrEmpty(o.ImageData)))throw new Exception("贴图缺少图片");
   }
  }
 }
 static class EffectRenderer {
  public static double Progress(OverlayClip o,double time,bool entry,string curve){double duration=entry?o.Enter:o.Exit;double sum=o.Enter+o.Exit;if(sum>o.End-o.Start)duration*=((o.End-o.Start)/sum);double f=duration<=.0001?1:EditDocument.Clamp((entry?time-o.Start:o.End-time)/duration,0,1);switch(curve){case "none":return 1;case "linear":return f;case "swift":return 1-Math.Pow(1-f,4);case "gentle":return 1-Math.Pow(1-f,3);case "spring":return MotionEvaluator.Ease(f,"spring");default:return f*f*f*(f*(f*6-15)+10);}}
  public static double StickerVisibility(OverlayClip o,double time){return Math.Min(o.Animation=="none"?1:Progress(o,time,true,o.Curve),(o.ExitAnimation=="none"||(o.ExitAnimation=="auto"&&o.Animation=="none"))?1:Progress(o,time,false,o.Curve));}
  public static double Suppression(EditDocument doc,double time){return doc.Overlays.Where(o=>o.Kind=="sticker"&&o.HideScreen&&o.Contains(time)).Select(o=>StickerVisibility(o,time)).DefaultIfEmpty(0).Max();}
  public static void Backdrop(Graphics g,Bitmap canvas,EditDocument doc,double time,int width,int height){if(canvas==null)return;double suppression=Suppression(doc,time),amount=doc.Overlays.Where(o=>o.Kind=="sticker"&&o.Contains(time)&&!o.HideScreen).Select(o=>o.BackdropBlur*StickerVisibility(o,time)).DefaultIfEmpty(0).Max()*(1-suppression);if(amount<.01)return;g.Flush();using(var softened=canvas.Clone(new Rectangle(0,0,width,height),PixelFormat.Format32bppArgb)){Gaussian(softened,amount*Math.Min(width,height)/720);g.DrawImageUnscaled(softened,0,0);}}
  public static double Envelope(OverlayClip o,double time){return Math.Min(Progress(o,time,true,o.Transition),Progress(o,time,false,o.Transition));}
  public static RectangleF SourceRect(OverlayClip o,SceneLayout layout,Pose pose){return new RectangleF((float)(layout.Video.X+(o.X-pose.X+.5/pose.Zoom)*layout.Video.Width*pose.Zoom),(float)(layout.Video.Y+(o.Y-pose.Y+.5/pose.Zoom)*layout.Video.Height*pose.Zoom),(float)(o.Width*layout.Video.Width*pose.Zoom),(float)(o.Height*layout.Video.Height*pose.Zoom));}
  public static Bitmap ApplyBlur(Bitmap source,EditDocument doc,double time){var active=doc.Overlays.Where(o=>o.Kind=="blur"&&o.Contains(time)&&Envelope(o,time)>.001).ToList();if(active.Count==0)return null;var result=source.Clone(new Rectangle(0,0,source.Width,source.Height),PixelFormat.Format32bppArgb);
   foreach(var o in active){var rect=new RectangleF((float)(o.X*source.Width),(float)(o.Y*source.Height),(float)(o.Width*source.Width),(float)(o.Height*source.Height));double radius=(4+o.Intensity*44)*Envelope(o,time)*source.Width/doc.Width;var sample=Rectangle.Intersect(Rectangle.FromLTRB((int)Math.Floor(rect.Left-radius*3),(int)Math.Floor(rect.Top-radius*3),(int)Math.Ceiling(rect.Right+radius*3),(int)Math.Ceiling(rect.Bottom+radius*3)),new Rectangle(0,0,source.Width,source.Height));if(sample.Width<1||sample.Height<1)continue;
    using(var tile=result.Clone(sample,PixelFormat.Format32bppArgb)){Gaussian(tile,radius);using(var g=Graphics.FromImage(result))using(var mask=StudioRenderer.Round(rect,(float)(Math.Min(rect.Width,rect.Height)*o.Corner))){g.SetClip(mask);g.DrawImageUnscaled(tile,sample.X,sample.Y);}}
   }return result;
  }
  // Three sliding box passes approximate the original local Gaussian, with bounded work per pixel.
  public static void Gaussian(Bitmap image,double sigma){if(sigma<.2)return;int w=image.Width,h=image.Height;var data=image.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);try{var a=new byte[w*h*4];for(int y=0;y<h;y++)Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),a,y*w*4,w*4);var b=new byte[a.Length];int radius=Math.Max(1,(int)Math.Round((Math.Sqrt(4*sigma*sigma+1)-1)/2));radius=Math.Min(radius,Math.Max(w,h));for(int pass=0;pass<3;pass++){Box(a,b,w,h,radius,true);Box(b,a,w,h,radius,false);}for(int y=0;y<h;y++)Marshal.Copy(a,y*w*4,IntPtr.Add(data.Scan0,y*data.Stride),w*4);}finally{image.UnlockBits(data);}}
  static void Box(byte[] source,byte[] output,int w,int h,int r,bool horizontal){int length=horizontal?w:h,lines=horizontal?h:w;int diameter=r*2+1;for(int line=0;line<lines;line++){var sum=new int[4];Func<int,int> offset=p=>horizontal?(line*w+p)*4:(p*w+line)*4;for(int p=-r;p<=r;p++){int at=offset(Math.Max(0,Math.Min(length-1,p)));for(int c=0;c<4;c++)sum[c]+=source[at+c];}for(int p=0;p<length;p++){int at=offset(p);for(int c=0;c<4;c++)output[at+c]=(byte)(sum[c]/diameter);int remove=offset(Math.Max(0,p-r)),add=offset(Math.Min(length-1,p+r+1));for(int c=0;c<4;c++)sum[c]+=source[add+c]-source[remove+c];}}}
  static void ClickCircle(Graphics g,PointF point,double diameter,double line,double opacity,bool filled,double glow,Color color){if(diameter<.01||opacity<.001)return;float r=(float)(diameter/2);int alpha=(int)(255*EditDocument.Clamp(opacity,0,1));if(glow>.1||filled){float spread=(float)(r+glow);using(var path=new GraphicsPath()){path.AddEllipse(point.X-spread,point.Y-spread,spread*2,spread*2);using(var gradient=new PathGradientBrush(path)){gradient.CenterColor=Color.FromArgb(filled?alpha:alpha/4,color);gradient.SurroundColors=new[]{Color.FromArgb(0,color)};g.FillPath(gradient,path);}}}if(!filled)using(var pen=new Pen(Color.FromArgb(alpha,color),(float)Math.Max(1,line)))g.DrawEllipse(pen,point.X-r,point.Y-r,r*2,r*2);}
  static void ClickGeometry(Graphics g,PointF point,double p,string style,double h,double opacity,Color color){double diameter=0,line=0,alpha=0,glow=0,secondary=0,secondaryAlpha=0,secondaryLine=0;bool filled=false,secondaryFilled=true;
   switch(style){case "ripple":diameter=h*(.45+1.45*(1-Math.Pow(1-p,3)));line=Math.Max(h*(.075-.04*p),1.2);alpha=opacity*.95*(1-Math.Pow(p,1.3));glow=h*.1*(1-p);if(p<.35){secondary=h*.28*(1-p/.35);secondaryAlpha=opacity*.85*(1-p/.35);}break;
    case "glow":if(p<.18){double bloom=p/.18;diameter=h*(.75+.5*(1-Math.Pow(1-bloom,2)));alpha=opacity*.78*bloom;}else{double fade=(p-.18)/.82;diameter=h*(1.25+.55*fade);alpha=opacity*.78*(1-Math.Pow(fade,1.4));}glow=h*.3;filled=true;secondary=diameter*.5;secondaryAlpha=alpha*.75;break;
    case "pulse":diameter=h*(.5+1.25*(1-Math.Pow(1-p,2.5)));line=Math.Max(h*.065,1.2);alpha=opacity*.9*(1-p);glow=h*.1*(1-p);if(p<=.55){double inner=p/.55;secondary=h*(.25+.45*Math.Sin(inner*Math.PI));secondaryAlpha=opacity*.55*(1-inner);}break;
    case "burst":diameter=Math.Max(h*(.35-.18*p),1);alpha=opacity*.95*(1-p*p);glow=h*.14*(1-p);filled=true;secondary=h*(.45+.55*p);secondaryLine=Math.Max(h*.035,1);secondaryAlpha=opacity*.55*(1-p);secondaryFilled=false;double offset=h*(.28+.38*(1-Math.Pow(1-p,2))),size=Math.Max(h*.075*(1-p),1.2);using(var pen=new Pen(Color.FromArgb((int)(255*opacity*.85*(1-p)),color),(float)size)){for(int i=0;i<4;i++){double angle=i*Math.PI/2;g.DrawLine(pen,point.X+(float)(Math.Cos(angle)*offset),point.Y+(float)(Math.Sin(angle)*offset),point.X+(float)(Math.Cos(angle)*(offset+size)),point.Y+(float)(Math.Sin(angle)*(offset+size)));}}break;
   }ClickCircle(g,point,secondary,secondaryLine,secondaryAlpha,secondaryFilled,h*.08*(1-p),color);ClickCircle(g,point,diameter,line,alpha,filled,glow,color);
  }
  public static void Cursor(Graphics g,EditDocument doc,MotionEvaluator motion,double time,SceneLayout layout,Pose camera,int width,int height){if(!doc.IndependentCursor||doc.Cursor.Asset=="hidden")return;var c=doc.Cursor;var p=motion.CursorAt(time);Func<double,double,PointF> map=(x,y)=>new PointF((float)(layout.Video.X+(x-camera.X+.5/camera.Zoom)*layout.Video.Width*camera.Zoom),(float)(layout.Video.Y+(y-camera.Y+.5/camera.Zoom)*layout.Video.Height*camera.Zoom));var state=g.Save();g.SetClip(layout.Video,CombineMode.Intersect);double duration=c.ClickEffect=="glow"?.32:c.ClickEffect=="pulse"?.38:c.ClickEffect=="burst"?.22:.36;
   if(c.ClickEffect!="none")foreach(var click in motion.RecentClicks(time,duration)){double progress=(time-c.TimeOffset-click.Time)/duration;var point=map(click.X,click.Y);ClickGeometry(g,point,progress,c.ClickEffect,44*c.Size*Math.Min(width,height)/1080.0*c.ClickScale,c.ClickOpacity*camera.Opacity,StudioRenderer.ColorOf(c.ClickColor));}
   if(p.Visible){var point=map(p.X,p.Y);float size=(float)(44*c.Size*Math.Min(width,height)/1080.0);g.TranslateTransform(point.X,point.Y);g.RotateTransform((float)p.Angle);g.ScaleTransform(size/27,size/27);int alpha=(int)(255*p.Opacity*camera.Opacity);using(var fill=new SolidBrush(Color.FromArgb(alpha,20,22,25)))using(var stroke=new Pen(Color.FromArgb(alpha,Color.White),1.5f)){stroke.LineJoin=LineJoin.Round;
    if(p.Asset=="dot"){g.FillEllipse(fill,-7,-7,14,14);g.DrawEllipse(stroke,-7,-7,14,14);}
    else if(p.Asset=="crosshair"){using(var pen=new Pen(Color.FromArgb(alpha,Color.Black),2)){g.DrawLine(stroke,-12,0,12,0);g.DrawLine(stroke,0,-12,0,12);g.DrawLine(pen,-11,0,11,0);g.DrawLine(pen,0,-11,0,11);}}
    else if(p.Asset=="ibeam"){using(var pen=new Pen(Color.FromArgb(alpha,Color.Black),2)){g.DrawLine(stroke,0,-11,0,11);g.DrawLine(pen,0,-10,0,10);g.DrawLine(pen,-4,-10,4,-10);g.DrawLine(pen,-4,10,4,10);}}
    else if(p.Asset=="notAllowed"){g.FillEllipse(fill,-9,-9,18,18);g.DrawEllipse(stroke,-9,-9,18,18);g.DrawLine(stroke,-6,6,6,-6);}
    else if(p.Asset=="hand"||p.Asset=="openHand"||p.Asset=="closedHand"){using(var path=new GraphicsPath()){path.AddPolygon(new[]{new PointF(-3,0),new PointF(-3,-9),new PointF(1,-11),new PointF(3,-9),new PointF(3,0),new PointF(8,-2),new PointF(14,1),new PointF(13,12),new PointF(8,17),new PointF(0,16),new PointF(-8,5),new PointF(-7,2),new PointF(-3,4)});if(p.Asset!="hand"){g.TranslateTransform(0,-4);g.ScaleTransform(1,p.Asset=="closedHand"?.75f:1.0f);}g.FillPath(fill,path);g.DrawPath(stroke,path);}}
    else{using(var path=new GraphicsPath()){path.AddPolygon(new[]{new PointF(0,0),new PointF(0,22),new PointF(6,17),new PointF(11,27),new PointF(15,25),new PointF(10,15),new PointF(19,15)});g.FillPath(fill,path);g.DrawPath(stroke,path);}}
   }}g.Restore(state);
  }
  public static void Spotlights(Graphics g,EditDocument doc,double time,SceneLayout layout,Pose pose,int width,int height){foreach(var o in doc.Overlays.Where(o=>o.Kind=="spotlight"&&o.Contains(time))){double progress=Envelope(o,time);if(progress<.001)continue;var rect=SourceRect(o,layout,pose);using(var path=new GraphicsPath(FillMode.Alternate))using(var hole=StudioRenderer.Round(rect,(float)(Math.Min(rect.Width,rect.Height)*o.Corner)))using(var brush=new SolidBrush(Color.FromArgb((int)(255*o.Dimming*progress),0,0,0))){path.AddRectangle(new RectangleF(0,0,width,height));path.AddPath(hole,false);g.FillPath(brush,path);}}}
  static string ExitFor(string entry){switch(entry){case "slideLeft":return "slideRight";case "slideRight":return "slideLeft";case "slideUp":return "slideDown";case "slideDown":return "slideUp";case "slideTopLeft":return "slideBottomRight";case "slideTopRight":return "slideBottomLeft";case "slideBottomLeft":return "slideTopRight";case "slideBottomRight":return "slideTopLeft";default:return entry;}}
  public static void Stickers(Graphics g,EditDocument doc,MotionEvaluator motion,double time,int width,int height){foreach(var o in doc.Overlays.Where(o=>o.Kind=="sticker"&&o.Contains(time))){var image=motion.StickerImage(o);if(image==null)continue;double opacity=o.Opacity,scale=1,x=o.X,y=o.Y;string exit=o.ExitAnimation=="auto"?ExitFor(o.Animation):o.ExitAnimation;string animation="none";double progress=1;
    double enter=o.Enter,leave=o.Exit,sum=enter+leave;if(sum>o.End-o.Start){enter*=(o.End-o.Start)/sum;leave*=(o.End-o.Start)/sum;}if(time<o.Start+enter){animation=o.Animation;progress=Progress(o,time,true,o.Curve);}else if(time>o.End-leave){animation=exit;progress=Progress(o,time,false,o.Curve);}
    if(animation!="none")opacity*=progress;if(animation=="pop")scale=.75+.25*progress;double distance=1-progress;
    if(animation.Contains("Left"))x-=distance;if(animation.Contains("Right"))x+=distance;if(animation=="slideUp"||animation.Contains("Top"))y-=distance;if(animation=="slideDown"||animation.Contains("Bottom"))y+=distance;if(opacity<.001)continue;
    float w=(float)(width*o.Width*scale),h=w*image.Height/image.Width,unit=Math.Min(width,height)/720f;var state=g.Save();g.TranslateTransform((float)(width*x),(float)(height*y));g.RotateTransform((float)o.Rotation);var rect=new RectangleF(-w/2,-h/2,w,h);float corner=(float)(o.Corner*unit);
    if(o.Shadow>0)for(int i=8;i>=1;i--){var shadow=rect;float spread=(float)o.ShadowRadius*unit*i/8;shadow.Inflate(spread,spread);shadow.Offset((float)o.ShadowX*unit,(float)o.ShadowY*unit);using(var path=StudioRenderer.Round(shadow,corner+spread))using(var brush=new SolidBrush(Color.FromArgb((int)(255*o.Shadow*opacity/14),0,0,0)))g.FillPath(brush,path);}
    using(var mask=StudioRenderer.Round(rect,corner)){var clip=g.Save();g.SetClip(mask,CombineMode.Intersect);using(var attrs=new ImageAttributes()){var matrix=new ColorMatrix();matrix.Matrix33=(float)opacity;attrs.SetColorMatrix(matrix);g.DrawImage(image,Rectangle.Round(rect),0,0,image.Width,image.Height,GraphicsUnit.Pixel,attrs);}g.Restore(clip);if(o.Border>0)using(var pen=new Pen(Color.FromArgb((int)(255*opacity),StudioRenderer.ColorOf(o.BorderColor)),(float)(o.Border*unit)))g.DrawPath(pen,mask);}g.Restore(state);
   }}
 }
}
