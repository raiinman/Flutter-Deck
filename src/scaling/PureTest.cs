using System;
using System.Drawing;
using DsiScaling;
public static class ScalingPureTest {
  public static int Main(){
    var pinned=new WindowInfo{Handle=new IntPtr(7),ProcessId=100,ProcessStartTicks=500};
    if(!Native.SameIdentity(pinned,100,500))throw new Exception("Stable identity rejected");
    if(Native.SameIdentity(pinned,101,500))throw new Exception("Reused HWND unrelated PID accepted");
    if(Native.SameIdentity(pinned,100,501))throw new Exception("Recycled PID lifetime accepted");
    if(Native.SameIdentity(null,100,500))throw new Exception("Absent pin accepted");
    if(Native.SameIdentity(new WindowInfo{Handle=IntPtr.Zero,ProcessId=100,ProcessStartTicks=500},100,500))throw new Exception("Closed handle accepted");
    if(Native.SameIdentity(new WindowInfo{Handle=new IntPtr(7)},100,500))throw new Exception("Unverified identity accepted");
    var fit=Native.Fit(320,240,1920,1080,false);
    if(fit!=new Rectangle(240,0,1440,1080))throw new Exception("Aspect fit");
    var integer=Native.PixelDestination(new Rectangle(0,0,320,240),1920,1080,false,"Integer");
    if(integer!=new Rectangle(320,60,1280,960))throw new Exception("Integer geometry");
    bool rejected=false;try{Native.PixelDestination(new Rectangle(0,0,320,240),100,100,false,"Integer");}catch(ArgumentException){rejected=true;}if(!rejected)throw new Exception("Integer downscale");
    using(var input=new Bitmap(2,2)){input.SetPixel(0,0,Color.Red);input.SetPixel(1,0,Color.Blue);input.SetPixel(0,1,Color.Red);input.SetPixel(1,1,Color.Blue);
      using(var output=Native.RenderPixels(input,new Rectangle(0,0,2,2),4,4,false,"Integer")){
        if(output.GetPixel(0,0).R!=255||output.GetPixel(3,0).B!=255)throw new Exception("Integer pixels");
      }
    }
    Console.WriteLine("PASS: synthetic identity/lifetime rejection, scaling geometry and pixel blocks; no windows/capture/game execution.");return 0;
  }
}
