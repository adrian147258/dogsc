using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DogSC.Windows;
using D=System.Drawing;

namespace DogSC.Replica {
 // Each pause closes the encoder, including microphone input. No paused frames or audio are retained.
 sealed class RecordingTake:IDisposable {
  [DllImport("user32.dll")]static extern bool GetClientRect(IntPtr hwnd,out RECT rect);
  [StructLayout(LayoutKind.Sequential)]struct RECT{public int Left,Top,Right,Bottom;}
  readonly IntPtr target;readonly D.Rectangle area;readonly int fps;readonly string microphone,output;
  readonly List<string> segments=new List<string>();readonly StringBuilder diagnostics=new StringBuilder();
  int next;public CaptureSession Current {get;private set;}public double CommittedSeconds {get;private set;}public bool Recovered {get;private set;}
  public bool IsPaused {get{return Current==null;}}public string Backend {get;private set;}
  public double Seconds {get{return CommittedSeconds+(Current==null?0:Math.Max(0,Current.Clock.Elapsed.TotalSeconds-Current.StartupSeconds));}}
  public RecordingTake(IntPtr target,D.Rectangle area,int fps,string microphone,string output){this.target=target;this.area=area;this.fps=fps;this.microphone=microphone;this.output=output;Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(output),"segments"));}
  public async Task Resume(Action<string> status){if(Current!=null)throw new InvalidOperationException("录制正在进行。");if(target!=IntPtr.Zero&&segments.Count>0){RECT r;if(!GetClientRect(target,out r))throw new Exception("选定窗口已关闭，已录片段仍可停止保存。");int w=r.Right-r.Left,h=r.Bottom-r.Top;if(w-w%2!=area.Width||h-h%2!=area.Height)throw new Exception("选定窗口尺寸已改变。请恢复原尺寸后继续，或停止保存已录片段。");}string part=Path.Combine(Path.GetDirectoryName(output),"segments","segment"+(next++).ToString("000")+".mp4");Current=await CaptureEngine.Start(target,area,fps,microphone,part,status,true);Backend=Current.Backend;File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"capture-backend.txt"),Backend);}
  public async Task Pause(){if(Current==null)return;var session=Current;string part=Path.Combine(Path.GetDirectoryName(output),"segments","segment"+(next-1).ToString("000")+".mp4");bool recovered=await CaptureEngine.Finish(session,part);session.Clock.Stop();var doc=await Task.Run(()=>StudioMedia.Import(part));
   if(segments.Count>0){var first=await Task.Run(()=>StudioMedia.Import(segments[0]));if(first.Width!=doc.Width||first.Height!=doc.Height||first.Audio!=doc.Audio||Math.Abs(first.FrameRate-doc.FrameRate)>.1)throw new Exception("录制片段格式发生变化。已保留所有片段，请停止后检查素材目录。");}
   double seconds=await Task.Run(()=>VideoSeconds(part));diagnostics.AppendLine("Segment "+segments.Count+": "+session.Backend+"; "+seconds.ToString("F6",CultureInfo.InvariantCulture)+" seconds; frames "+session.Frames+"; duplicated "+session.Duplicated+"; dropped "+session.Dropped+"; CPU seconds "+session.CpuSeconds.ToString("F3",CultureInfo.InvariantCulture)+"; recovered "+recovered);segments.Add(part);CommittedSeconds+=seconds;Recovered|=recovered;Current=null;session.Process.Dispose();File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"capture-performance.txt"),diagnostics.ToString());
  }
  public async Task Finish(){await Pause();if(segments.Count==0)throw new Exception("没有可保存的录制片段。");
   // A new output transaction leaves every committed segment available if concatenation is interrupted.
   string pending=Path.Combine(Path.GetDirectoryName(output),"assembled.tmp.mp4"),list=Path.Combine(Path.GetDirectoryName(output),"segments.ffconcat");
   if(File.Exists(pending))File.Delete(pending);
   await Assemble(segments,pending,list);
   var doc=await Task.Run(()=>StudioMedia.Import(pending));if(Math.Abs(doc.Duration-CommittedSeconds)>Math.Max(.15,segments.Count*.05))throw new Exception("暂停片段合并时长异常，原始片段已保留。");if(File.Exists(output))throw new Exception("最终素材路径已存在，请检查录制目录。");File.Move(pending,output);
  }
  internal static double VideoSeconds(string file){using(var p=Media.Start("ffprobe","-v error -select_streams v:0 -show_entries stream=duration -of default=noprint_wrappers=1:nokey=1 "+Media.Q(file),true)){var errors=p.StandardError.ReadToEndAsync();string text=p.StandardOutput.ReadToEnd();p.WaitForExit();double duration;if(p.ExitCode!=0||!double.TryParse(text.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out duration)||!EditDocument.Finite(duration)||duration<=0)throw new Exception("无法读取录制片段的画面时长。"+errors.Result);return duration;}}
  internal static async Task Assemble(IList<string> files,string output,string list){var seconds=await Task.Run(()=>files.Select(VideoSeconds).ToArray());string entries="ffconcat version 1.0\n"+string.Join("\n",files.Select((p,i)=>"file '"+p.Replace('\\','/').Replace("'","'\\''")+"'\nduration "+Media.N(seconds[i])));File.WriteAllText(list,entries,new UTF8Encoding(false));
   // Explicit video durations exclude AAC padding from every pause boundary. Reconcile audio timestamps once.
   await Task.Run(()=>StudioMedia.Run("-v warning -y -f concat -safe 0 -i "+Media.Q(list)+" -map 0:v -map 0:a? -c:v copy -c:a aac -af aresample=async=1:first_pts=0 -t "+Media.N(seconds.Sum())+" -movflags +faststart "+Media.Q(output),CancellationToken.None));}
  public void Dispose(){if(Current==null)return;var p=Current.Process;try{if(!p.HasExited)p.Kill();p.WaitForExit(3000);}catch{}p.Dispose();Current=null;}
 }
 public partial class RecorderWindow {
  RecordingTake take;System.Windows.Controls.Button pauseButton;int segmentPointStart;
  void UpdatePauseButton(){bool paused=take!=null&&take.IsPaused;pauseButton.Visibility=take==null?Visibility.Collapsed:Visibility.Visible;pauseButton.IsEnabled=take!=null&&!busy;pauseButton.Content=UI.IconLabel(paused?"play":"pause",paused?"继续":"暂停",12);pauseButton.Background=UI.Brush(paused?"#DEF2E8":"#FFFFFF");pauseButton.BorderBrush=UI.Brush(paused?"#BDDDCD":"#E8EAEC");pauseButton.Foreground=UI.Brush(paused?"#315D48":"#343638");pauseButton.ToolTip=paused?"继续录制，暂停期间不计入视频":"暂停画面、麦克风与鼠标轨迹采集";if(state!=null)state.Foreground=UI.Brush(take!=null?"#5C6863":"#858A8E");}
  void BeginSegment(){captureSession=take.Current;capture=captureSession.Process;watch=captureSession.Clock;segmentPointStart=points.Count;if(target!=IntPtr.Zero){Native.Point origin=new Native.Point();if(ClientToScreen(target,ref origin)){area.X=origin.X;area.Y=origin.Y;}}var p=NativeCursor.Sample(area,take.CommittedSeconds,false);p.Segment=segmentPointStart;points.Add(p);lastStatus=-1;StartPointerSampling();}
  void CommitPointerSegment(){double end=take.CommittedSeconds;for(int i=segmentPointStart;i<points.Count;i++){points[i].Time=Math.Min(points[i].Time,Math.Max(0,end-.00001));points[i].Segment=segmentPointStart;}}
  async Task ChangePause(){if(take==null||busy)return;busy=true;timer.Stop();StopPointerSampling();start.IsEnabled=false;UpdatePauseButton();
   try{if(!take.IsPaused){state.Text="正在暂停…";await take.Pause();CommitPointerSegment();capture=null;captureSession=null;state.Text="已暂停 · "+UI.Time(take.CommittedSeconds)+" · 点击继续或停止保存";}
    else {state.Text="正在继续…";await take.Resume(message=>state.Text=message);BeginSegment();}}
   catch(Exception ex){state.Text=take.IsPaused?"继续失败 · 已录片段已保留，可停止保存":"暂停失败 · 请停止保存";if(!take.IsPaused){capture=take.Current.Process;captureSession=take.Current;StartPointerSampling();}if(testingCapture)throw;AppDialogs.Notice(this,take.IsPaused?"无法继续录制":"无法暂停录制",ex.Message+"\n素材目录："+Path.GetDirectoryName(sourcePath));}
   finally{busy=false;start.IsEnabled=true;UpdatePauseButton();}
  }
  async void TogglePause(){await ChangePause();}
 }
}
