using System;
using System.Collections.Generic;
using System.IO;

// Renders Flutter Deck's two original chiptune loops. Everything here is original to this project: the keys,
// tempos, chord progressions and every note below were written for Flutter Deck. No game audio or samples are read or used, and the
// melodies were written new for this project. The output is deterministic (same bytes every run).
//   Dawn Launch : a calm rising opening that breaks into a driving theme. D minor, 160 BPM, 20 bars = 30.000 s.
//   Skyway Run  : an upbeat stage-style run. A minor / C major, 144 BPM, 18 bars = 30.000 s.
// Build and run through make-loops.ps1. Format: 44,100 Hz, 16-bit, mono.
public static class LoopSynth {
  const int Rate=44100,Length=Rate*30;

  static double Freq(string note){ // "A4", "C#5", "Bb4"
    int i=0;char letter=note[i++];string acc="";
    if(note[i]=='#'||note[i]=='b')acc=note[i++].ToString();
    int octave=Int32.Parse(note.Substring(i));
    int semitone;
    switch(letter){case 'C':semitone=0;break;case 'D':semitone=2;break;case 'E':semitone=4;break;case 'F':semitone=5;break;case 'G':semitone=7;break;case 'A':semitone=9;break;default:semitone=11;break;}
    if(acc=="#")semitone++;if(acc=="b")semitone--;
    return 440.0*Math.Pow(2,(semitone-9)/12.0+(octave-4));
  }
  // Band-limited step correction, so the square waves do not buzz with aliasing.
  static double Blep(double t,double dt){if(t<dt){t/=dt;return t+t-t*t-1;}if(t>1-dt){t=(t-1)/dt;return t*t+t+t+1;}return 0;}

  sealed class Voice { public double[] Buffer=new double[Length]; }
  // Notes may ring past the end of the loop: they wrap to the start, which is what makes the loop seamless.
  static void Pulse(Voice v,double start,double seconds,double freq,double duty,double level,double release){
    int a=(int)(start*Rate),n=(int)((seconds+release)*Rate);double phase=0,dt=freq/Rate;
    for(int i=0;i<n;i++){
      double t=(double)i/Rate,env=t<0.004?t/0.004:t<seconds?1-0.25*Math.Min(1,(t-0.004)/0.12):0.75*Math.Max(0,1-(t-seconds)/release);
      double s=phase<duty?1:-1;s+=Blep(phase,dt);double p2=phase-duty;if(p2<0)p2+=1;s-=Blep(p2,dt);
      v.Buffer[(a+i)%Length]+=s*env*level;phase+=dt;if(phase>=1)phase-=1;
    }
  }
  static void Triangle(Voice v,double start,double seconds,double freq,double level){
    int a=(int)(start*Rate),n=(int)((seconds+0.02)*Rate);double phase=0,dt=freq/Rate;
    for(int i=0;i<n;i++){
      double t=(double)i/Rate,env=t<0.003?t/0.003:t<seconds?1:Math.Max(0,1-(t-seconds)/0.02);
      double s=4*Math.Abs(phase-0.5)-1;v.Buffer[(a+i)%Length]+=s*env*level;phase+=dt;if(phase>=1)phase-=1;
    }
  }
  static void Kick(Voice v,double start,double level){
    int a=(int)(start*Rate),n=(int)(0.16*Rate);double phase=0;
    for(int i=0;i<n;i++){double t=(double)i/Rate,f=150*Math.Exp(-t*28)+48;phase+=f/Rate;v.Buffer[(a+i)%Length]+=Math.Sin(2*Math.PI*phase)*Math.Exp(-t*22)*level;}
  }
  static void Noise(Voice v,Random random,double start,double seconds,double decay,double level,double tone){
    int a=(int)(start*Rate),n=(int)(seconds*Rate);double held=0,low=0;int hold=Math.Max(1,(int)tone),count=0;
    for(int i=0;i<n;i++){double t=(double)i/Rate;if(count--<=0){held=random.NextDouble()*2-1;count=hold;}low+=(held-low)*0.6;v.Buffer[(a+i)%Length]+=low*Math.Exp(-t*decay)*level;}
  }

