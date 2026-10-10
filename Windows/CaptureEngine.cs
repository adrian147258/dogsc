// DogSC Windows recording backend. MIT License, Copyright (c) 2026 神烦老狗.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DogSC.Windows;
using D=System.Drawing;

namespace DogSC.Replica {
 public sealed class CaptureSession {
  public Process Process;public Task<string> Errors;public Task Progress;public string Backend;
  public int Frames,Duplicated,Dropped;public double OutputSeconds,StartupSeconds;public bool HardwareEncoding;public readonly Stopwatch Clock=new Stopwatch();
  internal TaskCompletionSource<bool> Ready=new TaskCompletionSource<bool>();
  public double CpuSeconds {get{try{return Process.TotalProcessorTime.TotalSeconds;}catch{return 0;}}}
 }
 public static class CaptureEngine {
  [DllImport("user32.dll")]static extern IntPtr MonitorFromPoint(Native.Point point,uint flags);
  static readonly object probeLock=new object();static readonly Dictionary<string,Task<bool>> probes=new Dictionary<string,Task<bool>>();
  static readonly HashSet<string> incompatiblePlans=new HashSet<string>();
  static Task<bool> Probe(string encoder,uint vendor,string options){lock(probeLock){Task<bool> probe;if(probes.TryGetValue(encoder,out probe))return probe;probe=Task.Run(()=>{if(!DesktopDuplication.HasVendor(vendor))return false;try{using(var p=Media.Start("ffmpeg","-v error -f lavfi -i color=size=640x360:rate=30 -frames:v 4 -an -c:v "+encoder+" "+options+" -f null -",false)){var error=p.StandardError.ReadToEndAsync();if(!p.WaitForExit(5000)){p.Kill();p.WaitForExit();return false;}return p.ExitCode==0;}}catch{return false;}});probes[encoder]=probe;return probe;}}
  public static Task<bool> HardwareAvailable(){return Probe("h264_nvenc",0x10DE,"-preset p1");}
  static Task<bool> AmdAvailable(){return Probe("h264_amf",0x1002,"-quality speed");}
  static Task<bool> IntelAvailable(){return Probe("h264_qsv",0x8086,"-preset veryfast");}
  public static Task Prewarm(){return Task.WhenAll(HardwareAvailable(),AmdAvailable(),IntelAvailable());}
  class Plan{public string Input,Filter,Encoding,Name,Device="";}
  static IEnumerable<Plan> Plans(IntPtr target,D.Rectangle area,int fps,bool nvenc,bool continuous,bool legacy,bool amf,bool qsv){
   string gpu="-c:v h264_nvenc -preset p1 -tune ll -rc vbr -cq 18 -b:v 0 -bf 0 "+(legacy?"-delay 0 -zerolatency 1":"-rc-lookahead 0 -surfaces 4 -delay 1 -zerolatency 1 -multipass disabled");
   string software="-c:v libx264 -preset ultrafast -crf 18 -threads "+Math.Min(4,Math.Max(1,Environment.ProcessorCount/4))+" -pix_fmt yuv420p";
   string amd="-c:v h264_amf -usage lowlatency -quality speed -rc cqp -qp_i 18 -qp_p 18 -bf 0",intel="-c:v h264_qsv -preset veryfast -global_quality 18 -look_ahead 0 -async_depth 4 -bf 0 -pix_fmt nv12";
   string source="gfxcapture=max_framerate="+fps+":capture_cursor=0:capture_border=0:display_border=0:width="+area.Width+":height="+area.Height;
   if(target!=IntPtr.Zero)source+=":hwnd="+target.ToInt64();else{var screen=System.Windows.Forms.Screen.FromRectangle(area).Bounds;var center=new Native.Point {X=area.X+area.Width/2,Y=area.Y+area.Height/2};source+=":hmonitor="+MonitorFromPoint(center,2).ToInt64()+":crop_left="+Math.Max(0,area.X-screen.X)+":crop_top="+Math.Max(0,area.Y-screen.Y)+":crop_right="+Math.Max(0,screen.Right-area.Right)+":crop_bottom="+Math.Max(0,screen.Bottom-area.Bottom);}
   if(continuous&&target==IntPtr.Zero){var screen=System.Windows.Forms.Screen.FromRectangle(area);var selection=DesktopDuplication.Find(MonitorFromPoint(new Native.Point {X=area.X+area.Width/2,Y=area.Y+area.Height/2},2));if(selection!=null){string device="-init_hw_device d3d11va=dogsc:"+selection[0]+" -filter_hw_device dogsc ";string dda="ddagrab=output_idx="+selection[1]+":framerate="+fps+":dup_frames=1:draw_mouse=0:video_size="+area.Width+"x"+area.Height+":offset_x="+(area.X-screen.Bounds.X)+":offset_y="+(area.Y-screen.Bounds.Y);
    uint vendor=DesktopDuplication.Vendor(selection[0]);if(amf&&vendor==0x1002)yield return new Plan {Input="",Device=device,Filter=dda+"[video]",Encoding=amd,Name="桌面复制捕获 · AMD 硬件编码"};
    if(nvenc){if(legacy||vendor==0||vendor==0x10DE)yield return new Plan {Input="",Device=device,Filter=dda+"[video]",Encoding=gpu,Name="桌面复制捕获 · NVIDIA 硬件编码"};yield return new Plan {Input="",Device=device,Filter=dda+",hwdownload,format=bgra,hwupload_cuda=device=0[video]",Encoding=gpu,Name="桌面复制捕获 · NVIDIA 硬件编码（跨显卡）"};}if(amf&&vendor==0x1002)yield return new Plan {Input="",Device=device,Filter=dda+",hwdownload,format=bgra[video]",Encoding=amd+" -pix_fmt nv12",Name="桌面复制捕获 · AMD 兼容硬件编码"};if(qsv)yield return new Plan {Input="",Device=device,Filter=dda+",hwdownload,format=bgra[video]",Encoding=intel,Name="桌面复制捕获 · Intel 硬件编码"};yield return new Plan {Input="",Device=device,Filter=dda+",hwdownload,format=bgra[video]",Encoding=software,Name="桌面复制捕获 · CPU 编码"};}}
   if(!continuous){if(nvenc){yield return new Plan {Input="",Filter=source+"[video]",Encoding=gpu,Name="Windows 图形捕获 · NVIDIA 硬件编码"};yield return new Plan {Input="",Filter=source+",hwdownload,format=bgra[video]",Encoding=gpu+" -pix_fmt bgra",Name="Windows 图形捕获 · NVIDIA 硬件编码（跨显卡）"};}
    yield return new Plan {Input="",Filter=source+",hwdownload,format=bgra[video]",Encoding=software,Name="Windows 图形捕获 · CPU 编码"};}
   string gdi="-thread_queue_size 4 -f gdigrab -framerate "+fps+" -draw_mouse 0 "+(target==IntPtr.Zero?"-offset_x "+area.X+" -offset_y "+area.Y+" -video_size "+area.Width+"x"+area.Height+" ":"")+"-i "+Media.Q(target==IntPtr.Zero?"desktop":"hwnd="+target.ToInt64())+" ";
   if(nvenc)yield return new Plan {Input=gdi,Filter="",Encoding=gpu+" -pix_fmt bgra",Name="兼容捕获 · NVIDIA 硬件编码"};
   if(amf)yield return new Plan {Input=gdi,Filter="",Encoding=amd+" -pix_fmt nv12",Name="兼容捕获 · AMD 硬件编码"};
   if(qsv)yield return new Plan {Input=gdi,Filter="",Encoding=intel,Name="兼容捕获 · Intel 硬件编码"};
   yield return new Plan {Input=gdi,Filter="",Encoding=software,Name="兼容捕获 · CPU 编码"};
  }
  public static Task<CaptureSession> Start(IntPtr target,D.Rectangle area,int fps,string microphone,string output,Action<string> status,bool continuous=false){return StartCore(target,area,fps,microphone,output,status,continuous,false);}
  internal static Task<CaptureSession> StartBenchmark(D.Rectangle area,int fps,string output,bool legacy){return StartCore(IntPtr.Zero,area,fps,null,output,message=>{},true,legacy);}
  static async Task<CaptureSession> StartCore(IntPtr target,D.Rectangle area,int fps,string microphone,string output,Action<string> status,bool continuous,bool legacy){
   if(!legacy)await Prewarm();bool nvenc=await HardwareAvailable(),amf=!legacy&&await AmdAvailable(),qsv=!legacy&&await IntelAvailable();var failures=new List<string>();
   foreach(var plan in Plans(target,area,fps,nvenc,continuous,legacy,amf,qsv)){
    string planKey=plan.Device+plan.Name+"/"+area.Width+"x"+area.Height;bool incompatible;lock(probeLock)incompatible=incompatiblePlans.Contains(planKey);if(!legacy&&incompatible){failures.Add(plan.Name+": 本次运行已检测到不兼容，直接回退。");continue;}
    status(plan.Name+" · 正在启动…");string args="-hide_banner -y -nostats -loglevel warning -stats_period 0.25 -progress pipe:1 -filter_threads 2 -filter_complex_threads 2 "+plan.Device+plan.Input;
    int audioInput=string.IsNullOrEmpty(plan.Input)?0:1;if(microphone!=null)args+="-thread_queue_size 8 -f dshow -i "+Media.Q("audio="+microphone)+" ";
    if(plan.Filter.Length>0)args+="-filter_complex "+Media.Q(plan.Filter)+" -map [video] ";else args+="-map 0:v -vf pad=ceil(iw/2)*2:ceil(ih/2)*2 ";
    if(microphone!=null)args+="-map "+audioInput+":a -c:a aac ";
    // Commit complete fragments while recording, so an interrupted encoder leaves playable footage.
    args+=plan.Encoding+" -r "+fps+" -fps_mode cfr -g "+fps+" -flush_packets 1 -movflags +frag_keyframe+empty_moov+default_base_moof+negative_cts_offsets -frag_duration 500000 "+Media.Q(output);
    var session=new CaptureSession {Backend=plan.Name,HardwareEncoding=!plan.Encoding.Contains("libx264")};try{
     session.Process=Media.Start("ffmpeg",args,true);try{session.Process.PriorityClass=session.HardwareEncoding&&!legacy?ProcessPriorityClass.Normal:ProcessPriorityClass.BelowNormal;}catch{}session.Clock.Start();session.Errors=session.Process.StandardError.ReadToEndAsync();session.Progress=ReadProgress(session);
     var exited=Task.Run(()=>session.Process.WaitForExit());await Task.WhenAny(session.Ready.Task,exited,Task.Delay(8000));
     if(session.Ready.Task.IsCompleted&&!session.Process.HasExited){File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"capture-backend.txt"),plan.Name+"\n"+string.Join("\n",failures));return session;}
     if(!session.Process.HasExited){session.Process.Kill();await exited;}string error=await session.Errors;if(!legacy&&(error.Contains("AMFConverter-Init() failed")||error.Contains("no encode device")||error.Contains("No such filter")||error.Contains("Unknown encoder")))lock(probeLock)incompatiblePlans.Add(planKey);failures.Add(plan.Name+": "+error);File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"capture-attempts.txt"),string.Join("\n",failures));session.Process.Dispose();
    }catch(Exception ex){failures.Add(plan.Name+": "+ex.Message);if(session.Process!=null){try{if(!session.Process.HasExited)session.Process.Kill();}catch{}session.Process.Dispose();}}
   }
   throw new Exception("无法启动录制。\n"+string.Join("\n",failures));
  }
  public static async Task<bool> Finish(CaptureSession session,string output){
   bool interrupted=false;var process=session.Process;
   if(!process.HasExited){try{process.StandardInput.WriteLine("q");process.StandardInput.Flush();}catch(IOException){}catch(InvalidOperationException){}
    if(!await Task.Run(()=>process.WaitForExit(8000))){interrupted=true;process.Kill();await Task.Run(()=>process.WaitForExit());}}
   await session.Progress;string errors=await session.Errors;
   File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"capture-stop.txt"),(interrupted?"Interrupted encoder; validating committed fragments.":"Encoder exit code: "+process.ExitCode)+"\n"+errors);
   if(!interrupted&&process.ExitCode!=0)throw new Exception("录制编码失败，诊断和素材已保留在录制目录。\n"+errors);
   var imported=await Task.Run(()=>StudioMedia.Import(output));if(imported.Duration<=0||imported.Width<16||imported.Height<16)throw new Exception("录制文件没有可播放的画面，诊断已保留在录制目录。");
   return interrupted;
  }
  static async Task ReadProgress(CaptureSession session){string line;try{while((line=await session.Process.StandardOutput.ReadLineAsync())!=null){int at=line.IndexOf('=');if(at<0)continue;string key=line.Substring(0,at),value=line.Substring(at+1);int number;long microseconds;if(key=="frame"&&int.TryParse(value,out number))session.Frames=number;else if(key=="dup_frames"&&int.TryParse(value,out number))session.Duplicated=number;else if(key=="drop_frames"&&int.TryParse(value,out number))session.Dropped=number;else if(key=="out_time_us"&&long.TryParse(value,out microseconds))session.OutputSeconds=microseconds/1000000.0;else if(key=="progress"&&session.Frames>0&&session.OutputSeconds>0&&!session.Ready.Task.IsCompleted){session.StartupSeconds=Math.Max(0,session.Clock.Elapsed.TotalSeconds-session.OutputSeconds);session.Ready.TrySetResult(true);}}}catch{} }
 }
 static partial class CapturePerformanceTests {
  [DllImport("user32.dll")]static extern bool GetClientRect(IntPtr hwnd,out RECT rect);
  [DllImport("user32.dll")]static extern bool ClientToScreen(IntPtr hwnd,ref Native.Point point);
  [StructLayout(LayoutKind.Sequential)]struct RECT{public int Left,Top,Right,Bottom;}
  public static async Task VerifyInterruption(){
   string folder=Path.Combine(Media.Root,"bin","capture-recovery-test",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string output=Path.Combine(folder,"interrupted.mp4");var text=new TextBlock {Text="Synthetic capture recovery",FontSize=24,Margin=new Thickness(24)};var surface=new Window {Title="DogSC capture recovery verification",Width=440,Height=280,Background=UI.Brush("#CDE9E1"),Content=text,Topmost=true,WindowStartupLocation=WindowStartupLocation.CenterScreen};var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(16)};int frame=0;timer.Tick+=(s,e)=>text.Text="Synthetic capture recovery\n"+frame++;CaptureSession session=null;
   try{surface.Show();timer.Start();await Task.Delay(100);var hwnd=new WindowInteropHelper(surface).Handle;RECT rect;if(!GetClientRect(hwnd,out rect))throw new Exception("Test window bounds");var area=new D.Rectangle(0,0,(rect.Right-rect.Left)/2*2,(rect.Bottom-rect.Top)/2*2);session=await CaptureEngine.Start(hwnd,area,60,null,output,message=>{});await Task.Delay(1400);session.Process.Kill();await Task.Run(()=>session.Process.WaitForExit());await session.Progress;var imported=StudioMedia.Import(output);if(imported.Duration<.5||Math.Abs(imported.FrameRate-60)>.1)throw new Exception("Interrupted fragments are not readable at requested frame rate");using(var bytes=new MemoryStream(StudioMedia.Frame(output,.25,imported.Width)))using(var image=new D.Bitmap(bytes)){if(image.Width!=area.Width)throw new Exception("Interrupted fragment could not decode");}File.WriteAllText(Path.Combine(Media.Root,"bin","capture-recovery-results.txt"),"PASS: actual synthetic window capture forcibly terminated before MP4 finalization; committed fragments reopened and decoded at 60 FPS.\nBackend: "+session.Backend+"\nReadable seconds: "+imported.Duration+"\n");}
   finally{timer.Stop();surface.Close();if(session!=null){try{if(!session.Process.HasExited)session.Process.Kill();}catch{}session.Process.Dispose();}}
  }
  public static async Task Run(){
   string folder=Path.Combine(Media.Root,"bin","capture-performance-test",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);var canvas=new Canvas {Background=UI.Brush("#193E51")};var square=new Border {Width=120,Height=120,Background=UI.Brush("#C5F4DD")};canvas.Children.Add(square);var surface=new Window {Title="DogSC synthetic performance test",Width=Math.Min(1800,SystemParameters.WorkArea.Width),Height=Math.Min(1100,SystemParameters.WorkArea.Height),WindowStartupLocation=WindowStartupLocation.CenterScreen,WindowStyle=WindowStyle.None,Content=canvas,Topmost=true,ShowInTaskbar=false};
   var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(16)};var motion=Stopwatch.StartNew();timer.Tick+=(s,e)=>{Canvas.SetLeft(square,(Math.Sin(motion.Elapsed.TotalSeconds*2)+1)*Math.Max(0,canvas.ActualWidth-120)/2);Canvas.SetTop(square,(Math.Cos(motion.Elapsed.TotalSeconds*2)+1)*Math.Max(0,canvas.ActualHeight-120)/2);};
   try{surface.Show();timer.Start();await Task.Delay(200);var hwnd=new WindowInteropHelper(surface).Handle;RECT rect;if(!GetClientRect(hwnd,out rect))throw new Exception("Performance test window bounds");var area=new D.Rectangle(0,0,(rect.Right-rect.Left)/2*2,(rect.Bottom-rect.Top)/2*2);string oldOutput=Path.Combine(folder,"old.mp4"),newOutput=Path.Combine(folder,"new.mp4");double oldCpu,oldElapsed;
    using(var process=Media.Start("ffmpeg","-hide_banner -y -thread_queue_size 1024 -f gdigrab -framerate 60 -draw_mouse 0 -i "+Media.Q("hwnd="+hwnd.ToInt64())+" -vf pad=ceil(iw/2)*2:ceil(ih/2)*2 -c:v libx264 -preset ultrafast -crf 18 -pix_fmt yuv420p -r 60 -fps_mode cfr "+Media.Q(oldOutput),false)){
     var errors=process.StandardError.ReadToEndAsync();var watch=Stopwatch.StartNew();await Task.Delay(3500);process.StandardInput.WriteLine("q");process.StandardInput.Flush();await Task.Run(()=>process.WaitForExit());if(process.ExitCode!=0)throw new Exception(await errors);oldCpu=process.TotalProcessorTime.TotalSeconds;oldElapsed=watch.Elapsed.TotalSeconds;
    }
    var session=await CaptureEngine.Start(hwnd,area,60,null,newOutput,message=>{});try{await Task.Delay(3000);session.Process.StandardInput.WriteLine("q");session.Process.StandardInput.Flush();await Task.Run(()=>session.Process.WaitForExit());await session.Progress;if(session.Process.ExitCode!=0)throw new Exception(await session.Errors);
     var imported=StudioMedia.Import(newOutput);if(imported.Width!=area.Width||imported.Height!=area.Height||Math.Abs(imported.FrameRate-60)>.1)throw new Exception("Optimized capture resolution/frame rate changed");double newCpu=session.CpuSeconds,newElapsed=session.Clock.Elapsed.TotalSeconds;var report="PASS: synthetic window "+area.Width+"x"+area.Height+" at 60 FPS\nBackend: "+session.Backend+"\nOld CPU seconds per wall second: "+(oldCpu/oldElapsed).ToString("F3")+"\nNew CPU seconds per wall second: "+(newCpu/newElapsed).ToString("F3")+"\nDuplicate frames: "+session.Duplicated+"; dropped frames: "+session.Dropped+"\nCPU load comparison measures only the recording process, not overall desktop responsiveness.\n";File.WriteAllText(Path.Combine(Media.Root,"bin","capture-performance-results.txt"),report);
    }finally{if(!session.Process.HasExited)session.Process.Kill();session.Process.Dispose();}
    // Exercise the monitor/region path, cropped strictly inside this opaque test window.
    Native.Point origin=new Native.Point();ClientToScreen(hwnd,ref origin);var region=new D.Rectangle(origin.X+20,origin.Y+20,640,360);var cropped=await CaptureEngine.Start(IntPtr.Zero,region,60,null,Path.Combine(folder,"region.mp4"),message=>{});
    try{await Task.Delay(1500);cropped.Process.StandardInput.WriteLine("q");cropped.Process.StandardInput.Flush();await Task.Run(()=>cropped.Process.WaitForExit());if(cropped.Process.ExitCode!=0)throw new Exception(await cropped.Errors);var imported=StudioMedia.Import(Path.Combine(folder,"region.mp4"));if(imported.Width!=640||imported.Height!=360)throw new Exception("Monitor-region crop did not preserve its physical size");using(var data=new MemoryStream(StudioMedia.Frame(imported.Source,0,640)))using(var frame=new D.Bitmap(data)){var pixel=frame.GetPixel(5,5);if(Math.Abs(pixel.R-25)+Math.Abs(pixel.G-62)+Math.Abs(pixel.B-81)>30)throw new Exception("Monitor-region crop is offset from the selected synthetic surface");}File.AppendAllText(Path.Combine(Media.Root,"bin","capture-performance-results.txt"),"PASS: monitor/region path captured only selected 640x360 synthetic surface; "+cropped.Backend+".\n");}
    finally{if(!cropped.Process.HasExited)cropped.Process.Kill();cropped.Process.Dispose();}
   }finally{timer.Stop();surface.Close();}
  }
 }
}
