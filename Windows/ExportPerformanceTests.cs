using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DogSC.Windows;

namespace DogSC.Replica {
 static class ExportPerformanceTests {
  public static void Run(bool noShadow=false){string root=Path.Combine(Media.Root,"bin","export-performance",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string report=Path.Combine(Media.Root,"bin","export-performance-results.txt");try{string input=Path.Combine(root,"source.mp4");StudioMedia.Run("-v error -y -f lavfi -i testsrc2=size=1280x720:rate=30 -f lavfi -i sine=frequency=440:sample_rate=48000 -t 3 -c:v libx264 -preset ultrafast -c:a aac "+Media.Q(input),CancellationToken.None);var doc=StudioMedia.Import(input);doc.Name="Benchmark";if(noShadow)doc.Style.Shadow=0;var watch=Stopwatch.StartNew();double first=-1;string detail="";string output=Path.Combine(root,"export.mp4");StudioRenderer.Export(doc,output,720,30,(n,s)=>{if(first<0&&n>=.1)first=watch.Elapsed.TotalSeconds;},CancellationToken.None,s=>detail=s);double elapsed=watch.Elapsed.TotalSeconds;var exported=StudioMedia.Import(output);if(!exported.Audio||exported.Width!=1280||exported.Height!=720||Math.Abs(exported.FrameRate-30)>.1||Math.Abs(exported.Duration-doc.Length)>.2)throw new Exception("Output media properties incorrect");File.WriteAllText(report,"PASS: 3 second 1280x720 / 30 FPS synthetic video and audio.\nTime to rendering: "+first.ToString("F2")+" seconds; total: "+elapsed.ToString("F2")+" seconds.\n"+detail+"\nArtifacts: "+root+"\n");}catch(Exception ex){File.WriteAllText(report,"FAIL\n"+ex+"\nArtifacts: "+root);Environment.ExitCode=1;}}
 }
}
