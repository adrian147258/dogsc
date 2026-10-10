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
 public class RecorderForm : Form {
  ComboBox screen=new ComboBox(), fps=new ComboBox(), theme=new ComboBox();
  Button record=new Button(), stop=new Button(), open=new Button(), export=new Button(), play=new Button(), regionButton=new Button();
  CheckBox autoZoom=new CheckBox(); NumericUpDown scale=new NumericUpDown(), trimStart=new NumericUpDown(), trimEnd=new NumericUpDown();
  Label status=new Label(); ProgressBar bar=new ProgressBar(); FlowLayoutPanel settings=new FlowLayoutPanel();
  System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer(); Stopwatch watch=new Stopwatch();
  Process capture; Task<string> captureErrors; Project project; string projectFile; Rectangle region; bool left, right, busy;
  CancellationTokenSource cancellation;
  public RecorderForm() {
   Text="DogSC · Windows 原型"; ClientSize=new Size(820,570); MinimumSize=new Size(760,610); BackColor=Color.FromArgb(24,27,40); ForeColor=Color.White; Font=new Font("Microsoft YaHei UI",10);
   var title=new Label {Text="DogSC  /  WINDOWS",Font=new Font(Font.FontFamily,22,FontStyle.Bold),AutoSize=true,Location=new Point(28,24)}; Controls.Add(title);
   Controls.Add(new Label {Text="录制 → 自动运镜 → 渐变背景 → MP4",AutoSize=true,Location=new Point(30,78),ForeColor=Color.LightSteelBlue});
   settings.Location=new Point(28,118); settings.Size=new Size(760,280); settings.FlowDirection=FlowDirection.TopDown; settings.WrapContents=false; Controls.Add(settings);
   screen.DropDownStyle=ComboBoxStyle.DropDownList; screen.Width=320;
   foreach(var s in Screen.AllScreens) screen.Items.Add(s.DeviceName+"  "+s.Bounds.Width+" × "+s.Bounds.Height); screen.SelectedIndex=0;
   screen.SelectedIndexChanged+=(s,e)=>{region=Screen.AllScreens[screen.SelectedIndex].Bounds; regionButton.Text="框选区域";}; region=Screen.AllScreens[0].Bounds;
   fps.DropDownStyle=ComboBoxStyle.DropDownList; fps.Width=95; fps.Items.AddRange(new object[]{"30 FPS","60 FPS"}); fps.SelectedIndex=0;
   regionButton.Text="框选区域"; regionButton.Width=150; regionButton.Click+=(s,e)=>ChooseRegion();
   settings.Controls.Add(Row("录制范围",screen,regionButton,fps));
   autoZoom.Text="点击自动缩放 + 鼠标跟随"; autoZoom.Checked=true; autoZoom.Width=295;
   scale.Minimum=1; scale.Maximum=3; scale.DecimalPlaces=1; scale.Increment=.1m; scale.Value=1.6m; scale.Width=65;
   settings.Controls.Add(Row("自动运镜",autoZoom,scale));
   theme.DropDownStyle=ComboBoxStyle.DropDownList; theme.Items.AddRange(new object[]{"暮色紫","海湾青","落日橙"}); theme.SelectedIndex=0;
   foreach(var combo in new[]{screen,fps,theme}) {
    combo.ForeColor=Color.Black;combo.DrawMode=DrawMode.OwnerDrawFixed;combo.ItemHeight=24;
    combo.DrawItem+=(s,e)=>{var control=(ComboBox)s;int i=e.Index<0?control.SelectedIndex:e.Index;e.Graphics.FillRectangle(SystemBrushes.Window,e.Bounds);if(i>=0)TextRenderer.DrawText(e.Graphics,control.Items[i].ToString(),control.Font,e.Bounds,Color.Black,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);e.DrawFocusRectangle();};
   }
   settings.Controls.Add(Row("导出背景",theme,new Label {Text="圆角 · 阴影 · 720p / 30 FPS",AutoSize=true}));
   foreach(var n in new[]{trimStart,trimEnd}) {n.DecimalPlaces=2;n.Maximum=86400;n.Width=110;}
   settings.Controls.Add(Row("保留范围",trimStart,new Label {Text="秒  →",AutoSize=true},trimEnd,new Label {Text="秒",AutoSize=true}));
   settings.Controls.Add(new Label {Text="原型：无音频采集、摄像头和多轨编辑；鼠标已录入素材。\n使用独立 Windows 项目格式，暂不兼容原版项目。\n导入视频中的已有音轨会保留。",Size=new Size(740,70),Font=new Font(Font.FontFamily,9),ForeColor=Color.LightSteelBlue,Margin=new Padding(0,10,0,0)});
   var actions=new FlowLayoutPanel {Location=new Point(28,405),Size=new Size(760,52)}; Controls.Add(actions);
   record.Text="开始录制"; stop.Text="停止录制"; open.Text="打开项目"; play.Text="播放素材"; export.Text="导出成片";
   foreach(var b in new[]{record,stop,open,play,export}) {b.Size=new Size(135,42); b.FlatStyle=FlatStyle.Flat; b.BackColor=Color.FromArgb(49,55,80); actions.Controls.Add(b);}
   record.BackColor=Color.FromArgb(107,77,211);
   record.Click+=async(s,e)=>await StartRecording(); stop.Click+=async(s,e)=>{if(cancellation!=null){cancellation.Cancel();return;} await StopRecording();};
   open.Click+=(s,e)=>OpenProject(); play.Click+=(s,e)=>{try{Process.Start(Source());}catch(Exception ex){ShowError(ex);}};
   export.Click+=async(s,e)=>await Export();
   status.Location=new Point(28,470); status.Size=new Size(760,48); status.Text="就绪。录制时窗口会最小化；返回窗口点击停止。"; Controls.Add(status);
   bar.Location=new Point(28,530); bar.Size=new Size(760,10); Controls.Add(bar);
   timer.Interval=33; timer.Tick+=(s,e)=>SamplePointer();
   FormClosing+=async(s,e)=>{if(capture!=null || busy) {e.Cancel=true;if(cancellation!=null){cancellation.Cancel();status.Text="正在取消导出，请稍后关闭。";}else{await StopRecording();}}}; UpdateButtons();
  }
  FlowLayoutPanel Row(string label,params Control[] items) {var r=new FlowLayoutPanel {Width=750,Height=44};r.Controls.Add(new Label {Text=label,Width=95,Margin=new Padding(0,7,4,0)});foreach(var c in items)r.Controls.Add(c);return r;}
  void ShowError(Exception ex) {status.Text="操作失败";MessageBox.Show(this,ex.Message,"DogSC",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  string Source() {return Path.Combine(Path.GetDirectoryName(projectFile),project.Video);}
  void UpdateButtons() {bool running=capture!=null;record.Enabled=!running&&!busy;open.Enabled=!running&&!busy;play.Enabled=projectFile!=null&&!running&&!busy;export.Enabled=play.Enabled;settings.Enabled=!running&&!busy;stop.Enabled=running||cancellation!=null;stop.Text=cancellation!=null?"取消导出":"停止录制";}
  void ChooseRegion() {Hide();using(var selector=new RegionSelector(Screen.AllScreens[screen.SelectedIndex].Bounds)){if(selector.ShowDialog()==DialogResult.OK){region=selector.Selection;regionButton.Text=region.Width+" × "+region.Height;}}Show();}
  async Task StartRecording() {
   busy=true; UpdateButtons();
   try {
    Media.Tool("ffmpeg");Media.Tool("ffprobe");region.Width-=region.Width%2;region.Height-=region.Height%2;if(region.Width<2||region.Height<2)throw new Exception("录制区域过小。");
    string folder=Path.Combine(Media.Root,"recordings",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));Directory.CreateDirectory(folder);
    projectFile=Path.Combine(folder,"project.dogscwin");project=new Project {Width=region.Width,Height=region.Height};
    string args="-hide_banner -y -f gdigrab -framerate "+(fps.SelectedIndex==0?30:60)+" -draw_mouse 1 -offset_x "+region.X+" -offset_y "+region.Y+" -video_size "+region.Width+"x"+region.Height+" -i desktop -vf pad=ceil(iw/2)*2:ceil(ih/2)*2 -c:v libx264 -preset ultrafast -crf 18 -pix_fmt yuv420p -movflags +faststart "+Media.Q(Source());
    WindowState=FormWindowState.Minimized; await Task.Delay(600);
    capture=Media.Start("ffmpeg",args,false);captureErrors=capture.StandardError.ReadToEndAsync();watch.Restart();left=(Native.GetAsyncKeyState(1)&0x8000)!=0;right=(Native.GetAsyncKeyState(2)&0x8000)!=0;timer.Start();status.Text="录制中… 返回本窗口可停止。";
   } catch(Exception ex) {WindowState=FormWindowState.Normal;projectFile=null;ShowError(ex);} finally {busy=false;UpdateButtons();}
  }
  async void SamplePointer() {
   if(capture==null)return;
   if(capture.HasExited){await StopRecording();return;}
   Native.Point p; bool l=(Native.GetAsyncKeyState(1)&0x8000)!=0,r=(Native.GetAsyncKeyState(2)&0x8000)!=0;
   if(Native.GetCursorPos(out p) && region.Contains(p.X,p.Y)) project.Pointer.Add(new PointerSample {Time=watch.Elapsed.TotalSeconds,X=(double)(p.X-region.X)/region.Width,Y=(double)(p.Y-region.Y)/region.Height,Click=(l&&!left)||(r&&!right)});
   left=l;right=r;status.Text="录制中  "+watch.Elapsed.ToString(@"hh\:mm\:ss")+"  · 点击停止后保存项目";
  }
  async Task StopRecording() {
   if(capture==null)return;timer.Stop();watch.Stop();var p=capture;capture=null;busy=true;UpdateButtons();status.Text="正在写入 MP4…";
   try {
    await Task.Run(()=>{if(!p.HasExited){p.StandardInput.WriteLine("q");p.StandardInput.Flush();if(!p.WaitForExit(15000)){p.Kill();throw new Exception("编码器未能正常结束，素材保留在录制目录，请检查。");}}});
    if(p.ExitCode!=0)throw new Exception("录制失败："+await captureErrors);
    project.Duration=await Task.Run(()=>Media.Duration(Source()));project.Save(projectFile);trimStart.Value=0;trimEnd.Value=(decimal)Math.Min(86400,project.Duration);status.Text="已保存项目："+projectFile;
   }catch(Exception ex){projectFile=null;ShowError(ex);}finally{p.Dispose();busy=false;WindowState=FormWindowState.Normal;UpdateButtons();}
  }
  void OpenProject() {
   using(var dlg=new OpenFileDialog {Filter="Windows 项目|*.dogscwin|视频素材|*.mp4;*.mov;*.mkv"}) if(dlg.ShowDialog()==DialogResult.OK) {
    try {
     if(Path.GetExtension(dlg.FileName).Equals(".dogscwin",StringComparison.OrdinalIgnoreCase)){var loaded=Project.Load(dlg.FileName);if(!File.Exists(Path.Combine(Path.GetDirectoryName(dlg.FileName),loaded.Video)))throw new Exception("项目素材不存在。");project=loaded;projectFile=dlg.FileName;}
     else {var duration=Media.Duration(dlg.FileName); project=new Project {Duration=duration,Video=Path.GetFileName(dlg.FileName)};
      using(var p=Media.Start("ffprobe","-v error -select_streams v:0 -show_entries stream=width,height -of csv=s=x:p=0 "+Media.Q(dlg.FileName),true)){var err=p.StandardError.ReadToEndAsync();var size=p.StandardOutput.ReadToEnd().Trim().Split('x');p.WaitForExit();if(p.ExitCode!=0||size.Length!=2)throw new Exception("无法读取视频尺寸。"+err.Result);project.Width=int.Parse(size[0]);project.Height=int.Parse(size[1]);}
      projectFile=Path.Combine(Path.GetDirectoryName(dlg.FileName),"import.dogscwin");}
     trimStart.Value=0;trimEnd.Value=(decimal)Math.Min(86400,project.Duration);status.Text="已打开："+dlg.FileName+(project.Pointer.Count==0?"（无鼠标轨迹，仅添加背景）":"");UpdateButtons();
    }catch(Exception ex){ShowError(ex);}
   }
  }
  async Task Export() {
   double start=(double)trimStart.Value,end=(double)trimEnd.Value;if(end<=start||end>project.Duration+.05){ShowError(new Exception("请设置有效的保留范围。"));return;}
   using(var dlg=new SaveFileDialog {Filter="MP4 视频|*.mp4",FileName="DogSC-成片.mp4",OverwritePrompt=true}) if(dlg.ShowDialog()==DialogResult.OK) {
    if(File.Exists(dlg.FileName)){ShowError(new Exception("请选择新文件名，原型不会覆盖现有视频。"));return;}
    busy=true;cancellation=new CancellationTokenSource();UpdateButtons();bar.Value=0;status.Text="正在渲染自动运镜和背景…";
    bool zoom=autoZoom.Checked;double amount=(double)scale.Value;int background=theme.SelectedIndex;
    try {await Task.Run(()=>Renderer.Export(project,Source(),dlg.FileName,start,end-start,zoom,amount,background,v=>BeginInvoke(new Action(()=>{bar.Value=v;status.Text="正在导出 "+v+"%";})),cancellation.Token));status.Text="导出完成："+dlg.FileName;}
    catch(OperationCanceledException){status.Text="已取消导出，项目仍保留。";}catch(Exception ex){ShowError(ex);}finally{cancellation.Dispose();cancellation=null;busy=false;UpdateButtons();}
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
 static class Program {
  [STAThread] static void Main(string[] args) {
   Native.SetProcessDPIAware();
   if(args.Length>0&&args[0]=="--ui-snapshot") {Application.EnableVisualStyles();using(var f=new RecorderForm()){f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-32000,-32000);f.Show();Application.DoEvents();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));b.Save(Path.Combine(Media.Root,"bin","ui.png"));}f.Close();}return;}
   if(args.Length>0&&args[0]=="--smoke-test") { SmokeTest.Run();return; }
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new RecorderForm());
  }
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
