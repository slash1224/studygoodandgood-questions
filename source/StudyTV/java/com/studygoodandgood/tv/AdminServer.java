package com.studygoodandgood.tv;

import android.graphics.Bitmap;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.net.URLDecoder;
import java.util.HashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;

/**
 * 内置的局域网家长管理页：手机和电视连同一个 WiFi，
 * 用手机浏览器打开 http://电视IP:8787 就能改规则、加时、看错题本、更新题库。
 * 访问码（6 位数字）在电视的家长面板里能看到。
 */
public class AdminServer {

    public interface Host {
        Rules rules();

        TimeKeeper time();

        QuestionBank bank();

        Stats stats();

        String lanUrl();

        void onRulesSaved();

        String updateBankNow();

        QuestionBank.LocalResult applyLocalBank(String json);

        QuestionBank.LocalResult clearLocalBank();
    }

    private final Host host;
    private ServerSocket server;
    private int port = 8787;
    private volatile boolean running = false;
    private Thread acceptThread;

    public AdminServer(Host host) {
        this.host = host;
    }

    public int port() {
        return port;
    }

    public void start() {
        if (running) return;
        for (int p = 8787; p <= 8800; p++) {
            try {
                ServerSocket s = new ServerSocket();
                s.setReuseAddress(true);
                s.bind(new InetSocketAddress(p), 16);
                server = s;
                port = p;
                break;
            } catch (Exception ignored) {
            }
        }
        if (server == null) return;
        running = true;
        acceptThread = new Thread(new Runnable() {
            public void run() {
                while (running) {
                    try {
                        final Socket sock = server.accept();
                        sock.setSoTimeout(15000);
                        new Thread(new Runnable() {
                            public void run() {
                                handle(sock);
                            }
                        }, "admin-conn").start();
                    } catch (Exception e) {
                        if (running) {
                            try {
                                Thread.sleep(200);
                            } catch (Exception ignored) {
                            }
                        }
                    }
                }
            }
        }, "admin-server");
        acceptThread.start();
    }

    public void stop() {
        running = false;
        try {
            if (server != null) server.close();
        } catch (Exception ignored) {
        }
        server = null;
    }

    // ---------------- 请求处理 ----------------

    private static class Req {
        String method = "GET";
        String path = "/";
        Map<String, String> query = new HashMap<String, String>();
        byte[] body = new byte[0];

        String str(String key) {
            String v = query.get(key);
            return v == null ? "" : v;
        }
    }

    private void handle(Socket sock) {
        try {
            InputStream in = sock.getInputStream();
            OutputStream out = sock.getOutputStream();
            Req req = readRequest(in);
            if (req == null) {
                sock.close();
                return;
            }

            if ("GET".equals(req.method) && (req.path.equals("/") || req.path.equals("/index.html"))) {
                writeText(out, 200, "text/html; charset=utf-8", pageHtml());
            } else if (req.path.startsWith("/api/")) {
                if (!host.rules().adminToken.equals(req.str("token"))) {
                    writeText(out, 200, "application/json; charset=utf-8",
                            "{\"ok\":false,\"msg\":\"访问码错误，请在电视家长面板里查看 6 位访问码\"}");
                } else {
                    handleApi(req, out);
                }
            } else {
                writeText(out, 404, "text/plain; charset=utf-8", "not found");
            }
            try {
                sock.close();
            } catch (Exception ignored) {
            }
        } catch (Exception e) {
            try {
                sock.close();
            } catch (Exception ignored) {
            }
        }
    }

