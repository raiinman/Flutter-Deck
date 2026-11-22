using System;
using System.IO;
using System.Globalization;
public static class UiPreferences {
  public static string PathName {get{return Path.Combine(DeckPaths.Root,"text-size.txt");}}
  // The stored number is the percent the user sees: 1 is shown as 100%. Since 2026-11-17 the launcher draws 100% at Base times the first design size
  // (the developer used the old 120% and asked for it to be the default). A new value is written with the mark "v2 ". A value without the mark was
  // written by an older build on the old scale, and is converted to the nearest step that is not smaller (old 1.20 becomes 1.00, old 1.10 becomes 0.90; old 1.00 and below become 0.80, the new smallest).
  public const double Base=1.2;const string Mark="v2 ";
  public static double Drawn(double value){return Clamp(value)*Base;}
  public static double FromOld(double old){return Double.IsNaN(old)||Double.IsInfinity(old)?1:Clamp(Math.Round(old/Base,1));}
  public static double Clamp(double value){return Double.IsNaN(value)||Double.IsInfinity(value)?1:Math.Max(0.8,Math.Min(1.2,value));}
  public static double Load(string path){double value;if(!File.Exists(path)||new FileInfo(path).Length>64)return 1;string text=File.ReadAllText(path);bool marked=text.StartsWith(Mark,StringComparison.Ordinal);if(marked)text=text.Substring(Mark.Length);if(!Double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out value))return 1;return marked?Clamp(value):FromOld(value);}
  public static void Save(string path,double value){Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,Mark+Clamp(value).ToString("0.00",CultureInfo.InvariantCulture));if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
}