  // A melody bar is "NOTE:steps NOTE:steps ..." on a sixteen-step grid; "-" is a rest.
  static void Melody(Voice v,string[] bars,int firstBar,double step,double duty,double level,double gate){
    for(int b=0;b<bars.Length;b++){
      double at=(firstBar+b)*16*step;
      foreach(string item in bars[b].Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries)){
        string[] parts=item.Split(':');int steps=Int32.Parse(parts[1]);
        if(parts[0]!="-")Pulse(v,at,steps*step*gate,Freq(parts[0]),duty,level,0.09);
        at+=steps*step;
      }
    }
  }
  static string[] Chord(string name){
    switch(name){
      case "Dm":return new[]{"D3","F3","A3"};case "Bb":return new[]{"Bb2","D3","F3"};case "C":return new[]{"C3","E3","G3"};case "A":return new[]{"A2","C#3","E3"};
      case "Gm":return new[]{"G2","Bb2","D3"};case "F":return new[]{"F2","A2","C3"};case "Am":return new[]{"A2","C3","E3"};case "G":return new[]{"G2","B2","D3"};
      case "E":return new[]{"E2","G#2","B2"};default:throw new ArgumentException(name);
    }
  }
  static double Up(string note,int octaves){return Freq(note)*Math.Pow(2,octaves);}

  // A soft echo that also wraps around the loop, then a gentle limiter and a fixed peak level.
  static short[] Finish(double[] mix){
    int delay=(int)(0.1875*Rate);var wet=new double[Length];
    for(int pass=0;pass<3;pass++)for(int i=0;i<Length;i++)wet[(i+delay)%Length]=(mix[i]+wet[i]*0.35)*0.28;
    double peak=0;var outp=new double[Length];
    for(int i=0;i<Length;i++){double s=mix[i]+wet[i];s=Math.Tanh(s*0.9);outp[i]=s;peak=Math.Max(peak,Math.Abs(s));}
    var pcm=new short[Length];double gain=0.708/peak; // -3 dBFS
    for(int i=0;i<Length;i++)pcm[i]=(short)Math.Round(outp[i]*gain*32767);
    return pcm;
  }
  static void Write(string path,short[] pcm){
    using(var w=new BinaryWriter(File.Create(path))){
      w.Write(new[]{'R','I','F','F'});w.Write(36+pcm.Length*2);w.Write(new[]{'W','A','V','E'});w.Write(new[]{'f','m','t',' '});w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(Rate);w.Write(Rate*2);w.Write((short)2);w.Write((short)16);
      w.Write(new[]{'d','a','t','a'});w.Write(pcm.Length*2);foreach(short s in pcm)w.Write(s);
    }
  }
  static double[] Mix(params Voice[] voices){var mix=new double[Length];foreach(var v in voices)for(int i=0;i<Length;i++)mix[i]+=v.Buffer[i];return mix;}

  // ---- Dawn Launch: D minor, 160 BPM, 20 bars ----
  static short[] DawnLaunch(){
    double step=60.0/160/4;var lead=new Voice();var harmony=new Voice();var bass=new Voice();var drums=new Voice();var random=new Random(160);
    string[] chords={"Dm","Bb","C","A", "Dm","Bb","C","Dm", "Dm","Bb","C","A", "Gm","C","F","A", "Dm","Bb","C","Dm"};
    string a1="A4:3 D5:3 F5:2 E5:2 D5:2 A4:4",a2="Bb4:3 D5:3 F5:2 G5:2 F5:2 D5:4";
    string[] theme={
      a1,a2,"G5:3 E5:3 C5:2 E5:2 G5:2 A5:4","A5:2 G5:2 F5:2 E5:2 D5:8",
      a1,a2,"G5:3 A5:3 Bb5:2 A5:2 G5:2 E5:4","A5:4 E5:4 C#5:4 E5:2 A5:2",
      "D5:2 G5:2 Bb5:4 A5:2 G5:2 D5:4","E5:2 G5:2 C6:4 Bb5:2 G5:2 E5:4","F5:2 A5:2 C6:4 A5:2 F5:2 C5:4","E5:4 A5:4 C#6:4 E6:4",
      a1,a2,"G5:3 E5:3 C5:2 E5:2 G5:2 C6:4","D6:4 A5:4 F5:4 A4:2 C#5:2"};
    Melody(lead,theme,4,step,0.25,0.34,0.92);
    for(int bar=0;bar<20;bar++){
      string[] c=Chord(chords[bar]);double at=bar*16*step;bool opening=bar<4;
      // Opening: slow rising arpeggios, getting louder. After that: a quiet sixteenth-note arpeggio under the tune.
      if(opening){int[] order={0,1,2,0,1,2,0,1};for(int i=0;i<8;i++)Pulse(harmony,at+i*2*step,2*step*0.9,Up(c[order[i]],1+i/3),0.5,0.16+0.03*bar,0.12);}
      else{int[] order={0,1,2,1};for(int i=0;i<16;i++)Pulse(harmony,at+i*step,step*0.8,Up(c[order[i%4]],1),0.125,0.11,0.03);}
      // Bass: held notes in the opening, then driving eighths with an octave jump.
      if(opening)Triangle(bass,at,16*step*0.98,Freq(c[0]),0.42);
      else for(int i=0;i<8;i++)Triangle(bass,at+i*2*step,2*step*0.85,Freq(c[0])*(i%4==2?2:1),0.46);
      // Drums enter with the theme; the last opening bar has a snare run into it.
      if(bar==3){for(int i=8;i<16;i++)Noise(drums,random,at+i*step,0.07,38,0.12+0.02*(i-8),3);}
      if(!opening){
        for(int i=0;i<16;i+=2)Noise(drums,random,at+i*step,0.03,90,i%4==2?0.07:0.10,1);
        Kick(drums,at,0.62);Kick(drums,at+8*step,0.62);if(bar%4==3)Kick(drums,at+14*step,0.5);
        Noise(drums,random,at+4*step,0.11,26,0.30,3);Noise(drums,random,at+12*step,0.11,26,0.30,3);
        if(bar%4==3){Noise(drums,random,at+13*step,0.06,40,0.2,3);Noise(drums,random,at+15*step,0.06,40,0.24,3);}
      }
    }
    return Finish(Mix(lead,harmony,bass,drums));
  }
  // ---- Skyway Run: A minor / C major, 144 BPM, 18 bars ----
  static short[] SkywayRun(){
    double step=60.0/144/4;var lead=new Voice();var harmony=new Voice();var bass=new Voice();var drums=new Voice();var random=new Random(144);
    string[] chords={"Am","F","C","G", "Am","F","C","G", "Dm","E","Am","E", "Am","F","C","G", "F","G"};
    string b1="E5:2 A5:2 C6:2 B5:2 A5:4 E5:4",b2="F5:2 A5:2 C6:2 D6:2 C6:4 A5:4",b3="G5:2 C6:2 E6:2 D6:2 C6:4 G5:4";
    string[] tune={
      b1,b2,b3,"D6:3 B5:3 G5:2 B5:2 D6:2 G5:4",
      b1,b2,"E6:2 D6:2 C6:2 G5:2 E5:2 G5:2 C6:4","B5:2 D6:2 G6:4 D6:4 B5:4",
      "A5:4 F5:2 D5:2 F5:2 A5:2 D6:4","B5:4 G#5:2 E5:2 G#5:2 B5:2 E6:4","C6:2 B5:2 A5:2 E5:2 A5:2 C6:2 E6:4","D6:2 B5:2 G#5:4 E5:2 G#5:2 B5:4",
      b1,b2,b3,"D6:2 G5:2 B5:2 D6:2 G6:4 D6:4",
      "C6:4 A5:4 F5:4 A5:2 C6:2","D6:4 B5:4 G5:2 B5:2 D6:2 G#5:2"};
    Melody(lead,tune,0,step,0.5,0.30,0.9);
    for(int bar=0;bar<18;bar++){
      string[] c=Chord(chords[bar]);double at=bar*16*step;bool bridge=bar>=8&&bar<12;
      // Second voice: off-beat chord stabs, or a rolling arpeggio in the bridge.
      if(bridge){int[] order={0,2,1,2};for(int i=0;i<16;i++)Pulse(harmony,at+i*step,step*0.8,Up(c[order[i%4]],1),0.25,0.10,0.03);}
      else for(int i=2;i<16;i+=4)foreach(string n in c)Pulse(harmony,at+i*step,2*step*0.7,Up(n,1),0.25,0.075,0.04);
      // Bass gallop: an eighth and two sixteenths on every beat.
      for(int beat=0;beat<4;beat++){double b=at+beat*4*step;double f=Freq(c[0])*(beat==3?1.5:1);Triangle(bass,b,2*step*0.85,f,0.46);Triangle(bass,b+2*step,step*0.8,f,0.40);Triangle(bass,b+3*step,step*0.8,f*(beat%2==1?2:1),0.40);}
      for(int i=0;i<16;i+=2)Noise(drums,random,at+i*step,0.03,90,i%4==2?0.07:0.10,1);
      Kick(drums,at,0.62);Kick(drums,at+6*step,0.5);Kick(drums,at+8*step,0.62);
      Noise(drums,random,at+4*step,0.11,26,0.30,3);Noise(drums,random,at+12*step,0.11,26,0.30,3);
      if(bar%4==3||bar==17){Noise(drums,random,at+14*step,0.06,40,0.2,3);Noise(drums,random,at+15*step,0.06,40,0.24,3);}
    }
    return Finish(Mix(lead,harmony,bass,drums));
  }

  public static int Main(string[] args){
    string folder=args.Length>0?args[0]:"assets";
    Write(Path.Combine(folder,"FlutterDeck-DawnLaunch.wav"),DawnLaunch());
    Write(Path.Combine(folder,"FlutterDeck-SkywayRun.wav"),SkywayRun());
    Console.WriteLine("Wrote FlutterDeck-DawnLaunch.wav and FlutterDeck-SkywayRun.wav to "+Path.GetFullPath(folder)+" (30.000 s each, 44,100 Hz, 16-bit, mono).");
    return 0;
  }
}
