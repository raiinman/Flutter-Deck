namespace DsiScaling {
    // Isolated DTO from the developer's DSI Scaling 0.4.1 preview; no mixed application/backend.
    public sealed class Profile {
        public string Name="Default", CaptureMode="DWM", Filter="Bilinear", SourceTitle="";
        public int FrameRate=60;
        public bool ShowFps=false;
        public int Display, Delay=3, CropLeft, CropTop, CropRight, CropBottom;
        public bool Stretch, Fullscreen=true, ClickThrough;
        public override string ToString() { return Name; }
    }
}
