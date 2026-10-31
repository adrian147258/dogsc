using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DogSC.Windows;

namespace DogSC.Replica {
 public partial class Studio {
  public async Task VerifyPlaybackRate(string folder){Directory.CreateDirectory(folder);string source=Path.Combine(folder,"source.mp4");await Task.Run(()=>StudioMedia.Run("-v error -y -f lavfi -i testsrc2=size=640x360:rate=60 -f lavfi -i sine=frequency=440:sample_rate=48000 -t 4 -c:v libx264 -preset ultrafast -c:a aac "+Media.Q(source),CancellationToken.None));await Load(source);if(Doc==null||Doc.Cuts.Count!=1||Doc.Cuts[0].Speed!=1||!Doc.Audio)throw new Exception("录制项目没有默认 1 倍速或音轨");Doc.Style.AutoZoom=true;Doc.Style.Follow=true;Doc.Zooms.Clear();Doc.Zooms.Add(new Zoom {Start=0,End=Doc.Length,Scale=2,Automatic=true});Doc.IndependentCursor=true;for(int i=0;i<240;i++)Doc.Pointer.Add(new PointerSample {Time=i/60.0,X=.3+.4*i/240.0,Y=.5,Click=i%60==0});Doc.Style.Volume=0;audio.Volume=0;playbackTestDelay=70;Seek(0);Play();var startup=Stopwatch.StartNew();while(position<.05&&playing&&startup.Elapsed.TotalSeconds<8)await Task.Delay(30);if(position<.05)throw new Exception("预览未启动");double start=position;var watch=Stopwatch.StartNew();await Task.Delay(1600);double advanced=position-start,elapsed=watch.Elapsed.TotalSeconds;if(advanced<elapsed*.7||advanced>elapsed*1.35)throw new Exception("播放进度与真实时间不符："+advanced+" / "+elapsed);var endWatch=Stopwatch.StartNew();while(playing&&endWatch.Elapsed.TotalSeconds<7)await Task.Delay(30);if(playing||position<3.8)throw new Exception("播放未按正常速度结束："+position);playbackTestDelay=0;Doc.Cuts[0].Speed=.5;Seek(0);Play();startup.Restart();while(position<.05&&playing&&startup.Elapsed.TotalSeconds<8)await Task.Delay(30);await Task.Delay(1000);int index;double timeline=position,sourceTime=Doc.SourceTime(timeline,out index);if(timeline<.65||Math.Abs(sourceTime-timeline*.5)>.03)throw new Exception("手动 0.5 倍速不正确："+timeline+" / "+sourceTime);Pause();playbackTestDelay=0;dirty=false;File.WriteAllText(Path.Combine(Media.Root,"bin","playback-rate-results.txt"),"PASS: 60 FPS video with mouse-follow zoom and audio plays at normal wall-clock rate under 70 ms per-frame render delay; slow frames are skipped; intentional 0.5x remains available.\n");}
 }
}
