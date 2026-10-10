using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using DogSC.Windows;

namespace DogSC.Replica {
 public class Cut { public double Start; public double End; public double Speed=1; public double Length { get { return (End-Start)/Speed; } } }
 public class Zoom { public double Start; public double End; public double Scale=1.6; public double X=.5; public double Y=.5; public bool Automatic; public double Transition=.7; public string Easing="spring"; }
 public class Appearance {
  public string Ratio="16:9"; public string Background="dots"; public string ImagePath="";
  public string ColorA="#F5F6F7"; public string ColorB="#F5F6F7";
  public double Padding=70; public double Radius=16; public double Shadow=25; public double Border=0;
  public double Scale=1; public double X=.5; public double Y=.5; public string Frame="window";
  public bool AutoZoom=true; public bool Follow=true; public double ZoomScale=1.6;
  public double Volume=1; public double Opening=0;
 }
 public class EditDocument {
  public string Format="dogsc-windows-editor"; public int Version=2; public string Name="未命名项目";
  public string Source; public int Width; public int Height; public double Duration; public bool Audio;public double FrameRate=30;
  public Appearance Style=new Appearance(); public List<Cut> Cuts=new List<Cut>();public bool IndependentCursor=false;public CursorSettings Cursor=new CursorSettings();public List<OverlayClip> Overlays=new List<OverlayClip>();
  public List<PointerSample> Pointer=new List<PointerSample>(); public List<Zoom> Zooms=new List<Zoom>();
  public double Length { get { return Cuts.Sum(c=>c.Length); } }
  public static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength=int.MaxValue }; }
  public string Serialize() { return Serializer().Serialize(this); }
  public static EditDocument Deserialize(string text) { return Serializer().Deserialize<EditDocument>(text); }
  public void Save(string path) {
   string pending=path+".tmp"; File.WriteAllText(pending,Serialize(),new UTF8Encoding(false));
   if(File.Exists(path)) File.Replace(pending,path,null); else File.Move(pending,path);
  }
  public void Validate() {
   if(Format!="dogsc-windows-editor"||Version!=2||!File.Exists(Source)||Width<2||Height<2||!Finite(Duration)||Duration<=0||Style==null||Cuts==null||Cuts.Count==0||Pointer==null||Zooms==null) throw new Exception("项目不完整、版本不支持，或找不到视频素材。");
   if(!Finite(FrameRate)||FrameRate<=0||FrameRate>240)FrameRate=30;
   if(Cuts.Any(c=>!Finite(c.Start)||!Finite(c.End)||!Finite(c.Speed)||c.Start<0||c.End>Duration+.1||c.End<=c.Start||c.Speed<.5||c.Speed>2)) throw new Exception("项目的剪辑范围无效。");
   if(Pointer.Any(p=>!Finite(p.Time)||!Finite(p.X)||!Finite(p.Y)))throw new Exception("鼠标轨迹无效。");
   if(Zooms.Any(z=>!Finite(z.Start)||!Finite(z.End)||!Finite(z.Scale)||!Finite(z.X)||!Finite(z.Y)||!Finite(z.Transition)||z.End<=z.Start||z.Start<0||z.Scale<1||z.Scale>3||z.Transition<.05||z.Transition>5))throw new Exception("运镜参数无效。");
   double[] values={Style.Padding,Style.Radius,Style.Shadow,Style.Border,Style.Scale,Style.X,Style.Y,Style.Volume,Style.ZoomScale,Style.Opening};
   if(values.Any(v=>!Finite(v)))throw new Exception("画面参数无效。");
   Style.Padding=Clamp(Style.Padding,0,180);Style.Radius=Clamp(Style.Radius,0,80);Style.Scale=Clamp(Style.Scale,.5,1.5);Style.X=Clamp(Style.X,0,1);Style.Y=Clamp(Style.Y,0,1);Style.Volume=Clamp(Style.Volume,0,1);Style.ZoomScale=Clamp(Style.ZoomScale,1,3);
   EffectModels.Validate(this);
  }
  public static bool Finite(double n) {return !double.IsNaN(n)&&!double.IsInfinity(n);}
  public static double Clamp(double n,double lo,double hi) {return Math.Max(lo,Math.Min(hi,n));}
  public double SourceTime(double time,out int index) { double offset=0;index=0;foreach(var c in Cuts){if(time<offset+c.Length||index==Cuts.Count-1)return c.Start+Clamp(time-offset,0,c.Length)*c.Speed;offset+=c.Length;index++;}return 0; }
  public List<PointerSample> TimelinePointer() {
   var result=new List<PointerSample>(); double offset=0;
   int segment=0;foreach(var cut in Cuts){var prior=Pointer.LastOrDefault(p=>p.Time<=cut.Start);if(prior!=null)result.Add(new PointerSample {Time=offset,X=prior.X,Y=prior.Y,Visible=prior.Visible,CursorKind=prior.CursorKind,Segment=segment});foreach(var p in Pointer.Where(p=>p.Time>=cut.Start&&p.Time<cut.End)) result.Add(new PointerSample {Time=offset+(p.Time-cut.Start)/cut.Speed,X=p.X,Y=p.Y,Click=p.Click,Visible=p.Visible,CursorKind=p.CursorKind,Segment=segment});offset+=cut.Length;segment++;}
   return result.OrderBy(p=>p.Time).ToList();
  }
  public void GenerateZooms() {
   Zooms.RemoveAll(z=>z.Automatic);var events=TimelinePointer().Where(p=>p.Visible).ToList();var clicks=events.Where(p=>p.Click).ToList();int index=0;
   // Port of the current planner's temporal grouping, lead time, hold extension,
   // and connected envelopes (RecorderCore/AutoZoomPlanner.swift).
   while(index<clicks.Count) {
    var first=clicks[index];var last=first;index++;
    while(index<clicks.Count&&clicks[index].Time-last.Time<=1.4)last=clicks[index++];
    double start=Math.Max(0,first.Time-.7*.18),hold=Math.Max(last.Time+.9,start+.7);
    double activity=last.Time;PointerSample previous=last;
    foreach(var p in events.Where(p=>p.Time>last.Time)) {
     if(p.Time-activity>1.4)break;
     if(p.Click||Math.Abs(p.X-previous.X)+Math.Abs(p.Y-previous.Y)>.001){activity=p.Time;previous=p;}
    }
    hold=Math.Max(hold,activity+.55);if(index<clicks.Count)hold=Math.Min(hold,Math.Max(start+.1,clicks[index].Time-.126));
    var z=new Zoom {Start=start,End=Math.Min(Length,hold+.7),X=Clamp(first.X,.12,.88),Y=Clamp(first.Y,.12,.88),Automatic=true,Scale=Style.ZoomScale};
    if(z.End-z.Start>.02)Zooms.Add(z);
   }
   Zooms=Zooms.OrderBy(z=>z.Start).ToList();
  }
 }
 public class Pose { public double Zoom=1,X=.5,Y=.5; public double Opacity=1; public double OpeningScale=1; }
 public partial class MotionEvaluator {
  readonly EditDocument doc; readonly List<PointerSample> pointers;
  public MotionEvaluator(EditDocument d){doc=d;pointers=d.TimelinePointer();}
  public static double Ease(double t,string easing){t=EditDocument.Clamp(t,0,1);if(t==0||t==1)return t;if(easing=="linear")return t;if(easing=="smooth")return t*t*(3-2*t);
   // RecorderCore/ZoomAnimation.swift springProgress, original MotionStyle defaults.
   const double mass=2.4,stiffness=210,damping=42;double omega=Math.Sqrt(stiffness/mass),zeta=Math.Min(damping/(2*Math.Sqrt(stiffness*mass)),.9999),wd=omega*Math.Sqrt(1-zeta*zeta);
   Func<double,double> response=s=>1-Math.Exp(-zeta*omega*s)*(Math.Cos(wd*s)+zeta*omega/wd*Math.Sin(wd*s));return EditDocument.Clamp(response(t*.72)/response(.72),0,1);
  }
  public Pose At(double t) {
   var pose=new Pose();
   foreach(var z in doc.Zooms){if(t<z.Start||t>z.End||(z.Automatic&&!doc.Style.AutoZoom))continue;
    double transition=Math.Min(z.Transition,(z.End-z.Start)/2);
    double v=t<z.Start+transition?Ease((t-z.Start)/transition,z.Easing):t>z.End-transition?1-Ease((t-z.End+transition)/transition,z.Easing):1;
    double scale=1+(z.Scale-1)*v;if(scale<pose.Zoom)continue;pose.Zoom=scale;pose.X=z.X;pose.Y=z.Y;
    if(z.Automatic&&doc.Style.Follow&&pointers.Count>0){
     // Time-based smoothing is deterministic for playback, random seeking and export.
     double x=0,y=0,weight=0;int lo=0,hi=pointers.Count;
     while(lo<hi){int mid=(lo+hi)/2;if(pointers[mid].Time<=t)lo=mid+1;else hi=mid;}
     for(int k=lo-1;k>=0;k--){var p=pointers[k];double age=t-p.Time;if(age>.6&&weight>0)break;if(!p.Visible)continue;double w=Math.Exp(-age*12);x+=p.X*w;y+=p.Y*w;weight+=w;if(age>.6)break;}
     if(weight>1e-12){pose.X=x/weight;pose.Y=y/weight;}
    }
   }
   double half=.5/pose.Zoom;pose.X=EditDocument.Clamp(pose.X,half,1-half);pose.Y=EditDocument.Clamp(pose.Y,half,1-half);
   if(doc.Style.Opening>0&&t<doc.Style.Opening){double f=EditDocument.Clamp(t/doc.Style.Opening,0,1);f=f*f*(3-2*f);pose.Opacity=f;pose.OpeningScale=.92+.08*f;}
   return pose;
  }
 }
 public class SceneLayout {
  public RectangleF Frame; public RectangleF Video; public float Radius; public float Header;
  public static double Aspect(EditDocument d){switch(d.Style.Ratio){case "1:1":return 1;case "9:16":return 9.0/16;case "4:3":return 4.0/3;case "适应":return (double)d.Width/d.Height;default:return 16.0/9;}}
  public static int[] OutputSize(EditDocument d,int shortEdge){double ratio=Aspect(d);return ratio>=1?new[]{(int)Math.Round(shortEdge*ratio/2)*2,shortEdge}:new[]{shortEdge,(int)Math.Round(shortEdge/ratio/2)*2};}
  public static SceneLayout Make(EditDocument d,int width,int height,Pose p) {
   double unit=Math.Min(width,height)/720.0,pad=d.Style.Padding*unit;
   double availableW=Math.Max(16,width-pad*2),availableH=Math.Max(16,height-pad*2),header=d.Style.Frame=="none"?0:24*unit;
   double ratio=(double)d.Width/d.Height;double h=Math.Min(availableH-header,availableW/ratio),w=h*ratio;
   double scale=d.Style.Scale*p.OpeningScale;w*=scale;h*=scale;header*=scale;
   double x=(width-w)*d.Style.X,y=(height-h-header)*d.Style.Y;
   return new SceneLayout {Frame=new RectangleF((float)x,(float)y,(float)w,(float)(h+header)),Video=new RectangleF((float)x,(float)(y+header),(float)w,(float)h),Radius=(float)(d.Style.Radius*unit),Header=(float)header};
  }
 }
 static class StudioMedia {
  public static EditDocument Import(string path) {
   using(var p=Media.Start("ffprobe","-v error -show_entries stream=codec_type,width,height,avg_frame_rate:format=duration -of json "+Media.Q(path),true)) {
    var err=p.StandardError.ReadToEndAsync();string text=p.StandardOutput.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0)throw new Exception(err.Result);
    var json=EditDocument.Serializer().Deserialize<Dictionary<string,object>>(text);var streams=(System.Collections.ArrayList)json["streams"];
    var video=streams.Cast<Dictionary<string,object>>().FirstOrDefault(s=>(string)s["codec_type"]=="video");if(video==null)throw new Exception("文件中没有视频轨道。");
    var format=(Dictionary<string,object>)json["format"];double duration=double.Parse(format["duration"].ToString(),CultureInfo.InvariantCulture);
    var d=new EditDocument {Source=Path.GetFullPath(path),Name=Path.GetFileNameWithoutExtension(path),Width=Convert.ToInt32(video["width"]),Height=Convert.ToInt32(video["height"]),Duration=duration,Audio=streams.Cast<Dictionary<string,object>>().Any(s=>(string)s["codec_type"]=="audio")};
    object rate;if(video.TryGetValue("avg_frame_rate",out rate)){var parts=rate.ToString().Split('/');double numerator,denominator;if(parts.Length==2&&double.TryParse(parts[0],NumberStyles.Float,CultureInfo.InvariantCulture,out numerator)&&double.TryParse(parts[1],NumberStyles.Float,CultureInfo.InvariantCulture,out denominator)&&denominator>0&&numerator>0)d.FrameRate=numerator/denominator;}
    d.Cuts.Add(new Cut {Start=0,End=duration});return d;
   }
  }
  public static void Run(string args,CancellationToken token) {
   using(var p=Media.Start("ffmpeg",args,false))using(token.Register(()=>{try{p.Kill();}catch{}})) {var errors=p.StandardError.ReadToEndAsync();p.StandardInput.Close();p.WaitForExit();token.ThrowIfCancellationRequested();if(p.ExitCode!=0)throw new Exception(errors.Result);}
  }
  public static byte[] Frame(string path,double time,int width) {
   using(var p=Media.Start("ffmpeg","-v error -ss "+Media.N(Math.Max(0,time))+" -i "+Media.Q(path)+" -frames:v 1 -vf scale="+width+":-2 -f image2pipe -vcodec png pipe:1",true)) {
    var errors=p.StandardError.ReadToEndAsync();using(var ms=new MemoryStream()){p.StandardOutput.BaseStream.CopyTo(ms);p.WaitForExit();if(p.ExitCode!=0)throw new Exception(errors.Result);return ms.ToArray();}
   }
  }
  public static string Filter(EditDocument doc,bool audio) {
   var s=new StringBuilder();var tracks=new StringBuilder();
   for(int i=0;i<doc.Cuts.Count;i++){var c=doc.Cuts[i];s.Append("[0:v]trim=start="+Media.N(c.Start)+":end="+Media.N(c.End)+",setpts=(PTS-STARTPTS)/"+Media.N(c.Speed)+"[v"+i+"];\n");tracks.Append("[v"+i+"]");
    if(audio){s.Append("[0:a:0]atrim=start="+Media.N(c.Start)+":end="+Media.N(c.End)+",asetpts=PTS-STARTPTS,atempo="+Media.N(c.Speed)+",apad,atrim=duration="+Media.N(c.Length)+"[a"+i+"];\n");tracks.Append("[a"+i+"]");}}
   s.Append(tracks+"concat=n="+doc.Cuts.Count+":v=1:a="+(audio?1:0)+"[video]"+(audio?"[audio]":"")+"\n");return s.ToString();
  }
 }
 static class StudioRenderer {
  public static GraphicsPath Round(RectangleF r,float radius) {if(radius<.1){var p=new GraphicsPath();p.AddRectangle(r);return p;}return Renderer.Rounded(r,Math.Min(radius,Math.Min(r.Width,r.Height)/2));}
  public static Color ColorOf(string hex){try{return ColorTranslator.FromHtml(hex);}catch{return Color.White;}}
  public static void Draw(Graphics g,Bitmap frame,EditDocument doc,MotionEvaluator motion,double time,int width,int height,Image wallpaper,Bitmap canvas=null) {
   var a=doc.Style;g.Clear(ColorOf(a.ColorA));
   if(a.Background=="gradient")using(var brush=new LinearGradientBrush(new Rectangle(0,0,width,height),ColorOf(a.ColorA),ColorOf(a.ColorB),45))g.FillRectangle(brush,0,0,width,height);
   else if(a.Background=="image"&&wallpaper!=null){double s=Math.Max((double)width/wallpaper.Width,(double)height/wallpaper.Height);g.DrawImage(wallpaper,new RectangleF((float)((width-wallpaper.Width*s)/2),(float)((height-wallpaper.Height*s)/2),(float)(wallpaper.Width*s),(float)(wallpaper.Height*s)));}
   if(a.Background=="dots"){float step=Math.Max(8,width/80f);using(var dot=new SolidBrush(Color.FromArgb(48,80,85,88)))for(float x=step/2;x<width;x+=step)for(float y=step/2;y<height;y+=step)g.FillEllipse(dot,x,y,1.2f,1.2f);}
   var pose=motion.At(time);pose.Opacity*=1-EffectRenderer.Suppression(doc,time);var layout=SceneLayout.Make(doc,width,height,pose);float unit=Math.Min(width,height)/720f;
   if(pose.Opacity>.001){
   if(a.Shadow>0)for(int i=10;i>=1;i--){var r=layout.Frame;float spread=i*unit*1.2f;r.Inflate(spread,spread);r.Y+=5*unit;using(var path=Round(r,layout.Radius+spread))using(var b=new SolidBrush(Color.FromArgb((int)(a.Shadow*.13*pose.Opacity),0,0,0)))g.FillPath(b,path);}
   using(var outline=Round(layout.Frame,layout.Radius)){
    var state=g.Save();g.SetClip(outline);using(var b=new SolidBrush(Color.FromArgb((int)(255*pose.Opacity),a.Frame=="dark"?Color.FromArgb(42,43,45):Color.FromArgb(243,243,245))))g.FillRectangle(b,layout.Frame);
    var v=layout.Video;var crop=new RectangleF((float)((pose.X-.5/pose.Zoom)*frame.Width),(float)((pose.Y-.5/pose.Zoom)*frame.Height),(float)(frame.Width/pose.Zoom),(float)(frame.Height/pose.Zoom));
    using(var softened=EffectRenderer.ApplyBlur(frame,doc,time)){var source=softened??frame;if(pose.Opacity<.999){using(var attrs=new ImageAttributes()){var matrix=new ColorMatrix();matrix.Matrix33=(float)pose.Opacity;attrs.SetColorMatrix(matrix);g.DrawImage(source,Rectangle.Round(v),crop.X,crop.Y,crop.Width,crop.Height,GraphicsUnit.Pixel,attrs);}}else g.DrawImage(source,v,crop,GraphicsUnit.Pixel);}
    EffectRenderer.Cursor(g,doc,motion,time,layout,pose,width,height);
    if(layout.Header>0){Color[] colors={Color.FromArgb(255,96,86),Color.FromArgb(255,188,46),Color.FromArgb(40,200,64)};for(int i=0;i<3;i++)using(var b=new SolidBrush(Color.FromArgb((int)(255*pose.Opacity),colors[i])))g.FillEllipse(b,layout.Frame.X+(9+10*i)*unit,layout.Frame.Y+layout.Header/2-3*unit,6*unit,6*unit);
     using(var font=new Font("Microsoft YaHei UI",8*unit,GraphicsUnit.Pixel))using(var b=new SolidBrush(Color.FromArgb((int)(255*pose.Opacity),130,132,136)))using(var format=new StringFormat {Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})g.DrawString(doc.Name,font,b,new RectangleF(layout.Frame.X,layout.Frame.Y,layout.Frame.Width,layout.Header),format);}
    g.Restore(state);if(a.Border>0)using(var pen=new Pen(Color.FromArgb((int)(100*pose.Opacity),90,90,95),(float)(a.Border*unit)))g.DrawPath(pen,outline);
   }
   }EffectRenderer.Spotlights(g,doc,time,layout,pose,width,height);EffectRenderer.Backdrop(g,canvas,doc,time,width,height);EffectRenderer.Stickers(g,doc,motion,time,width,height);
  }
  public static void Export(EditDocument doc,string output,int height,int fps,Action<double,string> progress,CancellationToken token) {
   doc.Validate();if(File.Exists(output))throw new Exception("请选择一个新的导出文件名。");
   var size=SceneLayout.OutputSize(doc,height);int width=size[0];height=size[1];
   string work=Path.Combine(Path.GetDirectoryName(output),".dogsc-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
   Process decoder=null,encoder=null;
   try {
    string timeline=Path.Combine(work,"timeline.mp4"),filter=Path.Combine(work,"timeline.txt"),result=Path.Combine(work,"result.mp4");
    File.WriteAllText(filter,StudioMedia.Filter(doc,doc.Audio),new UTF8Encoding(false));progress(0,"整理时间线");
    StudioMedia.Run("-v error -y -i "+Media.Q(doc.Source)+" -/filter_complex "+Media.Q(filter)+" -map [video] "+(doc.Audio?"-map [audio] -c:a aac ":"")+"-c:v libx264 -preset ultrafast -crf 16 -pix_fmt yuv420p -r "+fps+" "+Media.Q(timeline),token);
    token.ThrowIfCancellationRequested();progress(.1,"渲染画面与运镜");
    int dw=Math.Min(doc.Width,width);dw-=dw%2;int dh=Math.Max(2,(int)Math.Round((double)dw*doc.Height/doc.Width/2)*2);
    decoder=Media.Start("ffmpeg","-v error -i "+Media.Q(timeline)+" -an -vf scale="+dw+":"+dh+" -pix_fmt bgra -f rawvideo pipe:1",true);var decodeError=decoder.StandardError.ReadToEndAsync();
    encoder=Media.Start("ffmpeg","-v error -y -f rawvideo -pixel_format bgra -video_size "+width+"x"+height+" -framerate "+fps+" -i pipe:0 -i "+Media.Q(timeline)+" -map 0:v -map 1:a? -af volume="+Media.N(doc.Style.Volume)+" -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -c:a aac -t "+Media.N(doc.Length)+" -movflags +faststart "+Media.Q(result),false);var encodeError=encoder.StandardError.ReadToEndAsync();
    using(token.Register(()=>{try{decoder.Kill();}catch{}try{encoder.Kill();}catch{}}))
    using(var source=new Bitmap(dw,dh,PixelFormat.Format32bppArgb))using(var canvas=new Bitmap(width,height,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(canvas))
    using(var wall=doc.Style.Background=="image"&&File.Exists(doc.Style.ImagePath)?Image.FromFile(doc.Style.ImagePath):null){
     g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.SmoothingMode=SmoothingMode.AntiAlias;
     var input=new byte[dw*dh*4];var bytes=new byte[width*height*4];int frame=0;using(var motion=new MotionEvaluator(doc)){
     while(true){token.ThrowIfCancellationRequested();int filled=0,count;while(filled<input.Length&&(count=decoder.StandardOutput.BaseStream.Read(input,filled,input.Length-filled))>0)filled+=count;
      if(filled==0)break;if(filled!=input.Length)throw new Exception("解码帧不完整。");
      var data=source.LockBits(new Rectangle(0,0,dw,dh),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);try{Marshal.Copy(input,0,data.Scan0,input.Length);}finally{source.UnlockBits(data);}
      Draw(g,source,doc,motion,frame/(double)fps,width,height,wall,canvas);
      data=canvas.LockBits(new Rectangle(0,0,width,height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);try{Marshal.Copy(data.Scan0,bytes,0,bytes.Length);}finally{canvas.UnlockBits(data);}
      encoder.StandardInput.BaseStream.Write(bytes,0,bytes.Length);frame++;if(frame%15==0)progress(.1+.88*Math.Min(1,frame/(doc.Length*fps)),"渲染画面与运镜");
     }
     encoder.StandardInput.Close();decoder.WaitForExit();encoder.WaitForExit();token.ThrowIfCancellationRequested();
     if(frame==0||decoder.ExitCode!=0||encoder.ExitCode!=0)throw new Exception("导出失败："+decodeError.Result+encodeError.Result);}
    }
    File.Move(result,output);progress(1,"导出完成");
   }catch{token.ThrowIfCancellationRequested();throw;}finally{
    foreach(var p in new[]{decoder,encoder})if(p!=null){try{if(!p.HasExited)p.Kill();p.WaitForExit();}catch{}p.Dispose();}
    // Only delete the unique, explicitly created transaction directory.
    try{if(Directory.Exists(work))Directory.Delete(work,true);}catch{}
   }
  }
 }
}
