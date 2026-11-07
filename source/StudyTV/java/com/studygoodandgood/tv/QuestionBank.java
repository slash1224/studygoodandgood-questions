package com.studygoodandgood.tv;

import android.content.Context;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Random;
import java.util.Set;

/**
 * 题库：内置 assets/questions.json（与电脑版同一份数据，约 3 万题）。
 * <p>
 * 官方题库：在线更新写到 files/bank.json（没有就用内置 assets）。
 * 本地自建题库：家长在电脑编辑器里导出 JSON，手机在管理页上传，写到 files/local_bank.json，
 * 抽题时与电脑版语义一致——本地题优先，官方题补充；在线更新只覆盖 bank.json，不会冲掉本地题。
 */
public class QuestionBank {

    private static final String ASSET = "questions.json";
    private static final String OFFICIAL_FILE = "bank.json";
    private static final String LOCAL_FILE = "local_bank.json";

    private final Context ctx;
    private final Stats stats;
    /** 合并后的题库，整份替换（而不是 clear+addAll），避免更新时界面线程读到半个题库 */
    private volatile List<Question> all = new ArrayList<Question>();
    /** 官方题库（内置或在线更新后的） */
    private volatile List<Question> official = new ArrayList<Question>();
    /** 本地自建题库 */
    private volatile List<Question> local = new ArrayList<Question>();
    private volatile boolean useLocal = true;
    /** 家长指定的偏科科目，抽题时权重更高 */
    private volatile Set<String> weakSubjects = new HashSet<String>();
    private volatile String officialNote = "内置题库";
    private long lastUpdate = 0L;

    public QuestionBank(Context ctx, Stats stats) {
        this.ctx = ctx;
        this.stats = stats;
    }

    public List<Question> list() {
        return all;
    }

    public int count() {
        return all.size();
    }

    public int localCount() {
        return local.size();
    }

    public Stats stats() {
        return stats;
    }

    /** 家长关掉「启用本地题库」时只抽官方题；切换后立即重新合并 */
    public void setUseLocal(boolean v) {
        if (useLocal == v) return;
        useLocal = v;
        rebuild();
    }

    /** 设置偏科加权科目；空列表表示各科均衡随机 */
    public void setWeakSubjects(List<String> list) {
        Set<String> s = new HashSet<String>();
        if (list != null) {
            for (String x : list) {
                if (x != null && x.trim().length() > 0) s.add(x.trim());
            }
        }
        weakSubjects = s;
    }

    public String sourceNote() {
        String base = officialNote;
        if (useLocal && local.size() > 0) return base + " + 本地自建 " + local.size() + " 题";
        return base;
    }

    public long lastUpdate() {
        return lastUpdate;
    }

    /** 优先读本地已更新的官方题库，没有就拷贝内置题库；然后叠加本地自建题库 */
    public void load() {
        official = readOfficial();
        local = readLocal();
        rebuild();
    }

    private List<Question> readOfficial() {
        File f = new File(ctx.getFilesDir(), OFFICIAL_FILE);
        if (f.exists() && f.length() > 100) {
            try {
                List<Question> list = parseList(readFile(f));
                if (list.size() >= 10) {
                    officialNote = "题库（本地更新版）";
                    lastUpdate = f.lastModified();
                    return list;
                }
            } catch (Exception ignored) {
            }
        }
        try {
            List<Question> list = parseList(readAsset());
            officialNote = "内置题库";
            lastUpdate = 0L;
            return list;
        } catch (Exception e) {
            officialNote = "题库读取失败：" + (e.getMessage() == null ? e.toString() : e.getMessage());
            return new ArrayList<Question>();
        }
    }

    private List<Question> readLocal() {
        File f = new File(ctx.getFilesDir(), LOCAL_FILE);
        if (!f.exists() || f.length() < 2) return new ArrayList<Question>();
        try {
            return parseList(readFile(f));
        } catch (Exception e) {
            return new ArrayList<Question>();
        }
    }

    /** 本地题优先，官方题补充；同一题干只保留先出现的那道 */
    private void rebuild() {
        List<Question> merged = new ArrayList<Question>();
        Set<String> seen = new HashSet<String>();
        if (useLocal) {
            for (Question q : local) {
                if (seen.add(q.text.trim())) merged.add(q);
            }
        }
        for (Question q : official) {
            if (seen.add(q.text.trim())) merged.add(q);
        }
        if (merged.isEmpty()) {
            merged.addAll(official);
            if (useLocal) merged.addAll(local);
        }
        all = merged;
    }