    private void handleApi(Req req, OutputStream out) throws Exception {
        Rules r = host.rules();
        TimeKeeper t = host.time();

        if (req.path.equals("/api/status")) {
            JSONObject o = new JSONObject();
            o.put("ok", true);
            o.put("time", t.statusJson(r));
            o.put("rules", r.toJson());
            o.put("bankCount", host.bank().count());
            o.put("localBankCount", host.bank().localCount());
            o.put("bankSource", host.bank().sourceNote());
            o.put("wrongBookCount", host.stats().wrongBookCount(host.bank().list()));
            JSONArray wb = new JSONArray();
            for (Stats.WrongEntry e : host.stats().wrongBookTop(host.bank().list(), 10)) {
                JSONObject w = new JSONObject();
                w.put("text", e.text);
                w.put("subject", e.subject);
                w.put("wrong", e.wrong);
                w.put("right", e.right);
                wb.put(w);
            }
            o.put("wrongBook", wb);
            o.put("lanUrl", host.lanUrl());
            writeText(out, 200, "application/json; charset=utf-8", o.toString());
            return;
        }

        if (req.path.equals("/api/rules") && "POST".equals(req.method)) {
            JSONObject body = new JSONObject(new String(req.body, "UTF-8"));
            String token = r.adminToken;
            r.fromJson(body);
            r.adminToken = token;           // 访问码不允许被手机端改掉
            r.normalize();
            r.save(AppCtx.get());
            host.onRulesSaved();
            writeText(out, 200, "application/json; charset=utf-8", "{\"ok\":true,\"msg\":\"规则已保存\"}");
            return;
        }

        if (req.path.equals("/api/addtime") && "POST".equals(req.method)) {
            int m = 15;
            try {
                m = Integer.parseInt(req.str("minutes"));
            } catch (Exception ignored) {
            }
            t.addParentMinutes(Rules.clamp(m, 1, 300));
            host.onRulesSaved();
            writeText(out, 200, "application/json; charset=utf-8",
                    "{\"ok\":true,\"msg\":\"已加 " + m + " 分钟\"}");
            return;
        }

        if (req.path.equals("/api/reset") && "POST".equals(req.method)) {
            t.resetToday();
            host.onRulesSaved();
            writeText(out, 200, "application/json; charset=utf-8", "{\"ok\":true,\"msg\":\"今日数据已清零\"}");
            return;
        }

        if (req.path.equals("/api/bank") && "POST".equals(req.method)) {
            String msg = host.updateBankNow();
            writeText(out, 200, "application/json; charset=utf-8",
                    new JSONObject().put("ok", true).put("msg", msg).toString());
            return;
        }

        if (req.path.equals("/api/localbank") && "POST".equals(req.method)) {
            QuestionBank.LocalResult res = host.applyLocalBank(new String(req.body, "UTF-8"));
            writeText(out, 200, "application/json; charset=utf-8",
                    new JSONObject().put("ok", res.ok).put("msg", res.msg)
                            .put("count", res.count).toString());
            return;
        }

        if (req.path.equals("/api/localbank/clear") && "POST".equals(req.method)) {
            QuestionBank.LocalResult res = host.clearLocalBank();
            writeText(out, 200, "application/json; charset=utf-8",
                    new JSONObject().put("ok", res.ok).put("msg", res.msg)
                            .put("count", res.count).toString());
            return;
        }

        if (req.path.equals("/api/qr")) {
            Bitmap bmp = QrGen.make(host.lanUrl(), 480);
            ByteArrayOutputStream bos = new ByteArrayOutputStream();
            bmp.compress(Bitmap.CompressFormat.PNG, 100, bos);
            byte[] png = bos.toByteArray();
            writeBinary(out, "image/png", png);
            return;
        }

        writeText(out, 200, "application/json; charset=utf-8", "{\"ok\":false,\"msg\":\"未知接口\"}");
    }

    private android.content.Context tvCtx() {
        return AppCtx.get();
    }

    // ---------------- HTTP 底层 ----------------

    private Req readRequest(InputStream in) throws Exception {
        ByteArrayOutputStream head = new ByteArrayOutputStream();
        int state = 0;
        while (state < 4) {
            int b = in.read();
            if (b < 0) return null;
            head.write(b);
            if ((state == 0 || state == 2) && b == '\r') state++;
            else if ((state == 1 || state == 3) && b == '\n') state++;
            else state = 0;
            if (head.size() > 16384) return null;
        }
        String header = new String(head.toByteArray(), "UTF-8");
        String[] lines = header.split("\r\n");
        Req req = new Req();
        String first = lines[0];
        String[] fp = first.split(" ");
        if (fp.length >= 2) {
            req.method = fp[0];
            String target = fp[1];
            int q = target.indexOf('?');
            if (q >= 0) {
                req.path = target.substring(0, q);
                parseQuery(target.substring(q + 1), req.query);
            } else {
                req.path = target;
            }
        }
        int contentLength = 0;
        for (String line : lines) {
            String lower = line.toLowerCase(Locale.US);
            if (lower.startsWith("content-length:")) {
                try {
                    contentLength = Integer.parseInt(line.substring(15).trim());
                } catch (Exception ignored) {
                }
            }
        }
        if (contentLength > 0 && contentLength < 5 * 1024 * 1024) {
            byte[] body = new byte[contentLength];
            int read = 0;
            while (read < contentLength) {
                int n = in.read(body, read, contentLength - read);
                if (n <= 0) break;
                read += n;
            }
            req.body = body;
        }
        return req;
    }

