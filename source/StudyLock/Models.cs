using System;
using System.Collections.Generic;

namespace StudyLock
{
    /// <summary>一道选择题</summary>
    public class Question
    {
        public int Id { get; set; }
        /// <summary>科目: math/chinese/english/coding</summary>
        public string Subject { get; set; } = "";
        /// <summary>适用年级(1-6=小学, 7-9=初中)，0=通用</summary>
        public int Grade { get; set; }
        /// <summary>难度 1-3</summary>
        public int Difficulty { get; set; } = 1;
        public string Text { get; set; } = "";
        public List<string> Options { get; set; } = new();
        /// <summary>正确选项下标，从0开始</summary>
        public int Answer { get; set; }
        public string Explain { get; set; } = "";
    }

    /// <summary>家长规则配置</summary>
    public class RuleConfig
    {
        /// <summary>孩子当前年级 1-9(1-6小学, 7-9初中)，用于筛题</summary>
        public int Grade { get; set; } = 7;
        /// <summary>每组题量</summary>
        public int QuestionsPerGroup { get; set; } = 5;
        /// <summary>解锁所需答对比例 0-100</summary>
        public int PassPercent { get; set; } = 80;
        /// <summary>每组通过解锁分钟数</summary>
        public int UnlockMinutesPerGroup { get; set; } = 15;
        /// <summary>每天最多答几组（防刷）</summary>
        public int MaxGroupsPerDay { get; set; } = 8;
        /// <summary>每日解锁总额度上限(分钟)，防止攒太多</summary>
        public int MaxBonusMinutesPerDay { get; set; } = 120;
        /// <summary>自由时段列表 "HH:mm-HH:mm"，时段内完全放开</summary>
        public List<string> FreeWindows { get; set; } = new() { "12:00-13:00", "18:30-20:00" };
        /// <summary>是否启用管控（总开关）</summary>
        public bool Enabled { get; set; } = true;
        /// <summary>家长密码（明文存储在本机DPAPI加密文件中）</summary>
        public string ParentPassword { get; set; } = "123456";
        /// <summary>管理页访问码（6位数字，手机扫码用）</summary>
        public string AdminToken { get; set; } = "";
        /// <summary>题库在线更新地址（JSON），留空则不更新。默认走 jsDelivr CDN（国内可直连，免配置）。</summary>
        public string QuestionBankUrl { get; set; } = "https://cdn.jsdelivr.net/gh/slash1224/studygoodandgood-questions@main/questions.json";
        /// <summary>是否每3天自动在线更新题库</summary>
        public bool AutoUpdateBank { get; set; } = true;
        /// <summary>是否启用本地自建题库（与官方题库合并，本地题目优先）</summary>
        public bool UseLocalBank { get; set; } = true;
        /// <summary>家长指定的偏科科目（如 ["english","math"]），抽题时这些科目权重更高；空表示随机均衡</summary>
        public List<string> WeakSubjects { get; set; } = new();

        // ---------------- 本地 AI 老师（Omni-Edu） ----------------

        /// <summary>是否启用锁屏 AI 老师</summary>
        public bool AiEnabled { get; set; } = true;
        /// <summary>本地 AI 服务地址（Omni-Edu 的 llama.cpp 服务）</summary>
        public string AiServerUrl { get; set; } = "http://127.0.0.1:8080";
        /// <summary>Omni-Edu 启动器路径（留空时自动在程序目录/上一级目录查找 Omni-Edu）</summary>
        public string AiLauncherPath { get; set; } = "";
        /// <summary>答题不达标时自动打开 AI 老师讲错题</summary>
        public bool AiAutoOpenOnFail { get; set; } = true;
    }

    /// <summary>科目/年级的中文名称，界面和编辑器共用</summary>
    public static class Names
    {
        public static readonly Dictionary<string, string> Subject = new()
        {
            ["math"] = "数学", ["chinese"] = "语文", ["english"] = "英语", ["coding"] = "编程",
            ["physics"] = "物理", ["chemistry"] = "化学", ["biology"] = "生物",
            ["history"] = "历史", ["geography"] = "地理", ["politics"] = "道法"
        };

        public static readonly Dictionary<int, string> Grade = new()
        {
            [0] = "通用",
            [1] = "一年级", [2] = "二年级", [3] = "三年级",
            [4] = "四年级", [5] = "五年级", [6] = "六年级",
            [7] = "初一", [8] = "初二", [9] = "初三"
        };

        public static string SubjectCn(string s) => Subject.TryGetValue(s, out var n) ? n : s;
        public static string GradeCn(int g) => Grade.TryGetValue(g, out var n) ? n : $"{g}年级";
    }

    /// <summary>每日使用状态（改系统时间无法篡改：以单调时钟推进）</summary>
    public class UsageState
    {
        /// <summary>状态所属日期 yyyy-MM-dd（基于可信时钟）</summary>
        public string TrustedDate { get; set; } = "";
        /// <summary>今日剩余解锁额度（秒）</summary>
        public int BonusRemainingSeconds { get; set; }
        /// <summary>今日已累计获得的解锁额度（秒），用于每日上限</summary>
        public int BonusEarnedTodaySeconds { get; set; }
        /// <summary>今日已完成答题组数</summary>
        public int GroupsDoneToday { get; set; }
        /// <summary>今日答对题数</summary>
        public int CorrectToday { get; set; }
        /// <summary>今日答题总数</summary>
        public int AnsweredToday { get; set; }

        // ---- 可信时钟锚点（防改系统时间）----
        /// <summary>上次保存时的UTC时间</summary>
        public DateTime LastWallClockUtc { get; set; } = DateTime.MinValue;
        /// <summary>上次保存时的系统启动毫秒数 GetTickCount64</summary>
        public long LastTickMs { get; set; }
        /// <summary>检测到系统时间被篡改的次数</summary>
        public int TimeTamperCount { get; set; }
    }

    /// <summary>一条事件记录（供家长查看）</summary>
    public class EventLog
    {
        public DateTime Time { get; set; } = DateTime.Now;
        /// <summary>类型: unlock/lock/pass/fail/tamper/addbonus</summary>
        public string Type { get; set; } = "";
        public string Detail { get; set; } = "";
    }
}
