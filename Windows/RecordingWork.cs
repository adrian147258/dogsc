using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32.SafeHandles;
using DogSC.Windows;
using D=System.Drawing;

namespace DogSC.Replica {
 // A private, bounded cadence avoids WPF dispatcher stalls and never changes
 // the system-wide timer resolution. Pointer history is joined only on stop.
 sealed class PointerCaptureSampler:IDisposable {
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern SafeWaitHandle CreateWaitableTimerEx(IntPtr attributes,string name,uint flags,uint access);
  [DllImport("kernel32.dll")]static extern bool SetWaitableTimer(SafeWaitHandle timer,ref long due,int period,IntPtr completion,IntPtr context,bool resume);
  [DllImport("kernel32.dll")]static extern uint WaitForMultipleObjects(uint count,IntPtr[] handles,bool all,uint milliseconds);
  [DllImport("user32.dll")]static extern bool ClientToScreen(IntPtr window,ref Native.Point point);
  readonly IntPtr target;D.Rectangle area;readonly int fps,segment;readonly double committed;readonly CaptureSession session;readonly ManualResetEvent stop=new ManualResetEvent(false);readonly List<PointerSample> samples=new List<PointerSample>();Task worker;bool stopped,disposed;
  public int RawSamples,LateSamples;public Exception Failure;public string Timing;
  public PointerCaptureSampler(IntPtr target,D.Rectangle area,int fps,int segment,double committed,CaptureSession session){this.target=target;this.area=area;this.fps=Math.Min(90,Math.Max(30,fps));this.segment=segment;this.committed=committed;this.session=session;worker=Task.Factory.StartNew(Run,CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);}
  void Run(){try{using(var tick=CreateWaitableTimerEx(IntPtr.Zero,null,2,0x1F0003)){SafeWaitHandle timer=tick;if(tick==null||tick.IsInvalid){timer=CreateWaitableTimerEx(IntPtr.Zero,null,0,0x1F0003);Timing="普通时钟";}else Timing="高精度时钟";try{bool left=(Native.GetAsyncKeyState(1)&0x8000)!=0,right=(Native.GetAsyncKeyState(2)&0x8000)!=0;double next=Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency,step=1.0/fps;var handles=timer!=null&&!timer.IsInvalid?new[]{stop.SafeWaitHandle.DangerousGetHandle(),timer.DangerousGetHandle()}:null;
      while(!stop.WaitOne(0)){if(target!=IntPtr.Zero){var origin=new Native.Point();if(ClientToScreen(target,ref origin)){area.X=origin.X;area.Y=origin.Y;}}bool l=(Native.GetAsyncKeyState(1)&0x8000)!=0,r=(Native.GetAsyncKeyState(2)&0x8000)!=0;var point=NativeCursor.Sample(area,committed+Math.Max(0,session.Clock.Elapsed.TotalSeconds-session.StartupSeconds),(l&&!left)||(r&&!right));left=l;right=r;point.Segment=segment;RawSamples++;var last=samples.Count==0?null:samples[samples.Count-1];if(last==null||point.Click||last.Click||point.X!=last.X||point.Y!=last.Y||point.Visible!=last.Visible||point.CursorKind!=last.CursorKind||point.Time-last.Time>=.25)samples.Add(point);
       next+=step;double now=Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;if(now>next+step){LateSamples++;next=now+step;}long due=-(long)Math.Max(1,(next-now)*10000000);if(timer!=null&&!timer.IsInvalid&&SetWaitableTimer(timer,ref due,0,IntPtr.Zero,IntPtr.Zero,false)){if(WaitForMultipleObjects(2,handles,false,1000)==0)break;}else if(stop.WaitOne(Math.Max(1,(int)((next-now)*1000))))break;
      }
     }finally{if(timer!=null&&timer!=tick)timer.Dispose();}}}catch(Exception ex){Failure=ex;}}
  public List<PointerSample> Stop(){if(!stopped){stopped=true;stop.Set();worker.GetAwaiter().GetResult();}return samples;}
  public void Dispose(){if(disposed)return;Stop();stop.Dispose();disposed=true;}
 }
 public partial class Studio {
  bool recordingWorkSuspended;CancellationTokenSource previewWork,thumbnailWork;int thumbnailJobs,playbackJobs;
  public async Task SuspendForRecording(){recordingWorkSuspended=true;renderVersion++;if(previewWork!=null)previewWork.Cancel();if(thumbnailWork!=null)thumbnailWork.Cancel();Pause();while(rendering||thumbnailJobs>0||playbackJobs>0)await Task.Delay(16);}
  public void ResumeAfterRecording(bool refresh=true){recordingWorkSuspended=false;if(refresh&&Doc!=null){RefreshPreview();if(thumbs.Count<8||Doc.Audio&&waveform==null)LoadThumbnails();}}
 }
 public partial class RecorderWindow {
  PointerCaptureSampler pointerSampler;bool fallbackPointerSampling,fallbackLeft,fallbackRight;
  void StartPointerSampling(){StopPointerSampling();fallbackPointerSampling=false;pointerSampler=new PointerCaptureSampler(target,area,CaptureRate,segmentPointStart,take.CommittedSeconds,captureSession);timer.Interval=TimeSpan.FromMilliseconds(250);timer.Start();}
  void CheckPointerSampling(){if(pointerSampler!=null&&pointerSampler.Failure!=null){StopPointerSampling();fallbackPointerSampling=true;fallbackLeft=(Native.GetAsyncKeyState(1)&0x8000)!=0;fallbackRight=(Native.GetAsyncKeyState(2)&0x8000)!=0;timer.Interval=TimeSpan.FromMilliseconds(1000.0/Math.Min(60,CaptureRate));}if(!fallbackPointerSampling)return;if(target!=IntPtr.Zero){var origin=new Native.Point();if(ClientToScreen(target,ref origin)){area.X=origin.X;area.Y=origin.Y;}}bool l=(Native.GetAsyncKeyState(1)&0x8000)!=0,r=(Native.GetAsyncKeyState(2)&0x8000)!=0;var point=NativeCursor.Sample(area,take.Seconds,l&&!fallbackLeft||r&&!fallbackRight);point.Segment=segmentPointStart;points.Add(point);fallbackLeft=l;fallbackRight=r;}
  void StopPointerSampling(){if(pointerSampler==null)return;var sampler=pointerSampler;pointerSampler=null;var samples=sampler.Stop();points.AddRange(samples);try{if(sourcePath!=null)System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(sourcePath),"pointer-performance.txt"),"Samples "+sampler.RawSamples+"; stored "+samples.Count+"; late "+sampler.LateSamples+"; "+sampler.Timing+"; "+(sampler.Failure==null?"OK":sampler.Failure.Message)+"\n");}catch{}finally{sampler.Dispose();}}
 }
}