    private String readAsset() throws Exception {
        InputStream in = ctx.getAssets().open(ASSET);
        ByteArrayOutputStream bos = new ByteArrayOutputStream();
        byte[] buf = new byte[4096];
        int n;
        while ((n = in.read(buf)) > 0) bos.write(buf, 0, n);
        in.close();
        return new String(bos.toByteArray(), "UTF-8");
    }

    private static String readFile(File f) throws Exception {
        BufferedReader r = new BufferedReader(new InputStreamReader(new java.io.FileInputStream(f), "UTF-8"));
        StringBuilder sb = new StringBuilder();
        String line;
        while ((line = r.readLine()) != null) sb.append(line);
        r.close();
        return sb.toString();
    }

    private static List<Question> parseList(String json) throws Exception {
        JSONArray arr = new JSONArray(json);
        List<Question> list = new ArrayList<Question>();
        Set<String> seen = new HashSet<String>();
        for (int i = 0; i < arr.length(); i++) {
            JSONObject o = arr.optJSONObject(i);
            if (o == null) continue;
            Question q = new Question();
            q.id = o.optInt("id", i + 1);
            q.subject = o.optString("subject", "math");
            q.grade = o.optInt("grade", 0);
            q.difficulty = o.optInt("difficulty", 1);
            q.text = o.optString("text", "").trim();
            JSONArray op = o.optJSONArray("options");
            if (q.text.length() == 0 || op == null || op.length() < 2) continue;
            for (int k = 0; k < op.length(); k++) q.options.add(op.optString(k, ""));
            q.answer = o.optInt("answer", 0);
            if (q.answer < 0 || q.answer >= q.options.size()) continue;
            q.explain = o.optString("explain", "");
            if (seen.contains(q.text)) continue;
            seen.add(q.text);
            list.add(q);
        }
        return list;
    }

    // ---------------- 抽题 ----------------

    /** 按年级抽一组题：先抽本年级，不够则补相邻年级、通用、全库 */
    public List<Question> pickGroup(int grade, int count) {
        List<Question> pool = poolFor(grade);
        if (pool.size() < count) pool = all;
        return balancedPick(pool, Math.min(count, pool.size()));
    }

    private List<Question> poolFor(int grade) {
        List<Question> g0 = new ArrayList<Question>();
        List<Question> g1 = new ArrayList<Question>();
        List<Question> g2 = new ArrayList<Question>();
        for (Question q : all) {
            if (q.grade == grade) g0.add(q);
            else if (Math.abs(q.grade - grade) == 1 && q.grade > 0) g1.add(q);
            else if (q.grade == 0) g2.add(q);
        }
        List<Question> pool = new ArrayList<Question>();
        pool.addAll(g0);
        if (pool.size() < 20) pool.addAll(g1);
        // grade=0 时 g0 与"通用题"是同一批，不能再加一遍，否则这些题会被重复计数
        if (grade != 0 && pool.size() < 20) pool.addAll(g2);
        if (pool.size() < 10) {
            pool.clear();
            pool.addAll(all);
        }
        return pool;
    }

    /** 按科目轮转抽题，避免某一科题量过大时整组都是同一科；科内仍按错题权重抽取 */
    private List<Question> balancedPick(List<Question> pool, int count) {
        Map<String, List<Question>> groups = new HashMap<String, List<Question>>();
        for (Question q : pool) {
            List<Question> g = groups.get(q.subject);
            if (g == null) {
                g = new ArrayList<Question>();
                groups.put(q.subject, g);
            }
            g.add(q);
        }
        List<String> keys = new ArrayList<String>(groups.keySet());
        List<Question> out = new ArrayList<Question>();
        Random rnd = new Random();
        while (out.size() < count && !keys.isEmpty()) {
            for (int i = keys.size() - 1; i > 0; i--) {
                int j = rnd.nextInt(i + 1);
                String t = keys.get(i);
                keys.set(i, keys.get(j));
                keys.set(j, t);
            }
            boolean any = false;
            for (String k : keys) {
                List<Question> src = groups.get(k);
                if (src.isEmpty()) continue;
                any = true;
                int reps = weakSubjects.contains(k) ? 3 : 1;
                for (int r = 0; r < reps && src.size() > 0 && out.size() < count; r++) {
                    out.add(weightedRemoveOne(src, rnd));
                }
                if (out.size() >= count) break;
            }
            if (!any) break;
        }
        return out;
    }

