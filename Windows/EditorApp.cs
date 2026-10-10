using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using DogSC.Windows;
using D=System.Drawing;
using Imaging=System.Drawing.Imaging;
using Path=System.IO.Path;

namespace DogSC.Replica {
 static partial class UI {
  public static Brush Brush(string color){return (Brush)new BrushConverter().ConvertFromString(color);}
  public static TextBlock Text(string s,double size=12,string color="#363B3E"){var text=new TextBlock {Text=s,FontSize=size,Foreground=Brush(color),TextWrapping=TextWrapping.Wrap};if(color=="#363B3E")text.SetResourceReference(TextBlock.ForegroundProperty,"Ink");else if(color!="#FFFFFF"&&color!="#000000"){var c=(Color)ColorConverter.ConvertFromString(color);if(Math.Max(c.R,Math.Max(c.G,c.B))-Math.Min(c.R,Math.Min(c.G,c.B))<45)text.SetResourceReference(TextBlock.ForegroundProperty,c.R<70?"Ink":c.R<130?"Secondary":"Muted");}return text;}
  public static Button Button(string text,Action action){var b=new Button {Content=text,Margin=new Thickness(0,3,0,3)};b.Click+=(s,e)=>action();return b;}
  public static BitmapSource Image(byte[] bytes){using(var s=new MemoryStream(bytes)){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=s;image.EndInit();image.Freeze();return image;}}
  public static BitmapSource Image(D.Bitmap bitmap){var r=new D.Rectangle(0,0,bitmap.Width,bitmap.Height);var data=bitmap.LockBits(r,Imaging.ImageLockMode.ReadOnly,Imaging.PixelFormat.Format32bppArgb);try{var result=BitmapSource.Create(bitmap.Width,bitmap.Height,96,96,PixelFormats.Bgra32,null,data.Scan0,data.Stride*data.Height,data.Stride);result.Freeze();return result;}finally{bitmap.UnlockBits(data);}}
  public static void SaveImage(FrameworkElement element,string path){element.UpdateLayout();var bitmap=new RenderTargetBitmap((int)element.ActualWidth,(int)element.ActualHeight,96,96,PixelFormats.Pbgra32);var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(Brushes.White,null,new Rect(0,0,element.ActualWidth,element.ActualHeight));dc.DrawRectangle(new VisualBrush(element),null,new Rect(0,0,element.ActualWidth,element.ActualHeight));}bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(path))encoder.Save(f);}
  public static string Time(double t){return TimeSpan.FromSeconds(Math.Max(0,t)).ToString(@"mm\:ss\.ff");}
  public static FrameworkElement Icon(string name,double size=18){
   var shapes=new Dictionary<string,string>{
    {"folder","M2,6 L9,6 11,8 22,8 22,20 2,20 Z M2,6 L2,4 9,4 11,6 20,6 20,8"},
    {"picture","M3,5 L21,5 21,20 3,20 Z M5,17 L10,11 14,15 17,12 21,17 M7,8 L7.1,8"},
    {"opening","M4,6 L20,6 20,21 4,21 Z M7,3 L17,3 M12,10 L12,17 M9,13.5 L15,13.5"},
    {"motion","M8,3 L3,3 3,8 M16,3 L21,3 21,8 M3,16 L3,21 8,21 M21,16 L21,21 16,21"},
    {"cursor","M4,2 L19,15 12,15 9,22 Z"},
    {"camera","M3,6 L15,6 15,18 3,18 Z M15,10 L22,6 22,18 15,14"},
    {"sound","M3,9 L7,9 12,4 12,20 7,15 3,15 Z M16,8 Q20,12 16,16 M19,4 Q26,12 19,20"},
    {"mic","M9,4 Q12,-1 15,4 L15,12 Q12,17 9,12 Z M6,10 L6,13 Q12,23 18,13 L18,10 M12,19 L12,23 M8,23 L16,23"},
    {"monitor","M2,3 L22,3 22,17 2,17 Z M12,17 L12,22 M7,22 L17,22"},
    {"window","M3,4 L21,4 21,21 3,21 Z M3,9 L21,9 M6,6.5 L6.1,6.5 M9,6.5 L9.1,6.5"},
    {"export","M8,9 L4,9 4,22 20,22 20,9 16,9 M12,16 L12,1 M8,5 L12,1 16,5"},
    {"undo","M9,5 L3,11 9,17 M3,11 L15,11 Q24,11 21,19"},
    {"redo","M15,5 L21,11 15,17 M21,11 L9,11 Q0,11 3,19"},
    {"play","M7,3 L21,12 7,21 Z"}, {"pause","M8,4 L8,20 M16,4 L16,20"},
    {"cut","M10,10 L21,3 M10,14 L21,21 M3,5 C-1,5 -1,11 4,11 C9,11 9,5 3,5 M3,13 C-1,13 -1,19 4,19 C9,19 9,13 3,13"},
    {"trash","M5,7 L6,22 18,22 19,7 M3,5 L21,5 M9,5 L9,2 15,2 15,5 M10,10 L10,18 M14,10 L14,18"},
    {"sun","M8,12 A4,4 0 1 0 16,12 A4,4 0 1 0 8,12 M12,1 L12,4 M12,20 L12,23 M1,12 L4,12 M20,12 L23,12 M4,4 L6,6 M18,18 L20,20 M4,20 L6,18 M18,6 L20,4"},
    {"previous","M5,5 L5,19 M19,5 L8,12 19,19 Z"},{"next","M19,5 L19,19 M5,5 L16,12 5,19 Z"},
    {"pencil","M4,17 L4,21 8,21 21,8 17,4 Z M15,6 L19,10"},{"close","M5,5 L19,19 M19,5 L5,19"},
    {"minimize","M5,12 L19,12"},{"maximize","M5,5 L19,5 19,19 5,19 Z"},{"plus","M12,5 L12,19 M5,12 L19,12"},
    {"chevron","M6,9 L12,15 18,9"},{"left","M15,5 L8,12 15,19"},{"right","M9,5 L16,12 9,19"},
    {"layout","M3,5 L21,5 21,19 3,19 Z M8,5 L8,19"},{"background","M12,3 L22,8 12,13 2,8 Z M2,12 L12,17 22,12 M2,16 L12,21 22,16"},
    {"frame","M3,5 L21,5 21,19 3,19 Z M3,9 L21,9 M6,7 L6.1,7 M9,7 L9.1,7"},
    {"sticker","M4,4 L20,4 20,14 14,20 4,20 Z M14,20 L14,14 20,14 M7,8 L7.1,8 M8,15 L11,11 14,13"},
    {"soften","M8,3 L3,3 3,8 M16,3 L21,3 21,8 M3,16 L3,21 8,21 M21,16 L21,21 16,21 M9,9 L15,9 15,15 9,15 Z"},
    {"sliders","M3,6 L9,6 M15,6 L21,6 M3,12 L14,12 M18,12 L21,12 M3,18 L5,18 M11,18 L21,18 M9,3 L9,9 15,9 15,3 Z M14,9 L14,15 18,15 18,9 Z M5,15 L5,21 11,21 11,15 Z"},
    {"presets","M3,5 L11,5 11,13 3,13 Z M15,5 L22,5 22,10 15,10 Z M15,14 L22,14 22,20 15,20 Z M3,17 L11,17"},
    {"snap","M6,4 L6,13 Q6,21 12,21 Q18,21 18,13 L18,4 M4,4 L8,4 M16,4 L20,4 M6,8 L6,11 M18,8 L18,11"},
    {"target","M5,12 A7,7 0 1 0 19,12 A7,7 0 1 0 5,12 M12,1 L12,7 M12,17 L12,23 M1,12 L7,12 M17,12 L23,12"},
    {"device","M7,2 L17,2 17,22 7,22 Z M10,5 L14,5 M11,19 L13,19"},{"record","M6,12 A6,6 0 1 0 18,12 A6,6 0 1 0 6,12"},{"stop","M5,5 L19,5 19,19 5,19 Z"}
   };string geometry; if(!shapes.TryGetValue(name,out geometry))geometry=shapes["window"];
   var path=new System.Windows.Shapes.Path {Data=Geometry.Parse(geometry),StrokeThickness=1.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round};path.SetBinding(System.Windows.Shapes.Path.StrokeProperty,new System.Windows.Data.Binding("Foreground"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor,typeof(Control),1)});if(name=="play"||name=="record")path.SetBinding(System.Windows.Shapes.Path.FillProperty,new System.Windows.Data.Binding("Stroke"){Source=path});
   var canvas=new Canvas {Width=24,Height=24};canvas.Children.Add(path);return new Viewbox {Width=size,Height=size,Child=canvas,Margin=new Thickness(0,0,0,0)};
  }
  public static object IconLabel(string kind,string text="",double size=16){var panel=new StackPanel {Orientation=Orientation.Horizontal};panel.Children.Add(Icon(kind,size));if(text!="")panel.Children.Add(new TextBlock {Text=text,Margin=new Thickness(7,0,0,0),VerticalAlignment=VerticalAlignment.Center});return panel;}
  public static Window Dialog(Window owner,string title,double width=440,double height=300){return new Window {Owner=owner,Title=title,Width=width,Height=height,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,Background=Brush("#F8FAFA"),FontFamily=owner.FontFamily,FontSize=12};}
 }
 public partial class Studio {
  public Window Window; public EditDocument Doc; public string FilePath;public readonly MemoController Memo;
  readonly Stack<string> undo=new Stack<string>(),redo=new Stack<string>();
  readonly System.Windows.Controls.Image preview;readonly StackPanel inspector;readonly Canvas timeline;
  readonly List<BitmapSource> thumbs=new List<BitmapSource>(); readonly MediaPlayer audio=new MediaPlayer();
  double position;int selectedCut=-1,selectedZoom=-1;string tab="画面",frameTab="布局";bool dirty,dark,playing,snap=true,closing;
  CancellationTokenSource playback;RecorderWindow recorder;MotionEvaluator motion;
  int renderVersion;bool rendering;byte[] cachedFrame;double cachedTime=-100;string cachedSource="";
  public Studio(string memoFile=null) {
   string interfaceFile=Path.GetFullPath(Path.Combine(Media.Root,"Editor.xaml"));using(var source=File.OpenRead(interfaceFile))Window=(Window)XamlReader.Load(source,new ParserContext {BaseUri=new Uri(interfaceFile)});
   Memo=new MemoController(memoFile);
   try {var fonts=Fonts.GetFontFamilies(new Uri(Path.GetFullPath(Path.Combine(Media.Root,"../Resources/Fonts"))+"/"));var font=fonts.FirstOrDefault();if(font!=null)Window.FontFamily=font;}catch{}
   preview=Find<System.Windows.Controls.Image>("Preview");inspector=Find<StackPanel>("Inspector");timeline=Find<Canvas>("TimelineHost");
   InitializeEffects();
   Find<Border>("TitleBar").MouseLeftButtonDown+=(s,e)=>{if(!UI.InteractiveSource(e.OriginalSource as DependencyObject)&&(e.Source is TextBlock||e.Source is Border||e.Source is Grid)){if(e.ClickCount==2)ToggleMax();else Window.DragMove();}};
   Bind("Close",()=>Window.Close());Bind("Minimize",()=>Window.WindowState=WindowState.Minimized);Bind("Maximize",ToggleMax);
   Bind("EmptyImport",Open);Bind("EmptyRecord",Record);Bind("NewRecording",Record);Bind("ProjectMenu",ProjectMenu);Bind("Rename",Rename);
   Bind("Undo",()=>History(false));Bind("Redo",()=>History(true));Bind("Play",()=>{if(playing)Pause();else Play();});Bind("Previous",()=>Seek(0));Bind("Next",()=>{if(Doc!=null)Seek(Math.Max(0,Doc.Length-.04));});
   Bind("Split",Split);Bind("Delete",Delete);Bind("MoveLeft",()=>MoveCut(-1));Bind("MoveRight",()=>MoveCut(1));Bind("AddZoom",AddZoom);
   Bind("Export",Export);Bind("Presets",Presets);Bind("Ratio",Ratios);Bind("FitCanvas",()=>{if(Doc!=null){Checkpoint();Doc.Style.Scale=1;Doc.Style.X=Doc.Style.Y=.5;Changed();BuildInspector();}});
   Bind("Theme",Theme);Bind("Snap",()=>{snap=!snap;Find<Button>("Snap").Background=UI.Brush(snap?"#E3F2EB":"#F4F4F5");});
   Find<Slider>("TimelineScale").ValueChanged+=(s,e)=>DrawTimeline();
   Window.SizeChanged+=(s,e)=>DrawTimeline();Find<Grid>("Stage").SizeChanged+=(s,e)=>{if(Doc!=null&&!playing)RefreshPreview();};Window.AllowDrop=true;Window.Drop+=async(s,e)=>{var files=e.Data.GetData(DataFormats.FileDrop) as string[];if(files!=null&&files.Length>0)await Load(files[0]);};
   Window.PreviewKeyDown+=(s,e)=>{
    if(Keyboard.FocusedElement is TextBox)return;
    if((Keyboard.Modifiers&ModifierKeys.Control)!=0){if(e.Key==Key.O)Open();else if(e.Key==Key.S)Save();else if(e.Key==Key.Z)History(false);else if(e.Key==Key.Y)History(true);else if(e.Key==Key.B)Split();else return;e.Handled=true;}
    else if(e.Key==Key.Space){if(playing)Pause();else Play();e.Handled=true;}else if(e.Key==Key.Delete){Delete();e.Handled=true;}else if(e.Key==Key.Left){Seek(position-1.0/30);e.Handled=true;}else if(e.Key==Key.Right){Seek(position+1.0/30);e.Handled=true;}
   };
   Window.Closing+=(s,e)=>{if(closing)return;if(recorder!=null&&recorder.IsRecording){e.Cancel=true;MessageBox.Show(Window,"请先停止录制。","DogSC");return;}if(!MayDiscard()){e.Cancel=true;return;}Pause();audio.Close();Memo.Shutdown();if(recorder!=null)recorder.Close();closing=true;};
   Find<Button>("ProjectMenu").Content=UI.IconLabel("folder");Find<Button>("Undo").Content=UI.IconLabel("undo");Find<Button>("Redo").Content=UI.IconLabel("redo");Find<Button>("Export").Content=UI.IconLabel("export","导出");Find<Button>("Split").Content=UI.IconLabel("cut");Find<Button>("Delete").Content=UI.IconLabel("trash");Find<Button>("Theme").Content=UI.IconLabel("sun");
   timeline.MouseMove+=(s,e)=>{if(Doc!=null&&e.LeftButton==MouseButtonState.Pressed&&Mouse.Captured==null){Pause();position=EditDocument.Clamp(e.GetPosition(timeline).X/TimelineWidth*Doc.Length,0,Math.Max(0,Doc.Length-.001));Update();DrawPlayhead();RefreshPreview();}};
   InitializePolish();BuildRail();BuildInspector();Update();
  }
  public T Find<T>(string name) where T:class{return Window.FindName(name) as T;}
  void Bind(string name,Action action){Find<Button>(name).Click+=(s,e)=>{try{action();}catch(Exception ex){Error(ex);}};}
  void ToggleMax(){Window.WindowState=Window.WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;}
  void Status(string s){Find<TextBlock>("Status").Text=s;}
  void Error(Exception ex){Status("操作失败："+ex.Message);MessageBox.Show(Window,ex.Message,"DogSC",MessageBoxButton.OK,MessageBoxImage.Error);}
  bool MayDiscard(){if(!dirty)return true;var r=MessageBox.Show(Window,"保存当前项目的修改？","DogSC",MessageBoxButton.YesNoCancel);if(r==MessageBoxResult.Cancel)return false;if(r==MessageBoxResult.Yes)return Save();return true;}
  void Checkpoint(){if(Doc==null)return;Pause();undo.Push(Doc.Serialize());redo.Clear();dirty=true;}
  void Changed(){if(Doc==null)return;dirty=true;Doc.Zooms.RemoveAll(z=>z.Start>=Doc.Length);foreach(var z in Doc.Zooms)z.End=Math.Min(z.End,Doc.Length);Doc.Overlays.RemoveAll(o=>o.Start>=Doc.Length);foreach(var o in Doc.Overlays)o.End=Math.Min(o.End,Doc.Length);if(selectedOverlay>=Doc.Overlays.Count)selectedOverlay=-1;if(motion!=null)motion.Dispose();motion=new MotionEvaluator(Doc);position=Math.Min(position,Math.Max(0,Doc.Length-.001));Update();DrawTimeline();RefreshPreview();DrawEffectGuides();}
  void History(bool forward){var from=forward?redo:undo;var to=forward?undo:redo;if(Doc==null||from.Count==0)return;Pause();to.Push(Doc.Serialize());Doc=EditDocument.Deserialize(from.Pop());selectedCut=selectedZoom=selectedOverlay=-1;Changed();BuildInspector();}
  public async Task Load(string path){
   if(!MayDiscard())return;Pause();Status("正在打开…");try{
    EditDocument d;string saved=null;
    if(Path.GetExtension(path).Equals(".dogscwin",StringComparison.OrdinalIgnoreCase)){
     string json=File.ReadAllText(path);if(json.Contains("dogsc-windows-editor")){d=EditDocument.Deserialize(json);d.Validate();saved=path;}
     else {var old=Project.Load(path);d=await Task.Run(()=>StudioMedia.Import(Path.Combine(Path.GetDirectoryName(path),old.Video)));d.Pointer=old.Pointer;d.GenerateZooms();}
    }else d=await Task.Run(()=>StudioMedia.Import(path));
    Doc=d;FilePath=saved;undo.Clear();redo.Clear();dirty=false;position=0;selectedCut=selectedZoom=selectedOverlay=-1;cachedFrame=null;thumbs.Clear();waveform=null;waveformView=false;if(motion!=null)motion.Dispose();motion=new MotionEvaluator(Doc);
    audio.Open(new Uri(Doc.Source));audio.Volume=Doc.Style.Volume;audio.MediaFailed+=(s,e)=>Status("音频预览不可用；视频预览与导出仍可使用。");
    Update();BuildInspector();DrawTimeline();await RequestPreview();Status("已打开 · "+Doc.Width+" × "+Doc.Height+" · "+UI.Time(Doc.Duration));LoadThumbnails();
   }catch(Exception ex){Error(ex);}
  }
  public void SetDocument(EditDocument d){Doc=d;motion=new MotionEvaluator(d);position=0;dirty=false;thumbs.Clear();waveform=null;waveformView=false;Update();BuildInspector();DrawTimeline();RefreshPreview();LoadThumbnails();}
  async void LoadThumbnails(){var doc=Doc;try{for(int i=0;i<8;i++){double t=Math.Min(doc.Duration-.05,doc.Duration*i/8);var b=await Task.Run(()=>UI.Image(StudioMedia.Frame(doc.Source,t,200)));if(Doc==null||Doc.Source!=doc.Source)return;thumbs.Add(b);DrawTimeline();}var wave=await Task.Run(()=>ReadWaveform(doc));if(Doc!=null&&Doc.Source==doc.Source){waveform=wave;DrawTimeline();}}catch{}}
  async void Open(){var dlg=new Microsoft.Win32.OpenFileDialog {Filter="视频或 Windows 项目|*.mp4;*.mov;*.mkv;*.webm;*.dogscwin|所有文件|*.*"};if(dlg.ShowDialog(Window)==true)await Load(dlg.FileName);}
  bool Save(){if(Doc==null)return true;try{if(FilePath==null){var dlg=new Microsoft.Win32.SaveFileDialog {Filter="DogSC Windows 项目|*.dogscwin",FileName=Doc.Name+".dogscwin"};if(dlg.ShowDialog(Window)!=true)return false;FilePath=dlg.FileName;}Doc.Save(FilePath);dirty=false;Update();Status("项目已保存 · "+FilePath);return true;}catch(Exception ex){Error(ex);return false;}}
  void ProjectMenu(){Menu(Find<Button>("ProjectMenu"),new[]{"打开视频或项目…","保存项目","另存为…","新建录制","打开项目目录","备忘录"},i=>{if(i==0)Open();if(i==1)Save();if(i==2){string old=FilePath;FilePath=null;if(!Save())FilePath=old;}if(i==3)Record();if(i==4&&Doc!=null)Process.Start("explorer.exe",Media.Q(Path.GetDirectoryName(FilePath??Doc.Source)));if(i==5)Memo.Toggle(Window);});}
  void Menu(Button target,string[] items,Action<int> action){var menu=new ContextMenu();for(int i=0;i<items.Length;i++){int n=i;var item=new MenuItem {Header=items[i]};item.Click+=(s,e)=>{try{action(n);}catch(Exception ex){Error(ex);}};menu.Items.Add(item);}menu.PlacementTarget=target;menu.IsOpen=true;}
  void Rename(){if(Doc==null)return;var w=UI.Dialog(Window,"项目名称",380,180);var panel=new StackPanel {Margin=new Thickness(24)};var input=new TextBox {Text=Doc.Name,Margin=new Thickness(0,0,0,18)};panel.Children.Add(input);panel.Children.Add(UI.Button("保存",()=>{if(!string.IsNullOrWhiteSpace(input.Text)){Checkpoint();Doc.Name=input.Text.Trim();Changed();w.Close();}}));w.Content=panel;w.ShowDialog();}
  void Record(){Pause();if(recorder!=null){recorder.Activate();return;}recorder=new RecorderWindow(this);recorder.Closed+=(s,e)=>recorder=null;recorder.Show();}
  public async Task Recorded(string path,List<PointerSample> points){await Load(path);if(Doc!=null&&Doc.Source==path){Doc.Pointer=points;Doc.IndependentCursor=true;Doc.GenerateZooms();FilePath=Path.Combine(Path.GetDirectoryName(path),"project.dogscwin");Doc.Save(FilePath);Changed();dirty=false;Update();BuildInspector();}}
  void Update(){bool has=Doc!=null;Find<TextBlock>("ProjectTitle").Text=(has?Doc.Name:"未命名项目")+(dirty?" ·":"");Window.Title="DogSC · "+(has?Doc.Name:"Windows");Find<StackPanel>("EmptyState").Visibility=has?Visibility.Collapsed:Visibility.Visible;
   foreach(string key in new[]{"Export","Presets","Ratio","FitCanvas","AddZoom","Play","Previous","Next","Split","Delete","MoveLeft","MoveRight","Rename"})Find<Button>(key).IsEnabled=has;
   Find<Button>("Waveforms").IsEnabled=has&&Doc.Audio;Find<Button>("Undo").IsEnabled=undo.Count>0;Find<Button>("Redo").IsEnabled=redo.Count>0;
   Find<TextBlock>("Timecode").Text=UI.Time(position)+"  /  "+UI.Time(has?Doc.Length:0);if(has)Find<Button>("Ratio").Content=UI.MenuLabel("layout",Doc.Style.Ratio);AdaptiveChrome();
  }
  void Theme(){dark=!dark;PolishedTheme();}
  void BuildRail(){PolishedRail();}
  void Section(string text){PolishedSection(text);}
  void Note(string text){PolishedNote(text);}
  void Buttons(string[] labels,string selected,Action<string> change){PolishedButtons(labels,selected,change);}
  void Slider(string label,double min,double max,double value,string suffix,Action<double> set){PolishedSlider(label,min,max,value,suffix,set);}
  void Toggle(string label,bool value,Action<bool> set){var check=new CheckBox {Content=label,IsChecked=value,Foreground=UI.Brush(dark?"#DCE0E0":"#545D62")};check.Click+=(s,e)=>{Checkpoint();set(check.IsChecked==true);Changed();};inspector.Children.Add(check);}
  void Number(string label,double value,Action<double> change){PolishedNumber(label,value,change);}
  void PositionPad(double x,double y,Action<double,double> set){PolishedPosition(x,y,set);}
  void BuildInspector(){PrepareInspector();inspector.Children.Clear();Find<TextBlock>("InspectorTitle").Text=selectedCut>=0?"录屏片段":tab;if(Doc==null){Note("打开视频或完成录制后，在这里调整画面和运镜。");return;}var a=Doc.Style;if(tab=="贴图"||tab=="柔化"){BuildOverlayInspector();return;}if(tab=="光标"){BuildCursorInspector();return;}
   if(selectedCut>=0&&selectedCut<Doc.Cuts.Count){var c=Doc.Cuts[selectedCut];Section("片段范围");Number("素材起点 / 秒",c.Start,v=>{c.Start=EditDocument.Clamp(v,0,c.End-.05);Doc.GenerateZooms();});Number("素材终点 / 秒",c.End,v=>{c.End=EditDocument.Clamp(v,c.Start+.05,Doc.Duration);Doc.GenerateZooms();});Section("播放速度");Buttons(new[]{"0.5×","1×","1.5×","2×"},c.Speed+"×",v=>{Checkpoint();c.Speed=double.Parse(v.TrimEnd('×'),CultureInfo.InvariantCulture);Doc.GenerateZooms();Changed();BuildInspector();});Note("拖动时间线片段两端可裁短；分割后可删除或左右重排。");inspector.Children.Add(UI.Button("返回画面设置",()=>{selectedCut=-1;tab="画面";BuildInspector();}));return;}
   if(tab=="画面"){
    Buttons(new[]{"背景","布局","样机"},frameTab,v=>{frameTab=v;BuildInspector();});
    if(frameTab=="布局"){Section("画面留白");Slider("边距",0,180,a.Padding,"",v=>a.Padding=v);Section("位置与大小");PositionPad(a.X,a.Y,(x,y)=>{a.X=x;a.Y=y;});Slider("素材缩放",.5,1.5,a.Scale,"×",v=>a.Scale=v);inspector.Children.Add(UI.Button("恢复位置与大小",()=>{Checkpoint();a.X=a.Y=.5;a.Scale=1;Changed();BuildInspector();}));Section("圆角、描边与阴影");Slider("内容圆角",0,60,a.Radius,"",v=>a.Radius=v);Slider("外描边",0,8,a.Border,"",v=>a.Border=v);Slider("阴影强度",0,60,a.Shadow,"",v=>a.Shadow=v);}
    else if(frameTab=="样机"){Section("屏幕外框");Buttons(new[]{"无","浅色窗口","深色窗口"},a.Frame=="none"?"无":a.Frame=="dark"?"深色窗口":"浅色窗口",v=>{Checkpoint();a.Frame=v=="无"?"none":v=="深色窗口"?"dark":"window";Changed();BuildInspector();});Note("窗口外框与预览、导出保持一致。");}
    else BuildBackgroundInspector();
   }else if(tab=="运镜"){
    Buttons(new[]{"缩放","屏幕 3D"},"缩放",v=>{if(v=="屏幕 3D")Status("屏幕 3D 尚未完成 Windows 适配，当前版本支持自动与手动缩放。");});
    Toggle("启用自动缩放",a.AutoZoom,v=>a.AutoZoom=v);Toggle("自动跟随鼠标",a.Follow,v=>a.Follow=v);Slider("自动缩放倍率",1,3,a.ZoomScale,"×",v=>{a.ZoomScale=v;foreach(var z in Doc.Zooms.Where(z=>z.Automatic))z.Scale=v;});inspector.Children.Add(UI.Button("重新生成自动运镜",()=>{Checkpoint();Doc.GenerateZooms();Changed();BuildInspector();}));
    if(selectedZoom>=0&&selectedZoom<Doc.Zooms.Count){var z=Doc.Zooms[selectedZoom];Section(z.Automatic?"自动缩放片段":"手动缩放片段");Number("开始 / 秒",z.Start,v=>z.Start=EditDocument.Clamp(v,0,z.End-.05));Number("结束 / 秒",z.End,v=>z.End=EditDocument.Clamp(v,z.Start+.05,Doc.Length));Slider("目标大小",1,3,z.Scale,"×",v=>z.Scale=v);Slider("过渡时长",.05,2,z.Transition," 秒",v=>z.Transition=v);if(!z.Automatic){Section("缩放焦点");PositionPad(z.X,z.Y,(x,y)=>{z.X=x;z.Y=y;});}}
    else{Note(Doc.Pointer.Count==0?"当前素材没有鼠标轨迹，可添加手动缩放，并调整焦点。":"点击时间线上的缩放片段，可以修改范围、倍率和过渡时长。");inspector.Children.Add(UI.Button("＋ 添加手动缩放",AddZoom));}
   }else if(tab=="开场"){Section("开场动效");Buttons(new[]{"无","轻柔进入"},a.Opening>0?"轻柔进入":"无",v=>{Checkpoint();a.Opening=v=="无"?0:.8;Changed();BuildInspector();});if(a.Opening>0)Slider("动效时长",.2,2,a.Opening," 秒",v=>a.Opening=v);Note("淡入与轻微缩放会同时用于播放预览和导出。");}
   else if(tab=="声音"){Section("素材音轨");if(Doc.Audio){Slider("音量",0,1,a.Volume,"",v=>{a.Volume=v;audio.Volume=v;});Note("音轨随片段裁剪、重排和变速同步导出。");}else Note("当前素材没有音轨。新录制时可从录制条选择麦克风。");}
  }
  void Ratios(){if(Doc==null)return;Menu(Find<Button>("Ratio"),new[]{"适应","16:9","4:3","1:1","9:16"},i=>{Checkpoint();Doc.Style.Ratio=new[]{"适应","16:9","4:3","1:1","9:16"}[i];Changed();});}
  void Presets(){PresetGallery();}
  void Split(){if(Doc==null)return;int i;double source=Doc.SourceTime(position,out i);var c=Doc.Cuts[i];if(source-c.Start<.05||c.End-source<.05){Status("请将播放头放在片段中间再分割。");return;}Checkpoint();double end=c.End;c.End=source;Doc.Cuts.Insert(i+1,new Cut {Start=source,End=end,Speed=c.Speed});selectedCut=i;selectedZoom=selectedOverlay=-1;Changed();BuildInspector();}
  void Delete(){if(Doc==null)return;if(selectedOverlay>=0&&selectedOverlay<Doc.Overlays.Count){Checkpoint();Doc.Overlays.RemoveAt(selectedOverlay);selectedOverlay=-1;}else if(selectedZoom>=0&&selectedZoom<Doc.Zooms.Count){Checkpoint();Doc.Zooms.RemoveAt(selectedZoom);selectedZoom=-1;}else if(selectedCut>=0&&Doc.Cuts.Count>1){Checkpoint();Doc.Cuts.RemoveAt(selectedCut);selectedCut=-1;Doc.GenerateZooms();}else{Status("选择要删除的片段；至少保留一个录屏片段。");return;}Changed();BuildInspector();}
  void MoveCut(int direction){if(Doc==null||selectedCut<0)return;int target=selectedCut+direction;if(target<0||target>=Doc.Cuts.Count)return;Checkpoint();var c=Doc.Cuts[selectedCut];Doc.Cuts.RemoveAt(selectedCut);Doc.Cuts.Insert(target,c);selectedCut=target;Doc.GenerateZooms();Changed();}
  void AddZoom(){if(Doc==null)return;Checkpoint();double start=Math.Min(position,Math.Max(0,Doc.Length-.2));Doc.Zooms.Add(new Zoom {Start=start,End=Math.Min(Doc.Length,start+3),Scale=1.6});selectedZoom=Doc.Zooms.Count-1;position=start+Math.Min(1,(Doc.Zooms.Last().End-start)/2);selectedCut=-1;tab="运镜";Changed();BuildRail();BuildInspector();}
  void Seek(double t){Pause();if(Doc==null)return;position=EditDocument.Clamp(t,0,Math.Max(0,Doc.Length-.001));Update();DrawTimeline();RefreshPreview();}
  async void RefreshPreview(){await RequestPreview();}
  void Pause(){if(playback!=null){playback.Cancel();playback=null;}playing=false;audio.Pause();Find<Button>("Play").Content=UI.Icon("play",16);if(Doc!=null)RefreshPreview();}
  public Task PreviewNow(){return RequestPreview();}
  D.Size PreviewSize(EditDocument doc){var stage=Find<Grid>("Stage");var source=PresentationSource.FromVisual(stage);var transform=source!=null&&source.CompositionTarget!=null?source.CompositionTarget.TransformToDevice:Matrix.Identity;return PreviewPixels(SceneLayout.Aspect(doc),Math.Max(1,stage.ActualWidth)*transform.M11,Math.Max(1,stage.ActualHeight)*transform.M22);}
  internal static D.Size PreviewPixels(double ratio,double availableWidth,double availableHeight){double width=Math.Min(availableWidth,availableHeight*ratio);return new D.Size(Math.Max(2,(int)Math.Ceiling(width*1.25/2)*2),Math.Max(2,(int)Math.Ceiling(width/ratio*1.25/2)*2));}
  async Task RequestPreview(){renderVersion++;if(Doc==null||playing||rendering)return;rendering=true;try{int version;do{version=renderVersion;var snapshot=EditDocument.Deserialize(Doc.Serialize());var size=PreviewSize(snapshot);double time=position;int i;double source=snapshot.SourceTime(time,out i);var result=await Task.Run(()=>{
     // Keep the native source pixels for paused inspection and zoomed text.
     if(cachedFrame==null||cachedSource!=snapshot.Source||Math.Abs(cachedTime-source)>.015){cachedFrame=StudioMedia.Frame(snapshot.Source,Math.Min(source,snapshot.Duration-.03),snapshot.Width);cachedSource=snapshot.Source;cachedTime=source;}
     using(var stream=new MemoryStream(cachedFrame))using(var frame=new D.Bitmap(stream))return Compose(frame,snapshot,time,size);
    });if(version==renderVersion&&!playing){preview.Source=result;DrawEffectGuides();}
   }while(version!=renderVersion&&!playing&&!closing);}catch(Exception ex){Status("预览暂不可用："+ex.Message);}finally{rendering=false;}}
  static BitmapSource Compose(D.Bitmap frame,EditDocument doc,double time,D.Size size,MotionEvaluator evaluator=null,D.Image loadedWallpaper=null){int w=size.Width,h=size.Height;using(var own=evaluator==null?new MotionEvaluator(doc):null)using(var canvas=new D.Bitmap(w,h,Imaging.PixelFormat.Format32bppArgb))using(var g=D.Graphics.FromImage(canvas))using(var wallpaper=loadedWallpaper==null&&doc.Style.Background=="image"&&File.Exists(doc.Style.ImagePath)?D.Image.FromFile(doc.Style.ImagePath):null){g.InterpolationMode=D.Drawing2D.InterpolationMode.HighQualityBicubic;g.SmoothingMode=D.Drawing2D.SmoothingMode.AntiAlias;StudioRenderer.Draw(g,frame,doc,evaluator??own,time,w,h,loadedWallpaper??wallpaper,canvas);return UI.Image(canvas);}}
  async void Play(){if(Doc==null||playing)return;if(position>=Doc.Length-.05)position=0;playing=true;overlayGuides.Children.Clear();Find<Button>("Play").Content=UI.Icon("pause",16);var tokenSource=new CancellationTokenSource();playback=tokenSource;var token=tokenSource.Token;var doc=EditDocument.Deserialize(Doc.Serialize());var size=PreviewSize(doc);int playbackFps=Math.Min(90,Math.Max(1,(int)Math.Round(doc.FrameRate)));double start=position;
   try{await Task.Run(()=>{
    int first;double src=doc.SourceTime(start,out first);double offset=doc.Cuts.Take(first).Sum(c=>c.Length);
    using(var evaluator=new MotionEvaluator(doc))using(var wallpaper=doc.Style.Background=="image"&&File.Exists(doc.Style.ImagePath)?D.Image.FromFile(doc.Style.ImagePath):null){
    for(int i=first;i<doc.Cuts.Count;i++){token.ThrowIfCancellationRequested();var cut=doc.Cuts[i];double from=i==first?src:cut.Start,time=i==first?start:offset;
     Window.Dispatcher.Invoke(new Action(()=>{if(!token.IsCancellationRequested){audio.Position=TimeSpan.FromSeconds(from);audio.SpeedRatio=cut.Speed;audio.Volume=doc.Style.Volume;audio.Play();}}));
     double zoom=doc.Zooms.Count>0?doc.Zooms.Max(z=>z.Scale):1;int dw=Math.Min(doc.Width,(int)Math.Ceiling(size.Width*Math.Max(1,zoom*doc.Style.Scale)));dw=Math.Max(2,dw-dw%2);int dh=Math.Max(2,(int)Math.Round((double)dw*doc.Height/doc.Width/2)*2);
     using(var p=Media.Start("ffmpeg","-v error -ss "+Media.N(from)+" -t "+Media.N(cut.End-from)+" -i "+Media.Q(doc.Source)+" -an -vf setpts=(PTS-STARTPTS)/"+Media.N(cut.Speed)+",fps="+playbackFps+",scale="+dw+":"+dh+":flags=lanczos -pix_fmt bgra -f rawvideo pipe:1",true))using(token.Register(()=>{try{p.Kill();}catch{}}))using(var frame=new D.Bitmap(dw,dh,Imaging.PixelFormat.Format32bppArgb)){
      var errors=p.StandardError.ReadToEndAsync();var bytes=new byte[dw*dh*4];int n=0;var clock=Stopwatch.StartNew();
      while(true){token.ThrowIfCancellationRequested();int filled=0,count;while(filled<bytes.Length&&(count=p.StandardOutput.BaseStream.Read(bytes,filled,bytes.Length-filled))>0)filled+=count;if(filled==0)break;if(filled!=bytes.Length)throw new Exception("预览帧不完整。");
       var data=frame.LockBits(new D.Rectangle(0,0,dw,dh),Imaging.ImageLockMode.WriteOnly,Imaging.PixelFormat.Format32bppArgb);try{Marshal.Copy(bytes,0,data.Scan0,bytes.Length);}finally{frame.UnlockBits(data);}
       double t=time+n/(double)playbackFps;if(t>=offset+cut.Length)break;var image=Compose(frame,doc,t,size,evaluator,wallpaper);int wait=(int)(n*1000.0/playbackFps-clock.Elapsed.TotalMilliseconds);if(wait>0&&token.WaitHandle.WaitOne(wait))token.ThrowIfCancellationRequested();
       Window.Dispatcher.Invoke(new Action(()=>{if(!token.IsCancellationRequested){position=t;preview.Source=image;Update();DrawPlayhead();}}));n++;
      }try{if(!p.HasExited)p.Kill();p.WaitForExit();}catch{}
     }offset+=cut.Length;
    }}
   });}catch(OperationCanceledException){}catch(Exception ex){if(!token.IsCancellationRequested)Status("播放失败："+ex.Message);}finally{if(playback==tokenSource){playback=null;playing=false;audio.Pause();Find<Button>("Play").Content=UI.Icon("play",16);RefreshPreview();}tokenSource.Dispose();}
  }
  double TimelineWidth {get {return Math.Max(600,Find<ScrollViewer>("TimelineScroll").ActualWidth-8)*Find<Slider>("TimelineScale").Value;}}
  Line playhead;Ellipse head;
  void DrawPlayhead(){if(playhead==null||Doc==null)return;double x=position/Doc.Length*TimelineWidth;playhead.X1=playhead.X2=x;Canvas.SetLeft(head,x-3);}
  void DrawTimeline(){if(timeline==null)return;timeline.Children.Clear();double width=TimelineWidth;timeline.Width=width;if(Doc==null)return;double px=width/Math.Max(.001,Doc.Length);int steps=Math.Max(2,(int)(width/100));double step=Math.Max(.25,Math.Pow(10,Math.Floor(Math.Log10(Doc.Length/steps))));while(Doc.Length/step>steps*1.8)step*=2;
   for(double t=0;t<Doc.Length;t+=step){var text=UI.Text(UI.Time(t),10,"#8C9498");Canvas.SetLeft(text,t*px+3);Canvas.SetTop(text,5);timeline.Children.Add(text);timeline.Children.Add(new Line {X1=t*px,X2=t*px,Y1=24,Y2=170,Stroke=UI.Brush(dark?"#2D3135":"#F0F1F2"),StrokeThickness=1});}
   double offset=0;for(int i=0;i<Doc.Cuts.Count;i++){int index=i;var cut=Doc.Cuts[i];double start=offset;var box=new Border {Width=Math.Max(4,cut.Length*px-3),Height=60,CornerRadius=new CornerRadius(9),BorderThickness=new Thickness(selectedCut==i?2:1),BorderBrush=UI.Brush(selectedCut==i?"#667870":"#D2D7D8"),Background=UI.Brush("#D8DEDB"),ClipToBounds=true};var film=new Canvas {ClipToBounds=true};box.Child=film;
    PaintFilm(film,box,cut,px);
    var label=new Border {Background=UI.Brush("#AA343A3D"),CornerRadius=new CornerRadius(4),Padding=new Thickness(5,2,5,2),Child=UI.Text(UI.Time(cut.Length),10,"#FFFFFF")};Canvas.SetLeft(label,Math.Max(0,box.Width-66));Canvas.SetTop(label,5);film.Children.Add(label);Canvas.SetLeft(box,start*px);Canvas.SetTop(box,34);timeline.Children.Add(box);
    box.MouseLeftButtonDown+=(s,e)=>{selectedCut=index;selectedZoom=selectedOverlay=-1;Seek(start+e.GetPosition(box).X/px);BuildInspector();e.Handled=true;};
    AddHandle(start*px,34,60,delta=>{var c=Doc.Cuts[index];c.Start=EditDocument.Clamp(c.Start+delta*c.Speed,0,c.End-.05);Doc.GenerateZooms();},selectedCut==i);
    AddHandle((start+cut.Length)*px-5,34,60,delta=>{var c=Doc.Cuts[index];c.End=EditDocument.Clamp(c.End+delta*c.Speed,c.Start+.05,Doc.Duration);Doc.GenerateZooms();},selectedCut==i);
    offset+=cut.Length;
   }
   for(int i=0;i<Doc.Zooms.Count;i++){int index=i;var z=Doc.Zooms[i];var b=new Border {Width=Math.Max(4,(z.End-z.Start)*px),Height=32,CornerRadius=new CornerRadius(7),Background=UI.Brush(dark?(z.Automatic?"#665641":"#594832"):(z.Automatic?"#E7D7B9":"#D9C7A6")),BorderThickness=new Thickness(selectedZoom==i?2:1),BorderBrush=UI.Brush(selectedZoom==i?"#AE8F5C":"#D9C7A6"),Child=new TextBlock {Text="缩放 "+z.Scale.ToString("0.0")+"× · "+(z.Automatic?"自动":"手动"),FontSize=10,Foreground=UI.Brush(dark?"#F3EADF":"#5D5140"),VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis}};Canvas.SetLeft(b,z.Start*px);Canvas.SetTop(b,110);timeline.Children.Add(b);b.MouseLeftButtonDown+=(s,e)=>{selectedZoom=index;selectedCut=selectedOverlay=-1;tab="运镜";Seek(z.Start+e.GetPosition(b).X/px);BuildRail();BuildInspector();e.Handled=true;};AddHandle(z.Start*px,110,32,d=>z.Start=EditDocument.Clamp(z.Start+d,0,z.End-.1),selectedZoom==i);AddHandle(z.End*px-5,110,32,d=>z.End=EditDocument.Clamp(z.End+d,z.Start+.1,Doc.Length),selectedZoom==i);}
   AddEffectTracks(px);foreach(var gridLine in timeline.Children.OfType<Line>())gridLine.Y2=timeline.Height-8;playhead=new Line {X1=0,X2=0,Y1=20,Y2=timeline.Height-9,Stroke=UI.Brush(dark?"#F3F5F4":"#34393B"),StrokeThickness=1.2,IsHitTestVisible=false};head=new Ellipse {Width=6,Height=6,Fill=playhead.Stroke,IsHitTestVisible=false};Canvas.SetTop(head,19);timeline.Children.Add(playhead);timeline.Children.Add(head);DrawPlayhead();timeline.MouseLeftButtonDown-=TimelineSeek;timeline.MouseLeftButtonDown+=TimelineSeek;
  }
  void TimelineSeek(object s,MouseButtonEventArgs e){if(Doc==null)return;Seek(e.GetPosition(timeline).X/TimelineWidth*Doc.Length);e.Handled=true;}
  void AddHandle(double x,double y,double h,Action<double> adjust,bool visible){if(!visible)return;var handle=new Border {Width=6,Height=h,Background=UI.Brush("#B1A18A"),Cursor=Cursors.SizeWE};Canvas.SetLeft(handle,x);Canvas.SetTop(handle,y);timeline.Children.Add(handle);double previous=0;bool dragging=false;handle.MouseLeftButtonDown+=(s,e)=>{Checkpoint();previous=e.GetPosition(timeline).X;dragging=true;handle.CaptureMouse();e.Handled=true;};handle.MouseMove+=(s,e)=>{if(!dragging)return;double current=e.GetPosition(timeline).X;double delta=(current-previous)/TimelineWidth*Doc.Length;if(snap)delta=Math.Round(delta*30)/30;adjust(delta);previous=current;Canvas.SetLeft(handle,Math.Max(0,current-3));motion=new MotionEvaluator(Doc);RefreshPreview();};handle.MouseLeftButtonUp+=(s,e)=>{dragging=false;handle.ReleaseMouseCapture();Changed();BuildInspector();e.Handled=true;};}
  void Export(){if(Doc==null)return;Pause();var dialog=new ExportWindow(this,Doc,preview.Source);dialog.ShowDialog();}
  static void VerifyPreviewDetail(string folder){
   var landscape=PreviewPixels(16.0/9,1280,900);var portrait=PreviewPixels(9.0/16,1280,900);
   if(landscape.Width<1600||portrait.Height<1124)throw new Exception("高 DPI／竖屏预览仍被固定低分辨率限制");
   string image=Path.Combine(folder,"preview-detail-source.png");using(var chart=new D.Bitmap(2560,1440))using(var g=D.Graphics.FromImage(chart)){g.Clear(D.Color.White);for(int x=0;x<chart.Width;x+=4)g.FillRectangle(D.Brushes.Black,x,0,2,chart.Height);chart.Save(image,Imaging.ImageFormat.Png);}
   var doc=new EditDocument {Source=image,Width=2560,Height=1440,Duration=3};doc.Cuts.Add(new Cut {Start=0,End=3});doc.Style.Padding=0;doc.Style.Frame="none";doc.Style.Radius=0;doc.Style.Shadow=0;doc.Style.Follow=false;doc.Style.AutoZoom=false;doc.Zooms.Add(new Zoom {Start=0,End=3,Scale=3,Transition=.05});
   BitmapSource sharp,legacy;using(var s=new MemoryStream(StudioMedia.Frame(image,0,doc.Width)))using(var frame=new D.Bitmap(s)){if(frame.Width!=2560)throw new Exception("暂停预览解码丢失素材像素");sharp=Compose(frame,doc,1,landscape);}
   using(var s=new MemoryStream(StudioMedia.Frame(image,0,960)))using(var frame=new D.Bitmap(s))legacy=Compose(frame,doc,1,landscape);
   double nativeContrast=PreviewContrast(sharp),oldContrast=PreviewContrast(legacy);if(nativeContrast<30||nativeContrast<oldContrast*2)throw new Exception("高频细节测试失败：高清 "+nativeContrast+"，旧预览 "+oldContrast);
   File.WriteAllText(Path.Combine(folder,"preview-quality-results.txt"),"PASS: native 2560px source; 1600px canvas at display width 1280; zoomed fine-line contrast "+nativeContrast.ToString("F1")+" vs legacy "+oldContrast.ToString("F1")+"; portrait follows physical display height.\n");
  }
  static double PreviewContrast(BitmapSource image){var pixels=new byte[512*4];image.CopyPixels(new Int32Rect((image.PixelWidth-512)/2,image.PixelHeight/2,512,1),pixels,512*4,0);double average=0,sum=0;for(int i=0;i<512;i++)average+=pixels[i*4];average/=512;for(int i=0;i<512;i++)sum+=Math.Pow(pixels[i*4]-average,2);return Math.Sqrt(sum/512);}
  public async Task VerifyInterface(){
   string folder=Path.Combine(Media.Root,"bin","editor-test");await Task.Run(()=>VerifyPreviewDetail(folder));await Load(Path.Combine(folder,"project.dogscwin"));
   if(Doc==null)throw new Exception("项目未能加载");int count=Doc.Cuts.Count;Seek(1);Find<Button>("Split").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(Doc.Cuts.Count!=count+1)throw new Exception("分割按钮未生效");
   Find<Button>("Undo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(Doc.Cuts.Count!=count)throw new Exception("撤销未恢复片段");
   Find<Button>("Redo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(Doc.Cuts.Count!=count+1)throw new Exception("重做未恢复分割");History(false);
   Seek(1);int zoomCount=Doc.Zooms.Count;Find<Button>("AddZoom").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(Doc.Zooms.Count!=zoomCount+1)throw new Exception("手动缩放未添加");History(false);
   Seek(0);audio.Volume=0;Doc.Style.Volume=0;Play();await Task.Delay(1200);if(position<.2)throw new Exception("播放头未前进");Pause();Doc.Style.Volume=1;Seek(0);await RequestPreview();await Task.Delay(700);
   tab="画面";selectedCut=selectedZoom=-1;BuildRail();BuildInspector();dirty=false;Update();UI.SaveImage((FrameworkElement)Window.Content,Path.Combine(Media.Root,"bin","editor-preview.png"));
   var export=new ExportWindow(this,Doc,preview.Source);export.Show();await Task.Delay(150);UI.SaveImage((FrameworkElement)export.Content,Path.Combine(Media.Root,"bin","export-preview-v2.png"));export.Close();
   var recording=new RecorderWindow(this);recording.Show();await Task.Delay(150);UI.SaveImage((FrameworkElement)recording.Content,Path.Combine(Media.Root,"bin","recorder-preview.png"));await recording.VerifyCapture();
   File.WriteAllText(Path.Combine(folder,"interface-results.txt"),"PASS\nActual UI commands: load, split, undo, redo, add manual zoom, playback advance, pause, seek; editor/export/recorder rendered.\nNew recorder captured only a synthetic test window, stopped, saved a project and reopened it in the editor. No desktop or microphone captured.\n");
  }
 }
}
