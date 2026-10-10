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
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DogSC.Windows;
using D=System.Drawing;
using Path=System.IO.Path;

namespace DogSC.Replica {
 public partial class ExportWindow:Window {
  EditDocument doc;StackPanel panel;TextBlock summary,status;ProgressBar progress;Button start,cancel;
  int outputHeight=1080,outputFps=30;CancellationTokenSource cancellation;bool done;TextBox fileName;string outputFolder;
  public ExportWindow(Studio studio,EditDocument document,ImageSource preview){InitializeExportSurface(studio,document,preview);}
  void bump(Panel p,string selected){SelectExportChoice(p,selected);}
  void UpdateSummary(){UpdateExportSummary();}
  async void BeginExport(){if(done){Close();return;}string name=fileName.Text.Trim();if(string.IsNullOrWhiteSpace(name)||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0){MessageBox.Show(this,"请输入有效的文件名。","DogSC");return;}if(!name.EndsWith(".mp4",StringComparison.OrdinalIgnoreCase))name+=".mp4";string output=Path.Combine(outputFolder,name);if(File.Exists(output)){MessageBox.Show(this,"请选择新文件名，以保留已有视频。","DogSC");return;}
   cancellation=new CancellationTokenSource();progress.Visibility=Visibility.Visible;start.IsEnabled=false;panel.IsEnabled=false;cancel.Content="取消导出";var watch=Stopwatch.StartNew();
   try{await Task.Run(()=>StudioRenderer.Export(doc,output,outputHeight,outputFps,(fraction,stage)=>Dispatcher.BeginInvoke(new Action(()=>{progress.Value=fraction*100;status.Text=stage+" · "+(fraction*100).ToString("0")+"% · 已用 "+UI.Time(watch.Elapsed.TotalSeconds);})),cancellation.Token));done=true;status.Text="已导出到 "+output;start.Content="完成";cancel.Content="打开文件夹";cancel.Click+=(s,e)=>Process.Start("explorer.exe","/select,"+Media.Q(output));}
   catch(OperationCanceledException){status.Text="已取消；项目和原始素材已保留。";cancel.Content="取消";}
   catch(Exception ex){status.Text="导出失败";MessageBox.Show(this,ex.Message,"DogSC",MessageBoxButton.OK,MessageBoxImage.Error);}
   finally{cancellation.Dispose();cancellation=null;start.IsEnabled=true;panel.IsEnabled=true;}
  }
 }
 class WindowSource {public IntPtr Handle;public string Title;public override string ToString(){return Title;}}
 public partial class RecorderWindow:Window {
  [DllImport("user32.dll")] static extern bool SetWindowDisplayAffinity(IntPtr window,uint affinity);
  delegate bool EnumProc(IntPtr hwnd,IntPtr state);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc,IntPtr state);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd,StringBuilder text,int count);
  [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hwnd,out RECT rect);
  [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr hwnd,ref Native.Point point);
  [StructLayout(LayoutKind.Sequential)] struct RECT{public int Left,Top,Right,Bottom;}
  Studio studio;StackPanel options;ComboBox sources,frameRates;TextBlock state;Button start,mic,memoButton;readonly DispatcherTimer timer=new DispatcherTimer();
  static readonly string ratePreference=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DogSC.Windows","recording-fps.txt");bool testingCapture;
  int CaptureRate {get{return (int)frameRates.SelectedItem;}}
  static int PreferredRate(){int rate;try{if(int.TryParse(File.ReadAllText(ratePreference),out rate)&&new[]{30,60,90}.Contains(rate))return rate;}catch{}return 60;}
  string mode="显示器",microphone;D.Rectangle area;Process capture;CaptureSession captureSession;Task<string> error;Stopwatch watch=new Stopwatch();double lastStatus;List<PointerSample> points=new List<PointerSample>();bool left,right,busy;string sourcePath;IntPtr target;
  Task stopTask;Exception recordingFailure;
  public bool IsRecording{get{return capture!=null||busy;}}
  public RecorderWindow(Studio host){InitializeRecorderSurface(host);}
  void UpdateMemoButton(){memoButton.Background=UI.Brush(studio.Memo.Visible?"#D6F2E6":"#FFFFFF");memoButton.ToolTip=studio.Memo.Visible?"收起备忘录":"打开备忘录";}
  string RecordFolder(){string path=Path.Combine(Media.Root,"recordings");Directory.CreateDirectory(path);return path;}
  void SelectMode(string name){int previous=Math.Max(0,sources.SelectedIndex);mode=name;sources.Items.Clear();sources.DisplayMemberPath="";target=IntPtr.Zero;
   if(name=="窗口"){EnumWindows((handle,state)=>{if(!IsWindowVisible(handle))return true;var text=new StringBuilder(512);GetWindowText(handle,text,512);string title=text.ToString();if(!string.IsNullOrWhiteSpace(title)&&!title.StartsWith("DogSC"))sources.Items.Add(new WindowSource {Handle=handle,Title=title});return true;},IntPtr.Zero);}
   else {foreach(var screen in System.Windows.Forms.Screen.AllScreens)sources.Items.Add(screen.DeviceName+" · "+screen.Bounds.Width+" × "+screen.Bounds.Height);}
   if(sources.Items.Count>0)sources.SelectedIndex=Math.Min(previous,sources.Items.Count-1);if(name=="区域"){studio.Memo.SuspendSelection();Hide();try{using(var selection=new RegionSelector(System.Windows.Forms.Screen.AllScreens[Math.Min(previous,System.Windows.Forms.Screen.AllScreens.Length-1)].Bounds)){if(selection.ShowDialog()==System.Windows.Forms.DialogResult.OK){area=selection.Selection;state.Text="录制区域 "+area.Width+" × "+area.Height;}else mode="显示器";}}finally{Show();studio.Memo.ResumeSelection(this);}}UpdateRecordingModes();
  }
  async void Microphone(){state.Text="正在查找麦克风…";try{string text=await Task.Run(()=>{using(var p=Media.Start("ffmpeg","-hide_banner -list_devices true -f dshow -i dummy",false)){var result=p.StandardError.ReadToEnd();p.WaitForExit();return result;}});var menu=new ContextMenu();var off=new MenuItem {Header="关闭麦克风"};off.Click+=(s,e)=>{microphone=null;UI.Orb(mic,"mic",false);};menu.Items.Add(off);foreach(string line in text.Split('\n')){if(!line.Contains("(audio)"))continue;int begin=line.IndexOf('"'),end=line.LastIndexOf('"');if(begin<0||end<=begin)continue;string name=line.Substring(begin+1,end-begin-1);var item=new MenuItem {Header=name};item.Click+=(s,e)=>{microphone=name;UI.Orb(mic,"mic",true);mic.ToolTip=name;};menu.Items.Add(item);}if(menu.Items.Count==1)state.Text="未找到可用的 DirectShow 麦克风。";else state.Text="请选择麦克风";menu.PlacementTarget=mic;menu.IsOpen=true;}catch(Exception ex){state.Text=ex.Message;}}
  async void ToggleRecording(){if(busy)return;if(capture!=null){await Stop();return;}busy=true;start.IsEnabled=false;options.IsEnabled=false;sources.IsEnabled=false;frameRates.IsEnabled=false;
   try{
    recordingFailure=null;stopTask=null;Media.Tool("ffmpeg");Media.Tool("ffprobe");string input="desktop";
    if(mode=="窗口"){var window=sources.SelectedItem as WindowSource;if(window==null)throw new Exception("请选择一个窗口。");target=window.Handle;RECT r;if(!GetClientRect(target,out r))throw new Exception("选定窗口已关闭。");Native.Point p=new Native.Point();ClientToScreen(target,ref p);area=new D.Rectangle(p.X,p.Y,r.Right-r.Left,r.Bottom-r.Top);input="hwnd="+target.ToInt64();}
    else if(mode!="区域")area=System.Windows.Forms.Screen.AllScreens[Math.Max(0,sources.SelectedIndex)].Bounds;
    area.Width-=area.Width%2;area.Height-=area.Height%2;if(area.Width<16||area.Height<16)throw new Exception("录制范围过小。");
    var folder=Path.Combine(RecordFolder(),DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));Directory.CreateDirectory(folder);sourcePath=Path.Combine(folder,"source.mp4");points=new List<PointerSample>();
    for(int seconds=3;seconds>0;seconds--){state.Text=seconds+" 秒后开始录制";await Task.Delay(1000);}
    studio.Memo.BeginCapture();captureSession=await CaptureEngine.Start(target,area,CaptureRate,microphone,sourcePath,message=>state.Text=message);capture=captureSession.Process;error=captureSession.Errors;watch=captureSession.Clock;lastStatus=-1;state.ToolTip=captureSession.Backend;left=(Native.GetAsyncKeyState(1)&0x8000)!=0;right=(Native.GetAsyncKeyState(2)&0x8000)!=0;points.Add(NativeCursor.Sample(area,0,false));timer.Interval=TimeSpan.FromMilliseconds(1000.0/Math.Min(60,CaptureRate));timer.Start();start.Content=UI.IconLabel("stop","停止录制",12);start.Background=UI.Brush("#DE5E57");
   }catch(Exception ex){recordingFailure=ex;studio.Memo.EndCapture(this);state.Text="录制未启动";if(!testingCapture)MessageBox.Show(this,ex.Message,"DogSC");options.IsEnabled=true;sources.IsEnabled=true;frameRates.IsEnabled=true;}finally{busy=false;start.IsEnabled=true;}
  }
  async void Sample(){if(capture==null)return;if(capture.HasExited){await Stop();return;}
   if(target!=IntPtr.Zero){Native.Point origin=new Native.Point();if(ClientToScreen(target,ref origin)){area.X=origin.X;area.Y=origin.Y;}}
   bool l=(Native.GetAsyncKeyState(1)&0x8000)!=0,r=(Native.GetAsyncKeyState(2)&0x8000)!=0;points.Add(NativeCursor.Sample(area,Math.Max(0,watch.Elapsed.TotalSeconds-captureSession.StartupSeconds),(l&&!left)||(r&&!right)));left=l;right=r;
   if(watch.Elapsed.TotalSeconds-lastStatus>=.25){lastStatus=watch.Elapsed.TotalSeconds;state.Text="●  "+UI.Time(lastStatus)+" · "+CaptureRate+" FPS · "+(captureSession.Backend.Contains("NVIDIA")?"显卡编码":"CPU 编码");}
  }
  public async Task VerifyCapture(int rate=60){testingCapture=true;frameRates.SelectedItem=rate;
   var surface=new Window {Title="Windows Capture Verification",Width=440,Height=280,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=UI.Brush("#CDE9E1"),Topmost=true,Content=new TextBlock {Text="DogSC Windows\n录屏接口测试",FontSize=26,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Foreground=UI.Brush("#286054")}};
   var animation=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(16)};int animationFrame=0;animation.Tick+=(s,e)=>((TextBlock)surface.Content).Text="DogSC Windows\n录屏接口测试 "+animationFrame++;
   try{surface.Show();animation.Start();Show();SelectMode("窗口");var option=sources.Items.Cast<WindowSource>().FirstOrDefault(w=>w.Title==surface.Title);if(option==null)throw new Exception("未找到测试窗口");sources.SelectedItem=option;ToggleRecording();var deadline=Stopwatch.StartNew();while((capture==null||busy)&&recordingFailure==null&&deadline.Elapsed.TotalSeconds<45)await Task.Delay(100);if(recordingFailure!=null)throw recordingFailure;if(capture==null||busy)throw new Exception("未能启动窗口录制");if(frameRates.IsEnabled)throw new Exception("录制过程中允许更改帧率");await Task.Delay(1200);await Stop();if(recordingFailure!=null)throw recordingFailure;if(!File.Exists(sourcePath))throw new Exception("窗口录制素材不存在");var imported=StudioMedia.Import(sourcePath);if(imported.Duration<.5||Math.Abs(imported.FrameRate-rate)>.1)throw new Exception("窗口录制帧率不符合选择："+imported.FrameRate+" / "+rate);}
   finally{animation.Stop();surface.Close();}
  }
  Task Stop(){if(stopTask!=null)return stopTask;if(capture==null||busy)return Task.FromResult(false);stopTask=StopCore();return stopTask;}
  async Task StopCore(){busy=true;timer.Stop();var process=capture;start.IsEnabled=false;state.Text="正在保存素材…";
   try{bool recovered=await CaptureEngine.Finish(captureSession,sourcePath);File.WriteAllText(Path.Combine(Path.GetDirectoryName(sourcePath),"capture-performance.txt"),"Backend: "+captureSession.Backend+"\nTarget FPS: "+CaptureRate+"\nFrames: "+captureSession.Frames+"\nDuplicate frames: "+captureSession.Duplicated+"\nDropped frames: "+captureSession.Dropped+"\nRecovered fragments: "+recovered+"\nCPU seconds: "+captureSession.CpuSeconds.ToString("F3",CultureInfo.InvariantCulture)+"\nElapsed seconds: "+watch.Elapsed.TotalSeconds.ToString("F3",CultureInfo.InvariantCulture));capture=null;studio.Memo.EndCapture(this);Hide();studio.Window.Show();studio.Window.Activate();await studio.Recorded(sourcePath,points);busy=false;Close();}
   catch(Exception ex){recordingFailure=ex;capture=null;studio.Memo.EndCapture(this);state.Text="保存失败 · 素材和诊断已保留";state.ToolTip=Path.GetDirectoryName(sourcePath);File.WriteAllText(Path.Combine(Path.GetDirectoryName(sourcePath),"capture-save-error.txt"),ex.ToString());if(!testingCapture)MessageBox.Show(this,ex.Message+"\n素材目录："+Path.GetDirectoryName(sourcePath),"DogSC");options.IsEnabled=true;sources.IsEnabled=true;frameRates.IsEnabled=true;start.Content=UI.IconLabel("record","开始录制",12);start.Background=UI.Brush("#343638");}finally{process.Dispose();busy=false;start.IsEnabled=true;}
  }
 }
 static class ReplicaProgram {
  [STAThread] public static void Main(string[] args){
   try{Native.SetProcessDPIAware();if(args.Contains("--editor-test")){EditorTests.Run();return;}if(args.Contains("--smoke-test")){SmokeTest.Run();return;}
    var app=new Application();app.ShutdownMode=ShutdownMode.OnMainWindowClose;app.DispatcherUnhandledException+=(s,e)=>{File.WriteAllText(Path.Combine(Media.Root,"bin","last-error.txt"),e.Exception.ToString());MessageBox.Show(e.Exception.Message,"DogSC");e.Handled=true;};
    bool memoTest=args.Contains("--memo-test");string testFolder=Path.Combine(Media.Root,"bin","memo-test",Guid.NewGuid().ToString("N"));
    var studio=new Studio(memoTest?Path.Combine(testFolder,"memo.json"):null);app.MainWindow=studio.Window;
    if(args.Contains("--effects-test"))studio.Window.Loaded+=async(s,e)=>{string folder=Path.Combine(Media.Root,"bin","effects-test",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string log=Path.Combine(Media.Root,"bin","effects-results.txt");try{await Task.Run(()=>EffectsTests.Run(folder));await studio.VerifyEffectsInterface(folder);File.WriteAllText(log,File.ReadAllText(Path.Combine(folder,"core-results.txt"))+File.ReadAllText(Path.Combine(folder,"ui-results.txt")));}catch(Exception ex){File.WriteAllText(log,"FAIL\n"+ex);Environment.ExitCode=1;}app.Shutdown();};
    else if(args.Contains("--capture-recovery-test"))studio.Window.Loaded+=async(s,e)=>{try{await CapturePerformanceTests.VerifyInterruption();}catch(Exception ex){File.WriteAllText(Path.Combine(Media.Root,"bin","capture-recovery-results.txt"),"FAIL\n"+ex);Environment.ExitCode=1;}app.Shutdown();};
    else if(args.Contains("--capture-perf-test"))studio.Window.Loaded+=async(s,e)=>{try{await CapturePerformanceTests.Run();}catch(Exception ex){File.WriteAllText(Path.Combine(Media.Root,"bin","capture-performance-results.txt"),"FAIL\n"+ex);Environment.ExitCode=1;}app.Shutdown();};
    else if(memoTest)studio.Window.Loaded+=async(s,e)=>{string log=Path.Combine(Media.Root,"bin","memo-results.txt");try{await MemoTests.Run(studio,testFolder);File.WriteAllText(log,"PASS: memo persistence, editing/reading, typography, opacity, bounds, selection suspension, capture exclusion and restore\n");}catch(Exception ex){File.WriteAllText(log,"FAIL\n"+ex);Environment.ExitCode=1;}finally{studio.Memo.Shutdown();app.Shutdown();}};
    else if(args.Contains("--fps-test"))studio.Window.Loaded+=async(s,e)=>{string folder=Path.Combine(Media.Root,"bin","fps-test",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string log=Path.Combine(Media.Root,"bin","fps-results.txt");try{var report=new StringBuilder();foreach(int rate in new[]{30,60,90}){var recorder=new RecorderWindow(studio);await recorder.VerifyCapture(rate);if(Math.Abs(studio.Doc.FrameRate-rate)>.1)throw new Exception("项目未保留录制帧率");var doc=EditDocument.Deserialize(studio.Doc.Serialize());doc.Cuts=new List<Cut>{new Cut {Start=0,End=Math.Min(.6,doc.Duration)}};doc.Zooms.Clear();string output=Path.Combine(folder,"export-"+rate+".mp4");await Task.Run(()=>StudioRenderer.Export(doc,output,720,rate,(v,t)=>{},CancellationToken.None));if(Math.Abs(StudioMedia.Import(output).FrameRate-rate)>.1)throw new Exception("导出帧率不符");report.AppendLine("PASS: "+rate+" FPS recorded, imported, project saved and exported at requested frame rate.");}File.WriteAllText(log,report.ToString());}catch(Exception ex){File.WriteAllText(log,"FAIL\n"+ex);Environment.ExitCode=1;}app.Shutdown();};
    else if(args.Contains("--ui-test"))studio.Window.Loaded+=async(s,e)=>{try{await studio.VerifyInterface();}catch(Exception ex){File.WriteAllText(Path.Combine(Media.Root,"bin","editor-test","interface-results.txt"),"FAIL\n"+ex);Environment.ExitCode=1;}app.Shutdown();};
    else if(args.Contains("--snapshot"))studio.Window.Loaded+=async(s,e)=>{string demo=Path.Combine(Media.Root,"bin","editor-test","project.dogscwin");if(File.Exists(demo))await studio.Load(demo);await studio.PreviewNow();await Task.Delay(1500);UI.SaveImage((FrameworkElement)studio.Window.Content,Path.Combine(Media.Root,"bin","editor-preview.png"));app.Shutdown();};
    else if(args.Length>0&&File.Exists(args[0]))studio.Window.Loaded+=async(s,e)=>await studio.Load(args[0]);
    app.Run(studio.Window);
   }catch(Exception ex){Directory.CreateDirectory(Path.Combine(Media.Root,"bin"));File.WriteAllText(Path.Combine(Media.Root,"bin","last-error.txt"),ex.ToString());Environment.ExitCode=1;}
  }
 }
 static class EditorTests {
  public static void Run(){string folder=Path.Combine(Media.Root,"bin","editor-test");Directory.CreateDirectory(folder);string log=Path.Combine(folder,"results.txt");try{
   string source=Path.Combine(folder,"demo.mp4"),image=Path.GetFullPath(Path.Combine(Media.Root,"../Resources/Onboarding/SoftLight.png"));
   StudioMedia.Run("-v error -y -loop 1 -i "+Media.Q(image)+" -f lavfi -i sine=frequency=440:sample_rate=48000 -t 5 -vf scale=960:540:force_original_aspect_ratio=increase,crop=960:540 -c:v libx264 -preset ultrafast -pix_fmt yuv420p -c:a aac "+Media.Q(source),CancellationToken.None);
   var doc=StudioMedia.Import(source);doc.Name="画面示例 · Windows 适配";doc.Pointer.Add(new PointerSample {Time=.5,X=.35,Y=.5,Click=true});doc.Pointer.Add(new PointerSample {Time=1,X=.4,Y=.55});doc.Pointer.Add(new PointerSample {Time=1.8,X=.65,Y=.5,Click=true});doc.GenerateZooms();
   doc.Cuts=new List<Cut>{new Cut {Start=0,End=2},new Cut {Start=2,End=3},new Cut {Start=3,End=5}};doc.Save(Path.Combine(folder,"project.dogscwin"));
   int segment;Assert(Math.Abs(doc.SourceTime(3.5,out segment)-3.5)<.001&&segment==2,"timeline source mapping");
   var pose=new MotionEvaluator(doc).At(1);Assert(pose.Zoom>1,"auto zoom");var p2=new MotionEvaluator(doc).At(1);Assert(pose.X==p2.X&&pose.Zoom==p2.Zoom,"deterministic seeking");
   doc.Cuts=new List<Cut>{new Cut {Start=3,End=5,Speed=2},new Cut {Start=0,End=1}};doc.GenerateZooms();Assert(Math.Abs(doc.Length-2)<.001,"speed and reorder duration");Assert(Math.Abs(doc.SourceTime(.5,out segment)-4)<.001,"speed source mapping");
   string output=Path.Combine(folder,"composition-"+DateTime.Now.ToString("HHmmss")+".mp4");StudioRenderer.Export(doc,output,720,30,(v,s)=>{},CancellationToken.None);var result=StudioMedia.Import(output);Assert(Math.Abs(result.Duration-2)<.15&&result.Audio&&result.Width==1280&&result.Height==720,"composition export with trimmed reordered audio");
   string cancel=Path.Combine(folder,"cancel-"+Guid.NewGuid().ToString("N")+".mp4");using(var c=new CancellationTokenSource()){c.Cancel();try{StudioRenderer.Export(doc,cancel,720,30,(v,s)=>{},c.Token);throw new Exception("cancel not honored");}catch(OperationCanceledException){Assert(!File.Exists(cancel),"cancellation cleanup");}}
   doc.Style.Ratio="9:16";doc.Style.Padding=30;var layout=SceneLayout.Make(doc,608,1080,new Pose());Assert(layout.Frame.Width<=608&&layout.Frame.Height<=1080,"portrait layout");
   var portraitSize=SceneLayout.OutputSize(doc,1080);Assert(portraitSize[0]==1080&&portraitSize[1]==1920,"portrait 1080p resolution");
   Assert(MotionEvaluator.Ease(0,"spring")==0&&MotionEvaluator.Ease(1,"spring")==1&&MotionEvaluator.Ease(.5,"spring")>.5,"ported spring endpoints");
   string silent=Path.Combine(folder,"silent.mp4");StudioMedia.Run("-v error -y -i "+Media.Q(source)+" -an -c:v copy "+Media.Q(silent),CancellationToken.None);var shortDoc=StudioMedia.Import(silent);shortDoc.Cuts[0].End=.2;shortDoc.Style.Background="image";shortDoc.Style.ImagePath=image;shortDoc.Style.Opening=.2;
   string high=Path.Combine(folder,"1080p60-"+DateTime.Now.ToString("HHmmss")+".mp4");StudioRenderer.Export(shortDoc,high,1080,60,(v,s)=>{},CancellationToken.None);var highInfo=StudioMedia.Import(high);Assert(highInfo.Width==1920&&highInfo.Height==1080&&!highInfo.Audio,"1080p silent export");
   using(var probe=Media.Start("ffprobe","-v error -select_streams v:0 -show_entries stream=avg_frame_rate -of default=nw=1:nk=1 "+Media.Q(high),true)){var errors=probe.StandardError.ReadToEndAsync();string rate=probe.StandardOutput.ReadToEnd();probe.WaitForExit();Assert(rate.Trim()=="60/1","60 FPS output");}
   File.WriteAllText(log,"PASS\nImport / source mapping / split equivalent mapping / speed / reorder / deterministic auto zoom / original spring endpoints / video+audio export / cancel cleanup / portrait 1080p geometry / 1080p60 silent export / image background / opening effect\nExport: "+output);
  }catch(Exception ex){File.WriteAllText(log,"FAIL\n"+ex);Environment.ExitCode=1;}}
  static void Assert(bool ok,string name){if(!ok)throw new Exception(name);}
 }
}
