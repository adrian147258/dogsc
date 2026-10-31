using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using DogSC.Windows;

namespace DogSC.Replica {
 static class MotionStabilityTests {
  static void Check(bool value,string message){if(!value)throw new Exception(message);}
  static PointerSample P(double t,double x,double y,int segment=0,bool visible=true,bool click=false){return new PointerSample {Time=t,X=x,Y=y,Segment=segment,Visible=visible,Click=click};}
  static double X(StableFollow f,double time){double x,y;Check(f.At(time,out x,out y),"Missing follow position");return x;}
  static StableFollow Linear(int fps){var p=new List<PointerSample>();for(int i=0;i<=fps;i++)p.Add(P(i/(double)fps,.3+.4*i/fps,.5));return new StableFollow(p);}
  public static void Run(){string log=Path.Combine(Media.Root,"bin","motion-stability-results.txt");try{
   var samples=new List<PointerSample>{P(0,.4,.5),P(.25,.4,.5),P(.5,.4,.5),P(.75,.6,.5),P(.766,.6,.5)};var follow=new StableFollow(samples);double before=X(follow,.749999),at=X(follow,.75),after=X(follow,.80);Check(Math.Abs(at-before)<.0001&&after>at&&after<.6,"Camera jumps at compressed stationary/moving transition");Check(X(follow,1.5)>.59,"Camera failed to catch up to pointer");
   var slow=Linear(30);var fast=Linear(90);foreach(double t in new[]{.15,.3,.5,.8,1.0,1.2})Check(Math.Abs(X(slow,t)-X(fast,t))<.003,"Camera motion depends on recording sample rate at "+t);
   var jitter=new List<PointerSample>();for(int i=0;i<180;i++)jitter.Add(P(i/60.0,.5+(i%2==0?.0007:-.0007),.5));var stable=new StableFollow(jitter);for(int i=0;i<180;i++)Check(Math.Abs(X(stable,i/60.0)-.5)<.002,"Tiny cursor jitter moved camera");
   var boundaries=new StableFollow(new List<PointerSample>{P(0,.35,.5),P(.2,.35,.5),P(.4,.35,.5,0,false),P(.8,.75,.5,0,false),P(1,.7,.5),P(1.1,.7,.5),P(1.2,.3,.5,1)});Check(Math.Abs(X(boundaries,.7)-X(boundaries,.4))<.0001,"Camera drifted while pointer hidden");Check(Math.Abs(X(boundaries,1)-X(boundaries,.7))<.0001,"Camera jumped when pointer became visible");Check(Math.Abs(X(boundaries,1.2)-.3)<.0001,"Pause/cut segment did not reset independently");
   var doc=new EditDocument {Duration=4000,Width=1920,Height=1080,IndependentCursor=true};doc.Cuts.Add(new Cut {Start=0,End=4000});doc.Zooms.Add(new Zoom {Start=0,End=4000,Scale=2,Automatic=true});for(int i=0;i<240000;i++)doc.Pointer.Add(P(i/60.0,.48+(i%5)*.002,.5,0,true,i%137==0));var watch=Stopwatch.StartNew();using(var motion=new MotionEvaluator(doc)){for(int i=0;i<3000;i++){double t=(i*1.19)%3999;var pose=motion.At(t);Check(EditDocument.Finite(pose.X)&&EditDocument.Finite(pose.Y),"Invalid camera position");var actual=motion.RecentClicks(t,.36).Select(p=>p.Time).ToArray();if(i%300==0){var expected=doc.Pointer.Where(p=>p.Click&&p.Visible&&p.Time<=t&&t-p.Time<.36).Select(p=>p.Time).ToArray();Check(actual.SequenceEqual(expected),"Click search differs from original behaviour");}motion.CursorAt(t);}double probe=1234.567,first=motion.At(probe).X;motion.At(20);Check(Math.Abs(motion.At(probe).X-first)<1e-12,"Random seeking changed motion");}
   double computeTime=watch.Elapsed.TotalSeconds;string root=Path.Combine(Media.Root,"bin","motion-stability",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string source=Path.Combine(root,"source.mp4");StudioMedia.Run("-v error -y -f lavfi -i color=c=red:s=320x180:r=30:d=3 -vf "+Media.Q("drawbox=x=160:y=0:w=160:h=180:color=blue:t=fill")+" -c:v libx264 -preset ultrafast "+Media.Q(source),CancellationToken.None);var video=StudioMedia.Import(source);video.Style.Frame="none";video.Style.Padding=0;video.Style.Radius=0;video.Style.Shadow=0;video.IndependentCursor=true;video.Cursor.Asset="hidden";for(int i=0;i<=90;i++){double t=i/30.0,pos=t<1?.3:t>1.5?.7:.3+(t-1)*.8;video.Pointer.Add(P(t,pos,.5));}video.Zooms.Add(new Zoom {Start=0,End=3,Scale=2,Automatic=true,Transition=.2});string output=Path.Combine(root,"zoom.mp4");StudioRenderer.Export(video,output,180,30,(v,s)=>{},CancellationToken.None);foreach(var item in new[]{new {Time=.7,Red=true},new {Time=2.2,Red=false}})using(var data=new MemoryStream(StudioMedia.Frame(output,item.Time,320)))using(var image=new Bitmap(data)){Color pixel=image.GetPixel(160,90);Check(item.Red?pixel.R>170&&pixel.B<100:pixel.B>170&&pixel.R<100,"Exported zoom failed to follow the pointer at "+item.Time+": "+pixel);}
   File.WriteAllText(log,"PASS: smooth sample transitions, 30/90 Hz consistency, jitter suppression, hidden cursor and segment boundaries, deterministic seeking, indexed click effects over 240000 samples; actual exported zoom follows pointer.\n3000 camera/cursor evaluations including setup: "+computeTime.ToString("F2")+" seconds.\nArtifacts: "+root+"\n");
  }catch(Exception ex){File.WriteAllText(log,"FAIL\n"+ex);Environment.ExitCode=1;}}
 }
}
