using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QRCoder;

namespace StudyLock
{
    /// <summary>
    /// 锁屏窗口：全屏置顶覆盖所有显示器，含锁定主页 / 答题 / 结果 / 家长面板 四个视图。
    /// </summary>
    public partial class LockWindow : Window
    {
        private readonly Guardian _g;
        private readonly QuestionBank _bank;
        private readonly AdminServer _admin;
        private readonly DispatcherTimer _topmostTimer;

        private List<Question> _group = new();
        private int _index;
        private int _correct;
        private readonly List<string> _wrongExplains = new();

        // ---- AI 老师 ----
        private readonly List<(Question Q, int Choice)> _wrongQuestions = new();
        private readonly List<AiChatMessage> _aiMessages = new();
        private Task? _aiStartTask;
        private CancellationTokenSource? _aiCts;
        private bool _aiBusy;
        private bool _aiGreeted;

        private const string AiSystemPrompt =
            "你是一位耐心、亲切的中小学 AI 老师，正在学生电脑的锁屏学习界面里和学生对话。\n" +
            "要求：\n" +
            "1. 学生问题目时，先引导他自己思考：点出考查的知识点，用提问或举例启发，不要一上来就给最终答案；" +
            "等学生尝试过或明确要求后，再给出完整讲解和答案。\n" +
            "2. 讲解分步骤、口语化，适合中小学生，一次回答控制在 300 字以内。\n" +
            "3. 拒绝任何与学习无关的内容：游戏攻略、娱乐八卦、视频、小说、闲聊灌水等，礼貌说明只能聊学习。\n" +
            "4. 绝不提供绕过家长管控、解锁电脑、破解软件的方法。\n" +
            "5. 多鼓励学生，但不要空洞夸奖。";

        private static readonly Dictionary<string, string> SubjectNames = Names.Subject;

        private int _wrongBookInGroup;

        public LockWindow(Guardian g, QuestionBank bank, AdminServer admin)
        {
            InitializeComponent();
            _g = g; _bank = bank; _admin = admin;

            // 覆盖所有显示器
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            // 每500ms强制置顶，防止被其它窗口盖住
            _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _topmostTimer.Tick += (_, _) =>
            {
                if (!IsVisible) return;
                Topmost = false; Topmost = true;
                Activate();
            };
            _g.Tick += RefreshHome;
            _g.Tick += RotateQuote;

            // 调试模式：显示标识，方便开发者本机测试
            if (_g.DebugMode) DebugBadge.Visibility = Visibility.Visible;

            QuoteText.Text = Quotes.Pick();
        }

        private int _quoteSeconds;

        // 锁屏期间每60秒换一条名言
        private void RotateQuote()
        {
            if (!IsVisible) return;
            if (++_quoteSeconds < 60) return;
            _quoteSeconds = 0;
            QuoteText.Text = Quotes.Pick();
        }

        public void ShowLocked()
        {
            ShowHome();
            if (!IsVisible) Show();
            _topmostTimer.Start();
            Activate();
        }

        public void HideUnlocked()
        {
            _topmostTimer.Stop();
            ShutdownAiServer();   // 解锁后关掉本地模型，把显存还给游戏
            Hide();
        }

        // ---------------- 主页 ----------------

        private void ShowHome()
        {
            HomeView.Visibility = Visibility.Visible;
            QuizView.Visibility = Visibility.Collapsed;
            ResultView.Visibility = Visibility.Collapsed;
            ParentView.Visibility = Visibility.Collapsed;
            AiView.Visibility = Visibility.Collapsed;
            CancelAiReply();
            RefreshHome();
        }

