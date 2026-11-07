using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace StudyLock
{
    /// <summary>
    /// 题库：官方题库(%ProgramData%\StudyGoodAndGood\questions.json) + 本地自建题库(local_questions.json)。
    /// 抽题时错过的题权重更高；每60秒检查文件是否被替换，孩子无需重启即可用上新题。
    /// </summary>
    public class QuestionBank
    {
        private List<Question> _all = new();
        private List<Question> _official = new();
        private List<Question> _local = new();

        private readonly string _officialPath = Path.Combine(Guardian.DataDir, "questions.json");
        private readonly Random _rnd = new();
        private DateTime _officialStamp = DateTime.MinValue;
        private DateTime _localStamp = DateTime.MinValue;
        private DateTime _lastStampCheck = DateTime.MinValue;

        /// <summary>本地自建题库文件路径（编辑器写这里，在线更新不会覆盖它）</summary>
        public static string LocalPath => Path.Combine(Guardian.DataDir, "local_questions.json");

        /// <summary>错题统计（抽题权重依据）</summary>
        public QuestionStats Stats { get; } = new();

        /// <summary>是否启用本地自建题库</summary>
        public bool UseLocalBank { get; set; } = true;

        /// <summary>家长指定的偏科科目，抽题时权重更高；空集合表示各科均衡随机</summary>
        public HashSet<string> WeakSubjects { get; set; } = new();

        public int Count => _all.Count;
        public int OfficialCount => _official.Count;
        public int LocalCount => _local.Count;

        /// <summary>错题本题目数（错过且未连续答对2次）</summary>
        public int WrongBookCount => Stats.WrongBookCount(_all);

        public QuestionBank()
        {
            EnsureReleased();
            Reload();
        }

        /// <summary>首次运行：把内置题库释放到数据目录，方便家长查看/替换。</summary>
        private void EnsureReleased()
        {
            try
            {
                Directory.CreateDirectory(Guardian.DataDir);
                if (File.Exists(_officialPath)) return;
                var asm = typeof(QuestionBank).Assembly;
                using var s = asm.GetManifestResourceStream("StudyLock.questions.json");
                if (s == null) return;
                using var f = File.Create(_officialPath);
                s.CopyTo(f);
            }
            catch { }
        }

        public void Reload()
        {
            _official = ReadFile(_officialPath);
            _local = ReadFile(LocalPath);
            _officialStamp = StampOf(_officialPath);
            _localStamp = StampOf(LocalPath);
            Rebuild();
        }

        /// <summary>强制重载（编辑器关闭后立即生效）</summary>
        public void ForceReload() => Reload();

        private static DateTime StampOf(string path)
        {
            try { return File.Exists(path) ? File.GetLastWriteTime(path) : DateTime.MinValue; }
            catch { return DateTime.MinValue; }
        }

        private static List<Question> ReadFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return new List<Question>();
                var list = JsonSerializer.Deserialize<List<Question>>(File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return list?.Where(q => !string.IsNullOrWhiteSpace(q.Text) && q.Options != null
                                         && q.Options.Count >= 2 && q.Answer >= 0
                                         && q.Answer < q.Options.Count).ToList() ?? new List<Question>();
            }
            catch { return new List<Question>(); }
        }

        /// <summary>合并官方题库与本地题库（同一题干以本地为准）。</summary>
        private void Rebuild()
        {
            var merged = new List<Question>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (UseLocalBank)
            {
                foreach (var q in _local) if (seen.Add(q.Text.Trim())) merged.Add(q);
            }
            foreach (var q in _official) if (seen.Add(q.Text.Trim())) merged.Add(q);
            if (merged.Count == 0) merged = _official.Concat(_local).ToList();
            _all = merged;
        }

        private void ReloadIfChanged()
        {
            if ((DateTime.Now - _lastStampCheck).TotalSeconds < 60) return;
            _lastStampCheck = DateTime.Now;
            var os = StampOf(_officialPath);
            var ls = StampOf(LocalPath);
            if (os == _officialStamp && ls == _localStamp) return;
            Reload();
        }

        /// <summary>抽一组题。优先孩子所在年级，其次相邻年级，最后通用题(0)。</summary>
        public List<Question> PickGroup(int grade, int count)
        {
            ReloadIfChanged();

            var pool = _all.Where(q => q.Grade == grade).ToList();
            if (pool.Count < count)
                pool = pool.Concat(_all.Where(q => Math.Abs(q.Grade - grade) == 1)).ToList();
            if (pool.Count < count)
                pool = pool.Concat(_all.Where(q => q.Grade == 0)).ToList();
            if (pool.Count == 0) pool = _all.ToList();

            var candidates = pool.GroupBy(q => q.Text.Trim()).Select(g => g.First()).ToList();
            return BalancedPick(candidates, count);
        }

        /// <summary>按科目轮转抽题，避免某一科题量过大时整组都是同一科；科内仍按错题权重抽取。</summary>
        private List<Question> BalancedPick(List<Question> candidates, int count)
        {
            var groups = candidates.GroupBy(q => q.Subject ?? "math")
                                   .ToDictionary(g => g.Key, g => g.ToList());
            var keys = groups.Keys.ToList();
            var chosen = new List<Question>();
            while (chosen.Count < count && keys.Count > 0)
            {
                Shuffle(keys);
                bool any = false;
                foreach (var k in keys)
                {
                    var src = groups[k];
                    if (src.Count == 0) continue;
                    any = true;
                    // 家长指定的偏科目的一轮抽 3 道，其余科目抽 1 道
                    int reps = WeakSubjects.Contains(k) ? 3 : 1;
                    for (int r = 0; r < reps && src.Count > 0 && chosen.Count < count; r++)
                        chosen.Add(WeightedRemoveOne(src));
                    if (chosen.Count >= count) break;
                }
                if (!any) break;
            }
            return chosen;
        }

        private void Shuffle(List<string> keys)
        {
            for (int i = keys.Count - 1; i > 0; i--)
            {
                int j = _rnd.Next(i + 1);
                (keys[i], keys[j]) = (keys[j], keys[i]);
            }
        }

        /// <summary>按错题权重不放回地抽一道。</summary>
        private Question WeightedRemoveOne(List<Question> src)
        {
            double sum = src.Sum(Stats.Weight);
            double r = _rnd.NextDouble() * sum;
            int idx = src.Count - 1;
            for (int i = 0; i < src.Count; i++)
            {
                r -= Stats.Weight(src[i]);
                if (r <= 0) { idx = i; break; }
            }
            var q = src[idx];
            src.RemoveAt(idx);
            return q;
        }

        /// <summary>错题本 TOP N（家长管理页展示）</summary>
        public List<WrongItem> WrongBookTop(int top) => Stats.TopWrong(_all, top);

        // ---------------- 在线更新 ----------------

        private DateTime _lastOnlineCheck = DateTime.MinValue;

        /// <summary>距上次在线检查超过 interval 才更新，返回结果消息；不需要更新时返回 null。</summary>
        public async Task<string?> UpdateFromUrlIfStaleAsync(string url, TimeSpan interval)
        {
            if (DateTime.Now - _lastOnlineCheck < interval) return null;
            _lastOnlineCheck = DateTime.Now;
            return await UpdateFromUrlAsync(url);
        }

        /// <summary>
        /// 从 URL 下载官方题库并替换本地文件。带完整校验和自动备份，下载或格式有问题时保留旧题库。
        /// 本地自建题库(local_questions.json)不受影响。
        /// </summary>
        public async Task<string> UpdateFromUrlAsync(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                    return "未设置题库地址";

                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                var json = await http.GetStringAsync(url);
                var list = JsonSerializer.Deserialize<List<Question>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // 校验：至少10题，且每题结构合法，防止坏文件把题库搞挂
                if (list == null || list.Count < 10)
                    return $"更新失败：题库格式不正确（解析到 {list?.Count ?? 0} 题，需至少10题）";

                foreach (var q in list)
                {
                    if (string.IsNullOrWhiteSpace(q.Text) || q.Options == null ||
                        q.Options.Count < 2 || q.Answer < 0 || q.Answer >= q.Options.Count)
                        return $"更新失败：第 {q.Id} 题数据不完整（题干/选项/答案缺失或越界）";
                    // 兼容用中文科目名的题库（如"数学"），统一转成内部代码
                    if (string.IsNullOrWhiteSpace(q.Subject)) q.Subject = "math";
                    else if (!Names.Subject.ContainsKey(q.Subject))
                        q.Subject = Names.Subject.FirstOrDefault(kv => kv.Value == q.Subject).Key ?? "math";
                }

                Directory.CreateDirectory(Guardian.DataDir);
                if (File.Exists(_officialPath))
                    File.Copy(_officialPath, _officialPath + ".bak", true); // 备份旧题库
                File.WriteAllText(_officialPath, json);
                Reload();
                return $"更新成功，现有 {Count} 题（本地自建 {LocalCount} 题）";
            }
            catch (Exception ex)
            {
                return "更新失败：" + ex.Message;
            }
        }
    }
}