    private Question weightedRemoveOne(List<Question> src, Random rnd) {
        double sum = 0;
        for (Question q : src) sum += stats.weight(q);
        double x = rnd.nextDouble() * sum;
        int idx = src.size() - 1;
        for (int i = 0; i < src.size(); i++) {
            x -= stats.weight(src.get(i));
            if (x <= 0) {
                idx = i;
                break;
            }
        }
        return src.remove(idx);
    }

    // ---------------- 在线更新 ----------------

    public interface Callback {
        void done(boolean ok, String msg, int count);
    }

    /** 从配置的地址下载题库；成功才覆盖官方题库文件（不动本地自建题库） */
    public void updateFromUrl(final String url, final Callback cb) {
        new Thread(new Runnable() {
            public void run() {
                boolean ok = false;
                String msg;
                try {
                    if (url == null || url.trim().length() == 0) throw new Exception("题库地址为空");
                    HttpURLConnection conn = (HttpURLConnection) new URL(url.trim()).openConnection();
                    conn.setConnectTimeout(12000);
                    conn.setReadTimeout(20000);
                    conn.setRequestProperty("User-Agent", "StudyGoodAndGood-TV/1.0");
                    conn.setInstanceFollowRedirects(true);
                    int code = conn.getResponseCode();
                    if (code != 200) throw new Exception("服务器返回 " + code);
                    InputStream in = conn.getInputStream();
                    ByteArrayOutputStream bos = new ByteArrayOutputStream();
                    byte[] buf = new byte[8192];
                    int n;
                    while ((n = in.read(buf)) > 0) bos.write(buf, 0, n);
                    in.close();
                    String json = new String(bos.toByteArray(), "UTF-8");
                    // 先在内存里校验，再落盘
                    List<Question> tmp = parseList(json);
                    if (tmp.size() < 10) throw new Exception("下载内容不是有效题库");
                    File f = new File(ctx.getFilesDir(), OFFICIAL_FILE);
                    FileOutputStream out = new FileOutputStream(f);
                    out.write(json.getBytes("UTF-8"));
                    out.close();
                    load();
                    ok = true;
                    msg = "题库已更新，共 " + all.size() + " 题";
                } catch (Exception e) {
                    msg = "更新失败：" + (e.getMessage() == null ? e.toString() : e.getMessage());
                }
                if (cb != null) cb.done(ok, msg, all.size());
            }
        }, "bank-update").start();
    }

    // ---------------- 本地自建题库 ----------------

    public static class LocalResult {
        public final boolean ok;
        public final String msg;
        public final int count;

        public LocalResult(boolean ok, String msg, int count) {
            this.ok = ok;
            this.msg = msg;
            this.count = count;
        }
    }

    /** 手机管理页上传电脑编辑器导出的 JSON，整份替换本地自建题库 */
    public LocalResult applyLocalJson(String json) {
        try {
            List<Question> parsed = parseList(json);
            if (parsed.isEmpty()) {
                throw new Exception("没有可用题目（请检查题干、选项和答案下标，建议直接用电脑编辑器导出的文件）");
            }
            File f = new File(ctx.getFilesDir(), LOCAL_FILE);
            OutputStream out = new FileOutputStream(f);
            out.write(json.getBytes("UTF-8"));
            out.close();
            local = parsed;
            rebuild();
            return new LocalResult(true,
                    "已导入本地题库 " + parsed.size() + " 题，当前可用 " + all.size() + " 题", parsed.size());
        } catch (Exception e) {
            String m = e.getMessage() == null ? e.toString() : e.getMessage();
            return new LocalResult(false, "导入失败：" + m, local.size());
        }
    }

    /** 清空本地自建题库（官方题库不受影响） */
    public LocalResult clearLocal() {
        try {
            File f = new File(ctx.getFilesDir(), LOCAL_FILE);
            if (f.exists()) f.delete();
            local = new ArrayList<Question>();
            rebuild();
            return new LocalResult(true, "本地题库已清空，当前可用 " + all.size() + " 题", 0);
        } catch (Exception e) {
            return new LocalResult(false, "清空失败：" + e.getMessage(), local.size());
        }
    }

    public HashMap<String, Integer> subjectCounts() {
        HashMap<String, Integer> m = new HashMap<String, Integer>();
        for (Question q : all) {
            Integer c = m.get(q.subjectCn());
            m.put(q.subjectCn(), c == null ? 1 : c + 1);
        }
        return m;
    }
}