        private void RefreshHome()
        {
            if (!IsVisible || HomeView.Visibility != Visibility.Visible) return;
            var now = _g.TrustedNow;
            var s = _g.State; var r = _g.Rules;

            if (s.BonusRemainingSeconds > 0)
                StatusText.Text = $"剩余解锁时间：{s.BonusRemainingSeconds / 60} 分 {s.BonusRemainingSeconds % 60} 秒";
            else
                StatusText.Text = $"今日已完成 {s.GroupsDoneToday} 组题（答对 {s.CorrectToday}/{s.AnsweredToday}）\n答对 {r.PassPercent}% 可解锁 {r.UnlockMinutesPerGroup} 分钟";

            FreeWindowText.Text = "自由时段：" + string.Join("  ", r.FreeWindows);
            var localInfo = _bank.LocalCount > 0 ? $"（本地自建 {_bank.LocalCount} 题）" : "";
            var wrongInfo = _bank.WrongBookCount > 0 ? $" · 错题待巩固 {_bank.WrongBookCount} 道" : "";
            BankInfo.Text = $"题库 {_bank.Count} 题{localInfo} · 当前年级：{r.Grade} 年级{wrongInfo}";
            StartQuizBtn.IsEnabled =
                s.GroupsDoneToday < r.MaxGroupsPerDay &&
                s.BonusEarnedTodaySeconds < r.MaxBonusMinutesPerDay * 60;
            if (!StartQuizBtn.IsEnabled)
                StatusText.Text = "今日解锁次数已用完，明天再来吧！也可请家长临时加额度。";
        }

        // ---------------- 答题 ----------------

        private void OnStartQuiz(object sender, RoutedEventArgs e)
        {
            _group = _bank.PickGroup(_g.Rules.Grade, _g.Rules.QuestionsPerGroup);
            if (_group.Count == 0)
            {
                MessageBox.Show("题库为空，请检查 questions.json", "提示");
                return;
            }
            _index = 0; _correct = 0; _wrongExplains.Clear();
            _wrongQuestions.Clear();
            AiExplainBtn.Visibility = Visibility.Collapsed;
            _wrongBookInGroup = _group.Count(q => _bank.Stats.IsWrongBook(q));
            HomeView.Visibility = Visibility.Collapsed;
            QuizView.Visibility = Visibility.Visible;
            ShowQuestion();
        }

