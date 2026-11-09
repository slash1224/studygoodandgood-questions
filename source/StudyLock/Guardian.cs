using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StudyLock
{
    /// <summary>
    /// 时间管控核心（"大管家"）：
    /// 1. 用单调时钟推进"可信时间"，孩子改系统时间无效；
    /// 2. 每秒判断是否该锁屏：不在自由时段且没有剩余额度 → 锁；
    /// 3. 答题通过后发放额度，受每日上限约束；
    /// 4. 状态用 DPAPI 加密落盘（仅本机本用户可解密），重启后恢复。
    /// </summary>
    public class Guardian
    {
        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();

        public static readonly string DataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "StudyGoodAndGood");
        private static readonly string StatePath = Path.Combine(DataDir, "state.dat");
        private static readonly string EventsPath = Path.Combine(DataDir, "events.json");
        private static readonly string RulesPath = Path.Combine(DataDir, "rules.json");

        public RuleConfig Rules { get; private set; } = new();
        public UsageState State { get; private set; } = new();
        public List<EventLog> Events { get; private set; } = new();

        /// <summary>
        /// 调试模式（供开发者本机使用）：不禁任务管理器、不装键盘钩子、锁屏窗口可 Alt+F4 关闭。
        /// 开启方式：在 C:\ProgramData\StudyGoodAndGood\ 下新建空文件 debug.flag，重启程序生效。
        /// 删除该文件即恢复严格管控。安装包不会附带此文件。
        /// </summary>
        public bool DebugMode { get; set; }

        public static bool DetectDebugMode() =>
            File.Exists(Path.Combine(DataDir, "debug.flag"));

        /// <summary>锁屏状态发生变化（true=应锁定）。UI 订阅此事件。</summary>
        public event Action<bool>? LockStateChanged;
        /// <summary>每秒触发一次，用于界面刷新剩余时间。</summary>
        public event Action? Tick;

        private bool _lastLockState;
        private DateTime _lastSave = DateTime.MinValue;
        private string _lastDate = "";

        // ---------------- 可信时钟 ----------------

        /// <summary>当前可信本地时间：锚定上次落盘的系统时间 + 单调时钟增量。</summary>
        public DateTime TrustedNow
        {
            get
            {
                var s = State;
                if (s.LastWallClockUtc == DateTime.MinValue || s.LastTickMs == 0)
                    return DateTime.Now; // 首次运行，只能先用系统时间
                long elapsedMs = (long)GetTickCount64() - s.LastTickMs;
                var t = s.LastWallClockUtc.ToLocalTime() + TimeSpan.FromMilliseconds(elapsedMs);
                return t;
            }
        }

        /// <summary>每次采样：检测系统时间被往回改（篡改）并刷新锚点。</summary>
        private void RefreshAnchor()
        {
            var wallUtc = DateTime.UtcNow;
            long tick = (long)GetTickCount64();
            if (State.LastWallClockUtc != DateTime.MinValue && State.LastTickMs != 0)
            {
                // 可信UTC时间
                var trustedUtc = State.LastWallClockUtc + TimeSpan.FromMilliseconds(tick - State.LastTickMs);
                // 系统时间被往回拨超过5分钟 → 记录篡改，不采纳
                if (wallUtc < trustedUtc - TimeSpan.FromMinutes(5))
                {
                    State.TimeTamperCount++;
                    Log("tamper", $"检测到系统时间被回拨 {trustedUtc - wallUtc:hh\\:mm\\:ss}，已忽略（第{State.TimeTamperCount}次）");
                    wallUtc = trustedUtc; // 用可信时间覆盖，锚点继续走
                }
                // 系统时间被往后调：不影响额度计算（额度由单调时钟扣减），直接采纳
            }
            State.LastWallClockUtc = wallUtc;
            State.LastTickMs = tick;
        }

        // ---------------- 锁屏判定 ----------------

        /// <summary>此刻是否在自由时段内</summary>
        public bool InFreeWindow(DateTime now)
        {
            foreach (var w in Rules.FreeWindows)
            {
                var parts = w.Split('-');
                if (parts.Length != 2) continue;
                if (TimeSpan.TryParse(parts[0], out var from) && TimeSpan.TryParse(parts[1], out var to))
                {
                    var t = now.TimeOfDay;
                    if (from <= to) { if (t >= from && t < to) return true; }
                    else { if (t >= from || t < to) return true; } // 跨午夜
                }
            }
            return false;
        }

        /// <summary>家长手动触发的一键锁屏；答题通关或家长加时后自动解除</summary>
        public bool ForceLock { get; private set; }

        public bool ShouldLock(DateTime now) =>
            Rules.Enabled && (ForceLock || (!InFreeWindow(now) && State.BonusRemainingSeconds <= 0));

        /// <summary>一键锁屏：立刻弹出锁屏窗口（不扣额度，可用来调试或临时管控）</summary>
        public void ForceLockNow()
        {
            if (!ForceLock) Log("lock", "收到一键锁屏请求");
            ForceLock = true;
            _lastLockState = true;
            // 即使已经处于锁定状态也发一次通知：调试模式下 Alt+F4 关掉窗口后，
            // 状态仍是锁定，需要靠这次通知把窗口重新弹出来。
            LockStateChanged?.Invoke(true);
        }

        /// <summary>每秒主循环（由 App 调用）</summary>
        public void OnSecond()
        {
            RefreshAnchor();
            var now = TrustedNow;

            // 跨天重置
            string today = now.ToString("yyyy-MM-dd");
            if (today != _lastDate)
            {
                if (!string.IsNullOrEmpty(_lastDate))
                {
                    State.TrustedDate = today;
                    State.BonusRemainingSeconds = 0;
                    State.BonusEarnedTodaySeconds = 0;
                    State.GroupsDoneToday = 0;
                    State.CorrectToday = 0;
                    State.AnsweredToday = 0;
                    Log("lock", "新的一天，额度清零");
                }
                _lastDate = today;
                if (string.IsNullOrEmpty(State.TrustedDate)) State.TrustedDate = today;
            }

            bool locked = ShouldLock(now);
            // 解锁状态下，每秒扣减额度（自由时段内不扣）
            if (!locked && Rules.Enabled && !InFreeWindow(now) && State.BonusRemainingSeconds > 0)
            {
                State.BonusRemainingSeconds--;
                if (State.BonusRemainingSeconds == 0)
                {
                    Log("lock", "解锁额度用完，重新锁定");
                    locked = true;
                }
            }

            if (locked != _lastLockState)
            {
                _lastLockState = locked;
                LockStateChanged?.Invoke(locked);
            }

            // 每10秒落盘一次，防止断电丢状态
            if ((DateTime.Now - _lastSave).TotalSeconds >= 10)
            {
                SaveState();
                _lastSave = DateTime.Now;
            }
            Tick?.Invoke();
        }

        // ---------------- 答题结算 ----------------

        public enum GroupResult { Passed, Failed, DailyLimitReached }

        /// <summary>一组题答完结算。返回结果与本次获得的秒数。</summary>
        public (GroupResult, int) SettleGroup(int correct, int total)
        {
            State.AnsweredToday += total;
            State.CorrectToday += correct;
            int need = (int)Math.Ceiling(total * Rules.PassPercent / 100.0);
            if (correct < need)
            {
                Log("fail", $"答题未达标：{correct}/{total}（需{need}题正确）");
                SaveState();
                return (GroupResult.Failed, 0);
            }
            if (State.GroupsDoneToday >= Rules.MaxGroupsPerDay ||
                State.BonusEarnedTodaySeconds >= Rules.MaxBonusMinutesPerDay * 60)
            {
                Log("fail", "已达今日解锁上限");
                SaveState();
                return (GroupResult.DailyLimitReached, 0);
            }
            int grant = Rules.UnlockMinutesPerGroup * 60;
            int cap = Rules.MaxBonusMinutesPerDay * 60 - State.BonusEarnedTodaySeconds;
            grant = Math.Min(grant, Math.Max(cap, 0));
            State.GroupsDoneToday++;
            State.BonusEarnedTodaySeconds += grant;
            State.BonusRemainingSeconds += grant;
            ForceLock = false;
            Log("pass", $"答题通过 {correct}/{total}，解锁 {grant / 60} 分钟");
            SaveState();
            return (GroupResult.Passed, grant);
        }

        /// <summary>家长临时加额度（分钟）</summary>
        public void AddBonusMinutes(int minutes, string by)
        {
            int sec = minutes * 60;
            State.BonusRemainingSeconds += sec;
            ForceLock = false;
            Log("addbonus", $"{by} 临时增加 {minutes} 分钟");
            SaveState();
        }

        // ---------------- 持久化 ----------------

        public void LoadAll()
        {
            // 旧版本数据目录迁移（StudyLock -> StudyGoodAndGood）
            var oldDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "StudyLock");
            if (Directory.Exists(oldDir) && !Directory.Exists(DataDir))
            {
                try { Directory.Move(oldDir, DataDir); } catch { }
            }
            Directory.CreateDirectory(DataDir);
            Rules = LoadJson(RulesPath, new RuleConfig(), encrypt: false);
            State = LoadJson(StatePath, new UsageState(), encrypt: true);
            Events = LoadJson(EventsPath, new List<EventLog>(), encrypt: false);
            _lastDate = TrustedNow.ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(Rules.AdminToken))
            {
                Rules.AdminToken = Random.Shared.Next(100000, 999999).ToString();
                SaveRules();
            }
            if (ShouldLock(TrustedNow)) _lastLockState = true;
        }

        public void SaveRules() => SaveJson(RulesPath, Rules, encrypt: false);
        public void SaveState() => SaveJson(StatePath, State, encrypt: true);
        public void SaveEvents() => SaveJson(EventsPath, Events.TakeLast(500).ToList(), encrypt: false);

        public void Log(string type, string detail)
        {
            Events.Add(new EventLog { Time = DateTime.Now, Type = type, Detail = detail });
            if (Events.Count > 520) Events = Events.TakeLast(500).ToList();
            SaveEvents();
        }

        private static T LoadJson<T>(string path, T fallback, bool encrypt)
        {
            try
            {
                if (!File.Exists(path)) return fallback;
                var bytes = File.ReadAllBytes(path);
                if (encrypt) bytes = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
                // 兼容记事本 / PowerShell 5 保存的带 BOM 的 UTF-8，避免外部编辑后整份规则被静默重置
                var text = Encoding.UTF8.GetString(bytes).Trim().TrimStart('\uFEFF').Trim();
                return JsonSerializer.Deserialize<T>(text) ?? fallback;
            }
            catch { return fallback; } // 文件损坏时回退默认，不让程序崩溃
        }

        private static void SaveJson<T>(string path, T obj, bool encrypt)
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj));
                if (encrypt) bytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(path, bytes);
            }
            catch { /* 磁盘异常不影响主流程 */ }
        }
    }
}
