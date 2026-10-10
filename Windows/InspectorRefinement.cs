using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace DogSC.Replica {
 public partial class Studio {
  readonly Dictionary<string,double> inspectorOffsets=new Dictionary<string,double>();
  readonly Dictionary<string,bool> inspectorExpansions=new Dictionary<string,bool>();
  readonly List<Action> inspectorSummaries=new List<Action>();
  void PrepareRefinedInspector(){
   string scope=(Doc==null?"":Doc.Source)+"/"+(selectedCut>=0?"片段":tab=="画面"?tab+frameTab:tab=="运镜"?tab+motionTab:tab+(selectedOverlay>=0?"片段":"列表"));
   var scroll=Find<ScrollViewer>("InspectorScroll");if(scope!=inspectorScope){if(inspectorScope.Length>0)inspectorOffsets[inspectorScope]=scroll.VerticalOffset;inspectorScope=scope;double offset;inspectorOffsets.TryGetValue(scope,out offset);Window.Dispatcher.BeginInvoke(new Action(()=>{if(inspectorScope==scope)scroll.ScrollToVerticalOffset(offset);}),DispatcherPriority.Loaded);}
   Find<Border>("InspectorNavigation").Visibility=Visibility.Collapsed;Find<StackPanel>("InspectorNavigationItems").Children.Clear();inspectorSummaries.Clear();
  }
  void UpdateInspectorHeading(){
   string title=selectedCut>=0?"片段":tab,scope=null;
   if(Doc!=null){if(selectedCut>=0)scope="当前片段";else if(tab=="运镜")scope=motionTab=="屏幕 3D"?(selectedScreen>=0?"屏幕 3D 片段":"屏幕 3D"):(selectedZoom>=0?"缩放片段":"缩放");else if(selectedOverlay>=0)scope="当前片段";else if(tab=="光标"||tab=="开场"||tab=="声音")scope="全片";}
   Find<TextBlock>("InspectorTitle").Text=title;Find<TextBlock>("InspectorScopeTitle").Text=scope??"";Find<Border>("InspectorScope").Visibility=scope==null?Visibility.Collapsed:Visibility.Visible;
  }
  void InspectorResetLayout(Action reset){var row=new DockPanel {Margin=new Thickness(0,7,0,0)};var button=UI.Button("恢复位置与大小",reset);button.Style=(Style)Window.FindResource("PlainButton");button.FontSize=11;button.Padding=new Thickness(3);button.Margin=new Thickness(0);button.MinHeight=24;DockPanel.SetDock(button,Dock.Right);row.Children.Add(button);var hint=UI.Text("可直接在画面中拖动",11,"#8A9296");hint.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(hint);inspector.Children.Add(row);}
  void InspectorDisclosure(string title,Func<string> detail,Action build){
   string key=inspectorScope+"/"+title;int start=inspector.Children.Count;build();var content=new StackPanel();while(inspector.Children.Count>start){var child=inspector.Children[start];inspector.Children.RemoveAt(start);content.Children.Add(child);}
   var header=new StackPanel();header.Children.Add(UI.Text(title,13));var summary=UI.Text(detail(),11,"#63696D");summary.Margin=new Thickness(0,3,0,0);header.Children.Add(summary);inspectorSummaries.Add(()=>summary.Text=detail());bool expanded;inspectorExpansions.TryGetValue(key,out expanded);var disclosure=new Expander {Header=header,Content=content,IsExpanded=expanded,Style=(Style)Window.FindResource("InspectorDisclosure"),Margin=new Thickness(0,8,0,4),ToolTip=title};disclosure.Expanded+=(s,e)=>inspectorExpansions[key]=true;disclosure.Collapsed+=(s,e)=>inspectorExpansions[key]=false;inspector.Children.Add(disclosure);
  }
  void RefreshInspectorSummaries(){foreach(var refresh in inspectorSummaries)refresh();}
  static bool TryInspectorColor(string input,out string color){string value=input.Trim().TrimStart('#');if(value.Length==3)value=new string(new[]{value[0],value[0],value[1],value[1],value[2],value[2]});int rgb;if(value.Length==6&&int.TryParse(value,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out rgb)){color="#"+value.ToUpperInvariant();return true;}color=null;return false;}
  void InspectorHexColor(string label,string value,Action<string> change){
   var row=new DockPanel {Margin=new Thickness(0,5,0,8)};var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(32)});grid.ColumnDefinitions.Add(new ColumnDefinition());var capsule=new Border {Width=132,Height=32,CornerRadius=new CornerRadius(16),BorderThickness=new Thickness(.7),Child=grid};UI.Role(capsule,Border.BackgroundProperty,"Well");UI.Role(capsule,Border.BorderBrushProperty,"Line");DockPanel.SetDock(capsule,Dock.Right);row.Children.Add(capsule);var title=UI.Text(label.TrimEnd('…'),13,"#63696D");title.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(title);inspector.Children.Add(row);
   var swatch=new Border {Width=16,Height=16,CornerRadius=new CornerRadius(5),Background=UI.Brush(value),BorderBrush=UI.Brush("#30808080"),BorderThickness=new Thickness(.6)};var box=UI.Pill(Window,value.ToUpperInvariant(),96);box.Tag="color:"+label;box.Height=30;box.Background=System.Windows.Media.Brushes.Transparent;box.BorderThickness=new Thickness(0);box.BorderBrush=System.Windows.Media.Brushes.Transparent;box.ToolTip="输入十六进制颜色，如 #8FD7D4，按 Enter 应用";Grid.SetColumn(box,1);grid.Children.Add(box);
   Action<string> apply=color=>{if(!string.Equals(value,color,StringComparison.OrdinalIgnoreCase)){Checkpoint();value=color;change(color);Changed();BuildInspector();}else box.Text=value.ToUpperInvariant();};Button picker=null;picker=UI.Button("",()=>{bool edited=false;var palette=new ColorPalette(Window,picker,label,value,color=>{if(string.Equals(value,color,StringComparison.OrdinalIgnoreCase))return;if(!edited){Checkpoint();edited=true;}value=color;change(color);box.Text=color;swatch.Background=UI.Brush(color);Changed();});palette.Dismissed+=()=>{if(edited){BuildInspector();Window.Dispatcher.BeginInvoke(new Action(()=>{var restored=System.Linq.Enumerable.FirstOrDefault(UI.Descendants<Button>(inspector),b=>(string)b.Tag=="color-picker:"+label);if(restored!=null)restored.Focus();}),DispatcherPriority.Input);}};palette.Open();});picker.Tag="color-picker:"+label;picker.Content=swatch;picker.Style=(Style)Window.FindResource("PlainButton");picker.Padding=new Thickness(5);picker.Margin=new Thickness(0);picker.ToolTip="打开颜色选择器";grid.Children.Add(picker);box.LostKeyboardFocus+=(s,e)=>{string color;if(TryInspectorColor(box.Text,out color))apply(color);else box.Text=value.ToUpperInvariant();};
  }
 }
}