        private void ShowQuestion()
        {
            var q = _group[_index];
            ProgressText.Text = $"第 {_index + 1}/{_group.Count} 题";
            SubjectText.Text = $"【{(SubjectNames.TryGetValue(q.Subject, out var n) ? n : q.Subject)}】难度 {new string('★', Math.Clamp(q.Difficulty, 1, 3))}";
            QuestionText.Text = q.Text;
            OptionsPanel.Children.Clear();
            char label = 'A';
            foreach (var opt in q.Options)
            {
                var btn = new Button
                {
                    Content = $"{label}. {opt}",
                    FontSize = 18,
                    Padding = new Thickness(18, 12, 18, 12),
                    Margin = new Thickness(0, 6, 0, 6),
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1A, 0x1F, 0x26)),
                    Foreground = System.Windows.Media.Brushes.White,
                    BorderThickness = new Thickness(1),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3A, 0x42, 0x4C)),
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Cursor = Cursors.Hand
                };
                int choice = label - 'A';
                btn.Click += (_, _) => OnAnswer(q, choice);
                OptionsPanel.Children.Add(btn);
                label++;
            }
        }

        private void OnAnswer(Question q, int choice)
        {
            bool right = choice == q.Answer;
            _bank.Stats.Record(q, right);   // 记录历史，错题下次权重更高
            if (right) _correct++;
            else
            {
                _wrongExplains.Add($"【{q.Text}】正确答案：{q.Options[q.Answer]}。{q.Explain}");
                _wrongQuestions.Add((q, choice));
            }

            _index++;
            if (_index < _group.Count) { ShowQuestion(); return; }

            // 一组答完，结算
            var (result, grantSec) = _g.SettleGroup(_correct, _group.Count);
            QuizView.Visibility = Visibility.Collapsed;
            ResultView.Visibility = Visibility.Visible;
            switch (result)
            {
                case Guardian.GroupResult.Passed:
                    ResultTitle.Text = "恭喜通过！";
                    ResultTitle.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x34, 0xA8, 0x53));
                    ResultDetail.Text = $"答对 {_correct}/{_group.Count}，解锁 {grantSec / 60} 分钟使用时间！";
                    break;
                case Guardian.GroupResult.DailyLimitReached:
                    ResultTitle.Text = "答对了，但今日额度已满";
                    ResultTitle.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFB, 0xBC, 0x05));
                    ResultDetail.Text = "劳逸结合，明天再来吧！";
                    break;
                default:
                    ResultTitle.Text = "还差一点，加油！";
                    ResultTitle.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEA, 0x43, 0x35));
                    ResultDetail.Text = $"答对 {_correct}/{_group.Count}，需要达到 {_g.Rules.PassPercent}%。再试一次吧！";
                    break;
            }
            if (_wrongBookInGroup > 0)
                ResultDetail.Text += $"\n其中 {_wrongBookInGroup} 道是你之前做错过的题，多练几遍就掌握了。";
            ExplainText.Text = _wrongExplains.Count > 0
                ? "错题解析：\n" + string.Join("\n", _wrongExplains.Take(3))
                : "全部答对，太棒了！";

            AiExplainBtn.Visibility = _wrongQuestions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            // 没达标：稍等两秒，自动把孩子带到 AI 老师那里讲错题
            if (result == Guardian.GroupResult.Failed &&
                _g.Rules.AiEnabled && _g.Rules.AiAutoOpenOnFail && _wrongQuestions.Count > 0)
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    if (ResultView.Visibility == Visibility.Visible) OpenAiWithWrongQuestions();
                };
                timer.Start();
            }
        }

        private void OnResultOk(object sender, RoutedEventArgs e) => ShowHome();

        // ---------------- AI 老师（本地模型） ----------------

        private void OnOpenAiHome(object sender, RoutedEventArgs e) => ShowAiView();

        private void OnAiExplainWrong(object sender, RoutedEventArgs e) => OpenAiWithWrongQuestions();

        private void OnAiExplainWrongInChat(object sender, RoutedEventArgs e) => OpenAiWithWrongQuestions();

        private void OnAiBack(object sender, RoutedEventArgs e)
        {
            CancelAiReply();
            ShowHome();
        }

        /// <summary>打开 AI 页面，并把本次错题交给老师讲解</summary>
        private void OpenAiWithWrongQuestions()
        {
            ShowAiView();
            if (_wrongQuestions.Count == 0) return;
            _ = SendAiAsync(BuildWrongContext());
        }

        private void ShowAiView()
        {
            HomeView.Visibility = Visibility.Collapsed;
            QuizView.Visibility = Visibility.Collapsed;
            ResultView.Visibility = Visibility.Collapsed;
            ParentView.Visibility = Visibility.Collapsed;
            AiView.Visibility = Visibility.Visible;
            AiWrongBtn.Visibility = _wrongQuestions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            if (!_aiGreeted)
            {
                _aiGreeted = true;
                AddBubble("我是 AI 老师，可以帮你理解题目、讲讲知识点。\n这里只能聊学习，不能聊游戏哦。", false);
            }
            AiScroll.ScrollToEnd();
            AiInput.Focus();
        }

        private void OnAiSend(object sender, RoutedEventArgs e) => _ = SendAiAsync(AiInput.Text);

        private void OnAiInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            _ = SendAiAsync(AiInput.Text);
        }

        private async Task SendAiAsync(string? text)
        {
            text = (text ?? "").Trim();
            if (_aiBusy || text.Length == 0) return;
            if (AiView.Visibility != Visibility.Visible) ShowAiView();

            AiInput.Text = "";
            AddBubble(text, true);
            _aiMessages.Add(new AiChatMessage("user", text));

            _aiBusy = true;
            AiSendBtn.IsEnabled = false;
            var reply = AddBubble("", false);
            var sb = new StringBuilder();

            try
            {
                if (!await EnsureAiReadyAsync())
                {
                    reply.Text = "（AI 老师暂时不可用）";
                    return;
                }

                var rules = _g.Rules;
                var msgs = new List<AiChatMessage> { new AiChatMessage("system", AiSystemPrompt) };
                msgs.AddRange(_aiMessages.TakeLast(10));

                CancelAiReply();
                _aiCts = new CancellationTokenSource();
                var token = _aiCts.Token;

                await AiTutor.ChatStreamAsync(rules.AiServerUrl, msgs, delta =>
                {
                    sb.Append(delta);
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        reply.Text = sb.ToString();
                        AiScroll.ScrollToEnd();
                    }));
                }, token, () => Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (sb.Length == 0) reply.Text = "老师正在思考……";
                })));

                string answer = sb.ToString().Trim();
                if (answer.Length == 0) reply.Text = "（没有收到回复，请再试一次）";
                else _aiMessages.Add(new AiChatMessage("assistant", answer));
            }
            catch (OperationCanceledException)
            {
                if (sb.Length > 0) reply.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                reply.Text = "（回答失败：" + ex.Message + "）";
            }
            finally
            {
                _aiBusy = false;
                AiSendBtn.IsEnabled = true;
                AiScroll.ScrollToEnd();
            }
        }

        /// <summary>确保本地 AI 服务可用；首次启动会等待模型加载（1-3 分钟）</summary>
        private async Task<bool> EnsureAiReadyAsync()
        {
            var rules = _g.Rules;
            if (!rules.AiEnabled)
            {
                AiStatus.Text = "家长已关闭 AI 老师功能。";
                return false;
            }
            if (AiTutor.ServerReady(rules.AiServerUrl))
            {
                AiStatus.Text = "AI 老师已就绪（本地模型，断网也能用）";
                return true;
            }

            // 上一轮启动任务已完成但服务已不在（解锁后释放、或进程异常退出）→ 允许重新启动
            if (_aiStartTask != null && _aiStartTask.IsCompleted) _aiStartTask = null;

            if (_aiStartTask == null)
            {
                AiStatus.Text = "正在启动本地 AI 老师……首次加载需要 1-3 分钟，请耐心等待";
                _aiStartTask = Task.Run(() => AiTutor.EnsureServerAsync(rules.AiServerUrl, rules.AiLauncherPath,
                    s => Dispatcher.BeginInvoke(new Action(() => AiStatus.Text = s)),
                    CancellationToken.None));
            }

            try { await _aiStartTask; }
            catch (Exception ex)
            {
                _aiStartTask = null;
                AiStatus.Text = "AI 老师启动失败：" + ex.Message;
                return false;
            }

            if (AiTutor.ServerReady(rules.AiServerUrl)) return true;
            _aiStartTask = null;
            return false;
        }

        private void CancelAiReply()
        {
            try { _aiCts?.Cancel(); } catch { }
            _aiCts?.Dispose();
            _aiCts = null;
        }

        /// <summary>停止对话并结束本地模型服务（只结束自己拉起的进程）</summary>
        private void ShutdownAiServer()
        {
            CancelAiReply();
            AiTutor.StopServer();
            _aiStartTask = null;
        }

        private TextBlock AddBubble(string text, bool fromUser)
        {
            var tb = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 15,
                Foreground = Brushes.White,
                LineHeight = 22
            };
            var border = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 6, 0, 6),
                MaxWidth = 680,
                HorizontalAlignment = fromUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Background = new SolidColorBrush(fromUser
                    ? Color.FromRgb(0x1A, 0x73, 0xE8)
                    : Color.FromRgb(0x23, 0x2A, 0x33)),
                Child = tb
            };
            AiMessagesPanel.Children.Add(border);
            AiScroll.ScrollToEnd();
            return tb;
        }

        /// <summary>把本次错题整理成给 AI 的提问文本（含题干、选项、孩子答案、正确答案）</summary>
        private string BuildWrongContext()
        {
            var sb = new StringBuilder();
            sb.AppendLine("我这次答题有几道做错了，请帮我讲一讲。先别直接给答案，带我想一想我可能哪里想错了，再一步步讲解。");
            int i = 1;
            foreach (var item in _wrongQuestions.Take(5))
            {
                var q = item.Q;
                sb.AppendLine();
                sb.AppendLine($"{i}. 【{Names.SubjectCn(q.Subject)}】{q.Text}");
                for (int k = 0; k < q.Options.Count; k++)
                    sb.AppendLine($"   {(char)('A' + k)}. {q.Options[k]}");
                string mine = item.Choice >= 0 && item.Choice < q.Options.Count
                    ? $"{(char)('A' + item.Choice)}. {q.Options[item.Choice]}"
                    : "（未作答）";
                sb.AppendLine($"   我的答案：{mine}");
                sb.AppendLine($"   正确答案：{(char)('A' + q.Answer)}. {q.Options[q.Answer]}");
                if (!string.IsNullOrWhiteSpace(q.Explain)) sb.AppendLine($"   题库解析：{q.Explain}");
                i++;
            }
            return sb.ToString().TrimEnd();
        }

        // ---------------- 家长面板 ----------------

        private void OnParentPanel(object sender, RoutedEventArgs e)
        {
            HomeView.Visibility = Visibility.Collapsed;
            ParentView.Visibility = Visibility.Visible;
            PwdBox.Password = "";
            PwdError.Visibility = Visibility.Collapsed;
            QrImage.Visibility = Visibility.Collapsed;
            QrTip.Visibility = Visibility.Collapsed;
            EditorBtn.Visibility = Visibility.Collapsed;
            EditorTip.Visibility = Visibility.Collapsed;
            PwdBox.Focus();
        }

        private void OnVerifyParent(object sender, RoutedEventArgs e)
        {
            if (PwdBox.Password != _g.Rules.ParentPassword)
            {
                PwdError.Visibility = Visibility.Visible;
                return;
            }
            PwdError.Visibility = Visibility.Collapsed;
            try
            {
                string url = _admin.AdminUrl;
                using var gen = new QRCodeGenerator();
                using var data = gen.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
                var qr = new PngByteQRCode(data);
                byte[] png = qr.GetGraphic(8);
                QrImage.Source = ToImageSource(png);
                QrImage.Visibility = Visibility.Visible;
                QrTip.Text = $"手机连接同一WiFi扫码访问\n{url}";
                QrTip.Visibility = Visibility.Visible;
                EditorBtn.Visibility = Visibility.Visible;
                EditorTip.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                QrTip.Text = "二维码生成失败：" + ex.Message;
                QrTip.Visibility = Visibility.Visible;
            }
        }

        private static BitmapImage ToImageSource(byte[] png)
        {
            using var ms = new MemoryStream(png);
            var img = new BitmapImage();
            img.BeginInit();
            img.StreamSource = ms;
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            return img;
        }

        private void OnBackHome(object sender, RoutedEventArgs e) => ShowHome();

        /// <summary>打开本地题库编辑器（家长已通过密码验证）。打开期间暂停置顶，避免抢焦点。</summary>
        private void OnOpenEditor(object sender, RoutedEventArgs e)
        {
            _topmostTimer.Stop();
            try
            {
                var editor = new BankEditorWindow(this);
                editor.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开题库编辑器失败：" + ex.Message, "提示");
            }
            finally
            {
                _bank.ForceReload();          // 编辑结果立即生效，不用等60秒
                if (IsVisible) _topmostTimer.Start();
                RefreshHome();
            }
        }

        // 拦截 Alt+F4 之外的键盘关闭尝试（Alt+F4 已被全局钩子拦截，双保险）
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) e.Handled = true;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // 锁定状态下禁止关闭窗口（调试模式除外）
            if (_topmostTimer.IsEnabled && !_g.DebugMode) e.Cancel = true;
            if (!e.Cancel)
            {
                // 窗口确实要关闭（调试模式 Alt+F4）：停止计时器并退订事件，
                // 否则下次召唤重建窗口时会出现重复订阅
                _topmostTimer.Stop();
                _g.Tick -= RefreshHome;
                _g.Tick -= RotateQuote;
                ShutdownAiServer();
            }
            base.OnClosing(e);
        }
    }
}
