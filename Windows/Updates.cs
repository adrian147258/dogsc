using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using DogSC.Windows;

namespace DogSC.Replica {
 sealed class UpdateInfo { public string Tag,Name,Url,Digest;public long Size;public Version Version;public int Beta; }
 static class Updates {
  const string Api="https://api.github.com/repos/adrian147258/dogsc/releases?per_page=100";
  public static bool Installed {get{return File.Exists(Path.Combine(Media.Root,"installed.flag"));}}
  public static string Current {get{try{return File.ReadAllText(Path.Combine(Media.Root,"version.txt")).Trim();}catch{return "v0.1.0-windows-beta.2";}}}
  public static string RecordingFolder {get{return Installed?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"DogSC","recordings"):Path.Combine(Media.Root,"recordings");}}
  static bool Parse(string tag,out Version version,out int beta){version=null;beta=0;var m=Regex.Match(tag??"",@"^v(\d+)\.(\d+)\.(\d+)-windows(?:-beta\.(\d+))?$");int b;if(!m.Success||!Version.TryParse(m.Groups[1].Value+"."+m.Groups[2].Value+"."+m.Groups[3].Value,out version))return false;if(m.Groups[4].Success){if(!int.TryParse(m.Groups[4].Value,out b)||b<1)return false;beta=b;}else beta=int.MaxValue;return true;}
  public static UpdateInfo Find(string json,string current){Version v;int beta;if(!Parse(current,out v,out beta))throw new Exception("当前版本号无效。");var rows=new JavaScriptSerializer {MaxJsonLength=8*1024*1024}.DeserializeObject(json) as object[];if(rows==null)throw new Exception("更新信息格式无效。");UpdateInfo best=null;foreach(var obj in rows){var item=obj as Dictionary<string,object>;if(item==null||!item.ContainsKey("tag_name")||!item.ContainsKey("assets")||(item.ContainsKey("draft")&&Convert.ToBoolean(item["draft"])))continue;string tag=item["tag_name"] as string;Version candidate;int b;if(!Parse(tag,out candidate,out b)||candidate.CompareTo(v)<0||candidate==v&&b<=beta)continue;var assets=item["assets"] as object[];if(assets==null)continue;string name="dogsc-"+tag+"-setup-x64.exe";foreach(var a in assets){var asset=a as Dictionary<string,object>;if(asset==null||!asset.ContainsKey("name")||(string)asset["name"]!=name||!asset.ContainsKey("state")||(string)asset["state"]!="uploaded"||!asset.ContainsKey("browser_download_url")||!asset.ContainsKey("digest")||!asset.ContainsKey("size"))continue;string digest=asset["digest"] as string,url=asset["browser_download_url"] as string;Uri uri;long size;try{size=Convert.ToInt64(asset["size"]);}catch{continue;}if(size<=0||size>1024L*1024*1024||!Regex.IsMatch(digest??"",@"^sha256:[0-9a-fA-F]{64}$")||!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!=Uri.UriSchemeHttps||uri.Host!="github.com"||uri.AbsolutePath!="/adrian147258/dogsc/releases/download/"+tag+"/"+name)continue;if(best==null||candidate.CompareTo(best.Version)>0||candidate==best.Version&&b>best.Beta)best=new UpdateInfo {Tag=tag,Name=name,Url=url,Digest=digest.Substring(7).ToLowerInvariant(),Size=size,Version=candidate,Beta=b};}}
   return best;
  }
  static WebClient Client(){ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var c=new WebClient();c.Headers[HttpRequestHeader.UserAgent]="DogSC-Windows-Updater";c.Headers[HttpRequestHeader.Accept]="application/vnd.github+json";return c;}
  public static UpdateInfo Check(){using(var client=Client())return Find(client.DownloadString(Api),Current);}
  public static string Download(UpdateInfo info){string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DogSC.Windows","updates");Directory.CreateDirectory(dir);string part=Path.Combine(dir,Guid.NewGuid().ToString("N")+".download"),final=Path.ChangeExtension(part,".exe");try{using(var c=Client())c.DownloadFile(info.Url,part);Verify(part,info);File.Move(part,final);return final;}catch{try{if(File.Exists(part))File.Delete(part);}catch{}throw;}}
  public static void Verify(string path,UpdateInfo info){var f=new FileInfo(path);if(f.Length!=info.Size)throw new Exception("更新文件大小不符，请重试。");using(var stream=File.OpenRead(path))using(var sha=SHA256.Create()){string hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();if(hash!=info.Digest)throw new Exception("更新文件校验失败，请重试。");}}
 }
 public partial class Studio {
  bool updateBusy;
  public async void CheckUpdates(bool automatic=false){if(updateBusy)return;updateBusy=true;bool userEngaged=false;try{if(automatic)await Task.Delay(3000);if(!Window.IsVisible||recorder!=null&&recorder.IsRecording)return;if(!automatic)Status("正在检查更新…");var update=await Task.Run(()=>Updates.Check());if(!Window.IsVisible||recorder!=null&&recorder.IsRecording)return;if(update==null){if(!automatic)AppDialogs.Notice(Window,"检查更新","当前已是最新可用版本。\n当前版本："+Updates.Current);return;}bool installed=Updates.Installed;var prompt=new AppDialogWindow(Window,"发现新版本", "当前："+Updates.Current+"\n新版本："+update.Tag+"\n"+(installed?"下载完成后将关闭软件并安装更新。请先保存工作。":"下载完成后将打开安装程序。旧版录屏和项目仍保留在原位置。"),new[]{new DialogAction("cancel","稍后"),new DialogAction("update","下载更新",true)});prompt.ShowDialog();if(prompt.Result!="update")return;userEngaged=true;if(recorder!=null&&recorder.IsRecording){AppDialogs.Notice(Window,"正在录制","请先停止录制再更新。");return;}Status("正在下载更新，请稍候…");string path=await Task.Run(()=>Updates.Download(update));if(recorder!=null&&recorder.IsRecording){AppDialogs.Notice(Window,"正在录制","更新已下载，请停止录制后重新检查更新。");return;}if(!MayDiscard())return;dirty=false;Window.Close();if(!closing)return;var start=new ProcessStartInfo(path,installed?"/SILENT /NORESTART /DIR="+Media.Q(Path.GetFullPath(Path.Combine(Media.Root,".."))):"") {UseShellExecute=true};Process.Start(start);}catch(Exception ex){if(!automatic||userEngaged){string message=ex.Message+"\n当前版本和录制文件未被删除。";if(Window.IsVisible)AppDialogs.Notice(Window,"更新未完成",message);else MessageBox.Show(message,"更新未完成");}}finally{updateBusy=false;if(Window.IsVisible)Status("");}}
 }
}
