using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StudyLock
{
    /// <summary>
    /// 局域网管理页：家长手机扫码即可查看状态、改规则、临时加额度。
    /// 监听 http://&lt;电脑IP&gt;:8099/admin/?t=访问码
    /// </summary>
    public class AdminServer : IDisposable
    {
        private readonly Guardian _g;
        private readonly QuestionBank _bank;
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        public const int Port = 8099;

        public AdminServer(Guardian g, QuestionBank bank) { _g = g; _bank = bank; }

        /// <summary>本机局域网IP（用于生成二维码）</summary>
        public static string GetLanIp()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            var ip = ua.Address.ToString();
                            if (ip.StartsWith("192.168.") || ip.StartsWith("10.") || ip.StartsWith("172."))
                                return ip;
                        }
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }

        public string AdminUrl => $"http://{GetLanIp()}:{Port}/admin/?t={_g.Rules.AdminToken}";

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            try
            {
                // 需要 install.bat 先执行: netsh http add urlacl url=http://+:8099/ user=所有人
                _listener.Prefixes.Add($"http://+:{Port}/");
                _listener.Start();
            }
            catch
            {
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://localhost:{Port}/");
                    _listener.Start();
                }
                catch { return; } // 端口不可用则放弃管理页，不影响主功能
            }
            Task.Run(() => Loop(_cts.Token));
        }

        private async Task Loop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener?.IsListening == true)
            {
                HttpListenerContext ctx;
                try { ctx = await _listener.GetContextAsync(); }
                catch { break; }
                _ = Task.Run(() => Handle(ctx), ct);
            }
        }

        private async void Handle(HttpListenerContext ctx)
        {
            try
            {
                var req = ctx.Request;
                var res = ctx.Response;
                string path = req.Url?.AbsolutePath ?? "/";
                string token = req.QueryString["t"] ?? "";

                // 访问码校验
                if (token != _g.Rules.AdminToken)
                {
                    Write(res, 403, "text/plain; charset=utf-8", "访问码错误。请在电脑锁屏界面点「家长管理」重新扫码。");
                    return;
                }

                switch (path)
                {
                    case "/admin/":
                    case "/admin":
                        Write(res, 200, "text/html; charset=utf-8", Page());
                        break;
                    case "/admin/state":
                        Write(res, 200, "application/json; charset=utf-8", StateJson());
                        break;
                    case "/admin/addbonus":
                        if (int.TryParse(req.QueryString["min"], out int min) && min > 0 && min <= 180)
                            _g.AddBonusMinutes(min, "家长手机");
                        Write(res, 200, "application/json; charset=utf-8", "{\"ok\":true}");
                        break;
                    case "/admin/lock":
                        // 一键锁屏：立刻弹锁屏窗，孩子答题过关或家长加时后自动解除
                        _g.ForceLockNow();
                        Write(res, 200, "application/json; charset=utf-8", "{\"ok\":true}");
                        break;
                    case "/admin/updatebank":
                        {
                            var msg = await _bank.UpdateFromUrlAsync(_g.Rules.QuestionBankUrl);
                            _g.Log("unlock", "题库手动更新：" + msg);
                            Write(res, 200, "application/json; charset=utf-8",
                                JsonSerializer.Serialize(new { msg }));
                            break;
                        }
                    case "/admin/uninstall":
                        {
                            // 卸载：需要管理页访问码才能触发，孩子在本机找不到入口
                            Write(res, 200, "application/json; charset=utf-8",
                                "{\"ok\":true,\"msg\":\"正在卸载，电脑将在数秒后恢复自由\"}");
                            _g.Log("unlock", "家长远程卸载了软件");
                            _ = Task.Run(async () =>
                            {
                                await Task.Delay(1500);
                                Installer.Uninstall();
                                Environment.Exit(0);
                            });
                            break;
                        }
                    case "/admin/save":
                        using (var sr = new StreamReader(req.InputStream, req.ContentEncoding))
                        {
                            var body = sr.ReadToEnd();
                            var cfg = JsonSerializer.Deserialize<RuleConfig>(body,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (cfg != null)
                            {
                                cfg.AdminToken = _g.Rules.AdminToken; // 防止访问码被覆盖
                                CopyRules(cfg);
                                _g.SaveRules();
                                _g.Log("unlock", "家长更新了规则");
                            }
                        }
                        Write(res, 200, "application/json; charset=utf-8", "{\"ok\":true}");
                        break;
                    default:
                        Write(res, 404, "text/plain; charset=utf-8", "404");
                        break;
                }
            }
            catch { }
            finally { try { ctx.Response.Close(); } catch { } }
        }

        private void CopyRules(RuleConfig cfg)
        {
            var r = _g.Rules;
            r.Grade = cfg.Grade; r.QuestionsPerGroup = cfg.QuestionsPerGroup;
            r.PassPercent = cfg.PassPercent; r.UnlockMinutesPerGroup = cfg.UnlockMinutesPerGroup;
            r.MaxGroupsPerDay = cfg.MaxGroupsPerDay; r.MaxBonusMinutesPerDay = cfg.MaxBonusMinutesPerDay;
            r.FreeWindows = cfg.FreeWindows ?? r.FreeWindows;
            r.Enabled = cfg.Enabled; r.ParentPassword = cfg.ParentPassword;
            r.QuestionBankUrl = cfg.QuestionBankUrl ?? r.QuestionBankUrl;
            r.AutoUpdateBank = cfg.AutoUpdateBank;
            r.UseLocalBank = cfg.UseLocalBank;
            r.WeakSubjects = cfg.WeakSubjects ?? r.WeakSubjects;
            _bank.UseLocalBank = cfg.UseLocalBank;
            _bank.WeakSubjects = r.WeakSubjects.ToHashSet();
            _bank.ForceReload();
        }

        private string StateJson()
        {
            var s = _g.State; var r = _g.Rules;
            var obj = new
            {
                enabled = r.Enabled,
                locked = _g.ShouldLock(_g.TrustedNow),
                grade = r.Grade,
                bonusLeft = s.BonusRemainingSeconds / 60,
                earnedToday = s.BonusEarnedTodaySeconds / 60,
                groups = s.GroupsDoneToday,
                correct = s.CorrectToday,
                answered = s.AnsweredToday,
                tamper = s.TimeTamperCount,
                bankCount = _bank.Count,
                localCount = _bank.LocalCount,
                useLocalBank = _bank.UseLocalBank,
                wrongBook = _bank.WrongBookTop(10),
                rules = r,
                events = _g.Events.TakeLast(50).Reverse().ToList()
            };
            return JsonSerializer.Serialize(obj);
        }

        private static void Write(HttpListenerResponse res, int code, string contentType, string body)
        {
            res.StatusCode = code;
            res.ContentType = contentType;
            var bytes = Encoding.UTF8.GetBytes(body);
            res.OutputStream.Write(bytes, 0, bytes.Length);
        }

        private string Page() => """
<!DOCTYPE html><html lang="zh"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>StudyGoodAndGood 家长管理</title>
<style>
body{font-family:system-ui;margin:0;background:#f0f2f5;color:#222}
header{background:#1a73e8;color:#fff;padding:14px 16px;font-size:18px}
.card{background:#fff;margin:12px;padding:14px;border-radius:10px;box-shadow:0 1px 3px rgba(0,0,0,.1)}
.card h3{margin:0 0 10px;font-size:15px;color:#1a73e8}
.row{display:flex;justify-content:space-between;align-items:center;padding:7px 0;border-bottom:1px solid #eee;font-size:14px}
.row:last-child{border:none}
input,select{padding:6px;border:1px solid #ccc;border-radius:6px;font-size:14px;width:110px}
button{background:#1a73e8;color:#fff;border:none;padding:8px 14px;border-radius:6px;font-size:14px;margin:4px 4px 0 0}
button.gray{background:#888}
.ev{font-size:12px;color:#555;padding:3px 0;border-bottom:1px dashed #eee}
.warn{color:#d93025;font-weight:bold}
</style></head><body>
<header>StudyGoodAndGood 家长管理</header>
<div class="card"><h3>今日状态</h3>
<div class="row"><span>剩余解锁额度</span><b id="bonus">-</b></div>
<div class="row"><span>今日已获得</span><span id="earned">-</span></div>
<div class="row"><span>已完成题组</span><span id="groups">-</span></div>
<div class="row"><span>答对 / 总答</span><span id="acc">-</span></div>
<div class="row"><span>时间篡改检测</span><span id="tamper" class="warn">-</span></div>
<div class="row"><span>管控开关</span><span id="enabled">-</span></div>
<div class="row"><span>锁屏状态</span><span id="lockstate">-</span></div>
<div class="row"><span>当前题库</span><span id="bankcount">-</span></div>
<div class="row"><span>其中本地自建</span><span id="localcount">-</span></div>
</div>
<div class="card"><h3>本地自建题库</h3>
<div class="row"><span>启用本地题库</span><select id="uselocal"><option value="true">启用</option><option value="false">关闭</option></select></div>
<div style="font-size:12px;color:#666;margin-top:8px">用电脑上的「题库编辑器」录入自己的题目（安装目录里的 StudyGoodAndGood.exe 复制一份改名为「题库编辑器.exe」双击即可，需家长密码）。本地题目与官方题库合并使用，优先出现。</div>
</div>
<div class="card"><h3>错题本 TOP10（出现概率更高）</h3><div id="wrongbook"></div></div>
<div class="card"><h3>题库在线更新</h3>
<div class="row"><span>题库地址(URL)</span><input id="bankurl" style="width:170px" placeholder="https://.../questions.json"></div>
<div class="row"><span>每3天自动更新</span><select id="autobank"><option value="true">开启</option><option value="false">关闭</option></select></div>
<button onclick="updateBank()">立即更新题库</button><span id="bankmsg" style="font-size:12px;color:#666"></span>
</div>
<div class="card"><h3>临时加额度 / 立即锁屏</h3>
<button onclick="add(10)">+10分钟</button>
<button onclick="add(30)">+30分钟</button>
<button onclick="add(60)">+60分钟</button>
<button class="gray" onclick="lockNow()">立即锁屏</button>
<div style="font-size:12px;color:#666;margin-top:8px">「立即锁屏」会马上弹出锁屏页，孩子答题过关或加时后自动解除；调试时不用再等到时间用完。</div>
</div>
<div class="card"><h3>规则设置</h3>
<div class="row"><span>年级(7=初一)</span><input id="grade" type="number" min="1" max="12"></div>
<div class="row"><span>每组题数</span><input id="qpg" type="number" min="3" max="20"></div>
<div class="row"><span>通过正确率%</span><input id="pass" type="number" min="50" max="100"></div>
<div class="row"><span>每组解锁分钟</span><input id="umin" type="number" min="5" max="120"></div>
<div class="row"><span>每日最多组数</span><input id="maxg" type="number" min="1" max="30"></div>
<div class="row"><span>每日上限(分钟)</span><input id="maxm" type="number" min="15" max="600"></div>
<div class="row"><span>自由时段(逗号分隔)</span><input id="free" style="width:170px"></div>
<div style="padding:7px 0;font-size:14px">偏科加权（可多选，不选=各科均衡随机）</div>
<div id="weak" style="display:flex;flex-wrap:wrap;gap:8px 14px;font-size:13px;padding-bottom:8px">
<label><input type="checkbox" value="math" style="width:auto">数学</label>
<label><input type="checkbox" value="chinese" style="width:auto">语文</label>
<label><input type="checkbox" value="english" style="width:auto">英语</label>
<label><input type="checkbox" value="physics" style="width:auto">物理</label>
<label><input type="checkbox" value="chemistry" style="width:auto">化学</label>
<label><input type="checkbox" value="biology" style="width:auto">生物</label>
<label><input type="checkbox" value="history" style="width:auto">历史</label>
<label><input type="checkbox" value="geography" style="width:auto">地理</label>
<label><input type="checkbox" value="politics" style="width:auto">道法</label>
<label><input type="checkbox" value="coding" style="width:auto">编程</label>
</div>
<div style="font-size:12px;color:#666">勾选后这些科目抽到的概率会明显提高，适合补偏科。</div>
<div class="row"><span>家长密码</span><input id="pwd"></div>
<div class="row"><span>启用管控</span><select id="en"><option value="true">启用</option><option value="false">停用</option></select></div>
<button onclick="save()">保存规则</button>
</div>
<div class="card"><h3>最近事件</h3><div id="events"></div></div>
<div class="card"><h3>卸载软件</h3>
<div style="font-size:13px;color:#666;margin-bottom:8px">卸载只在这里进行（孩子在本机找不到卸载入口）。卸载后电脑恢复自由，数据和题库保留在 C:\ProgramData\StudyGoodAndGood。</div>
<button class="gray" onclick="uninstall()">确认卸载</button>
</div>
<script>
const t = new URLSearchParams(location.search).get('t');
async function refresh(){
  const r = await fetch('/admin/state?t='+t); const d = await r.json();
  bonus.textContent = d.bonusLeft + ' 分钟';
  earned.textContent = d.earnedToday + ' 分钟';
  groups.textContent = d.groups + ' 组';
  acc.textContent = d.correct + ' / ' + d.answered;
  tamper.textContent = d.tamper + ' 次';
  enabled.textContent = d.enabled ? '启用中' : '已停用';
  lockstate.textContent = d.locked ? '锁屏中' : '已解锁';
  bankcount.textContent = d.bankCount + ' 题';
  localcount.textContent = d.localCount + ' 题';
  uselocal.value = String(d.useLocalBank);
  wrongbook.innerHTML = (d.wrongBook && d.wrongBook.length)
    ? d.wrongBook.map(w=>`<div class="ev">[${w.Subject}] 错${w.Wrong}次 / 共${w.Total}次 — ${w.Text.length>34?w.Text.substring(0,34)+'…':w.Text}</div>`).join('')
    : '<div style="font-size:13px;color:#888">暂无错题记录</div>';
  bankurl.value = d.rules.QuestionBankUrl || '';
  autobank.value = String(d.rules.AutoUpdateBank);
  grade.value=d.rules.Grade; qpg.value=d.rules.QuestionsPerGroup; pass.value=d.rules.PassPercent;
  umin.value=d.rules.UnlockMinutesPerGroup; maxg.value=d.rules.MaxGroupsPerDay;
  maxm.value=d.rules.MaxBonusMinutesPerDay; free.value=(d.rules.FreeWindows||[]).join(',');
  pwd.value=d.rules.ParentPassword; en.value=String(d.rules.Enabled);
  const weakSet = new Set((d.rules.WeakSubjects||[]).map(String));
  document.querySelectorAll('#weak input').forEach(cb=>cb.checked=weakSet.has(cb.value));
  events.innerHTML = d.events.map(e=>`<div class="ev">${e.Time.substring(5,16)} [${e.Type}] ${e.Detail}</div>`).join('');
}
async function add(m){ await fetch('/admin/addbonus?t='+t+'&min='+m); refresh(); alert('已加 '+m+' 分钟'); }
async function lockNow(){ await fetch('/admin/lock?t='+t); refresh(); alert('已锁屏（孩子答题过关或加时后自动解除）'); }
async function save(){ await save0(); alert('已保存'); refresh(); }
async function updateBank(){
  bankmsg.textContent = ' 更新中...';
  await save0();
  const r = await fetch('/admin/updatebank?t='+t); const d = await r.json();
  bankmsg.textContent = ' ' + d.msg; refresh();
}
async function save0(){
  const body={Grade:+grade.value,QuestionsPerGroup:+qpg.value,PassPercent:+pass.value,
    UnlockMinutesPerGroup:+umin.value,MaxGroupsPerDay:+maxg.value,MaxBonusMinutesPerDay:+maxm.value,
    FreeWindows:free.value.split(',').map(s=>s.trim()).filter(Boolean),ParentPassword:pwd.value,
    Enabled:en.value==='true',QuestionBankUrl:bankurl.value.trim(),AutoUpdateBank:autobank.value==='true',
    UseLocalBank:uselocal.value==='true',
    WeakSubjects:[...document.querySelectorAll('#weak input:checked')].map(x=>x.value)};
  await fetch('/admin/save?t='+t,{method:'POST',body:JSON.stringify(body)});
}
async function uninstall(){
  if(!confirm('确认卸载 StudyGoodAndGood 吗？卸载后本软件将从电脑删除。')) return;
  if(!confirm('再次确认：卸载后孩子使用电脑将不再受限制。确定卸载？')) return;
  await fetch('/admin/uninstall?t='+t);
  document.body.innerHTML='<h2 style="margin:40px">已卸载，软件将在几秒内从电脑删除。本页面可关闭。</h2>';
}
refresh(); setInterval(refresh, 10000);
</script></body></html>
""";

        public void Dispose()
        {
            _cts?.Cancel();
            try { _listener?.Stop(); } catch { }
        }
    }
}
