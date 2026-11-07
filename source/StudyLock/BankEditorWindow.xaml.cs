using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace StudyLock
{
    /// <summary>
    /// 本地自建题库编辑器。两种进入方式：
    /// 1) 把程序 exe 复制一份改名为「题库编辑器.exe」双击（或用 --editor 参数启动）；
    /// 2) 锁屏界面 → 家长管理 → 验证密码后点「本地题库编辑器」。
    /// 第一次启动需输入家长密码，防止孩子自己改题绕过管控。
    /// </summary>
    public partial class BankEditorWindow : Window
    {
        private sealed class Row
        {
            public Question Q { get; }
            public string Display { get; }
            public Row(Question q)
            {
                Q = q;
                var text = q.Text.Length > 26 ? q.Text[..26] + "…" : q.Text;
                Display = $"【{Names.SubjectCn(q.Subject)}·{Names.GradeCn(q.Grade)}】{text}";
            }
        }

        private List<Question> _items = new();
        private Question? _editing;
        /// <summary>程序内改动选中项时置为 true，避免被当成"用户点选某题"而进入修改状态</summary>
        private bool _suppressSelect;

        public BankEditorWindow(Window? owner = null)
        {
            InitializeComponent();
            if (owner != null)
            {
                Owner = owner;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            Topmost = owner != null; // 从锁屏界面打开时要盖在锁屏之上

            PathText.Text = "保存位置：" + QuestionBank.LocalPath;
            foreach (var kv in Names.Subject)
                SubjectBox.Items.Add(new ComboBoxItem { Content = kv.Value, Tag = kv.Key });
            foreach (var kv in Names.Grade)
                GradeBox.Items.Add(new ComboBoxItem { Content = kv.Value, Tag = kv.Key });
            DiffBox.Items.Add(new ComboBoxItem { Content = "1 · 简单", Tag = 1 });
            DiffBox.Items.Add(new ComboBoxItem { Content = "2 · 中等", Tag = 2 });
            DiffBox.Items.Add(new ComboBoxItem { Content = "3 · 较难", Tag = 3 });

            LoadBank();
            ResetForm();
        }

        // ---------------- 读写本地题库 ----------------

        private void LoadBank()
        {
            try
            {
                if (File.Exists(QuestionBank.LocalPath))
                {
                    var list = JsonSerializer.Deserialize<List<Question>>(
                        File.ReadAllText(QuestionBank.LocalPath),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (list != null) _items = list;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("读取本地题库失败：" + ex.Message, "提示");
            }
            RefreshList();
        }

        private void SaveBank()
        {
            try
            {
                Directory.CreateDirectory(Guardian.DataDir);
                for (int i = 0; i < _items.Count; i++) _items[i].Id = 900001 + i; // 本地题 id 与官方题分开
                var json = JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(QuestionBank.LocalPath, json, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存失败：" + ex.Message + "\n请确认程序有权限写入该目录。", "提示");
            }
        }

        // ---------------- 列表 ----------------

        private void RefreshList()
        {
            var kw = SearchBox.Text.Trim();
            var rows = _items
                .Where(q => kw.Length == 0 || q.Text.Contains(kw) || Names.SubjectCn(q.Subject).Contains(kw))
                .Select(q => new Row(q)).ToList();
            QuestionList.ItemsSource = rows;
            CountText.Text = kw.Length == 0
                ? $"共 {_items.Count} 题"
                : $"筛选 {rows.Count} / {_items.Count} 题";
        }

        private void SelectItem(Question q)
        {
            if (QuestionList.ItemsSource is IEnumerable<Row> rows)
                QuestionList.SelectedItem = rows.FirstOrDefault(r => r.Q == q);
        }

        private void OnSearch(object sender, TextChangedEventArgs e) => RefreshList();

        private void OnSelect(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelect) return;
            if (QuestionList.SelectedItem is Row r) LoadForm(r.Q);
        }

        private void LoadForm(Question? q)
        {
            _editing = q;
            TextQ.Text = q?.Text ?? "";
            OptA.Text = q?.Options.ElementAtOrDefault(0) ?? "";
            OptB.Text = q?.Options.ElementAtOrDefault(1) ?? "";
            OptC.Text = q?.Options.ElementAtOrDefault(2) ?? "";
            OptD.Text = q?.Options.ElementAtOrDefault(3) ?? "";
            var ans = q?.Answer ?? -1;
            RbA.IsChecked = ans == 0;
            RbB.IsChecked = ans == 1;
            RbC.IsChecked = ans == 2;
            RbD.IsChecked = ans == 3;
            SelectTag(SubjectBox, q?.Subject ?? "math");
            SelectTag(GradeBox, q?.Grade ?? 7);
            var diff = q?.Difficulty ?? 0;
            if (diff < 1 || diff > 3) diff = 2;
            SelectTag(DiffBox, diff);
            ExplainQ.Text = q?.Explain ?? "";
        }

        private static void SelectTag(ComboBox box, object tag)
        {
            foreach (var item in box.Items)
                if (item is ComboBoxItem ci && Equals(ci.Tag, tag))
                {
                    box.SelectedItem = ci;
                    return;
                }
            if (box.Items.Count > 0) box.SelectedIndex = 0;
        }

        private static object? TagOf(ComboBox box) =>
            (box.SelectedItem as ComboBoxItem)?.Tag;

        // ---------------- 按钮 ----------------

        private void OnNew(object sender, RoutedEventArgs e)
        {
            QuestionList.SelectedItem = null;
            ResetForm();
            TextQ.Focus();
            Status("已进入新建模式：填好后点「保存到本地题库」。");
        }

        private void OnClear(object sender, RoutedEventArgs e)
        {
            QuestionList.SelectedItem = null;
            ResetForm();
            Status("表单已清空。");
        }

        private void ResetForm() => LoadForm(null);

        private void OnSave(object sender, RoutedEventArgs e)
        {
            var text = TextQ.Text.Trim();
            if (text.Length == 0) { Status("请先填写题干。"); return; }

            var opts = new[] { OptA.Text.Trim(), OptB.Text.Trim(), OptC.Text.Trim(), OptD.Text.Trim() };
            if (opts.Any(o => o.Length == 0)) { Status("四个选项都要填写，否则孩子看到的选择不完整。"); return; }

            int ans = RbA.IsChecked == true ? 0 : RbB.IsChecked == true ? 1
                    : RbC.IsChecked == true ? 2 : RbD.IsChecked == true ? 3 : -1;
            if (ans < 0) { Status("请在 A/B/C/D 后面勾选正确答案。"); return; }

            // 只有在列表里主动选中某题时才算"修改"；否则一律新增（题干完全相同才视为同一题）
            Question target;
            bool isNewQuestion;
            if (_editing != null && _items.Contains(_editing))
            {
                target = _editing;
                isNewQuestion = false;
            }
            else
            {
                var same = _items.FirstOrDefault(x => x.Text.Trim() == text);
                if (same != null)
                {
                    target = same;
                    isNewQuestion = false;
                }
                else
                {
                    target = new Question();
                    _items.Add(target);
                    isNewQuestion = true;
                }
            }

            target.Text = text;
            target.Options = opts.ToList();
            target.Answer = ans;
            target.Subject = TagOf(SubjectBox) as string ?? "math";
            target.Grade = TagOf(GradeBox) is int g ? g : 7;
            target.Difficulty = TagOf(DiffBox) is int d ? d : 2;
            target.Explain = ExplainQ.Text.Trim();

            SaveBank();
            RefreshList();

            if (isNewQuestion)
            {
                // 保存新题后回到"新建"状态，避免下一题被误当成修改上一题
                _editing = null;
                _suppressSelect = true;
                QuestionList.SelectedItem = null;
                _suppressSelect = false;
                ResetForm();
                Status($"已添加，本地题库共 {_items.Count} 题。可以直接录入下一题，或在左侧点选已有题目修改。");
            }
            else
            {
                _editing = target;
                _suppressSelect = true;
                SelectItem(target);
                _suppressSelect = false;
                Status($"已更新这道题，本地题库共 {_items.Count} 题。锁屏程序会在 1 分钟内自动加载。");
            }
        }

        /// <summary>导出为电视版能直接识别的 JSON（字段名小写），手机管理页上传即可。</summary>
        private void OnExport(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0)
            {
                Status("本地题库是空的，先录几道题再导出。");
                return;
            }
            for (int i = 0; i < _items.Count; i++) _items[i].Id = 900001 + i;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "导出给电视",
                FileName = $"本地题库-给电视-{DateTime.Now:yyyyMMdd}.json",
                Filter = "JSON 文件 (*.json)|*.json",
                DefaultExt = ".json",
                AddExtension = true
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                var json = JsonSerializer.Serialize(_items, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                File.WriteAllText(dlg.FileName, json, new UTF8Encoding(false));
                Status($"已导出 {_items.Count} 题到 {dlg.FileName}。把这个文件发到手机，在电视家长管理页「本地题库」里上传即可。");
            }
            catch (Exception ex)
            {
                MessageBox.Show("导出失败：" + ex.Message, "提示");
            }
        }

        private void OnDelete(object sender, RoutedEventArgs e)
        {
            if (_editing == null || !_items.Contains(_editing))
            {
                Status("请先在左侧列表里选中要删除的题目。");
                return;
            }
            if (MessageBox.Show($"确认删除这道题吗？\n\n{_editing.Text}", "删除确认",
                    MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;

            _items.Remove(_editing);
            SaveBank();
            RefreshList();
            LoadForm(null);
            Status($"已删除，本地题库还剩 {_items.Count} 题。");
        }

        private void Status(string msg) => StatusText.Text = msg;

        // ---------------- 家长密码 ----------------

        /// <summary>读取家长密码（rules.json 未生成时用默认值）</summary>
        private static string ReadParentPassword()
        {
            try
            {
                var path = Path.Combine(Guardian.DataDir, "rules.json");
                if (!File.Exists(path)) return new RuleConfig().ParentPassword;
                var cfg = JsonSerializer.Deserialize<RuleConfig>(File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return cfg?.ParentPassword ?? new RuleConfig().ParentPassword;
            }
            catch { return new RuleConfig().ParentPassword; }
        }

        /// <summary>弹出密码框；密码正确返回 true。prompt/okText 可自定义提示语和按钮文字。</summary>
        public static bool AskPassword(Window? owner,
            string prompt = "编辑本地题库需要家长密码", string okText = "进入编辑器")
        {
            string expected = ReadParentPassword();
            bool ok = false;

            var dlg = new Window
            {
                Title = "家长验证",
                Width = 400,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = owner == null
                    ? WindowStartupLocation.CenterScreen
                    : WindowStartupLocation.CenterOwner,
                Topmost = true,
                Background = new SolidColorBrush(Color.FromRgb(0xFA, 0xFB, 0xFC))
            };
            if (owner != null) dlg.Owner = owner;

            var root = new StackPanel { Margin = new Thickness(22) };
            root.Children.Add(new TextBlock
            {
                Text = prompt,
                FontSize = 15,
                FontWeight = FontWeights.Bold
            });
            root.Children.Add(new TextBlock
            {
                Text = "默认密码 123456，可在家长管理页修改。",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x76, 0x84)),
                Margin = new Thickness(0, 6, 0, 0)
            });
            var box = new PasswordBox { Margin = new Thickness(0, 12, 0, 0), FontSize = 16, Padding = new Thickness(6) };
            root.Children.Add(box);

            var err = new TextBlock
            {
                Text = "密码错误",
                Foreground = new SolidColorBrush(Color.FromRgb(0xD9, 0x30, 0x25)),
                FontSize = 12,
                Margin = new Thickness(0, 8, 0, 0),
                Visibility = Visibility.Collapsed
            };
            root.Children.Add(err);

            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
            var okBtn = new Button
            {
                Content = okText,
                Padding = new Thickness(16, 7, 16, 7),
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x73, 0xE8)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            var cancelBtn = new Button
            {
                Content = "取消",
                Padding = new Thickness(14, 7, 14, 7),
                Margin = new Thickness(10, 0, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(0x8A, 0x94, 0xA0)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            bar.Children.Add(okBtn);
            bar.Children.Add(cancelBtn);
            root.Children.Add(bar);
            dlg.Content = root;

            void TryOk()
            {
                if (box.Password == expected) { ok = true; dlg.Close(); }
                else { err.Visibility = Visibility.Visible; box.SelectAll(); box.Focus(); }
            }

            okBtn.Click += (_, _) => TryOk();
            cancelBtn.Click += (_, _) => dlg.Close();
            box.KeyDown += (_, e) => { if (e.Key == Key.Enter) TryOk(); };
            dlg.Loaded += (_, _) =>
            {
                // 显式按工作区居中：多显示器/DPI 缩放时默认的 CenterScreen 位置可能跑到屏幕外
                var wa = SystemParameters.WorkArea;
                dlg.Left = wa.Left + Math.Max(0, (wa.Width - dlg.ActualWidth) / 2);
                dlg.Top = wa.Top + Math.Max(0, (wa.Height - dlg.ActualHeight) / 2);
                box.Focus();
            };

            dlg.ShowDialog();
            return ok;
        }
    }
}