    private static void parseQuery(String q, Map<String, String> into) {
        for (String pair : q.split("&")) {
            int i = pair.indexOf('=');
            if (i < 0) continue;
            try {
                into.put(URLDecoder.decode(pair.substring(0, i), "UTF-8"),
                        URLDecoder.decode(pair.substring(i + 1), "UTF-8"));
            } catch (Exception ignored) {
            }
        }
    }

    private static void writeText(OutputStream out, int code, String type, String body) throws Exception {
        byte[] data = body.getBytes("UTF-8");
        String h = "HTTP/1.1 " + code + " OK\r\n"
                + "Content-Type: " + type + "\r\n"
                + "Content-Length: " + data.length + "\r\n"
                + "Cache-Control: no-store, no-cache\r\n"
                + "Connection: close\r\n\r\n";
        out.write(h.getBytes("UTF-8"));
        out.write(data);
        out.flush();
    }

    private static void writeBinary(OutputStream out, String type, byte[] data) throws Exception {
        String h = "HTTP/1.1 200 OK\r\n"
                + "Content-Type: " + type + "\r\n"
                + "Content-Length: " + data.length + "\r\n"
                + "Cache-Control: no-store\r\n"
                + "Connection: close\r\n\r\n";
        out.write(h.getBytes("UTF-8"));
        out.write(data);
        out.flush();
    }

    // ---------------- 管理页 ----------------

