using System;
using System.IO;
public static class AudioPureTest {
 public static int Main(string[] args){
  var w=WaveData.Read(args[0]);if(Math.Abs(w.Duration-30)>0.001||w.Rate!=44100)throw new Exception("Bundled duration/format");
  var silent=w.Levels(1,16,0);foreach(var x in silent)if(x!=0)throw new Exception("Mute levels");
  bool nonzero=false;foreach(var x in w.Levels(5,16,1))if(x>0)nonzero=true;if(!nonzero)throw new Exception("No sample-driven levels");
  var ended=w.Levels(31,16,1);foreach(var x in ended)if(x!=0)throw new Exception("Past-end samples");
  if(DeckAudio.NextIndex(0,1,1)!=0||DeckAudio.NextIndex(0,3,-1)!=2||DeckAudio.NextIndex(2,3,1)!=0||DeckAudio.NextIndex(0,0,1)!=-1)throw new Exception("Playlist wrap");
  bool rejected=false;try{WaveData.Read(args[1]);}catch{rejected=true;}if(!rejected)throw new Exception("Invalid WAV accepted");
  Console.WriteLine("PASS: original PCM/duration, mute/sample/past-end levels, playlist math, invalid file rejection. No playback/audio device/UI executed.");return 0;
 }
}
