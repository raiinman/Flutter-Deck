using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Media;
public sealed class WaveData {
 public float[] Samples; public int Rate;
 public double Duration {get{return (double)Samples.Length/Rate;}}
 public static WaveData Read(string path){
  if(!File.Exists(path)||new FileInfo(path).Length>33554432)throw new InvalidDataException();
  using(var r=new BinaryReader(File.OpenRead(path))){
   if(new string(r.ReadChars(4))!="RIFF")throw new InvalidDataException();r.ReadUInt32();if(new string(r.ReadChars(4))!="WAVE")throw new InvalidDataException();
   int channels=0,rate=0,bits=0,align=0;long data=-1;int size=0;
   while(r.BaseStream.Position+8<=r.BaseStream.Length){
    string id=new string(r.ReadChars(4));uint n=r.ReadUInt32();long end=r.BaseStream.Position+n;if(end>r.BaseStream.Length)throw new InvalidDataException();
    if(id=="fmt "){if(n<16||r.ReadUInt16()!=1)throw new InvalidDataException();channels=r.ReadUInt16();rate=r.ReadInt32();r.ReadInt32();align=r.ReadUInt16();bits=r.ReadUInt16();}
    if(id=="data"){data=r.BaseStream.Position;size=checked((int)n);}
    r.BaseStream.Position=end+(n%2);
   }
   if(data<0||(channels!=1&&channels!=2)||rate<8000||rate>192000||bits!=16||align!=channels*2||size==0||size%align!=0||size/(double)align/rate>600)throw new InvalidDataException();
   var result=new WaveData{Rate=rate,Samples=new float[size/align]};r.BaseStream.Position=data;
   for(int i=0;i<result.Samples.Length;i++){double sum=0;for(int c=0;c<channels;c++){double v=r.ReadInt16()/32768.0;sum+=v*v;}result.Samples[i]=(float)Math.Sqrt(sum/channels);}
   return result;
  }
 }
 public double[] Levels(double seconds,int bars,double volume){
  var levels=new double[bars];int start=(int)(Math.Max(0,seconds)*Rate),span=Math.Max(1,Rate/200);
  for(int b=0;b<bars;b++){double sum=0;int count=0;for(int j=0;j<span;j++){int i=start+b*span+j;if(i>=Samples.Length)break;sum+=Samples[i]*Samples[i];count++;}levels[b]=count==0?0:Math.Min(1,Math.Sqrt(sum/count)*Math.Max(0,Math.Min(1,volume)));}
  return levels;
 }
}
public sealed class AudioTrack {public string Path,Title;public WaveData Wave;}
public sealed class DeckAudio : IDisposable {
 // Loading always ends: a file that neither opens nor fails within this many seconds becomes a retryable failure.
 public const double LoadSeconds=10;
 // Music starts quiet: 20% (developer, 2026-11-16). The lower-deck slider starts at the same value.
 public const double StartVolume=.2;
 MediaPlayer player=new MediaPlayer();System.Windows.Threading.DispatcherTimer deadline;readonly Random random=new Random();
 public readonly List<AudioTrack> Tracks=new List<AudioTrack>();
 public int Index=-1;public bool Playing,Ready,Paused,Failed,Repeat=true,Shuffle,Muted;
 public string Notice="Stopped";public event Action Changed;double volume=StartVolume;
 public double Volume {get{return volume;}set{volume=Math.Max(0,Math.Min(1,value));player.Volume=Muted?0:volume;Notify();}}
 public double Position {get{return Ready?player.Position.TotalSeconds:0;}}
 public double Duration {get{return Index>=0?Tracks[Index].Wave.Duration:0;}}
 void Notify(){if(Changed!=null)Changed();}
 void Fail(){Ready=false;Playing=false;Paused=false;Failed=true;Notice="Audio unavailable. Check the WAV file and Windows audio output.";}
 void StopDeadline(){if(deadline!=null){deadline.Stop();deadline=null;}}
 // One MediaPlayer per open attempt. A reused player can stay silent (no MediaOpened or MediaFailed) when asked to
 // reopen a Uri it has held before, so each attempt gets a new one and events from superseded attempts are ignored.
 void Open(Uri uri){
  StopDeadline();var old=player;var p=new MediaPlayer();player=p;p.Volume=Muted?0:volume;old.Close();
  p.MediaOpened+=delegate{if(p!=player)return;StopDeadline();Ready=true;Notice="Ready";if(Playing){p.Play();Notice="Playing";}Notify();};
  // Owner, 2026-11-21: the loops play one after another, and after the last one the first starts again. One track plays again.
  p.MediaEnded+=delegate{if(p!=player)return;if(Repeat||Tracks.Count<2){p.Position=TimeSpan.Zero;p.Play();}else{Select(NextIndex(Index,Tracks.Count,Shuffle?random.Next(1,Tracks.Count):1),true);}};
  p.MediaFailed+=delegate{if(p!=player)return;StopDeadline();Fail();Notify();};
  var d=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(LoadSeconds)};deadline=d;
  d.Tick+=delegate{d.Stop();if(p!=player||Ready||Failed)return;deadline=null;p.Close();Fail();Notify();};d.Start();
  p.Open(uri);
 }
 public static int NextIndex(int index,int count,int step){return count<1?-1:((index+step)%count+count)%count;}
 public void Add(string path,string title){if(Tracks.Count>=32)throw new InvalidOperationException();var wave=WaveData.Read(path);long total=wave.Samples.Length;foreach(var track in Tracks)total+=track.Wave.Samples.Length;if(total>16000000)throw new InvalidOperationException();Tracks.Add(new AudioTrack{Path=System.IO.Path.GetFullPath(path),Title=title,Wave=wave});if(Index<0)Select(0,false);else Notify();}
 public void Select(int index,bool play){if(index<0||index>=Tracks.Count)return;Ready=false;Failed=false;Paused=false;Playing=play;Index=index;Notice="Loading";Open(new Uri(Tracks[index].Path,UriKind.Absolute));Notify();}
 // After a failed open, Play reopens the track instead of entering a "playing" state that can never start.
 public void Toggle(){if(Index<0)return;if(Failed){Select(Index,true);return;}if(Playing){player.Pause();Playing=false;Paused=Ready;Notice="Paused";}else{Playing=true;Paused=false;if(Ready){player.Play();Notice="Playing";}}Notify();}
 public void Stop(){player.Stop();Playing=false;Paused=false;if(!Failed)Notice="Stopped";Notify();}
 public void Seek(double value){if(Ready)player.Position=TimeSpan.FromSeconds(Math.Max(0,Math.Min(Duration,value)));}
 public void Next(){if(Tracks.Count>1)Select(NextIndex(Index,Tracks.Count,Shuffle?random.Next(1,Tracks.Count):1),Playing);}
 public void Previous(){if(Tracks.Count>1){if(Position>3)Seek(0);else Select(NextIndex(Index,Tracks.Count,-1),Playing);}}
 public void ToggleMute(){Muted=!Muted;player.Volume=Muted?0:volume;Notify();}
 public double[] Levels(int bars){return Playing&&Ready&&Index>=0?Tracks[Index].Wave.Levels(Position,bars,Muted||volume<=0?0:1):new double[bars];}
 public void Dispose(){Playing=false;StopDeadline();player.Close();}
}
