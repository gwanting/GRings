using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace AntigravityQuotaBar {
    sealed class QuotaPool {
        public string Id;
        public string Group;
        public string Name;
        public string Window;
        public double? Remaining;
        public DateTimeOffset? Reset;
        public string ResetRaw;

        public string Percent {
            get { return Remaining.HasValue ? Math.Round(Remaining.Value, MidpointRounding.AwayFromZero).ToString("0") + "%" : "--"; }
        }

        public string ResetText(DateTimeOffset now) {
            if (Reset.HasValue) {
                TimeSpan time = Reset.Value - now;
                if (time <= TimeSpan.Zero) return "等待重置确认";
                if (time.TotalDays >= 1) return String.Format("{0} 天 {1} 小时后重置", (int)time.TotalDays, time.Hours);
                if (time.TotalMinutes < 1) return "不到 1 分钟后重置";
                return String.Format("{0} 小时 {1} 分钟后重置", time.Hours, time.Minutes);
            }
            return String.IsNullOrWhiteSpace(ResetRaw) ? "重置时间未知" : ResetRaw;
        }
    }

    sealed class AntigravitySnapshot {
        public DateTimeOffset Fetched;
        public string Plan;
        public string Source = "agy /usage";
        public int? PromptCredits;
        public List<QuotaPool> Pools = new List<QuotaPool>();

        public QuotaPool Primary {
            get {
                return Pools.FirstOrDefault(p => String.Equals(p.Id, "gemini-5h", StringComparison.OrdinalIgnoreCase))
                    ?? Pools.FirstOrDefault(p => p.Group != null && p.Group.IndexOf("gemini", StringComparison.OrdinalIgnoreCase) >= 0 && p.Window == "5h")
                    ?? Pools.FirstOrDefault();
            }
        }

        public QuotaPool Secondary {
            get {
                var primary = Primary;
                return Pools.FirstOrDefault(p => String.Equals(p.Id, "gemini-weekly", StringComparison.OrdinalIgnoreCase))
                    ?? Pools.FirstOrDefault(p => !Object.ReferenceEquals(p, primary) && p.Group != null && p.Group.IndexOf("gemini", StringComparison.OrdinalIgnoreCase) >= 0 && p.Window == "weekly")
                    ?? Pools.FirstOrDefault(p => !Object.ReferenceEquals(p, primary));
            }
        }

        public QuotaPool FindById(string id) {
            return Pools.FirstOrDefault(p => String.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsCurrent(DateTimeOffset now) {
            return Pools.Count > 0 && Fetched <= now.AddSeconds(10) && now - Fetched <= TimeSpan.FromMinutes(3);
        }

        public static AntigravitySnapshot Parse(string rawOutput) {
            if (String.IsNullOrWhiteSpace(rawOutput)) return null;
            string trimmed = rawOutput.Trim();
            int jsonStart = trimmed.IndexOf('{');
            int jsonEnd = trimmed.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart) {
                string jsonSlice = trimmed.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var dict = ReadJson(jsonSlice);
                if (dict != null) {
                    var parsed = ParseJsonRoot(dict);
                    if (parsed != null && parsed.Pools.Count > 0) return parsed;
                }
            }
            return ParseTextLines(trimmed, "agy CLI");
        }

        static AntigravitySnapshot ParseJsonRoot(Dictionary<string, object> root) {
            var snapshot = new AntigravitySnapshot { Fetched = DateTimeOffset.Now };
            snapshot.Plan = Text(root, "plan") ?? Text(root, "tier") ?? Text(root, "planType");

            // 1. Structured groups (root.groups or root.command.data.groups)
            Dictionary<string, object> dataNode = root;
            var cmd = Dict(root, "command");
            if (cmd != null) {
                var data = Dict(cmd, "data");
                if (data != null) dataNode = data;
            }
            var credits = Dict(dataNode, "promptCredits") ?? Dict(root, "promptCredits");
            if (credits != null) {
                var rem = Number(credits, "remaining");
                if (rem.HasValue) snapshot.PromptCredits = (int)Math.Max(0, Math.Floor(rem.Value));
            }

            object rawGroups;
            if (dataNode.TryGetValue("groups", out rawGroups) && rawGroups is IEnumerable) {
                foreach (var item in (IEnumerable)rawGroups) {
                    var g = item as Dictionary<string, object>;
                    if (g == null) continue;
                    string groupName = Text(g, "displayName") ?? Text(g, "name") ?? Text(g, "id") ?? "模型池";
                    object rawBuckets;
                    if (g.TryGetValue("buckets", out rawBuckets) && rawBuckets is IEnumerable) {
                        foreach (var bItem in (IEnumerable)rawBuckets) {
                            var b = bItem as Dictionary<string, object>;
                            if (b == null) continue;
                            string id = Text(b, "id");
                            string win = Text(b, "window");
                            double? frac = Number(b, "remaining_fraction") ?? Number(b, "remainingFraction");
                            double? pct = frac.HasValue ? frac.Value * 100.0 : (Number(b, "remaining_percent") ?? Number(b, "remainingPercent") ?? Number(b, "percentage"));
                            DateTimeOffset resetDt;
                            string resetStr = Text(b, "reset_time") ?? Text(b, "resetTime") ?? Text(b, "resetsAt");
                            snapshot.Pools.Add(new QuotaPool {
                                Id = id,
                                Group = groupName,
                                Window = win,
                                Name = FormatBucketName(groupName, win, Text(b, "name")),
                                Remaining = pct.HasValue ? Math.Max(0, Math.Min(100, pct.Value)) : (double?)null,
                                Reset = !String.IsNullOrWhiteSpace(resetStr) && DateTimeOffset.TryParse(resetStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out resetDt) ? resetDt : (DateTimeOffset?)null,
                                ResetRaw = resetStr
                            });
                        }
                        continue;
                    }

                    double? gFrac = Number(g, "remaining_fraction") ?? Number(g, "remainingFraction");
                    double? gPct = gFrac.HasValue ? gFrac.Value * 100.0 : (Number(g, "remainingPercent") ?? Number(g, "percentage") ?? Number(g, "remaining"));
                    if (!gPct.HasValue) {
                        var used = Number(g, "usedPercent");
                        if (used.HasValue) gPct = 100.0 - used.Value;
                    }
                    DateTimeOffset gResetDt;
                    string gResetStr = Text(g, "reset_time") ?? Text(g, "resetTime") ?? Text(g, "resetsAt");
                    snapshot.Pools.Add(new QuotaPool {
                        Id = Text(g, "id"),
                        Group = groupName,
                        Name = LocalizePoolName(groupName),
                        Remaining = gPct.HasValue ? Math.Max(0, Math.Min(100, gPct.Value)) : (double?)null,
                        Reset = !String.IsNullOrWhiteSpace(gResetStr) && DateTimeOffset.TryParse(gResetStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out gResetDt) ? gResetDt : (DateTimeOffset?)null,
                        ResetRaw = gResetStr
                    });
                }
                if (snapshot.Pools.Count > 0) return snapshot;
            }

            // 2. Official `agy -p "/usage" --output-format json` envelope: { status, response }
            string responseText = Text(root, "response");
            if (!String.IsNullOrWhiteSpace(responseText)) {
                var fromText = ParseTextLines(responseText, "agy /usage");
                if (fromText != null) {
                    if (String.IsNullOrEmpty(fromText.Plan)) fromText.Plan = snapshot.Plan;
                    return fromText;
                }
            }
            return null;
        }

        static readonly Regex PercentRegex = new Regex(@"(?<!\d)(\d{1,3}(?:\.\d+)?)\s*%", RegexOptions.Compiled);
        static readonly Regex IsoTimeRegex = new Regex(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})", RegexOptions.Compiled);

        static AntigravitySnapshot ParseTextLines(string text, string source) {
            var snapshot = new AntigravitySnapshot { Fetched = DateTimeOffset.Now, Source = source };
            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawLine in lines) {
                string line = rawLine.Trim();
                if (line.Length == 0) continue;

                if (String.IsNullOrEmpty(snapshot.Plan)) {
                    if (line.IndexOf("Ultra", StringComparison.OrdinalIgnoreCase) >= 0) snapshot.Plan = "Google AI Ultra";
                    else if (line.IndexOf("Pro", StringComparison.OrdinalIgnoreCase) >= 0) snapshot.Plan = "Google AI Pro";
                    else if (line.IndexOf("Free", StringComparison.OrdinalIgnoreCase) >= 0) snapshot.Plan = "Antigravity Free";
                }

                var pm = PercentRegex.Match(line);
                if (!pm.Success) continue;
                double pct;
                if (!Double.TryParse(pm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out pct)) continue;
                bool isUsed = line.IndexOf("used", StringComparison.OrdinalIgnoreCase) >= 0 || line.Contains("已用") || line.Contains("已使用");
                double remaining = isUsed ? 100.0 - pct : pct;
                remaining = Math.Max(0, Math.Min(100, remaining));

                DateTimeOffset? reset = null;
                var tm = IsoTimeRegex.Match(line);
                if (tm.Success) {
                    DateTimeOffset dt;
                    if (DateTimeOffset.TryParse(tm.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out dt)) reset = dt;
                }

                string[] parts = line.Split(new[] { '\t', '|' }, StringSplitOptions.RemoveEmptyEntries);
                string rawName = parts.Length > 0 ? parts[0].Trim() : line.Substring(0, pm.Index).Trim();
                rawName = Regex.Replace(rawName, @"[:\-–—]+$", "").Trim();
                if (String.IsNullOrWhiteSpace(rawName)) rawName = "额度池 " + (snapshot.Pools.Count + 1);

                snapshot.Pools.Add(new QuotaPool {
                    Name = LocalizePoolName(rawName),
                    Remaining = remaining,
                    Reset = reset
                });
            }
            return snapshot.Pools.Count > 0 ? snapshot : null;
        }

        static string FormatBucketName(string group, string window, string bucketName) {
            string g = LocalizePoolName(group);
            if (window == "5h") return g + " · 5 小时";
            if (window == "weekly") return g + " · 本周";
            return String.IsNullOrWhiteSpace(bucketName) ? g : g + " · " + bucketName;
        }

        static string LocalizePoolName(string raw) {
            if (String.IsNullOrWhiteSpace(raw)) return "额度池";
            string lower = raw.Trim().ToLowerInvariant();
            if (lower.Contains("argon")) return "Gemini 4 Argon";
            if (lower.Contains("gemini") && lower.Contains("flash")) return "Gemini Flash";
            if (lower.Contains("gemini") && lower.Contains("pro")) return "Gemini Pro";
            if (lower.Contains("gemini")) return "Gemini";
            if (lower.Contains("claude")) return "Claude / GPT";
            if (lower.Contains("5-hour") || lower.Contains("5 hour") || lower == "5h") return "5 小时额度";
            if (lower.Contains("weekly") || lower.Contains("week")) return "本周额度";
            return raw.Trim();
        }

        static Dictionary<string, object> ReadJson(string s) {
            try { return new JavaScriptSerializer { MaxJsonLength = 2097152 }.Deserialize<Dictionary<string, object>>(s); }
            catch { return null; }
        }
        static Dictionary<string, object> Dict(Dictionary<string, object> d, string key) {
            object v; return d != null && d.TryGetValue(key, out v) ? v as Dictionary<string, object> : null;
        }
        static string Text(Dictionary<string, object> d, string key) {
            object v; return d != null && d.TryGetValue(key, out v) && v != null ? Convert.ToString(v) : null;
        }
        static double? Number(Dictionary<string, object> d, string key) {
            object v; double n;
            return d != null && d.TryGetValue(key, out v) && v != null && Double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out n) && !Double.IsNaN(n) && !Double.IsInfinity(n) ? n : (double?)null;
        }
    }

    static class AntigravityClient {
        public static string FindAgyExe() {
            string envPath = Environment.GetEnvironmentVariable("PATH");
            if (!String.IsNullOrEmpty(envPath)) {
                foreach (string dir in envPath.Split(Path.PathSeparator)) {
                    if (String.IsNullOrWhiteSpace(dir)) continue;
                    foreach (string name in new[] { "agy.exe", "agy.cmd", "agy.bat" }) {
                        try {
                            string candidate = Path.Combine(dir.Trim(), name);
                            if (File.Exists(candidate)) return candidate;
                        } catch { }
                    }
                }
            }

            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] probes = {
                Path.Combine(localApp, "agy", "bin", "agy.exe"),
                Path.Combine(localApp, "Programs", "antigravity-cli", "agy.exe"),
                Path.Combine(localApp, "Programs", "Antigravity", "agy.exe"),
                Path.Combine(profile, ".local", "bin", "agy.exe"),
                Path.Combine(profile, ".antigravity", "bin", "agy.exe"),
                Path.Combine(profile, ".gemini", "antigravity", "bin", "agy.exe"),
                Path.Combine(appData, "Antigravity", "bin", "agy.exe"),
                Path.Combine(appData, "npm", "agy.cmd")
            };
            foreach (string path in probes) {
                if (File.Exists(path)) return path;
            }
            return null;
        }

        public static AntigravitySnapshot Read() {
            string customConfig = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "source-command.txt");
            if (File.Exists(customConfig)) {
                string customCmd = File.ReadAllText(customConfig, Encoding.UTF8).Trim();
                if (!String.IsNullOrEmpty(customCmd)) {
                    string customOut = RunProcess("cmd.exe", "/c " + customCmd, 20000);
                    var customSnap = AntigravitySnapshot.Parse(customOut);
                    if (customSnap != null) {
                        customSnap.Source = "自定义命令";
                        return customSnap;
                    }
                    throw new Exception("自定义数据源未返回可解析的额度信息");
                }
            }

            string agy = FindAgyExe();
            if (String.IsNullOrEmpty(agy)) {
                throw new Exception("未检测到 agy CLI，请点击下方按钮安装并登录");
            }

            string output;
            if (agy.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || agy.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)) {
                output = RunProcess("cmd.exe", "/c \"" + agy + "\" -p \"/usage\" --output-format json", 25000);
            } else {
                output = RunProcess(agy, "-p \"/usage\" --output-format json", 25000);
            }

            var snapshot = AntigravitySnapshot.Parse(output);
            if (snapshot == null) {
                if (!String.IsNullOrEmpty(output) && (output.IndexOf("login", StringComparison.OrdinalIgnoreCase) >= 0 || output.IndexOf("auth", StringComparison.OrdinalIgnoreCase) >= 0)) {
                    throw new Exception("agy 尚未登录，请先在终端运行 agy 登录");
                }
                throw new Exception("未能解析 agy /usage 返回结果");
            }
            return snapshot;
        }

        static string RunProcess(string fileName, string arguments, int timeoutMs) {
            var psi = new ProcessStartInfo(fileName, arguments) {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            using (var process = Process.Start(psi)) {
                var outTask = process.StandardOutput.ReadToEndAsync();
                var errTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(timeoutMs)) {
                    try { process.Kill(); } catch { }
                    throw new Exception("读取额度超时");
                }
                string stdout = outTask.Result ?? "";
                string stderr = errTask.Result ?? "";
                if (process.ExitCode != 0 && String.IsNullOrWhiteSpace(stdout)) {
                    string msg = stderr.Trim();
                    if (msg.Length > 120) msg = msg.Substring(0, 120) + "…";
                    throw new Exception(String.IsNullOrEmpty(msg) ? ("命令退出码 " + process.ExitCode) : msg);
                }
                return stdout;
            }
        }
    }
}
