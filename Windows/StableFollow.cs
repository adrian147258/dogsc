using System;
using System.Collections.Generic;
using DogSC.Windows;

namespace DogSC.Replica {
 // Continuous-time smoothing avoids changing camera behaviour when the pointer
 // sampler compresses stationary points or misses a sample. Each segment starts
 // independently, so a cut or recording pause cannot pull the camera across it.
 sealed class StableFollow {
  const double Tau=.18,DeadZone=.0015,InterpolationGap=.1;
  readonly IList<PointerSample> points;readonly double[] x,y,targetX,targetY;readonly bool[] hasPosition;
  public StableFollow(IList<PointerSample> samples){points=samples;int count=samples.Count;x=new double[count];y=new double[count];targetX=new double[count];targetY=new double[count];hasPosition=new bool[count];
   for(int i=0;i<count;i++){var p=samples[i];if(i==0||p.Segment!=samples[i-1].Segment){if(p.Visible){x[i]=targetX[i]=p.X;y[i]=targetY[i]=p.Y;hasPosition[i]=true;}continue;}
    var previous=samples[i-1];double dt=Math.Max(0,p.Time-previous.Time);if(!hasPosition[i-1]){if(p.Visible){x[i]=targetX[i]=p.X;y[i]=targetY[i]=p.Y;hasPosition[i]=true;}continue;}
    hasPosition[i]=true;targetX[i]=targetX[i-1];targetY[i]=targetY[i-1];if(p.Visible){double dx=p.X-targetX[i],dy=p.Y-targetY[i];if(dx*dx+dy*dy>DeadZone*DeadZone){targetX[i]=p.X;targetY[i]=p.Y;}}
    bool ramp=previous.Visible&&p.Visible&&dt>0&&dt<=InterpolationGap;x[i]=previous.Visible?Advance(x[i-1],targetX[i-1],ramp?targetX[i]:targetX[i-1],dt,dt):x[i-1];y[i]=previous.Visible?Advance(y[i-1],targetY[i-1],ramp?targetY[i]:targetY[i-1],dt,dt):y[i-1];
   }
  }
  static double Advance(double value,double from,double to,double duration,double elapsed){if(duration<=0||elapsed<=0)return value;double slope=(to-from)/duration;return from+slope*elapsed-slope*Tau+(value-from+slope*Tau)*Math.Exp(-elapsed/Tau);}
  public bool At(double time,out double px,out double py){px=py=0;int lo=0,hi=points.Count;while(lo<hi){int mid=(lo+hi)/2;if(points[mid].Time<=time)lo=mid+1;else hi=mid;}int index=lo-1;if(index<0||!hasPosition[index])return false;var p=points[index];px=x[index];py=y[index];if(!p.Visible)return true;double elapsed=Math.Max(0,time-p.Time),duration=elapsed,toX=targetX[index],toY=targetY[index];if(index+1<points.Count){var next=points[index+1];double gap=next.Time-p.Time;if(next.Segment==p.Segment&&next.Visible&&gap>0&&gap<=InterpolationGap){duration=gap;elapsed=Math.Min(elapsed,gap);toX=targetX[index+1];toY=targetY[index+1];}}px=Advance(px,targetX[index],toX,duration,elapsed);py=Advance(py,targetY[index],toY,duration,elapsed);return true;}
 }
}
