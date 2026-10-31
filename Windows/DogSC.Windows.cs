// Windows prototype inspired by DogSC (MIT, Copyright (c) 2026 神烦老狗).
// This is a separate Windows implementation, not the macOS project format.
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
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace DogSC.Windows {
 public class PointerSample { public double Time; public double X; public double Y; public bool Click;public bool Visible=true;public string CursorKind="arrow";public int Segment; }
 public class Project {
  public string Format = "dogsc-windows-prototype"; public int Version = 1;
  public string Video = "source.mp4"; public int Width; public int Height;
  public double Duration; public List<PointerSample> Pointer = new List<PointerSample>();
  public void Save(string file) { File.WriteAllText(file, new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(this)); }
  public static Project Load(string file) {
   var p = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<Project>(File.ReadAllText(file));
   if (p == null || p.Format != "dogsc-windows-prototype" || p.Version != 1 || p.Duration <= 0 || p.Width < 2 || p.Height < 2 || p.Pointer == null)
    throw new Exception("不支持的项目。此原型暂不读取 macOS .dogscproject。");
   if (Path.GetFileName(p.Video) != p.Video || p.Pointer.Any(s => double.IsNaN(s.Time) || double.IsInfinity(s.Time) || double.IsNaN(s.X) || double.IsInfinity(s.X) || double.IsNaN(s.Y) || double.IsInfinity(s.Y)))
    throw new Exception("项目数据无效。");
   p.Pointer = p.Pointer.OrderBy(s => s.Time).ToList(); return p;
  }
 }
 static class Native {
  [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point p);
  [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 }
 static class Media {
  public static string Root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
  public static string Tool(string name) {
   var local = Path.Combine(Root, "tools", name + ".exe"); if (File.Exists(local)) return local;
   foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')) {
    try { var path = Path.Combine(dir.Trim('"'), name + ".exe"); if (File.Exists(path)) return path; } catch { }
   }
   throw new Exception("缺少 " + name + ".exe。请按 Windows/README.md 安装编码工具。");
  }
  // All user-controlled paths use Windows CRT argument quoting; no shell execution.
  public static string Q(string s) {
   var result = new System.Text.StringBuilder("\""); int slashes = 0;
   foreach(char c in s) {
    if(c == '\\') { slashes++; continue; }
    if(c == '"') result.Append('\\',slashes*2+1).Append(c);
    else result.Append('\\',slashes).Append(c);
    slashes=0;
   }
   return result.Append('\\',slashes*2).Append('"').ToString();
  }
  public static Process Start(string name, string args, bool output) {
   var p = new Process(); p.StartInfo = new ProcessStartInfo(Tool(name), args) {
    UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
    RedirectStandardError = true, RedirectStandardOutput = output,
    StandardErrorEncoding = System.Text.Encoding.UTF8,
    StandardOutputEncoding = output ? System.Text.Encoding.UTF8 : null
   }; p.Start(); return p;
  }
  public static double Duration(string path) {
   using (var p = Start("ffprobe", "-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 " + Q(path), true)) {
    var readError = p.StandardError.ReadToEndAsync(); var s = p.StandardOutput.ReadToEnd(); p.WaitForExit();
    double d; if (p.ExitCode != 0 || !double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d) || d <= 0) throw new Exception("无法读取视频时长。" + readError.Result);
    return d;
   }
  }
  public static string N(double v) { return v.ToString("0.######", CultureInfo.InvariantCulture); }
 }
 public class Camera {
  readonly List<PointerSample> samples; readonly List<double[]> envelopes = new List<double[]>(); int index;
  double cx = .5, cy = .5;
  public Camera(List<PointerSample> events) {
   samples = events.OrderBy(e => e.Time).ToList();
   // DogSC-inspired click grouping and 0.7 s transitions, simplified for this prototype.
   foreach (var e in samples.Where(e => e.Click)) {
    double start = Math.Max(0, e.Time - .2), end = e.Time + 1.4;
    if (envelopes.Count > 0 && start <= envelopes[envelopes.Count - 1][1] + .7) envelopes[envelopes.Count - 1][1] = end;
    else envelopes.Add(new double[] { start, end });
   }
  }
  public double[] Evaluate(double time, double scale, bool enabled) {
   while (index + 1 < samples.Count && samples[index + 1].Time <= time) index++;
   double tx = .5, ty = .5;
   if (samples.Count > 0 && samples[index].Time <= time) {
    tx = samples[index].X; ty = samples[index].Y;
    if (index + 1 < samples.Count && samples[index + 1].Time - samples[index].Time <= .2) {
     var a = samples[index]; var b = samples[index + 1]; double f = Math.Max(0, Math.Min(1, (time - a.Time) / Math.Max(.001, b.Time - a.Time)));
     tx += (b.X - a.X) * f; ty += (b.Y - a.Y) * f;
    }
   }
   cx += (tx - cx) * .16; cy += (ty - cy) * .16;
   double strength = 0;
   if (enabled) foreach (var range in envelopes) {
    if (time < range[0] || time > range[1] + .7) continue;
    double f = time < range[0] + .7 ? (time - range[0]) / .7 : time <= range[1] ? 1 : 1 - (time - range[1]) / .7;
    f = Math.Max(0, Math.Min(1, f)); strength = Math.Max(strength, f * f * (3 - 2 * f));
   }
   double z = 1 + (scale - 1) * strength, half = .5 / z;
   return new double[] { z, Math.Max(half, Math.Min(1 - half, cx)), Math.Max(half, Math.Min(1 - half, cy)) };
  }
 }
 static class Renderer {
  public static GraphicsPath Rounded(RectangleF r, float radius) {
   var p = new GraphicsPath(); float d = radius * 2;
   p.AddArc(r.X,r.Y,d,d,180,90); p.AddArc(r.Right-d,r.Y,d,d,270,90);
   p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.X,r.Bottom-d,d,d,90,90); p.CloseFigure(); return p;
  }
  public static void Export(Project project, string source, string destination, double start, double length, bool zoom, double scale, int theme, Action<int> progress, CancellationToken token) {
   string temporary = Path.Combine(Path.GetDirectoryName(destination), ".dogsc-export-" + Guid.NewGuid().ToString("N") + ".mp4");
   Process decoder = null, encoder = null; Task<string> decodeErrors = null, encodeErrors = null;
   try {
    const int width = 1280, height = 720, fps = 30;
    decoder = Media.Start("ffmpeg", "-v error -ss " + Media.N(start) + " -i " + Media.Q(source) + " -t " + Media.N(length) + " -an -vf fps=30,scale=1280:720:force_original_aspect_ratio=decrease,pad=1280:720:(ow-iw)/2:(oh-ih)/2 -pix_fmt bgra -f rawvideo pipe:1", true);
    decodeErrors = decoder.StandardError.ReadToEndAsync();
    encoder = Media.Start("ffmpeg", "-v error -y -f rawvideo -pixel_format bgra -video_size 1280x720 -framerate 30 -i pipe:0 -ss " + Media.N(start) + " -i " + Media.Q(source) + " -map 0:v -map 1:a? -t " + Media.N(length) + " -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p -c:a aac -movflags +faststart " + Media.Q(temporary), false);
    encodeErrors = encoder.StandardError.ReadToEndAsync();
    using (token.Register(() => { try { decoder.Kill(); } catch { } try { encoder.Kill(); } catch { } }))
    using (var frame = new Bitmap(width,height,PixelFormat.Format32bppArgb))
    using (var canvas = new Bitmap(width,height,PixelFormat.Format32bppArgb))
    using (var g = Graphics.FromImage(canvas)) {
     var bytes = new byte[width*height*4]; var output = new byte[bytes.Length]; var camera = new Camera(project.Pointer);
     Color[] top = { Color.FromArgb(35,42,92), Color.FromArgb(19,96,109), Color.FromArgb(151,67,72) };
     Color[] bottom = { Color.FromArgb(128,73,166), Color.FromArgb(54,187,167), Color.FromArgb(244,170,99) };
     var box = new RectangleF(64,56,1152,608);
     // Fit original aspect ratio inside the frame; avoid stretching ultrawide/portrait video.
     double aspect = (double)project.Width / project.Height;
     if (aspect > box.Width / box.Height) { float h=(float)(box.Width/aspect); box.Y+=(box.Height-h)/2; box.Height=h; }
     else { float w=(float)(box.Height*aspect); box.X+=(box.Width-w)/2; box.Width=w; }
     double srcW=width, srcH=height;
     if (aspect > (double)width/height) srcH=width/aspect; else srcW=height*aspect;
     var sourceBox = new RectangleF((float)((width-srcW)/2),(float)((height-srcH)/2),(float)srcW,(float)srcH);
     g.InterpolationMode=InterpolationMode.HighQualityBicubic; g.SmoothingMode=SmoothingMode.AntiAlias;
     int number=0;
     using (var bg = new LinearGradientBrush(new Rectangle(0,0,width,height),top[theme],bottom[theme],45f))
     using (var clip = Rounded(box,16)) {
      while (true) {
       token.ThrowIfCancellationRequested(); int filled=0, count;
       while (filled<bytes.Length && (count=decoder.StandardOutput.BaseStream.Read(bytes,filled,bytes.Length-filled))>0) filled+=count;
       if (filled==0) break; if (filled!=bytes.Length) throw new Exception("解码帧不完整。");
       var data=frame.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
       try { Marshal.Copy(bytes,0,data.Scan0,bytes.Length); } finally { frame.UnlockBits(data); }
       var c=camera.Evaluate(start+number/(double)fps,scale,zoom);
       g.FillRectangle(bg,0,0,width,height);
       using (var shadow=new SolidBrush(Color.FromArgb(65,0,0,0))) using (var shadowPath=Rounded(new RectangleF(box.X+4,box.Y+10,box.Width,box.Height),18)) g.FillPath(shadow,shadowPath);
       var state=g.Save(); g.SetClip(clip);
       var crop=new RectangleF(sourceBox.X+(float)((c[1]-.5/c[0])*srcW),sourceBox.Y+(float)((c[2]-.5/c[0])*srcH),(float)(srcW/c[0]),(float)(srcH/c[0]));
       g.DrawImage(frame,box,crop,GraphicsUnit.Pixel); g.Restore(state);
       data=canvas.LockBits(new Rectangle(0,0,width,height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
       try { Marshal.Copy(data.Scan0,output,0,output.Length); } finally { canvas.UnlockBits(data); }
       encoder.StandardInput.BaseStream.Write(output,0,output.Length); number++;
       if (number%15==0) progress(Math.Min(99,(int)(number/(length*fps)*100)));
      }
      if(number==0) throw new Exception("所选范围没有视频帧。");
     }
     encoder.StandardInput.Close(); decoder.WaitForExit(); encoder.WaitForExit(); token.ThrowIfCancellationRequested();
     if(decoder.ExitCode!=0 || encoder.ExitCode!=0) throw new Exception(decodeErrors.Result+encodeErrors.Result);
    }
    // Save dialogs disallow overwrite: source/project media cannot be destroyed.
    File.Move(temporary,destination); progress(100);
   } catch {
    if(token.IsCancellationRequested) throw new OperationCanceledException(); throw;
   } finally {
    foreach(var p in new[]{decoder,encoder}) if(p!=null) { try { if(!p.HasExited) p.Kill(); p.WaitForExit(); } catch { } p.Dispose(); }
    try { if(File.Exists(temporary)) File.Delete(temporary); } catch { }
   }
  }
 }
 class RegionSelector : Form {
  Point anchor; Rectangle selected; bool dragging; public Rectangle Selection;
  public RegionSelector(Rectangle bounds) {FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;Bounds=bounds;BackColor=Color.Black;Opacity=.45;TopMost=true;Cursor=Cursors.Cross;DoubleBuffered=true;KeyPreview=true;
   KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape){DialogResult=DialogResult.Cancel;Close();}};
   MouseDown+=(s,e)=>{anchor=e.Location;dragging=true;};
   MouseMove+=(s,e)=>{if(!dragging)return;selected=Rectangle.FromLTRB(Math.Min(anchor.X,e.X),Math.Min(anchor.Y,e.Y),Math.Max(anchor.X,e.X),Math.Max(anchor.Y,e.Y));Invalidate();};
   MouseUp+=(s,e)=>{dragging=false;if(selected.Width<16||selected.Height<16)return;Selection=new Rectangle(bounds.X+selected.X,bounds.Y+selected.Y,selected.Width,selected.Height);DialogResult=DialogResult.OK;Close();};
  }
  protected override void OnPaint(PaintEventArgs e) {base.OnPaint(e);using(var pen=new Pen(Color.White,3))e.Graphics.DrawRectangle(pen,selected);e.Graphics.DrawString("拖动框选录制区域 · Esc 取消",Font,Brushes.White,20,20);}
 }
 static class SmokeTest {
  public static void Run() {
   string folder=Path.Combine(Media.Root,"bin","smoke test 中文-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string log=Path.Combine(Media.Root,"bin","smoke-result.txt");
   try {
    var p=new Project {Width=640,Height=360,Duration=3}; p.Pointer.Add(new PointerSample {Time=.5,X=.7,Y=.4,Click=true});p.Pointer.Add(new PointerSample {Time=1.4,X=.8,Y=.6});
    string source=Path.Combine(folder,"source.mp4");using(var ff=Media.Start("ffmpeg","-v error -y -f lavfi -i testsrc2=size=640x360:rate=30 -f lavfi -i sine=frequency=440:sample_rate=48000 -t 3 -c:v libx264 -pix_fmt yuv420p -c:a aac "+Media.Q(source),false)){var errors=ff.StandardError.ReadToEndAsync();ff.WaitForExit();if(ff.ExitCode!=0)throw new Exception(errors.Result);}
    string manifest=Path.Combine(folder,"project.dogscwin");p.Save(manifest);p=Project.Load(manifest);
    var camera=new Camera(p.Pointer);if(camera.Evaluate(1.2,1.6,true)[0]<=1.1)throw new Exception("自动缩放测试失败");
    if(new Camera(p.Pointer).Evaluate(1.2,1.6,false)[0]!=1)throw new Exception("关闭缩放测试失败");
    if(new Camera(p.Pointer).Evaluate(0,1.6,true)[0]!=1 || new Camera(p.Pointer).Evaluate(3,1.6,true)[0]!=1)throw new Exception("缩放边界测试失败");
    var pose=new Camera(p.Pointer).Evaluate(1.2,1.6,true); if(pose[1]<.5/pose[0] || pose[1]>1-.5/pose[0])throw new Exception("运镜越界");
    string output=Path.Combine(folder,"export.mp4");Renderer.Export(p,source,output,.25,2,true,1.6,0,v=>{},CancellationToken.None);
    double duration=Media.Duration(output);if(Math.Abs(duration-2)>.15)throw new Exception("裁剪时长错误："+duration);
    string canceled=Path.Combine(folder,"canceled.mp4");using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{Renderer.Export(p,source,canceled,0,2,true,1.6,0,v=>{},cancel.Token);throw new Exception("未能取消导出");}catch(OperationCanceledException){if(File.Exists(canceled))throw new Exception("取消后仍生成成片");}}
    // Exercise gdigrab only against this synthetic window, never the user's desktop.
    using(var testWindow=new Form {Text="DogSC Capture Smoke",ClientSize=new Size(400,240),BackColor=Color.Teal,StartPosition=FormStartPosition.Manual,Location=new Point(40,40),TopMost=true}) {
     testWindow.Controls.Add(new Label {Text="DogSC Windows capture test",ForeColor=Color.White,AutoSize=true,Location=new Point(20,50)});testWindow.Show();Application.DoEvents();
     using(var ff=Media.Start("ffmpeg","-v error -y -f gdigrab -framerate 30 -draw_mouse 0 -i "+Media.Q("title=DogSC Capture Smoke")+" -t 1 -vf pad=ceil(iw/2)*2:ceil(ih/2)*2 -c:v libx264 -pix_fmt yuv420p "+Media.Q(Path.Combine(folder,"capture.mp4")),false)){var errors=ff.StandardError.ReadToEndAsync();if(!ff.WaitForExit(15000)){ff.Kill();throw new Exception("录屏测试超时");}if(ff.ExitCode!=0)throw new Exception("Windows 录屏测试失败："+errors.Result);}
     testWindow.Close();
    }
    if(Math.Abs(Media.Duration(Path.Combine(folder,"capture.mp4"))-1)>.15)throw new Exception("录屏时长错误");
    using(var probe=Media.Start("ffprobe","-v error -show_entries stream=codec_type,width,height -of json "+Media.Q(output),true)){var error=probe.StandardError.ReadToEndAsync();string details=probe.StandardOutput.ReadToEnd();probe.WaitForExit();if(!details.Contains("1280")||!details.Contains("720")||!details.Contains("audio"))throw new Exception("输出尺寸或音轨错误");File.WriteAllText(log,"PASS\nSynthetic video and synthetic test window only; no desktop captured.\nAuto zoom, boundaries, cancellation, paths with spaces/Chinese, project roundtrip, gdigrab capture, trim, output audio checked.\nDuration="+duration+"\n"+details+"\nArtifacts="+folder);}
   }catch(Exception ex){File.WriteAllText(log,"FAIL\n"+ex);Environment.ExitCode=1;}
  }
 }
}
