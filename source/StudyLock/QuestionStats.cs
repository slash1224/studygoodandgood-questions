using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StudyLock
{
    /// <summary>单题历史表现。按题干哈希索引，题库更新后 id 变化也不会丢。</summary>
    public class QStat
    {
        /// <summary>累计答错次数</summary>
        public int Wrong { get; set; }
        /// <summary>累计答对次数</summary>
        public int Right { get; set; }
        /// <summary>连续答对次数（答错即归零）</summary>
        public int Streak { get; set; }
        public DateTime LastWrong { get; set; }
        public DateTime LastSeen { get; set; }
    }

    /// <summary>错题本条目（给家长页展示）</summary>
    public class WrongItem
    {
        public string Text { get; set; } = "";
        public string Subject { get; set; } = "";
        public int Wrong { get; set; }
        public int Total { get; set; }
        public DateTime LastWrong { get; set; }
    }

    /// <summary>
    /// 答题统计 + 抽题权重：
    /// 错得多的题权重高（更容易再出现），连续答对后权重回落，避免孩子永远卡在同一道题。
    /// </summary>
    public class QuestionStats
    {
        private static readonly string StorePath = Path.Combine(Guardian.DataDir, "qstats.json");

        private Dictionary<string, QStat> _map = new();
        private readonly object _gate = new();

        public QuestionStats() { Load(); }

        /// <summary>题目指纹：题干哈希（取前12位十六进制）</summary>
        public static string KeyOf(Question q)
        {
            var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(q.Text.Trim()));
            return Convert.ToHexString(bytes)[..12];
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(StorePath)) return;
                var map = JsonSerializer.Deserialize<Dictionary<string, QStat>>(File.ReadAllText(StorePath));
                if (map != null) _map = map;
            }
            catch { /* 统计文件坏了就重新开始，不影响答题 */ }
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(Guardian.DataDir);
                if (_map.Count > 3000)
                {
                    // 只保留最近半年的记录，防止文件无限膨胀
                    var keep = _map.Where(kv => (DateTime.Now - kv.Value.LastSeen).TotalDays <= 180)
                                   .ToDictionary(kv => kv.Key, kv => kv.Value);
                    _map = keep;
                }
                File.WriteAllText(StorePath, JsonSerializer.Serialize(_map), new UTF8Encoding(false));
            }
            catch { }
        }

        public QStat? Get(Question q)
        {
            var key = KeyOf(q);
            lock (_gate) return _map.TryGetValue(key, out var s) ? s : null;
        }

        /// <summary>记录一次答题结果</summary>
        public void Record(Question q, bool correct)
        {
            var key = KeyOf(q);
            lock (_gate)
            {
                if (!_map.TryGetValue(key, out var s))
                {
                    s = new QStat();
                    _map[key] = s;
                }
                s.LastSeen = DateTime.Now;
                if (correct) { s.Right++; s.Streak++; }
                else { s.Wrong++; s.Streak = 0; s.LastWrong = DateTime.Now; }
                Save();
            }
        }

        /// <summary>是否属于"错题本"：错过且还没连续答对 2 次</summary>
        public bool IsWrongBook(Question q) => Get(q) is { Wrong: > 0, Streak: < 2 };

        /// <summary>
        /// 抽题权重。1.0 为正常；错得越多越高（最高6），连续答对3次减半。
        /// 说明：故意答错只会拿不到解锁时间，无法借此刷简单题，所以无需额外防作弊。
        /// </summary>
        public double Weight(Question q)
        {
            var st = Get(q);
            if (st == null) return 1.0;               // 没答过的新题，正常权重
            int total = st.Right + st.Wrong;
            if (total == 0) return 1.0;

            double w = 1.0 + 4.0 * (st.Wrong / (double)total);   // 正确率越低，权重越高
            if (st.Streak >= 3) w *= 0.5;                        // 已掌握，降权
            if (st.Wrong > 0 && st.Streak < 2 &&
                (DateTime.Now - st.LastWrong).TotalDays <= 7)
                w *= 1.5;                                        // 近一周错过的题，再提权
            return Math.Clamp(w, 0.5, 6.0);
        }

        public int WrongBookCount(IEnumerable<Question> all) =>
            all.Count(IsWrongBook);

        public List<WrongItem> TopWrong(IEnumerable<Question> all, int top)
        {
            var items = new List<WrongItem>();
            foreach (var q in all)
            {
                var st = Get(q);
                if (st == null || st.Wrong == 0) continue;
                items.Add(new WrongItem
                {
                    Text = q.Text,
                    Subject = Names.SubjectCn(q.Subject),
                    Wrong = st.Wrong,
                    Total = st.Right + st.Wrong,
                    LastWrong = st.LastWrong
                });
            }
            return items.OrderByDescending(i => i.Wrong)
                        .ThenByDescending(i => i.LastWrong)
                        .Take(top).ToList();
        }
    }
}
