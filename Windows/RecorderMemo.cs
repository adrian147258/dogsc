// Windows adaptation of Sources/DogSCApp/RecorderMemo.swift.
// DogSC MIT License, Copyright (c) 2026 神烦老狗.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DogSC.Windows;
using Path=System.IO.Path;

namespace DogSC.Replica {
 public class MemoDraft {
  public int Version=1;public string Text="";public double FontSize=22,Opacity=.92;
  public double Left=double.NaN,Top=double.NaN,Width=390,Height=420;
 }
 public sealed class MemoStore {
  public readonly string FilePath;public MemoDraft Draft;public string Error="";
  readonly DispatcherTimer pending=new DispatcherTimer();bool dirty;public event Action Saved;
  public MemoStore(string file){FilePath=file;Draft=new MemoDraft();pending.Interval=TimeSpan.FromMilliseconds(300);pending.Tick+=(s,e)=>Flush();
   try{if(File.Exists(file)){var loaded=new JavaScriptSerializer().Deserialize<MemoDraft>(File.ReadAllText(file,Encoding.UTF8));if(loaded==null||loaded.Version!=1)throw new Exception("不支持的备忘录版本");Draft=loaded;}}
   catch{Error="文稿读取失败，原文件保留";}
   Draft.Text=Draft.Text??"";Draft.FontSize=Safe(Draft.FontSize,22,16,40);Draft.Opacity=Safe(Draft.Opacity,.92,.55,1);Draft.Width=Safe(Draft.Width,390,300,760);Draft.Height=Safe(Draft.Height,420,280,1000);
  }
  static double Safe(double value,double fallback,double min,double max){return EditDocument.Finite(value)?EditDocument.Clamp(value,min,max):fallback;}
  public void Changed(){dirty=true;pending.Stop();pending.Start();}
  public bool Flush(){pending.Stop();if(!dirty)return true;string temporary=FilePath+".tmp";
   try{Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
    // JSON cannot represent NaN, so unsited windows store finite coordinates.
    if(!EditDocument.Finite(Draft.Left))Draft.Left=0;if(!EditDocument.Finite(Draft.Top))Draft.Top=0;
    File.WriteAllText(temporary,new JavaScriptSerializer().Serialize(Draft),new UTF8Encoding(false));
    if(File.Exists(FilePath))File.Replace(temporary,FilePath,FilePath+".bak");else File.Move(temporary,FilePath);
    dirty=false;Error="";if(Saved!=null)Saved();return true;
   }catch{Error="保存失败，请检查本机存储位置";if(Saved!=null)Saved();return false;}
  }
 }
 public sealed class MemoController {
  public readonly MemoStore Store;public MemoWindow Panel {get;private set;}
  public bool Visible {get{return Panel!=null&&Panel.IsVisible;}}
  public event Action VisibilityChanged;bool selecting,capturing,resumeSelection,resumeCapture,disposed;
  public MemoController(string file=null){Store=new MemoStore(file??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DogSC.Windows","memo.json"));}
  void Notify(){if(VisibilityChanged!=null)VisibilityChanged();}
  public void Toggle(Window relativeTo){if(Visible)Hide();else Show(relativeTo);}
  public void Show(Window relativeTo){if(disposed)return;if(Panel==null)Panel=new MemoWindow(this,relativeTo);Panel.Fit(relativeTo);
   if(capturing&&!Panel.TryExclude()){resumeCapture=true;Notify();return;}
   Panel.Show();Panel.TryExclude();Panel.RememberFrame();Notify();
  }
  public void Hide(){if(Panel!=null){Panel.RememberFrame();Store.Flush();Panel.Hide();}resumeSelection=false;resumeCapture=false;Notify();}
  public void SuspendSelection(){resumeSelection=Visible;if(Visible){Panel.RememberFrame();Store.Flush();Panel.Hide();}selecting=true;Notify();}
  public void ResumeSelection(Window relativeTo){bool resume=resumeSelection;resumeSelection=false;selecting=false;if(resume)Show(relativeTo);}
  public void BeginCapture(){capturing=true;if(Visible&&!Panel.TryExclude()){resumeCapture=true;Panel.RememberFrame();Store.Flush();Panel.Hide();Notify();}}
  public void EndCapture(Window relativeTo){capturing=false;bool resume=resumeCapture;resumeCapture=false;if(resume)Show(relativeTo);}
  public void Shutdown(){if(disposed)return;disposed=true;if(Panel!=null){Panel.RememberFrame();Store.Flush();Panel.DisposeWindow();}else Store.Flush();}
  public bool Suspended {get{return (selecting&&resumeSelection)||(capturing&&resumeCapture);}}
 }
 public sealed class MemoWindow:Window {
  [DllImport("user32.dll",SetLastError=true)]static extern bool SetWindowDisplayAffinity(IntPtr hwnd,uint affinity);
  [DllImport("user32.dll")]static extern bool GetWindowDisplayAffinity(IntPtr hwnd,out uint affinity);
  [DllImport("ntdll.dll",CharSet=CharSet.Unicode)]static extern int RtlGetVersion(ref OSVersion version);
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct OSVersion{public uint Size,Major,Minor,Build,Platform;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string ServicePack;}
  readonly MemoController controller;readonly Border paper;readonly TextBlock placeholder,fontValue,opacityValue,saveState,privacy;
  readonly Button edit,read,smaller,larger;public readonly TextBox Editor;public readonly Slider BackgroundSlider;
  bool disposing,placing,ready;public bool Editing {get;private set;}public bool Excluded {get;private set;}
  public MemoWindow(MemoController owner,Window relativeTo){controller=owner;Title="DogSC · 备忘录";Width=390;Height=420;MinWidth=300;MinHeight=280;MaxWidth=760;MaxHeight=1000;
   WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=false;ShowActivated=false;FontFamily=relativeTo.FontFamily;FontSize=12;Resources=UI.LightSurfaceResources(relativeTo.Resources);
   Editing=string.IsNullOrEmpty(owner.Store.Draft.Text);
   var root=new Grid {Margin=new Thickness(7)};Content=root;
   paper=new Border {CornerRadius=new CornerRadius(20),BorderBrush=new SolidColorBrush(Color.FromArgb(204,255,255,255)),BorderThickness=new Thickness(1)};paper.Effect=new System.Windows.Media.Effects.DropShadowEffect {BlurRadius=12,Opacity=.15,ShadowDepth=3};root.Children.Add(paper);
   var grid=new Grid();grid.RowDefinitions.Add(new RowDefinition {Height=new GridLength(68)});grid.RowDefinitions.Add(new RowDefinition());grid.RowDefinitions.Add(new RowDefinition {Height=new GridLength(84)});paper.Child=grid;
   var header=new DockPanel {Margin=new Thickness(15,11,15,10)};grid.Children.Add(header);
   var close=UI.Button("×",()=>controller.Hide());close.Foreground=UI.Brush("#293037");close.FontSize=17;close.Width=28;close.Height=28;close.MinHeight=28;close.Padding=new Thickness(0);close.Background=Brushes.Transparent;close.BorderBrush=Brushes.Transparent;close.ToolTip="收起备忘录";DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);
   var modes=new StackPanel {Orientation=Orientation.Horizontal};var modeWell=new Border {Child=modes,Background=UI.Brush("#10000000"),CornerRadius=new CornerRadius(10),Margin=new Thickness(5,0,8,0),VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(modeWell,Dock.Right);header.Children.Add(modeWell);
   edit=ModeButton("编辑",true);read=ModeButton("阅读",false);modes.Children.Add(edit);modes.Children.Add(read);
   var handle=new StackPanel {Orientation=Orientation.Horizontal,Background=Brushes.Transparent};header.Children.Add(handle);
   var icon=new Border {Width=32,Height=32,CornerRadius=new CornerRadius(10),Background=UI.Brush("#D6F2E6"),VerticalAlignment=VerticalAlignment.Center,Child=MemoIcon(17)};handle.Children.Add(icon);
   var titles=new StackPanel {Margin=new Thickness(9,0,0,0),VerticalAlignment=VerticalAlignment.Center};handle.Children.Add(titles);var title=UI.Text("备忘录",15,"#293037");title.FontWeight=FontWeights.SemiBold;titles.Children.Add(title);privacy=UI.Text("仅自己可见",10,"#788186");privacy.Margin=new Thickness(0,2,0,0);titles.Children.Add(privacy);
   handle.MouseLeftButtonDown+=(s,e)=>{try{DragMove();Fit(null);RememberFrame();}catch{}e.Handled=true;};
   var content=new Border {Margin=new Thickness(11,0,11,0),CornerRadius=new CornerRadius(14),BorderThickness=new Thickness(1),BorderBrush=UI.Brush("#10000000"),Background=UI.Brush("#1FFFFFFF"),ClipToBounds=true};Grid.SetRow(content,1);grid.Children.Add(content);var body=new Grid();content.Child=body;
   Editor=new TextBox {Text=owner.Store.Draft.Text,AcceptsReturn=true,AcceptsTab=true,TextWrapping=TextWrapping.Wrap,VerticalContentAlignment=VerticalAlignment.Top,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(16,14,16,14),BorderThickness=new Thickness(0),Background=Brushes.Transparent,Foreground=UI.Brush("#293037"),CaretBrush=UI.Brush("#293037"),FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI"),FontSize=owner.Store.Draft.FontSize,IsUndoEnabled=true,UndoLimit=200};body.Children.Add(Editor);
   placeholder=UI.Text("写下或粘贴要讲的内容…",owner.Store.Draft.FontSize,"#7A858C");placeholder.FontFamily=Editor.FontFamily;placeholder.Margin=new Thickness(13,12,13,12);placeholder.IsHitTestVisible=false;body.Children.Add(placeholder);
   Editor.TextChanged+=(s,e)=>{owner.Store.Draft.Text=Editor.Text;owner.Store.Changed();placeholder.Visibility=Editor.Text.Length==0?Visibility.Visible:Visibility.Collapsed;ShowSaveState();};
   var footer=new StackPanel {Margin=new Thickness(17,9,17,13)};Grid.SetRow(footer,2);grid.Children.Add(footer);
   var fontRow=new DockPanel();footer.Children.Add(fontRow);saveState=UI.Text("自动保存到本机",10,"#788186");saveState.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(saveState,Dock.Right);fontRow.Children.Add(saveState);
   var sizing=new StackPanel {Orientation=Orientation.Horizontal};fontRow.Children.Add(sizing);smaller=UI.Button("A−",()=>SetSize(controller.Store.Draft.FontSize-2));larger=UI.Button("A＋",()=>SetSize(controller.Store.Draft.FontSize+2));
   foreach(var b in new[]{smaller,larger}){b.Foreground=UI.Brush("#293037");b.Width=28;b.Height=26;b.MinHeight=26;b.Padding=new Thickness(0);b.FontSize=12;b.Margin=new Thickness(0);b.Background=Brushes.White;b.BorderBrush=UI.Brush("#10000000");}smaller.ToolTip="缩小备忘录字号";larger.ToolTip="放大备忘录字号";
   fontValue=UI.Text("22",12,"#293037");fontValue.Width=28;fontValue.TextAlignment=TextAlignment.Center;fontValue.VerticalAlignment=VerticalAlignment.Center;sizing.Children.Add(smaller);sizing.Children.Add(fontValue);sizing.Children.Add(larger);
   var opacityRow=new DockPanel {Margin=new Thickness(0,7,0,0)};footer.Children.Add(opacityRow);var label=UI.Text("背景不透明度",11,"#293037");label.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(label,Dock.Left);opacityRow.Children.Add(label);
   opacityValue=UI.Text("92%",11,"#293037");opacityValue.Width=35;opacityValue.TextAlignment=TextAlignment.Right;opacityValue.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(opacityValue,Dock.Right);opacityRow.Children.Add(opacityValue);
   BackgroundSlider=new Slider {Minimum=.55,Maximum=1,Value=owner.Store.Draft.Opacity,Margin=new Thickness(9,0,9,0),Height=24,ToolTip="备忘录背景不透明度"};BackgroundSlider.ValueChanged+=(s,e)=>{owner.Store.Draft.Opacity=BackgroundSlider.Value;owner.Store.Changed();RefreshPaper();ShowSaveState();};opacityRow.Children.Add(BackgroundSlider);
   var resize=new Thumb {Width=18,Height=18,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Cursor=Cursors.SizeNWSE,Margin=new Thickness(0,0,4,4),ToolTip="拖动调整备忘录大小"};var template=new ControlTemplate(typeof(Thumb));var drawing=new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));drawing.SetValue(System.Windows.Shapes.Path.DataProperty,Geometry.Parse("M4,14 L14,4 M8,14 L14,8 M12,14 L14,12"));drawing.SetValue(System.Windows.Shapes.Path.StrokeProperty,UI.Brush("#999FA3"));drawing.SetValue(System.Windows.Shapes.Path.StrokeThicknessProperty,1.0);template.VisualTree=drawing;resize.Template=template;root.Children.Add(resize);
   resize.DragDelta+=(s,e)=>{Width=EditDocument.Clamp(Width+e.HorizontalChange,MinWidth,MaxWidth);Height=EditDocument.Clamp(Height+e.VerticalChange,MinHeight,MaxHeight);};resize.DragCompleted+=(s,e)=>{Fit(null);RememberFrame();owner.Store.Flush();ShowSaveState();};
   SourceInitialized+=(s,e)=>{ready=true;TryExclude();};LocationChanged+=(s,e)=>{if(ready&&!placing)RememberFrame();};SizeChanged+=(s,e)=>{if(ready&&!placing)RememberFrame();};Deactivated+=(s,e)=>{owner.Store.Flush();ShowSaveState();};
   PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){controller.Hide();e.Handled=true;}else if(e.Key==Key.S&&(Keyboard.Modifiers&ModifierKeys.Control)!=0){owner.Store.Flush();ShowSaveState();e.Handled=true;}};
   owner.Store.Saved+=ShowSaveState;Closed+=(s,e)=>owner.Store.Saved-=ShowSaveState;
   Closing+=(s,e)=>{if(!disposing){e.Cancel=true;controller.Hide();}};UpdateMode();RefreshTypography();RefreshPaper();ShowSaveState();
  }
  public static FrameworkElement MemoIcon(double size=17){var p=new System.Windows.Shapes.Path {Data=Geometry.Parse("M3,3 L21,3 21,21 3,21 Z M7,8 L17,8 M7,12 L17,12 M7,16 L14,16"),Stroke=UI.Brush("#35464B"),StrokeThickness=1.6,StrokeLineJoin=PenLineJoin.Round};var canvas=new Canvas {Width=24,Height=24};canvas.Children.Add(p);return new Viewbox {Width=size,Height=size,Child=canvas};}
  Button ModeButton(string title,bool editing){var b=UI.Button(title,()=>SetMode(editing));b.Foreground=UI.Brush("#293037");b.Width=38;b.Height=27;b.MinHeight=27;b.Padding=new Thickness(0);b.FontSize=11;b.BorderBrush=Brushes.Transparent;b.Margin=new Thickness(2,3,2,3);b.ToolTip=editing?"编辑备忘录":"切换到阅读，避免误改文稿";return b;}
  public void SetMode(bool editing){Editing=editing;UpdateMode();controller.Store.Flush();ShowSaveState();}
  void UpdateMode(){int start=Editor.SelectionStart,length=Editor.SelectionLength;Editor.IsReadOnly=!Editing;Editor.Select(start,length);edit.Background=Editing?Brushes.White:Brushes.Transparent;read.Background=Editing?Brushes.Transparent:Brushes.White;placeholder.Visibility=Editor.Text.Length==0?Visibility.Visible:Visibility.Collapsed;}
  public void SetSize(double size){controller.Store.Draft.FontSize=EditDocument.Clamp(size,16,40);controller.Store.Changed();RefreshTypography();}
  void RefreshTypography(){double size=controller.Store.Draft.FontSize;Editor.FontSize=size;TextBlock.SetLineHeight(Editor,size*1.55);TextBlock.SetLineStackingStrategy(Editor,LineStackingStrategy.BlockLineHeight);placeholder.FontSize=size;fontValue.Text=size.ToString("0");smaller.IsEnabled=size>16;larger.IsEnabled=size<40;}
  void RefreshPaper(){double opacity=controller.Store.Draft.Opacity;paper.Background=new LinearGradientBrush(Color.FromArgb((byte)Math.Round(255*opacity),255,255,255),Color.FromArgb((byte)Math.Round(255*opacity),246,248,249),45);opacityValue.Text=(opacity*100).ToString("0")+"%";}
  void ShowSaveState(){saveState.Text=string.IsNullOrEmpty(controller.Store.Error)?"自动保存到本机":controller.Store.Error;}
  public bool TryExclude(){try{var version=new OSVersion {Size=(uint)Marshal.SizeOf(typeof(OSVersion))};if(RtlGetVersion(ref version)!=0||version.Build<19041){Excluded=false;privacy.Text="录制时自动收起";return false;}
    var hwnd=new WindowInteropHelper(this).EnsureHandle();uint affinity;Excluded=SetWindowDisplayAffinity(hwnd,0x11)&&GetWindowDisplayAffinity(hwnd,out affinity)&&affinity==0x11;
   }catch{Excluded=false;}privacy.Text=Excluded?"仅自己可见":"录制时自动收起";return Excluded;}
  public void RememberFrame(){if(!ready||placing)return;var d=controller.Store.Draft;d.Left=Left;d.Top=Top;d.Width=Width;d.Height=Height;controller.Store.Changed();}
  public void Fit(Window relativeTo){placing=true;try{
   var d=controller.Store.Draft;double x=EditDocument.Finite(d.Left)?d.Left:relativeTo!=null?relativeTo.Left+relativeTo.ActualWidth-390:SystemParameters.WorkArea.Right-402;
   double y=EditDocument.Finite(d.Top)?d.Top:relativeTo!=null?relativeTo.Top-434:SystemParameters.WorkArea.Bottom-434;
   var source=PresentationSource.FromVisual(relativeTo??this);double sx=source!=null&&source.CompositionTarget!=null?source.CompositionTarget.TransformToDevice.M11:1,sy=source!=null&&source.CompositionTarget!=null?source.CompositionTarget.TransformToDevice.M22:1;
   var screen=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)((x+d.Width/2)*sx),(int)((y+d.Height/2)*sy))).WorkingArea;var safe=new Rect(screen.X/sx+12,screen.Y/sy+12,Math.Max(40,screen.Width/sx-24),Math.Max(40,screen.Height/sy-24));
   MinWidth=Math.Min(300,safe.Width);MinHeight=Math.Min(280,safe.Height);MaxWidth=Math.Min(760,safe.Width);MaxHeight=Math.Min(1000,safe.Height);Width=EditDocument.Clamp(d.Width,MinWidth,MaxWidth);Height=EditDocument.Clamp(d.Height,MinHeight,MaxHeight);Left=EditDocument.Clamp(x,safe.Left,safe.Right-Width);Top=EditDocument.Clamp(y,safe.Top,safe.Bottom-Height);
   }finally{placing=false;}}
  public void DisposeWindow(){disposing=true;Close();}
 }
 static class MemoTests {
  [DllImport("user32.dll")]static extern bool SetWindowDisplayAffinity(IntPtr hwnd,uint affinity);
  [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")]static extern bool GetClientRect(IntPtr hwnd,out RECT rect);
  [DllImport("user32.dll")]static extern bool ClientToScreen(IntPtr hwnd,ref Native.Point point);
  [StructLayout(LayoutKind.Sequential)]struct RECT{public int Left,Top,Right,Bottom;}
  static void Check(bool value,string message){if(!value)throw new Exception(message);}
  public static async Task Run(Studio studio,string folder){
   Directory.CreateDirectory(folder);var memo=studio.Memo;Check(memo.Store.Draft.FontSize==22&&memo.Store.Draft.Opacity==.92,"Default settings");
   studio.Window.Activate();await Task.Delay(100);var foreground=GetForegroundWindow();memo.Show(studio.Window);await Task.Delay(150);
   var panel=memo.Panel;Check(GetForegroundWindow()==foreground&&!panel.ShowActivated&&panel.Owner==null,"Opening memo stole focus or is owned by editor");Check(panel.Editing&&!panel.Editor.IsReadOnly,"Empty memo must begin editable");
   string text="今天的演示\r\n\r\n先介绍画面，再展示操作。\r\n记得放慢语速，留一点停顿。\r\n\r\n最后，谢谢大家。 ✨";panel.Editor.Text=text;await Task.Delay(500);
   Check(new MemoStore(memo.Store.FilePath).Draft.Text==text,"Debounced Unicode text persistence");
   panel.Editor.Select(0,4);int selectionLength=panel.Editor.SelectionLength;panel.SetMode(false);Check(panel.Editor.IsReadOnly&&panel.Editor.SelectionLength==selectionLength&&selectionLength==4,"Reading mode must preserve selectable text");panel.SetMode(true);Check(!panel.Editor.IsReadOnly&&panel.Editor.Text==text,"Editing switch lost text");
   panel.SetSize(100);Check(panel.Editor.FontSize==40,"Font maximum");panel.SetSize(0);Check(panel.Editor.FontSize==16,"Font minimum");panel.SetSize(22);panel.BackgroundSlider.Value=.55;Check(panel.Opacity==1&&panel.Editor.Opacity==1&&panel.Editor.Text==text,"Background opacity faded text");panel.BackgroundSlider.Value=.92;
   memo.SuspendSelection();Check(!memo.Visible&&memo.Suspended,"Area selection suspension");memo.ResumeSelection(studio.Window);Check(memo.Visible,"Area selection restore");
   panel.Editor.AppendText("\r\n保存测试");memo.Hide();Check(new MemoStore(memo.Store.FilePath).Draft.Text.EndsWith("保存测试"),"Hide must flush pending text");memo.Show(studio.Window);panel.Editor.Text=text;panel.SetMode(false);await Task.Delay(350);UI.SaveImage((FrameworkElement)panel.Content,Path.Combine(Media.Root,"bin","memo-preview.png"));
   memo.Store.Draft.Left=-100000;memo.Store.Draft.Top=-100000;panel.Fit(studio.Window);panel.RememberFrame();Check(panel.Left>=SystemParameters.WorkArea.Left&&panel.Top>=SystemParameters.WorkArea.Top,"Off-screen frame was not clamped");
   // Restrict all real captures to an opaque synthetic window; no personal desktop is recorded.
   var surface=new Window {Title="DogSC memo capture test",Width=700,Height=600,Left=SystemParameters.WorkArea.Left+30,Top=SystemParameters.WorkArea.Top+30,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,Background=UI.Brush("#24465B"),Topmost=true,ShowInTaskbar=false};
   try{memo.Hide();surface.Show();await Task.Delay(200);var hwnd=new WindowInteropHelper(surface).Handle;RECT rect;Check(GetClientRect(hwnd,out rect),"Test surface rect");Native.Point origin=new Native.Point();ClientToScreen(hwnd,ref origin);
    string crop="-offset_x "+origin.X+" -offset_y "+origin.Y+" -video_size "+(rect.Right-rect.Left)+"x"+(rect.Bottom-rect.Top);string baseline=Path.Combine(folder,"baseline.png"),control=Path.Combine(folder,"visible.png"),excluded=Path.Combine(folder,"excluded.png");await Capture(crop,baseline);
    memo.Store.Draft.Left=surface.Left+100;memo.Store.Draft.Top=surface.Top+70;memo.Show(surface);await Task.Delay(200);var mh=new WindowInteropHelper(panel).Handle;Check(SetWindowDisplayAffinity(mh,0),"Positive capture control affinity");await Task.Delay(200);await Capture(crop,control);
    var transform=PresentationSource.FromVisual(surface).CompositionTarget.TransformToDevice;int x=(int)(200*transform.M11),y=(int)(200*transform.M22);
    using(var b=new System.Drawing.Bitmap(baseline))using(var c=new System.Drawing.Bitmap(control)){Check(Distance(b.GetPixel(x,y),c.GetPixel(x,y))>100,"Capture positive control did not see visible memo");}
    bool supported=panel.TryExclude();memo.BeginCapture();await Task.Delay(250);await Capture(crop,excluded);
    using(var b=new System.Drawing.Bitmap(baseline))using(var c=new System.Drawing.Bitmap(excluded)){Check(Distance(b.GetPixel(x,y),c.GetPixel(x,y))<15,"Memo leaked into real GDI recording");}
    Check(supported?memo.Visible:!memo.Visible,"Capture visibility policy");memo.EndCapture(surface);Check(memo.Visible,"Memo did not return after recording");
    File.WriteAllText(Path.Combine(folder,"capture-results.txt"),supported?"PASS: WDA_EXCLUDEFROMCAPTURE verified with GDI capture; memo stays visible locally.":"PASS: unsupported display affinity; memo hides during recording and returns afterward.");
   }finally{surface.Close();}
   panel.Width=460;panel.Height=500;panel.RememberFrame();memo.Hide();var restored=new MemoController(memo.Store.FilePath);try{restored.Show(studio.Window);Check(!restored.Panel.Editing&&restored.Panel.Editor.Text==text,"Reopen reading mode/text");Check(restored.Panel.Width==460&&restored.Panel.Height==500&&restored.Panel.Editor.FontSize==22&&restored.Panel.BackgroundSlider.Value==.92,"Persisted window/settings");}finally{restored.Shutdown();}
  }
  static int Distance(System.Drawing.Color a,System.Drawing.Color b){return Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B);}
  static Task Capture(string crop,string output){return Task.Run(()=>StudioMedia.Run("-hide_banner -y -f gdigrab -framerate 10 -draw_mouse 0 "+crop+" -i desktop -frames:v 1 -update 1 "+Media.Q(output),CancellationToken.None));}
 }
}
