using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace DeepSeekBalance {
sealed class BalanceSnapshot {
 public decimal Total,Granted,ToppedUp;public string Currency;public DateTimeOffset Fetched;public bool Available;
 public decimal TopShare{get{return Total>0?100*ToppedUp/Total:0;}}
 public decimal GrantShare{get{return Total>0?100*Granted/Total:0;}}
 public string Amount{get{return Total.ToString("0.00",CultureInfo.InvariantCulture);}}
 public string Unit{get{return Currency=="CNY"?"¥":Currency=="USD"?"$":Currency;}}
 public bool Current{get{return DateTimeOffset.Now-Fetched<TimeSpan.FromMinutes(3);}}
 public static BalanceSnapshot Parse(string json){
  var doc=DeepSeekClient.Json(json);var list=doc["balance_infos"] as IEnumerable;if(list==null)throw new InvalidDataException("余额响应缺少金额");BalanceSnapshot selected=null;
  foreach(var raw in list){var item=raw as Dictionary<string,object>;if(item==null)continue;var value=new BalanceSnapshot{Currency=Convert.ToString(item["currency"]),Total=Money(item,"total_balance"),Granted=Money(item,"granted_balance"),ToppedUp=Money(item,"topped_up_balance"),Available=Convert.ToBoolean(doc["is_available"]),Fetched=DateTimeOffset.Now};if(value.Currency=="CNY")return value;if(selected==null)selected=value;}
  if(selected==null)throw new InvalidDataException("未返回可用的余额币种");return selected;
 }
 static decimal Money(Dictionary<string,object> item,string key){decimal value;if(!decimal.TryParse(Convert.ToString(item[key],CultureInfo.InvariantCulture),NumberStyles.Number,CultureInfo.InvariantCulture,out value)||value<0)throw new InvalidDataException("余额金额格式无效");return value;}
}
static class DeepSeekClient {
 public static string DataFolder{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GRings");}}
 public static string KeyPath{get{return Path.Combine(DataFolder,"deepseek-key.dat");}}
 public static Dictionary<string,object> Json(string text){return new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(text);}
 public static void SaveApiKey(string apiKey){
  if(String.IsNullOrWhiteSpace(apiKey))throw new ArgumentException("API 密钥不能为空");
  Directory.CreateDirectory(DataFolder);
  var bytes=Encoding.UTF8.GetBytes(apiKey.Trim());
  try{
   var encrypted=ProtectedData.Protect(bytes,null,DataProtectionScope.CurrentUser);
   File.WriteAllBytes(KeyPath,encrypted);
  }finally{Array.Clear(bytes,0,bytes.Length);}
 }
 public static string LoadApiKey(){
  if(File.Exists(KeyPath)){
   var plain=ProtectedData.Unprotect(File.ReadAllBytes(KeyPath),null,DataProtectionScope.CurrentUser);
   try{
    string saved=Encoding.UTF8.GetString(plain).Trim();
    if(!String.IsNullOrWhiteSpace(saved))return saved;
   }finally{Array.Clear(plain,0,plain.Length);}
  }
  string migrated=TryMigrateFromCodexBar();
  if(!String.IsNullOrWhiteSpace(migrated)){
   try{SaveApiKey(migrated);}catch{}
   return migrated;
  }
  return null;
 }
 static string TryMigrateFromCodexBar(){
  try{
   var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"CodexBar","api_keys.json");
   if(!File.Exists(path))return null;
   var document=Json(File.ReadAllText(path));object raw;
   if(document.TryGetValue("format",out raw)&&Convert.ToString(raw)=="codexbar.secure-file"){
    if(Convert.ToString(document["protection"])!="windows-dpapi-user")return null;
    var plain=ProtectedData.Unprotect(Convert.FromBase64String(Convert.ToString(document["payload"])),null,DataProtectionScope.CurrentUser);
    try{document=Json(Encoding.UTF8.GetString(plain));}finally{Array.Clear(plain,0,plain.Length);}
   }
   var keys=document["keys"] as Dictionary<string,object>;
   if(keys==null||!keys.TryGetValue("deepseek",out raw))return null;
   var entry=raw as Dictionary<string,object>;
   return entry==null?null:Convert.ToString(entry["api_key"]);
  }catch{return null;}
 }
 public static BalanceSnapshot Read(){
  string key=LoadApiKey();
  if(String.IsNullOrWhiteSpace(key))throw new InvalidOperationException("请右键圆环点击「设置 DeepSeek API 密钥…」");
  ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
  var request=(HttpWebRequest)WebRequest.Create("https://api.deepseek.com/user/balance");request.Method="GET";request.AllowAutoRedirect=false;request.Timeout=15000;request.ReadWriteTimeout=15000;request.Proxy=WebRequest.GetSystemWebProxy();request.Headers[HttpRequestHeader.Authorization]="Bearer "+key;request.UserAgent="GRings/1.0";
  try{using(var response=(HttpWebResponse)request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream())){if(response.StatusCode!=HttpStatusCode.OK)throw new InvalidOperationException("DeepSeek 未返回余额");return BalanceSnapshot.Parse(reader.ReadToEnd());}}
  catch(WebException ex){var response=ex.Response as HttpWebResponse;if(response!=null){int status=(int)response.StatusCode;response.Dispose();throw new InvalidOperationException("DeepSeek 余额查询失败（HTTP "+status+"）");}throw new InvalidOperationException(ex.Status==WebExceptionStatus.Timeout?"余额查询超时":"无法连接 DeepSeek 余额接口");}
 }
}
}
