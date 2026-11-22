using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace DsiScaling {
    public sealed class WindowInfo {
        public IntPtr Handle; public string Title; public uint ProcessId; public long ProcessStartTicks;
        public override string ToString() { return Title; }
    }
    public static class Native {
        public delegate bool EnumWindowProc(IntPtr hwnd, IntPtr data);
        [StructLayout(LayoutKind.Sequential)] public struct Rect {
            public int Left, Top, Right, Bottom;
            public Rect(int x, int y, int w, int h) { Left=x; Top=y; Right=x+w; Bottom=y+h; }
        }
        [StructLayout(LayoutKind.Sequential)] public struct Size { public int Width, Height; }
        [StructLayout(LayoutKind.Sequential)] public struct Thumbnail {
            public uint Flags; public Rect Destination, Source; public byte Opacity;
            [MarshalAs(UnmanagedType.Bool)] public bool Visible;
            [MarshalAs(UnmanagedType.Bool)] public bool ClientOnly;
        }
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowProc callback, IntPtr data);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int length);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint id);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hwnd, out Rect rect);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint mods, uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll", SetLastError=true)] private static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
        [DllImport("dwmapi.dll")] public static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr source, out IntPtr thumbnail);
        [DllImport("dwmapi.dll")] public static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail, ref Thumbnail properties);
        [DllImport("dwmapi.dll")] public static extern int DwmUnregisterThumbnail(IntPtr thumbnail);
        [DllImport("dwmapi.dll")] public static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);
        public static bool SameIdentity(WindowInfo pinned,uint pid,long ticks) {
            return pinned!=null && pinned.Handle!=IntPtr.Zero && pinned.ProcessId!=0 && pinned.ProcessStartTicks>0 && pinned.ProcessId==pid && pinned.ProcessStartTicks==ticks;
        }
        public static WindowInfo Pin(IntPtr handle,string title) {
            uint pid; if(!IsWindow(handle)||GetWindowThreadProcessId(handle,out pid)==0||pid==0)throw new InvalidOperationException("Source identity unavailable.");
            using(Process process=Process.GetProcessById((int)pid)) {
                var pinned=new WindowInfo{Handle=handle,Title=title,ProcessId=pid,ProcessStartTicks=process.StartTime.ToUniversalTime().Ticks};
                RequireIdentity(pinned);return pinned;
            }
        }
        public static void RequireIdentity(WindowInfo pinned) {
            try {
                uint pid;if(pinned==null||!IsWindow(pinned.Handle)||GetWindowThreadProcessId(pinned.Handle,out pid)==0)throw new InvalidOperationException();
                using(Process process=Process.GetProcessById((int)pid)) {
                    if(!SameIdentity(pinned,pid,process.StartTime.ToUniversalTime().Ticks))throw new InvalidOperationException();
                }
            }catch {throw new InvalidOperationException("Selected source closed, changed, or cannot be verified. Refresh and select it again.");}
        }
        public static List<WindowInfo> Windows() {
            List<WindowInfo> result = new List<WindowInfo>();
            EnumWindows(delegate(IntPtr hwnd, IntPtr ignored) {
                uint pid; GetWindowThreadProcessId(hwnd, out pid);
                if (pid == (uint)Process.GetCurrentProcess().Id || !IsWindowVisible(hwnd) || IsIconic(hwnd)) return true;
                StringBuilder title = new StringBuilder(1024); GetWindowText(hwnd, title, title.Capacity);
                if (title.Length > 0) {try{result.Add(Pin(hwnd,title.ToString()));}catch{ /* Unverifiable windows are not offered. */ }}
                return true;
            }, IntPtr.Zero);
            result.Sort(delegate(WindowInfo a, WindowInfo b) { return String.Compare(a.Title,b.Title,StringComparison.CurrentCultureIgnoreCase); });
            return result;
        }
        public static Rectangle Fit(int sourceW, int sourceH, int destW, int destH, bool stretch) {
            if (sourceW < 1 || sourceH < 1 || destW < 1 || destH < 1) throw new ArgumentException("Invalid source or output dimensions.");
            if (stretch) return new Rectangle(0, 0, destW, destH);
            double factor = Math.Min((double)destW / sourceW, (double)destH / sourceH);
            int w=Math.Max(1,(int)Math.Round(sourceW*factor)), h=Math.Max(1,(int)Math.Round(sourceH*factor));
            return new Rectangle((destW-w)/2,(destH-h)/2,w,h);
        }
        internal static Rectangle Crop(int width, int height, Profile p) {
            if(p.CropLeft<0 || p.CropTop<0 || p.CropRight<0 || p.CropBottom<0 || (long)p.CropLeft+p.CropRight>=width || (long)p.CropTop+p.CropBottom>=height)
                throw new ArgumentException("Crop must leave at least one source pixel in each dimension.");
            return new Rectangle(p.CropLeft,p.CropTop,width-p.CropLeft-p.CropRight,height-p.CropTop-p.CropBottom);
        }
        internal static Bitmap CaptureClient(IntPtr window) { return CaptureClient(Pin(window,"")); }
        internal static Bitmap CaptureClient(WindowInfo pinned) {
            RequireIdentity(pinned);IntPtr window=pinned.Handle;
            Rect r;
            if(!IsWindow(window) || IsIconic(window) || !GetClientRect(window,out r)) throw new Exception("Source window closed, minimized, or unavailable.");
            int w=r.Right-r.Left,h=r.Bottom-r.Top;
            if(w<1 || h<1 || (long)w*h>33554432) throw new Exception("Source size is invalid or exceeds the 32 megapixel capture limit.");
            Bitmap bitmap=new Bitmap(w,h,PixelFormat.Format32bppRgb);
            try {
                using(Graphics g=Graphics.FromImage(bitmap)) {
                    g.Clear(Color.Black); IntPtr dc=g.GetHdc(); bool success;
                    try { success=PrintWindow(window,dc,1); } finally { g.ReleaseHdc(dc); }
                    if(!success) throw new Exception("PrintWindow client capture failed. This source may not support GDI capture.");
                }
                RequireIdentity(pinned);return bitmap;
            } catch { bitmap.Dispose(); throw; }
        }
        internal static Rectangle PixelDestination(Rectangle crop,int width,int height,bool stretch,string filter) {
            if(filter=="Integer") {
                if(width<crop.Width || height<crop.Height) throw new ArgumentException("Integer filtering needs an output at least as large as the cropped source.");
                int factor=Math.Min(width/crop.Width,height/crop.Height),w=crop.Width*factor,h=crop.Height*factor;
                return new Rectangle((width-w)/2,(height-h)/2,w,h);
            }
            return Fit(crop.Width,crop.Height,width,height,stretch);
        }
        internal static Bitmap RenderPixels(Bitmap source, Rectangle crop, int width,int height,bool stretch,string filter) {
            if(width<1 || height<1 || (long)width*height>33554432)throw new ArgumentException("Output size is invalid or exceeds 32 megapixels.");
            if(crop.Width<1 || crop.Height<1 || crop.Left<0 || crop.Top<0 || crop.Right>source.Width || crop.Bottom>source.Height)throw new ArgumentException("Invalid crop.");
            Rectangle dest=PixelDestination(crop,width,height,stretch,filter);
            InterpolationMode interpolation;
            switch(filter) {
                case "Nearest": case "Integer": interpolation=InterpolationMode.NearestNeighbor; break;
                case "Bilinear": interpolation=InterpolationMode.HighQualityBilinear; break;
                case "Bicubic": interpolation=InterpolationMode.HighQualityBicubic; break;
                default: throw new ArgumentException("Unknown pixel filter.");
            }
            Bitmap output=new Bitmap(width,height,PixelFormat.Format32bppRgb);
            try {
                // Clone the crop so the resampler cannot mix discarded border pixels into the output.
                using(Bitmap input=source.Clone(crop,PixelFormat.Format32bppRgb))
                using(Graphics g=Graphics.FromImage(output))
                using(ImageAttributes attributes=new ImageAttributes()) {
                    g.Clear(Color.Black); g.InterpolationMode=interpolation; g.PixelOffsetMode=PixelOffsetMode.Half;
                    g.CompositingMode=CompositingMode.SourceCopy; attributes.SetWrapMode(WrapMode.TileFlipXY);
                    g.DrawImage(input,dest,0,0,input.Width,input.Height,GraphicsUnit.Pixel,attributes);
                }
                return output;
            } catch { output.Dispose(); throw; }
        }
        private sealed class ProbeSource : Form {
            protected override bool ShowWithoutActivation { get { return true; } }
            public ProbeSource() {
                ShowInTaskbar=false; StartPosition=FormStartPosition.Manual; Bounds=new Rectangle(-28000,-28000,640,400);
                Text="DSI disposable pixel fixture"; BackColor=Color.DarkSlateBlue;
            }
            protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); e.Graphics.Clear(Color.Red); e.Graphics.FillRectangle(Brushes.Blue,ClientSize.Width/2,0,ClientSize.Width-ClientSize.Width/2,ClientSize.Height); }
            protected override void WndProc(ref Message m) {
                if(m.Msg==0x318 || m.Msg==0x317) {
                    using(Graphics g=Graphics.FromHdc(m.WParam)) { g.Clear(Color.Red); g.FillRectangle(Brushes.Blue,ClientSize.Width/2,0,ClientSize.Width-ClientSize.Width/2,ClientSize.Height); }
                    m.Result=new IntPtr(1); return;
                }
                base.WndProc(ref m);
            }
        }
        public static void ProbeLifecycle() {
            using(ProbeSource source=new ProbeSource()) {
                source.Show(); Application.DoEvents();
                foreach(bool clickThrough in new[]{false,true}) {
                    using(ScaleSurface output=new ScaleSurface(source.Handle,Screen.PrimaryScreen,new Profile {Fullscreen=false,ClickThrough=clickThrough})) {
                        output.Bounds=new Rectangle(-25000,-25000,800,600); output.Show(); Application.DoEvents();
                        if(!String.IsNullOrEmpty(output.LastError) || !output.Registered)throw new Exception("Compositor lifecycle failed: "+output.LastError);
                        output.Close();
                    }
                }
                source.Close();
            }
        }
        private static void Require(bool condition,string message) { if(!condition)throw new Exception("Pixel fixture: "+message); }
        public static void ProbePixelPipeline() {
            ScaleSurface.ProbeFrameOwnership();
            using(Bitmap source=new Bitmap(4,4,PixelFormat.Format32bppRgb)) {
                using(Graphics g=Graphics.FromImage(source)) { g.Clear(Color.Lime); g.FillRectangle(Brushes.Red,1,1,1,2); g.FillRectangle(Brushes.Blue,2,1,1,2); }
                Rectangle crop=new Rectangle(1,1,2,2);
                foreach(string filter in new[]{"Nearest","Bilinear","Bicubic","Integer"}) {
                    using(Bitmap output=RenderPixels(source,crop,10,8,false,filter)) {
                        Require(output.GetPixel(0,4).ToArgb()==Color.Black.ToArgb(),filter+" letterbox");
                        Color a=output.GetPixel(1,4),b=output.GetPixel(8,4);
                        Require(a.R>240 && a.G==0 && b.B>240 && b.G==0,filter+" cropped edge colors");
                        if(filter=="Nearest" || filter=="Integer") {
                            Require(output.GetPixel(4,4).R==255 && output.GetPixel(5,4).B==255,filter+" exact pixel blocks");
                        } else {
                            Color middle=output.GetPixel(4,4); Require(middle.R>0 && middle.B>0 && middle.G==0,filter+" blended transition");
                        }
                    }
                }
                using(Bitmap stretched=RenderPixels(source,crop,9,5,true,"Nearest"))Require(stretched.GetPixel(0,0).R==255 && stretched.GetPixel(8,4).B==255,"stretch geometry");
                Require(PixelDestination(crop,11,9,true,"Integer")==new Rectangle(1,0,8,8),"integer keeps whole factor even when stretch requested");
                bool rejected=false; try { using(Bitmap invalid=RenderPixels(source,crop,1,1,false,"Integer")) {} } catch(ArgumentException) { rejected=true; }
                Require(rejected,"integer downscale rejection");
                rejected=false; try { Crop(4,4,new Profile { CropLeft=4 }); } catch(ArgumentException) { rejected=true; } Require(rejected,"empty crop rejection");
                using(Bitmap downscaled=RenderPixels(source,new Rectangle(0,0,4,4),2,2,false,"Bicubic"))Require(downscaled.Width==2 && downscaled.Height==2,"bicubic downscale");
            }
            using(ProbeSource source=new ProbeSource()) {
                source.Show(); Application.DoEvents();
                using(Bitmap captured=CaptureClient(source.Handle)) {
                    Require(captured.Width==source.ClientSize.Width && captured.Height==source.ClientSize.Height,"client capture dimensions");
                    Require(captured.GetPixel(8,8).R==255 && captured.GetPixel(captured.Width-8,8).B==255,"PrintWindow client pattern left="+captured.GetPixel(8,8)+" right="+captured.GetPixel(captured.Width-8,8));
                }
                foreach(bool click in new[]{false,true}) {
                    using(ScaleSurface output=new ScaleSurface(source.Handle,Screen.PrimaryScreen,new Profile { CaptureMode="GDI", Filter="Nearest", FrameRate=30, ShowFps=true, Fullscreen=false,ClickThrough=click })) {
                        output.Bounds=new Rectangle(-24000,-24000,800,600); output.Show(); Stopwatch deadline=Stopwatch.StartNew();
                        while(output.PresentedFrameCount<2 && String.IsNullOrEmpty(output.LastError) && deadline.ElapsedMilliseconds<5000) { Application.DoEvents(); using(Bitmap paint=new Bitmap(output.Width,output.Height))output.DrawToBitmap(paint,new Rectangle(0,0,paint.Width,paint.Height)); Thread.Sleep(5); }
                        Require(String.IsNullOrEmpty(output.LastError) && output.PresentedFrameCount>=2,"background capture/paint lifecycle captured="+output.CapturedFrameCount+" painted="+output.PresentedFrameCount+" "+output.LastError);
                        Require(output.CapturedFrameCount>=output.PresentedFrameCount,"capture counters");
                        using(ScaleSurface overlap=new ScaleSurface(source.Handle,Screen.PrimaryScreen,new Profile {CaptureMode="GDI",Fullscreen=false})) {
                            overlap.Bounds=new Rectangle(-23000,-23000,400,300); overlap.Show(); Application.DoEvents();
                            Require(!String.IsNullOrEmpty(overlap.LastError) && overlap.LastError.IndexOf("previous GDI capture",StringComparison.Ordinal)>=0,"overlapping worker refused");
                        }
                        Require(ScaleSurface.CaptureWorkerActive,"overlap refusal retains original worker lease"); output.Close();
                        Stopwatch stopping=Stopwatch.StartNew(); while(ScaleSurface.CaptureWorkerActive && stopping.ElapsedMilliseconds<1000) { Application.DoEvents();Thread.Sleep(5); }
                        Require(!ScaleSurface.CaptureWorkerActive,"worker releases process-wide lease on close");
                    }
                }
                source.Close();
            }
        }
    }
    public sealed class ScaleSurface : Form {
        private IntPtr thumbnail, source; private WindowInfo sourceIdentity;
        private Profile profile;
        private System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private PixelCapture worker;
        private Bitmap frame;
        private bool unpainted,closed;
        private long presented;
        private Stopwatch metrics=Stopwatch.StartNew();
        private long lastCaptured,lastPresented;
        private double lastTime, lastFrameTime;
        public string LastError;
        public bool Registered { get { return thumbnail!=IntPtr.Zero; } }
        public long CapturedFrameCount { get { return worker==null ? 0 : worker.Count; } }
        public long PresentedFrameCount { get { return presented; } }
        public double CaptureFps { get; private set; }
        public double PresentationFps { get; private set; }
        public ScaleSurface(IntPtr window, Screen screen, Profile settings) : this(Native.Pin(window,""),screen,settings) {}
        public ScaleSurface(WindowInfo selected, Screen screen, Profile settings) {
            Native.RequireIdentity(selected);sourceIdentity=selected;IntPtr window=selected.Handle;
            source=window; profile=settings;
            Text="DSI Scaling Output"; BackColor=Color.Black; ShowInTaskbar=false;
            SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);
            FormBorderStyle=settings.Fullscreen ? FormBorderStyle.None : FormBorderStyle.Sizable;
            StartPosition=FormStartPosition.Manual; TopMost=true;
            if (settings.Fullscreen) Bounds=screen.Bounds;
            else Bounds=new Rectangle(screen.WorkingArea.X+40,screen.WorkingArea.Y+40,Math.Min(1120,screen.WorkingArea.Width-80),Math.Min(720,screen.WorkingArea.Height-80));
            timer.Interval=profile.CaptureMode=="GDI" ? Math.Max(1,1000/Math.Max(1,Math.Min(240,profile.FrameRate))) : 250;
            timer.Tick+=delegate {
                if (!Native.IsWindow(source) || Native.IsIconic(source)) { LastError="Source window closed or minimized."; Close(); return; }
                try {
                    Native.RequireIdentity(sourceIdentity);if(worker==null)UpdateThumbnail();
                    else {
                        worker.SetOutput(ClientSize.Width,ClientSize.Height);
                        string failure; Bitmap next=worker.Take(out failure);
                        if(failure!=null) { if(next!=null)next.Dispose();throw new Exception(failure); }
                        if(next!=null) { if(frame!=null)frame.Dispose(); frame=next; unpainted=true; lastFrameTime=metrics.Elapsed.TotalSeconds; Invalidate(); }
                        if(metrics.Elapsed.TotalSeconds-lastFrameTime>5)throw new Exception("GDI capture stalled: no completed frame for 5 seconds. Stop the source or wait for it to respond before retrying.");
                        double now=metrics.Elapsed.TotalSeconds;
                        if(now-lastTime>=1) {
                            long count=CapturedFrameCount; CaptureFps=(count-lastCaptured)/(now-lastTime); PresentationFps=(presented-lastPresented)/(now-lastTime);
                            lastCaptured=count;lastPresented=presented;lastTime=now;
                        }
                    }
                } catch(Exception e) { LastError=e.Message; Close(); }
            };
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams {
            get { CreateParams p=base.CreateParams; p.ExStyle|=0x08000000|0x80; if(profile!=null && profile.ClickThrough) p.ExStyle|=0x20|0x80000; return p; }
        }
        [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint color, byte alpha, uint flags);
        protected override void OnShown(EventArgs e) {
            base.OnShown(e);
            try {
                Native.RequireIdentity(sourceIdentity);
                if(profile.ClickThrough && !SetLayeredWindowAttributes(Handle,0,255,2)) throw new Exception("Unable to enable transparent output.");
                if(profile.CaptureMode=="GDI") { worker=new PixelCapture(sourceIdentity,profile,ClientSize.Width,ClientSize.Height); lastFrameTime=metrics.Elapsed.TotalSeconds; worker.Start(); }
                else { Marshal.ThrowExceptionForHR(Native.DwmRegisterThumbnail(Handle,source,out thumbnail)); Native.RequireIdentity(sourceIdentity);UpdateThumbnail(); }
                timer.Start();
            } catch(Exception error) { LastError=error.Message; Close(); }
        }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            if(frame!=null) {
                e.Graphics.DrawImageUnscaled(frame,0,0);
                if(unpainted) { presented++;unpainted=false; }
                if(profile.ShowFps) {
                    string label=String.Format("GDI captures {0:F1}/s | new paints {1:F1}/s",CaptureFps,PresentationFps);
                    e.Graphics.FillRectangle(Brushes.Black,4,4,420,26); e.Graphics.DrawString(label,Font,Brushes.White,8,8);
                }
            }
        }
        private void UpdateThumbnail() {
            Native.RequireIdentity(sourceIdentity);
            Native.Rect r;
            if (!Native.GetClientRect(source,out r)) throw new Exception("Unable to read source dimensions.");
            Rectangle crop=Native.Crop(r.Right-r.Left,r.Bottom-r.Top,profile);
            Rectangle dest=Native.Fit(crop.Width,crop.Height,ClientSize.Width,ClientSize.Height,profile.Stretch);
            Native.Thumbnail p=new Native.Thumbnail {
                Flags=1|2|4|8|16, Destination=new Native.Rect(dest.X,dest.Y,dest.Width,dest.Height),
                Source=new Native.Rect(crop.X,crop.Y,crop.Width,crop.Height), Opacity=255, Visible=true, ClientOnly=true
            };
            Marshal.ThrowExceptionForHR(Native.DwmUpdateThumbnailProperties(thumbnail,ref p));
        }
        private void ReleaseResources() {
            if(!closed) {
                closed=true; timer.Stop(); timer.Dispose();
                if(worker!=null)worker.Stop();
                if(frame!=null) { frame.Dispose();frame=null; }
                if(thumbnail!=IntPtr.Zero) { Native.DwmUnregisterThumbnail(thumbnail); thumbnail=IntPtr.Zero; }
            }
        }
        protected override void OnFormClosed(FormClosedEventArgs e) {
            ReleaseResources(); base.OnFormClosed(e);
        }
        protected override void Dispose(bool disposing) {
            if(disposing)ReleaseResources(); base.Dispose(disposing);
        }
        // One background producer, one replaceable pending frame. PrintWindow can block in the
        // source, so shutdown never waits for it on the UI thread. No desktop capture fallback.
        internal static void ProbeFrameOwnership() { PixelCapture.ProbeOwnership(); }
        internal static bool CaptureWorkerActive { get { return PixelCapture.Active; } }
        private sealed class PixelCapture {
            private static int active;
            public static bool Active { get { return Interlocked.CompareExchange(ref active,0,0)!=0; } }
            private readonly object gate=new object();
            private readonly WindowInfo source;
            private readonly Profile options;
            private int width,height;
            private bool stopped;
            private Bitmap pending;
            private string error;
            private long count;
            private readonly Thread thread;
            public long Count { get { return Interlocked.Read(ref count); } }
            public PixelCapture(IntPtr window,Profile p,int w,int h) : this(new WindowInfo{Handle=window},p,w,h) {}
            public PixelCapture(WindowInfo window,Profile p,int w,int h) {
                source=window;options=new Profile { CropLeft=p.CropLeft,CropTop=p.CropTop,CropRight=p.CropRight,CropBottom=p.CropBottom,Filter=p.Filter,FrameRate=p.FrameRate,Stretch=p.Stretch };
                width=w;height=h;thread=new Thread(Run);thread.IsBackground=true;thread.Name="DSI client capture";
            }
            public void Start() {
                if(Interlocked.CompareExchange(ref active,1,0)!=0)throw new Exception("A previous GDI capture is still stopping or blocked in its source. Wait for it to respond before retrying.");
                try { thread.Start(); } catch { Interlocked.Exchange(ref active,0);throw; }
            }
            public void SetOutput(int w,int h) { lock(gate) { width=w;height=h; } }
            public Bitmap Take(out string failure) {
                lock(gate) {
                    failure=error; Bitmap result=pending;pending=null;
                    if(failure!=null && result!=null) { result.Dispose();return null; }
                    return result;
                }
            }
            public void Stop() { lock(gate) { stopped=true;Monitor.PulseAll(gate);if(pending!=null) { pending.Dispose();pending=null; } } }
            private static void RequireDisposed(Bitmap image,string scenario) {
                bool disposed=false; try { image.GetPixel(0,0); } catch(ArgumentException) { disposed=true; }
                if(!disposed)throw new Exception("Pixel ownership fixture: "+scenario);
            }
            public static void ProbeOwnership() {
                PixelCapture capture=new PixelCapture(IntPtr.Zero,new Profile(),2,2);
                Bitmap failed=new Bitmap(2,2); capture.pending=failed;capture.error="owned test failure";
                string failure; Bitmap rejected=capture.Take(out failure);
                if(rejected!=null || failure!="owned test failure" || capture.pending!=null)throw new Exception("Pixel ownership fixture: error handoff contract");
                RequireDisposed(failed,"pending bitmap on error must be disposed"); capture.Stop();
                capture=new PixelCapture(IntPtr.Zero,new Profile(),2,2);
                Bitmap stopped=new Bitmap(2,2);capture.pending=stopped;capture.Stop();capture.Stop();
                RequireDisposed(stopped,"stop must dispose pending bitmap");
                capture=new PixelCapture(IntPtr.Zero,new Profile(),2,2);
                Bitmap transferred=new Bitmap(2,2);capture.pending=transferred;
                using(Bitmap frame=capture.Take(out failure)) {
                    if(frame!=transferred || failure!=null || capture.pending!=null)throw new Exception("Pixel ownership fixture: successful transfer");
                    capture.Stop(); frame.SetPixel(0,0,Color.Red);
                    if(frame.GetPixel(0,0).R!=255)throw new Exception("Pixel ownership fixture: caller owns transferred frame");
                }
                RequireDisposed(transferred,"caller disposal of transferred frame");
                using(ScaleSurface output=new ScaleSurface(IntPtr.Zero,Screen.PrimaryScreen,new Profile {Fullscreen=false})) {
                    Bitmap displayed=new Bitmap(2,2), queued=new Bitmap(2,2);output.frame=displayed;
                    capture=new PixelCapture(IntPtr.Zero,new Profile(),2,2);capture.pending=queued;capture.error="owned close error";output.worker=capture;
                    output.Dispose(); RequireDisposed(displayed,"Dispose must release displayed frame without Close");
                    RequireDisposed(queued,"Dispose must release errored worker pending frame");
                }
            }
            private void Run() {
                try {
                    double interval=1000.0/Math.Max(1,Math.Min(240,options.FrameRate));
                    while(true) {
                        int w,h; lock(gate) { if(stopped)return;w=width;h=height; }
                        Stopwatch elapsed=Stopwatch.StartNew();
                        if(w>0 && h>0) {
                            Bitmap next;
                            using(Bitmap raw=Native.CaptureClient(source)) { Interlocked.Increment(ref count);next=Native.RenderPixels(raw,Native.Crop(raw.Width,raw.Height,options),w,h,options.Stretch,options.Filter); }
                            lock(gate) { if(stopped) { next.Dispose();return; } if(pending!=null)pending.Dispose();pending=next; }
                        }
                        int delay=(int)Math.Ceiling(interval-elapsed.Elapsed.TotalMilliseconds);
                        if(delay>0)lock(gate) { if(stopped)return; Monitor.Wait(gate,delay); }
                    }
                } catch(Exception e) { lock(gate) { if(!stopped)error=e.Message; } }
                finally { Interlocked.Exchange(ref active,0); }
            }
        }
    }
}
