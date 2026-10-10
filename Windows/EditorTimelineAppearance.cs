using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DogSC.Windows;

namespace DogSC.Replica {
 public partial class Studio {
  BitmapSource waveform;bool waveformView;
  static BitmapSource ReadWaveform(EditDocument doc,System.Threading.CancellationToken token=default(System.Threading.CancellationToken)){if(!doc.Audio)return null;try{using(var process=Media.Start("ffmpeg","-v error -threads 2 -filter_complex_threads 2 -i "+Media.Q(doc.Source)+" -filter_complex "+Media.Q("[0:a:0]aformat=channel_layouts=mono,showwavespic=s=1600x28:colors=0x7f8585:scale=sqrt:draw=full[wave]")+" -map [wave] -frames:v 1 -c:v png -f image2pipe pipe:1",true))using(token.Register(()=>{try{if(!process.HasExited)process.Kill();}catch{}}))using(var bytes=new MemoryStream()){token.ThrowIfCancellationRequested();var error=process.StandardError.ReadToEndAsync();process.StandardOutput.BaseStream.CopyTo(bytes);process.WaitForExit();token.ThrowIfCancellationRequested();return process.ExitCode==0&&bytes.Length>0?UI.Image(bytes.ToArray()):null;}}catch{return null;}}
  void PaintFilm(Canvas film,Border box,Cut cut,double px){film.Clip=new RectangleGeometry(new Rect(0,0,Math.Max(1,box.Width-2),58),8,8);bool wave=Doc.Audio&&waveform!=null;double imageHeight=wave?44:58;
   if(!waveformView&&thumbs.Count>0)for(double x=0;x<box.Width;x+=95){double source=cut.Start+x/px*cut.Speed;int k=Math.Min(thumbs.Count-1,(int)(source/Doc.Duration*8));var image=new Image {Source=thumbs[Math.Max(0,k)],Width=96,Height=imageHeight,Stretch=Stretch.UniformToFill};Canvas.SetLeft(image,x);film.Children.Add(image);}
   if(wave){int start=Math.Min(waveform.PixelWidth-1,Math.Max(0,(int)(cut.Start/Doc.Duration*waveform.PixelWidth))),end=Math.Min(waveform.PixelWidth,Math.Max(start+1,(int)Math.Ceiling(cut.End/Doc.Duration*waveform.PixelWidth)));var crop=new CroppedBitmap(waveform,new Int32Rect(start,0,end-start,waveform.PixelHeight));crop.Freeze();double height=waveformView?54:14;var background=new Border {Width=Math.Max(1,box.Width-2),Height=height,Background=UI.Brush(dark?"#303436":"#F3F4F3"),Child=new Image {Source=crop,Stretch=Stretch.Fill}};Canvas.SetTop(background,waveformView?2:44);film.Children.Add(background);}
   else if(waveformView){var message=UI.Text("正在读取音轨…",10,"#858A8E");Canvas.SetTop(message,22);Canvas.SetLeft(message,12);film.Children.Add(message);}
  }
  void SetTimelineView(bool audio){waveformView=audio;UI.Role(Find<Button>("TimelineView"),Control.BackgroundProperty,audio?"Well":"SelectedControl");UI.Role(Find<Button>("Waveforms"),Control.BackgroundProperty,audio?"SelectedControl":"Well");DrawTimeline();}
 }
}
