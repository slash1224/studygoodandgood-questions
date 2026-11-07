package com.studygoodandgood.tv;

import android.content.Context;

import org.json.JSONArray;
import org.json.JSONObject;

import java.security.MessageDigest;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Date;
import java.util.Iterator;
import java.util.Locale;

/**
 * 错题本与抽题权重。
 * 与电脑版 QuestionStats 使用完全相同的公式：
 *   w = 1 + 4*(Wrong/Total)
 *   连续答对 >=3 次 -> w *= 0.5
 *   最近 7 天内做错且尚未连续答对 2 次 -> w *= 1.5
 *   最终限制在 0.5 ~ 6.0
 * 题目标识 = 题干 SHA1 前 12 位（换题库 id 也不会丢统计）。
 */
public class Stats {

    private static final String PREF = "sgag_stats";

    public static class Item {
        public int wrong = 0;
        public int right = 0;
        public int streak = 0;
        public long lastWrong = 0L;
        public long lastSeen = 0L;

        public int total() {
            return wrong + right;
        }
    }

    private final Context ctx;
    private final java.util.HashMap<String, Item> map = new java.util.HashMap<String, Item>();

    public Stats(Context ctx) {
        this.ctx = ctx;
        load();
    }

    public static String keyOf(Question q) {
        try {
            MessageDigest md = MessageDigest.getInstance("SHA-1");
            byte[] d = md.digest(q.text == null ? "".getBytes() : q.text.trim().getBytes("UTF-8"));
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 6; i++) sb.append(String.format(Locale.US, "%02x", d[i] & 0xff));
            return sb.toString();
        } catch (Exception e) {
            return Integer.toHexString(q.text == null ? 0 : q.text.hashCode());
        }
    }

    public Item get(Question q) {
        return map.get(keyOf(q));
    }

    public void record(Question q, boolean right) {
        String k = keyOf(q);
        Item it = map.get(k);
        if (it == null) {
            it = new Item();
            map.put(k, it);
        }
        long now = System.currentTimeMillis();
        it.lastSeen = now;
        if (right) {
            it.right++;
            it.streak++;
        } else {
            it.wrong++;
            it.streak = 0;
            it.lastWrong = now;
        }
        save();
    }

    /** 抽题权重：错过的题更容易出现，已经掌握的题少出现 */
    public double weight(Question q) {
        Item st = get(q);
        if (st == null || st.total() == 0) return 1.0;
        double total = st.total();
        double w = 1.0 + 4.0 * (st.wrong / total);
        if (st.streak >= 3) w *= 0.5;
        if (st.wrong > 0 && st.streak < 2) {
            double days = (System.currentTimeMillis() - st.lastWrong) / 86400000.0;
            if (days <= 7) w *= 1.5;
        }
        if (w < 0.5) w = 0.5;
        if (w > 6.0) w = 6.0;
        return w;
    }

    public boolean isWrongBook(Question q) {
        Item st = get(q);
        return st != null && st.wrong > 0 && st.streak < 2;
    }

    public int wrongBookCount(java.util.List<Question> bank) {
        int n = 0;
        for (Question q : bank) if (isWrongBook(q)) n++;
        return n;
    }

    public static class WrongEntry {
        public String text = "";
        public String subject = "";
        public int wrong = 0;
        public int right = 0;
        public long lastWrong = 0L;
    }

    /** 错题本 TOP N（用于家长手机查看） */
    public ArrayList<WrongEntry> wrongBookTop(java.util.List<Question> bank, int n) {
        ArrayList<WrongEntry> list = new ArrayList<WrongEntry>();
        for (Question q : bank) {
            Item st = get(q);
            if (st == null || st.wrong <= 0 || st.streak >= 2) continue;
            WrongEntry e = new WrongEntry();
            e.text = q.text;
            e.subject = q.subjectCn();
            e.wrong = st.wrong;
            e.right = st.right;
            e.lastWrong = st.lastWrong;
            list.add(e);
        }
        java.util.Collections.sort(list, new java.util.Comparator<WrongEntry>() {
            public int compare(WrongEntry a, WrongEntry b) {
                if (a.wrong != b.wrong) return b.wrong - a.wrong;
                return a.text.compareTo(b.text);
            }
        });
        if (list.size() > n) {
            ArrayList<WrongEntry> cut = new ArrayList<WrongEntry>();
            for (int i = 0; i < n; i++) cut.add(list.get(i));
            return cut;
        }
        return list;
    }

    // ---------------- 持久化 ----------------

    private void load() {
        String raw = ctx.getSharedPreferences(PREF, Context.MODE_PRIVATE).getString("json", null);
        if (raw == null || raw.length() == 0) return;
        try {
            JSONObject root = new JSONObject(raw);
            Iterator<String> keys = root.keys();
            while (keys.hasNext()) {
                String k = keys.next();
                JSONObject o = root.optJSONObject(k);
                if (o == null) continue;
                Item it = new Item();
                it.wrong = o.optInt("w", 0);
                it.right = o.optInt("r", 0);
                it.streak = o.optInt("s", 0);
                it.lastWrong = o.optLong("lw", 0L);
                it.lastSeen = o.optLong("ls", 0L);
                map.put(k, it);
            }
        } catch (Exception ignored) {
        }
    }

    private void save() {
        try {
            if (map.size() > 3000) prune();
            JSONObject root = new JSONObject();
            for (java.util.Map.Entry<String, Item> e : map.entrySet()) {
                Item it = e.getValue();
                JSONObject o = new JSONObject();
                o.put("w", it.wrong);
                o.put("r", it.right);
                o.put("s", it.streak);
                o.put("lw", it.lastWrong);
                o.put("ls", it.lastSeen);
                root.put(e.getKey(), o);
            }
            ctx.getSharedPreferences(PREF, Context.MODE_PRIVATE)
                    .edit().putString("json", root.toString()).apply();
        } catch (Exception ignored) {
        }
    }

    /** 超过 3000 条时清理 180 天没见过的记录 */
    private void prune() {
        long cut = System.currentTimeMillis() - 180L * 86400000L;
        Iterator<java.util.Map.Entry<String, Item>> it = map.entrySet().iterator();
        while (it.hasNext()) {
            if (it.next().getValue().lastSeen < cut) it.remove();
        }
    }

    public String todayString() {
        return new SimpleDateFormat("yyyy-MM-dd", Locale.US).format(new Date());
    }
}