    private String pageHtml() {
        String token = host.rules().adminToken;
        StringBuilder s = new StringBuilder();
        s.append("<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">")
                .append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1,maximum-scale=1\">")
                .append("<title>学习好电视 · 家长管理</title><style>")
                .append("*{box-sizing:border-box;-webkit-tap-highlight-color:transparent}")
                .append("body{margin:0;background:#0d1520;color:#e8f1f8;font-family:-apple-system,'PingFang SC','Microsoft YaHei',sans-serif;padding:14px 14px 60px}")
                .append("h1{font-size:19px;margin:6px 0 4px}")
                .append(".sub{color:#93a6ba;font-size:13px;margin-bottom:14px}")
                .append(".card{background:#172433;border-radius:14px;padding:14px;margin-bottom:12px;border:1px solid #22374f}")
                .append(".card h2{font-size:15px;margin:0 0 10px;color:#7fb3ff;font-weight:600}")
                .append(".chips{display:flex;gap:8px;flex-wrap:wrap}")
                .append(".chip{flex:1 1 45%;background:#1e2c3e;border-radius:10px;padding:10px}")
                .append(".chip .v{font-size:22px;font-weight:700}")
                .append(".chip .k{font-size:12px;color:#93a6ba;margin-top:2px}")
                .append(".row{display:flex;align-items:center;justify-content:space-between;padding:9px 0;border-bottom:1px solid #1e2c3e;font-size:14px}")
                .append(".row:last-child{border-bottom:none}")
                .append("input,select{background:#0f1b28;color:#e8f1f8;border:1px solid #2a4160;border-radius:9px;padding:8px 10px;font-size:14px;width:110px;text-align:right}")
                .append("input[type=text]{width:200px;text-align:left}")
                .append("input[type=checkbox]{width:auto;text-align:left}")
                .append("button{border:none;border-radius:11px;padding:12px 14px;font-size:15px;font-weight:600;color:#fff;background:#3d8bfd;margin:5px 4px 0 0}")
                .append("button.g{background:#2bb673}button.r{background:#e05a5a}button.d{background:#2a4160}")
                .append(".btns{display:flex;flex-wrap:wrap}")
                .append(".wb{font-size:13px;padding:8px 0;border-bottom:1px dashed #24384f;color:#cfe0ee}")
                .append(".tag{display:inline-block;background:#2a4160;border-radius:6px;padding:1px 6px;font-size:11px;margin-right:6px}")
                .append("#msg{position:fixed;left:0;right:0;bottom:0;padding:12px;background:#1e2c3e;color:#8fe3b0;font-size:14px;text-align:center;display:none}")
                .append("h1 span{font-size:12px;color:#93a6ba;font-weight:400;margin-left:6px}")
                .append("</style></head><body>")
                .append("<h1>学习好电视 · 家长管理</h1>")
                .append("<div class=\"sub\">手机和电视连同一个 WiFi 才能打开本页</div>");

        s.append("<div class=\"card\"><h2>今日状态</h2><div class=\"chips\">")
                .append("<div class=\"chip\"><div class=\"v\" id=\"v-remain\">-</div><div class=\"k\">剩余可用(分钟)</div></div>")
                .append("<div class=\"chip\"><div class=\"v\" id=\"v-groups\">-</div><div class=\"k\">今日答题组数</div></div>")
                .append("<div class=\"chip\"><div class=\"v\" id=\"v-used\">-</div><div class=\"k\">今日已用(分钟)</div></div>")
                .append("<div class=\"chip\"><div class=\"v\" id=\"v-acc\">-</div><div class=\"k\">今日正确率</div></div>")
                .append("</div><div class=\"sub\" id=\"v-free\" style=\"margin-top:10px;margin-bottom:0\"></div></div>");

        s.append("<div class=\"card\"><h2>快捷操作</h2><div class=\"btns\">")
                .append("<button class=\"g\" onclick=\"addTime(15)\">加 15 分钟</button>")
                .append("<button class=\"g\" onclick=\"addTime(30)\">加 30 分钟</button>")
                .append("<button class=\"r\" onclick=\"resetToday()\">清零今日</button>")
                .append("<button class=\"d\" onclick=\"updateBank()\">更新题库</button>")
                .append("</div></div>");

        s.append("<div class=\"card\"><h2>学习规则</h2>")
                .append(row("孩子年级", "<select id=\"grade\"><option value=\"7\">初一</option><option value=\"8\">初二</option><option value=\"9\">初三</option><option value=\"0\">通用</option></select>"))
                .append(row("每天基础时长(分钟)", "<input id=\"baseDailyMinutes\" type=\"number\">"))
                .append(row("每组题量", "<input id=\"questionsPerGroup\" type=\"number\">"))
                .append(row("通过正确率(%)", "<input id=\"passPercent\" type=\"number\">"))
                .append(row("每组奖励分钟", "<input id=\"unlockMinutesPerGroup\" type=\"number\">"))
                .append(row("每天最多组数", "<input id=\"maxGroupsPerDay\" type=\"number\">"))
                .append(row("每天奖励上限(分钟)", "<input id=\"maxBonusMinutesPerDay\" type=\"number\">"))
                .append(row("自由时段", "<input id=\"freeWindows\" type=\"text\" placeholder=\"12:00-13:00,18:30-20:00\">"))
                .append(row("总开关(管控生效)", "<select id=\"enabled\"><option value=\"true\">开启</option><option value=\"false\">关闭</option></select>"))
                .append(row("严格模式(设为桌面)", "<select id=\"strictMode\"><option value=\"true\">开启</option><option value=\"false\">关闭</option></select>"))
                .append(row("答错显示解析", "<select id=\"showExplain\"><option value=\"true\">显示</option><option value=\"false\">隐藏</option></select>"))
                .append(row("孩子称呼", "<input id=\"childName\" type=\"text\">"))
                .append(row("题库地址", "<input id=\"bankUrl\" type=\"text\">"))
                .append(row("启用本地题库", "<select id=\"useLocalBank\"><option value=\"true\">开启</option><option value=\"false\">关闭</option></select>"))
                .append("<div class=\"sub\" style=\"margin:12px 0 4px 0\">偏科加权（可多选，不选=各科均衡随机）</div>")
                .append("<div id=\"weak\" style=\"display:flex;flex-wrap:wrap;gap:8px 14px;margin-bottom:8px;font-size:14px\">"
                        + "<label><input type=\"checkbox\" value=\"math\">数学</label>"
                        + "<label><input type=\"checkbox\" value=\"chinese\">语文</label>"
                        + "<label><input type=\"checkbox\" value=\"english\">英语</label>"
                        + "<label><input type=\"checkbox\" value=\"physics\">物理</label>"
                        + "<label><input type=\"checkbox\" value=\"chemistry\">化学</label>"
                        + "<label><input type=\"checkbox\" value=\"biology\">生物</label>"
                        + "<label><input type=\"checkbox\" value=\"history\">历史</label>"
                        + "<label><input type=\"checkbox\" value=\"geography\">地理</label>"
                        + "<label><input type=\"checkbox\" value=\"politics\">道法</label>"
                        + "<label><input type=\"checkbox\" value=\"coding\">编程</label>"
                        + "</div>")
                .append("<div class=\"sub\" style=\"margin:0 0 10px 0\">勾选后这些科目抽到的概率会明显提高，适合补偏科。</div>")
                .append(row("家长密码", "<input id=\"parentPassword\" type=\"text\">"))
                .append("<button onclick=\"saveRules()\" style=\"margin-top:12px;width:100%\">保存规则</button>")
                .append("</div>");

        s.append("<div class=\"card\"><h2>本地题库（电脑导出 → 手机上传）</h2>")
                .append("<div class=\"sub\" style=\"margin:0 0 10px 0\">在电脑上用「题库编辑器」点「导出给电视（JSON）」，"
                        + "把文件发到手机上，在这里选中并上传。本地题会优先出现，官方题库继续保留，在线更新也不会覆盖本地题。</div>")
                .append("<div class=\"row\"><span>选择 JSON 文件</span><input id=\"lf\" type=\"file\" accept=\".json,application/json\" style=\"width:190px;text-align:left;padding:6px\"></div>")
                .append("<div class=\"btns\"><button class=\"g\" onclick=\"uploadLocal()\">上传本地题库</button>")
                .append("<button class=\"r\" onclick=\"clearLocal()\">清空本地题库</button></div>")
                .append("<div class=\"sub\" id=\"v-local\" style=\"margin:10px 0 0 0\"></div>")
                .append("</div>");

        s.append("<div class=\"card\"><h2>题库与错题本</h2>")
                .append("<div class=\"sub\" id=\"v-bank\" style=\"margin:0 0 10px 0\"></div>")
                .append("<div id=\"v-wrongbook\"></div></div>");

        s.append("<div class=\"sub\" style=\"text-align:center\">")
                .append("访问码 <b>").append(token).append("</b><br>地址 <b>").append(host.lanUrl()).append("</b>")
                .append("</div>");
        s.append("<div id=\"msg\"></div>");

        s.append("<script>var T=").append(jsonStr(token)).append(";")
                .append("function api(p,opt){var u=p+(p.indexOf('?')>=0?'&':'?')+'token='+T;return fetch(u,opt).then(function(r){return r.json()})}")
                .append("function tip(m){var e=document.getElementById('msg');e.textContent=m;e.style.display='block';setTimeout(function(){e.style.display='none'},2500)}")
                .append("function addTime(m){api('/api/addtime?minutes='+m,{method:'POST'}).then(function(j){tip(j.msg);load()})}")
                .append("function resetToday(){if(!confirm('确定把今天的用时和答题次数清零吗？'))return;api('/api/reset',{method:'POST'}).then(function(j){tip(j.msg);load()})}")
                .append("function updateBank(){tip('正在更新题库…');api('/api/bank',{method:'POST'}).then(function(j){tip(j.msg);load()})}")
                .append("function uploadLocal(){var f=document.getElementById('lf').files[0];if(!f){tip('请先选择电脑导出的 JSON 文件');return}"
                + "var rd=new FileReader();rd.onload=function(){tip('正在上传…');api('/api/localbank',{method:'POST',headers:{'Content-Type':'application/json'},body:rd.result})"
                + ".then(function(j){tip(j.msg);load()})};rd.readAsText(f,'utf-8')}")
                .append("function clearLocal(){if(!confirm('确定清空电视上的本地自建题库吗？官方题库不受影响。'))return;"
                + "api('/api/localbank/clear',{method:'POST'}).then(function(j){tip(j.msg);load()})}")
                .append("function setv(id,v){var e=document.getElementById(id);if(e)e.value=v}")
                .append("function load(){api('/api/status').then(function(j){if(!j.ok){tip(j.msg);return}"
                + "document.getElementById('v-remain').textContent=j.time.freeWindow?'不限':j.time.remainingMinutes;"
                + "document.getElementById('v-groups').textContent=j.time.groupsDone;"
                + "document.getElementById('v-used').textContent=j.time.usedMinutes;"
                + "document.getElementById('v-acc').textContent=j.time.answered?Math.round(j.time.correct*100/j.time.answered)+'%':'--';"
                + "document.getElementById('v-free').textContent=(j.time.locked?'⛔ 当前已锁定，需要答题解锁':(j.time.freeWindow?'✅ 当前是自由时段':'▶ 当前可以正常观看'))+' · 累计奖励 '+j.time.earnedMinutes+' 分钟';"
                + "setv('grade',j.rules.grade);setv('baseDailyMinutes',j.rules.baseDailyMinutes);setv('questionsPerGroup',j.rules.questionsPerGroup);"
                + "setv('passPercent',j.rules.passPercent);setv('unlockMinutesPerGroup',j.rules.unlockMinutesPerGroup);setv('maxGroupsPerDay',j.rules.maxGroupsPerDay);"
                + "setv('maxBonusMinutesPerDay',j.rules.maxBonusMinutesPerDay);setv('freeWindows',j.rules.freeWindows.join(','));"
                + "setv('enabled',String(j.rules.enabled));setv('strictMode',String(j.rules.strictMode));setv('showExplain',String(j.rules.showExplain));"
                + "setv('childName',j.rules.childName);setv('bankUrl',j.rules.bankUrl);setv('useLocalBank',String(j.rules.useLocalBank));setv('parentPassword',j.rules.parentPassword);"
                + "var ws=j.rules.weakSubjects||[];var cbs=document.querySelectorAll('#weak input');for(var wi=0;wi<cbs.length;wi++){cbs[wi].checked=ws.indexOf(cbs[wi].value)>=0;}"
                + "document.getElementById('v-bank').textContent='当前题库 '+j.bankCount+' 题（'+j.bankSource+'）· 错题待巩固 '+j.wrongBookCount+' 道';"
                + "document.getElementById('v-local').textContent='电视上已有本地自建题 '+j.localBankCount+' 道'+(j.localBankCount>0?'（优先出现在答题里）':'，尚未上传');"
                + "var wb=document.getElementById('v-wrongbook');wb.innerHTML='';"
                + "if(j.wrongBook.length===0){wb.innerHTML='<div class=\"sub\">暂时没有错题，继续保持！</div>'}"
                + "else{for(var i=0;i<j.wrongBook.length;i++){var w=j.wrongBook[i];var d=document.createElement('div');d.className='wb';"
                + "d.innerHTML='<span class=\"tag\">'+w.subject+'</span>错 '+w.wrong+' 次 / 对 '+w.right+' 次<br>'+w.text;wb.appendChild(d)}}"
                + "})}")
                .append("function saveRules(){var v=function(id){var e=document.getElementById(id);return e.type==='number'?parseInt(e.value||'0'):e.value};"
                + "var body={grade:v('grade'),baseDailyMinutes:v('baseDailyMinutes'),questionsPerGroup:v('questionsPerGroup'),passPercent:v('passPercent'),"
                + "unlockMinutesPerGroup:v('unlockMinutesPerGroup'),maxGroupsPerDay:v('maxGroupsPerDay'),maxBonusMinutesPerDay:v('maxBonusMinutesPerDay'),"
                + "freeWindows:v('freeWindows').split(',').map(function(x){return x.trim()}).filter(function(x){return x.length>0}),"
                + "enabled:v('enabled')==='true',strictMode:v('strictMode')==='true',showExplain:v('showExplain')==='true',"
                + "childName:v('childName'),bankUrl:v('bankUrl'),useLocalBank:v('useLocalBank')==='true',parentPassword:v('parentPassword'),"
                + "weakSubjects:Array.prototype.slice.call(document.querySelectorAll('#weak input:checked')).map(function(x){return x.value})};"
                + "api('/api/rules',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)}).then(function(j){tip(j.msg);load()})}")
                .append("load();setInterval(load,5000);")
                .append("</script></body></html>");
        return s.toString();
    }

    private static String row(String label, String input) {
        return "<div class=\"row\"><span>" + label + "</span>" + input + "</div>";
    }

    private static String jsonStr(String s) {
        if (s == null) return "\"\"";
        return "\"" + s.replace("\\", "\\\\").replace("\"", "\\\"") + "\"";
    }
}
