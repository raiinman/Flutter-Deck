using System;
public static class ResizeGeometry {
  public static double WidthFromPointer(double start,double dx,double dy,double minimum,double maximum){
    const double ratio=1672.0/941.0;
    // Project the anchored pointer displacement onto the fixed-aspect diagonal.
    // Independent of event frequency: never accumulate Thumb deltas.
    double width=start+(dx+dy/ratio)/(1+1/(ratio*ratio));
    return Math.Max(minimum,Math.Min(maximum,width));
  }
}
