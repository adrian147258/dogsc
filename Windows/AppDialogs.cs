using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DogSC.Replica {
 // Matches the original AppDialog card. A closed card always cancels; only
 // an explicit primary action is the Return key's default action.
 sealed class DialogAction {
  public readonly string Id,Text;public readonly bool Primary,RequiresInput,Destructive;
  public DialogAction(string id,string text,bool primary=false,bool requiresInput=false,bool destructive=false){Id=id;Text=text;Primary=primary;RequiresInput=requiresInput;Destructive=destructive;}
 }
 sealed class AppDialogWindow:Window {
  public string Result;public readonly TextBox Input;public readonly Dictionary<string,Button> Actions=new Dictionary<string,Button>();
  public AppDialogWindow(Window owner,string title,string message,DialogAction[] actions,string item=null,string input=null){
   Owner=owner;Title=title;Width=440;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;WindowStartupLocation=owner==null?WindowStartupLocation.CenterScreen:WindowStartupLocation.CenterOwner;FontSize=13;
   if(owner!=null){Resources=owner.Resources;FontFamily=owner.FontFamily;Topmost=owner.Topmost;}UI.Chrome(this,24);UI.Role(this,Control.BackgroundProperty,"Panel");UI.Role(this,Control.ForegroundProperty,"Ink");
   var content=new StackPanel {Margin=new Thickness(26)};var card=new Border {CornerRadius=new CornerRadius(24),BorderThickness=new Thickness(.75),Child=content};UI.Role(card,Border.BackgroundProperty,"Panel");UI.Role(card,Border.BorderBrushProperty,"Line");Content=card;
   var header=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,20)};var symbol=new Border {Width=46,Height=46,CornerRadius=new CornerRadius(14),Child=UI.Icon(input!=null?"pencil":item!=null?"folder":"info",21)};UI.Role(symbol,Border.BackgroundProperty,"Well");header.Children.Add(symbol);var heading=UI.Text(title,19);heading.FontWeight=FontWeights.SemiBold;heading.Margin=new Thickness(13,0,0,0);heading.VerticalAlignment=VerticalAlignment.Center;header.Children.Add(heading);content.Children.Add(header);header.MouseLeftButtonDown+=(s,e)=>{if(!UI.InteractiveSource(e.OriginalSource as DependencyObject))try{DragMove();}catch{}};
   if(!string.IsNullOrEmpty(message)){var body=UI.Text(message,13,"#858A8E");body.LineHeight=21;var scroll=new ScrollViewer {Content=body,MaxHeight=180,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(0,0,0,20)};scroll.SetResourceReference(StyleProperty,"SlimScrollViewer");content.Children.Add(scroll);}
   if(!string.IsNullOrEmpty(item)){var row=new Border {CornerRadius=new CornerRadius(12),Padding=new Thickness(14),Margin=new Thickness(0,0,0,20),Child=UI.Text(item,14)};UI.Role(row,Border.BackgroundProperty,"Well");content.Children.Add(row);}
   if(input!=null){Input=new TextBox {Text=input,Height=42,FontSize=14,Padding=new Thickness(13,5,13,5),Margin=new Thickness(0,0,0,20),MaxLength=160};System.Windows.Automation.AutomationProperties.SetName(Input,title);content.Children.Add(Input);}
   var footer=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,2,0,0)};content.Children.Add(footer);
   foreach(var action in actions){var a=action;var b=UI.Button(a.Text,()=>Choose(a));b.MinWidth=84;b.Height=38;b.Padding=new Thickness(16,0,16,0);b.Margin=new Thickness(8,0,0,0);b.IsDefault=a.Primary;b.IsCancel=a.Id=="cancel";if(a.Primary)b.SetResourceReference(StyleProperty,"AccentButton");else {UI.Role(b,Control.BackgroundProperty,"Well");if(a.Destructive){b.Foreground=UI.Brush("#D95050");b.Background=UI.Brush("#15D95050");}}Actions[a.Id]=b;footer.Children.Add(b);}
   if(Input!=null)Input.TextChanged+=(s,e)=>UpdateActions(actions);UpdateActions(actions);
   PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){Result=actions.Any(a=>a.Id=="cancel")?"cancel":null;Close();e.Handled=true;}};
   Loaded+=(s,e)=>{UI.ClampDialog(this);Dispatcher.BeginInvoke(new Action(()=>{if(Input!=null){Input.Focus();Input.SelectAll();}else {var entry=actions.FirstOrDefault(a=>a.Primary)??actions.FirstOrDefault(a=>a.Id=="cancel")??actions.FirstOrDefault();if(entry!=null)Actions[entry.Id].Focus();}}),DispatcherPriority.Input);};
  }
  void UpdateActions(DialogAction[] actions){foreach(var a in actions)Actions[a.Id].IsEnabled=!a.RequiresInput||Input!=null&&!string.IsNullOrWhiteSpace(Input.Text);}
  void Choose(DialogAction action){if(!Actions[action.Id].IsEnabled)return;Result=action.Id;Close();}
 }
 static class AppDialogs {
  public static void Notice(Window owner,string title,string message){new AppDialogWindow(owner,title,message,new[]{new DialogAction("ok","知道了",true)}).ShowDialog();}
  public static string Unsaved(Window owner,string name){var dialog=new AppDialogWindow(owner,"保存项目修改？","保存后可继续编辑，也可以放弃本次修改。",new[]{new DialogAction("cancel","取消"),new DialogAction("discard","不保存",false,false,true),new DialogAction("save","保存",true)},name);dialog.ShowDialog();return dialog.Result;}
  public static string Rename(Window owner,string name){var dialog=new AppDialogWindow(owner,"项目名称","为这个作品取一个名字。",new[]{new DialogAction("cancel","取消"),new DialogAction("save","保存",true,true)},null,name);dialog.ShowDialog();return dialog.Result=="save"?dialog.Input.Text.Trim():null;}
 }
 sealed class FormsWindowOwner:System.Windows.Forms.IWin32Window {
  public IntPtr Handle {get;private set;}public FormsWindowOwner(Window window){Handle=new WindowInteropHelper(window).Handle;}
 }
 static partial class UI {
  public static void ClampDialog(Window window){var work=SystemParameters.WorkArea;if(window.Owner!=null){var source=PresentationSource.FromVisual(window.Owner);var area=System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(window.Owner).Handle).WorkingArea;if(source!=null&&source.CompositionTarget!=null){var matrix=source.CompositionTarget.TransformFromDevice;work=new Rect(matrix.Transform(new Point(area.Left,area.Top)),matrix.Transform(new Point(area.Right,area.Bottom)));}}window.Left=Math.Max(work.Left+16,Math.Min(window.Left,work.Right-window.ActualWidth-16));window.Top=Math.Max(work.Top+16,Math.Min(window.Top,work.Bottom-window.ActualHeight-16));}
  public static ContextMenu PopupMenu(Window owner,FrameworkElement target){var menu=new ContextMenu {Resources=owner.Resources,FontFamily=owner.FontFamily,FontSize=12,PlacementTarget=target,Placement=PlacementMode.Bottom,VerticalOffset=6};menu.Opened+=(s,e)=>menu.Dispatcher.BeginInvoke(new Action(()=>{var first=menu.Items.OfType<MenuItem>().FirstOrDefault(i=>i.IsEnabled);if(first!=null)first.Focus();}),DispatcherPriority.Input);menu.Closed+=(s,e)=>{if(target.IsVisible&&target.IsEnabled)target.Focus();};return menu;}
  public static void MenuKeyboardEntry(Button button){button.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Down&&Keyboard.Modifiers==ModifierKeys.None){button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));e.Handled=true;}};}
  public static bool PresentedKeyTarget(object focused){for(var node=focused as DependencyObject;node!=null;){if(node is ContextMenu||node is ColorPalette)return true;var visual=node as Visual;node=visual!=null?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node);}return false;}
 }
 // Original palette geometry: 272 pt card, 164 pt saturation/brightness
 // plane, hue strip and HEX field. Closing keeps the previewed color.
 sealed class ColorPalette:Border {
  readonly Popup popup;readonly Action<string> preview;readonly Canvas plane,hueStrip;readonly Ellipse planeThumb,hueThumb;readonly Rectangle hueFill;readonly Border swatch;readonly TextBlock hint;readonly TextBox hex;
  double hue,saturation,brightness;string color;bool updating;
  public event Action Dismissed;public string Value {get{return color;}}public TextBox HexInput {get{return hex;}}
  public ColorPalette(Window owner,FrameworkElement anchor,string title,string value,Action<string> onPreview){
   preview=onPreview;Resources=owner.Resources;TextElement.SetFontFamily(this,owner.FontFamily);Width=306;Padding=new Thickness(16);CornerRadius=new CornerRadius(14);BorderThickness=new Thickness(.75);UI.Role(this,BackgroundProperty,"Surface");UI.Role(this,BorderBrushProperty,"Line");var stack=new StackPanel();Child=stack;
   popup=new Popup {Child=this,PlacementTarget=anchor,Placement=PlacementMode.Bottom,VerticalOffset=6,StaysOpen=false,AllowsTransparency=true};popup.Closed+=(s,e)=>{if(Dismissed!=null)Dismissed();if(anchor.IsVisible)anchor.Focus();};
   var header=new Grid {Margin=new Thickness(0,0,0,14)};var caption=UI.Text(title,13);caption.FontWeight=FontWeights.SemiBold;header.Children.Add(caption);var close=UI.Button("",()=>popup.IsOpen=false);close.Content=UI.Icon("close",10);close.Width=close.Height=24;close.MinHeight=24;close.Padding=new Thickness(6);close.Margin=new Thickness(0);close.HorizontalAlignment=HorizontalAlignment.Right;close.SetResourceReference(StyleProperty,"PlainButton");close.ToolTip="关闭调色面板";header.Children.Add(close);stack.Children.Add(header);
   plane=new Canvas {Width=272,Height=164,Focusable=true,Clip=new RectangleGeometry(new Rect(0,0,272,164),10,10)};hueFill=new Rectangle {Width=272,Height=164};plane.Children.Add(hueFill);plane.Children.Add(new Rectangle {Width=272,Height=164,Fill=new LinearGradientBrush(Colors.White,Colors.Transparent,0)});plane.Children.Add(new Rectangle {Width=272,Height=164,Fill=new LinearGradientBrush(Colors.Transparent,Colors.Black,90)});planeThumb=Thumb();plane.Children.Add(planeThumb);stack.Children.Add(plane);
   hueStrip=new Canvas {Width=272,Height=20,Focusable=true,Background=Brushes.Transparent,Margin=new Thickness(0,14,0,14)};var strip=new Rectangle {Width=256,Height=10,RadiusX=5,RadiusY=5};var rainbow=new LinearGradientBrush {StartPoint=new Point(0,.5),EndPoint=new Point(1,.5)};for(int i=0;i<=6;i++)rainbow.GradientStops.Add(new GradientStop(Hsv(i/6.0,1,1),i/6.0));strip.Fill=rainbow;Canvas.SetLeft(strip,8);Canvas.SetTop(strip,5);hueStrip.Children.Add(strip);hueThumb=Thumb();hueStrip.Children.Add(hueThumb);Canvas.SetTop(hueThumb,2);stack.Children.Add(hueStrip);
   var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(42)});row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(40)});row.ColumnDefinitions.Add(new ColumnDefinition());swatch=new Border {Width=32,Height=32,CornerRadius=new CornerRadius(8),HorizontalAlignment=HorizontalAlignment.Left};row.Children.Add(swatch);var label=UI.Text("HEX",10,"#858A8E");label.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(label,1);row.Children.Add(label);hex=new TextBox {Height=32,FontFamily=new FontFamily("Consolas"),FontSize=12,Padding=new Thickness(10,3,10,3),MaxLength=7};System.Windows.Automation.AutomationProperties.SetName(hex,title+"十六进制值");Grid.SetColumn(hex,2);row.Children.Add(hex);stack.Children.Add(row);hint=UI.Text("请输入六位色值，例如 #EDA647",10,"#858A8E");hint.Margin=new Thickness(0,6,0,0);hint.Visibility=Visibility.Collapsed;stack.Children.Add(hint);
   Rebase(value);hex.TextChanged+=(s,e)=>{if(updating)return;string valid;if(TryHex(hex.Text,out valid)){hint.Visibility=Visibility.Collapsed;if(valid!=color){Rebase(valid);preview(valid);}}else hint.Visibility=Visibility.Visible;};hex.KeyDown+=(s,e)=>{if(e.Key==Key.Enter){Refresh();e.Handled=true;}};hex.LostKeyboardFocus+=(s,e)=>Refresh();
   Drag(plane,p=>{saturation=Clamp(p.X/272);brightness=1-Clamp(p.Y/164);Publish();});Drag(hueStrip,p=>{hue=Clamp((p.X-8)/256);Publish();});
   plane.KeyDown+=(s,e)=>{if(e.Key==Key.Left||e.Key==Key.Right)saturation=Clamp(saturation+(e.Key==Key.Left?-.02:.02));else if(e.Key==Key.Up||e.Key==Key.Down)brightness=Clamp(brightness+(e.Key==Key.Down?-.02:.02));else return;Publish();e.Handled=true;};hueStrip.KeyDown+=(s,e)=>{if(e.Key!=Key.Left&&e.Key!=Key.Right)return;hue=Clamp(hue+(e.Key==Key.Left?-.01:.01));Publish();e.Handled=true;};PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){popup.IsOpen=false;e.Handled=true;}};
   System.Windows.Automation.AutomationProperties.SetName(plane,"饱和度与亮度，方向键调整");System.Windows.Automation.AutomationProperties.SetName(hueStrip,"色相，左右方向键调整");Loaded+=(s,e)=>hex.Focus();
  }
  public void Open(){popup.IsOpen=true;}public void Close(){popup.IsOpen=false;}
  public static bool TryHex(string text,out string value){string raw=(text??"").Trim().TrimStart('#');if(raw.Length==3)raw=new string(new[]{raw[0],raw[0],raw[1],raw[1],raw[2],raw[2]});int rgb;if(raw.Length==6&&int.TryParse(raw,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out rgb)){value="#"+raw.ToUpperInvariant();return true;}value=null;return false;}
  static Ellipse Thumb(){return new Ellipse {Width=16,Height=16,Stroke=Brushes.White,StrokeThickness=2,IsHitTestVisible=false,Effect=new System.Windows.Media.Effects.DropShadowEffect {BlurRadius=4,ShadowDepth=1,Opacity=.3}};}
  static void Drag(Canvas surface,Action<Point> move){surface.MouseLeftButtonDown+=(s,e)=>{surface.Focus();surface.CaptureMouse();move(e.GetPosition(surface));e.Handled=true;};surface.MouseMove+=(s,e)=>{if(surface.IsMouseCaptured&&e.LeftButton==MouseButtonState.Pressed)move(e.GetPosition(surface));};surface.MouseLeftButtonUp+=(s,e)=>surface.ReleaseMouseCapture();surface.LostMouseCapture+=(s,e)=>{};}
  static double Clamp(double value){return Math.Max(0,Math.Min(1,value));}
  static Color Hsv(double h,double s,double v){double segment=h*6,x=v*s*(1-Math.Abs(segment%2-1)),c=v*s,m=v-c;double r=0,g=0,b=0;int i=(int)segment%6;if(i==0){r=c;g=x;}else if(i==1){r=x;g=c;}else if(i==2){g=c;b=x;}else if(i==3){g=x;b=c;}else if(i==4){r=x;b=c;}else {r=c;b=x;}return Color.FromRgb((byte)Math.Round((r+m)*255),(byte)Math.Round((g+m)*255),(byte)Math.Round((b+m)*255));}
  void Rebase(string value){color=value.ToUpperInvariant();var rgb=(Color)ColorConverter.ConvertFromString(color);double r=rgb.R/255.0,g=rgb.G/255.0,b=rgb.B/255.0,max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),delta=max-min;brightness=max;saturation=max==0?0:delta/max;if(delta>.0001){double h=max==r?(g-b)/delta:max==g?2+(b-r)/delta:4+(r-g)/delta;hue=(h/6+1)%1;}Refresh();}
  void Publish(){var rgb=Hsv(hue,saturation,brightness);string next="#"+rgb.R.ToString("X2")+rgb.G.ToString("X2")+rgb.B.ToString("X2");if(next!=color){color=next;Refresh();preview(color);}else Refresh();}
  void Refresh(){updating=true;hex.Text=color;hint.Visibility=Visibility.Collapsed;var fill=UI.Brush(color);swatch.Background=fill;planeThumb.Fill=fill;hueFill.Fill=new SolidColorBrush(Hsv(hue,1,1));hueThumb.Fill=hueFill.Fill;Canvas.SetLeft(planeThumb,Math.Max(0,Math.Min(256,saturation*272-8)));Canvas.SetTop(planeThumb,Math.Max(0,Math.Min(148,(1-brightness)*164-8)));Canvas.SetLeft(hueThumb,hue*256);updating=false;}
 }
}
