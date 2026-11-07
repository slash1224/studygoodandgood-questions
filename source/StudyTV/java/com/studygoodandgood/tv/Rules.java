package com.studygoodandgood.tv;

import android.content.Context;
import android.content.SharedPreferences;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;
import java.util.Random;

/** 家长规则配置（与电脑版 RuleConfig 语义一致，额外增加了电视端的基础时长） */
public class Rules {

    public static final String PREF = "sgag_rules";

    /** 孩子年级 7-9，0=通用 */
    public int grade = 7;
    public int questionsPerGroup = 5;
    /** 每组通过所需正确率 0-100 */
    public int passPercent = 80;
    public int unlockMinutesPerGroup = 15;
    public int maxGroupsPerDay = 8;
    /** 每天答题累计奖励上限（分钟） */
    public int maxBonusMinutesPerDay = 120;
    /** 每天不看电视也能用的基础时长（分钟），耗尽后才需要答题 */
    public int baseDailyMinutes = 60;
    /** 自由时段 HH:mm-HH:mm */
    public List<String> freeWindows = new ArrayList<String>();
    /** 总开关 */
    public boolean enabled = true;
    public String parentPassword = "123456";
    /** 管理页访问码 */
    public String adminToken = "";
    public String bankUrl = "https://cdn.jsdelivr.net/gh/slash1224/studygoodandgood-questions@main/questions.json";
    public boolean autoUpdateBank = true;
    /** 是否启用本地自建题库（电脑导出、手机上传的 JSON；本地题优先） */
    public boolean useLocalBank = true;
    /** 家长指定的偏科科目（如 math、english），抽题时权重更高；空表示各科均衡随机 */
    public List<String> weakSubjects = new ArrayList<String>();
    /** 严格模式：本应用作为电视桌面，孩子按 HOME 就回到答题页 */
    public boolean strictMode = false;
    /** 答错后显示解析 */
    public boolean showExplain = true;
    /** 孩子称呼 */
    public String childName = "宝贝";
    /** 电视上是否显示倒计时浮窗 */
    public boolean overlayEnabled = true;

    public Rules() {
        freeWindows.add("12:00-13:00");
        freeWindows.add("18:30-20:00");
    }

    public static Rules load(Context ctx) {
        SharedPreferences sp = ctx.getSharedPreferences(PREF, Context.MODE_PRIVATE);
        String raw = sp.getString("json", null);
        Rules r = new Rules();
        if (raw == null || raw.length() == 0) {
            r.adminToken = newToken();
            r.save(ctx);
            return r;
        }
        try {
            r.fromJson(new JSONObject(raw));
        } catch (Exception e) {
            r.adminToken = newToken();
        }
        if (r.adminToken == null || r.adminToken.length() == 0) r.adminToken = newToken();
        return r;
    }

    public void save(Context ctx) {
        SharedPreferences sp = ctx.getSharedPreferences(PREF, Context.MODE_PRIVATE);
        sp.edit().putString("json", toJson().toString()).apply();
    }

    public JSONObject toJson() {
        JSONObject o = new JSONObject();
        try {
            o.put("grade", grade);
            o.put("questionsPerGroup", questionsPerGroup);
            o.put("passPercent", passPercent);
            o.put("unlockMinutesPerGroup", unlockMinutesPerGroup);
            o.put("maxGroupsPerDay", maxGroupsPerDay);
            o.put("maxBonusMinutesPerDay", maxBonusMinutesPerDay);
            o.put("baseDailyMinutes", baseDailyMinutes);
            JSONArray arr = new JSONArray();
            for (String s : freeWindows) arr.put(s);
            o.put("freeWindows", arr);
            o.put("enabled", enabled);
            o.put("parentPassword", parentPassword);
            o.put("adminToken", adminToken);
            o.put("bankUrl", bankUrl);
            o.put("autoUpdateBank", autoUpdateBank);
            o.put("useLocalBank", useLocalBank);
            JSONArray weak = new JSONArray();
            for (String s : weakSubjects) weak.put(s);
            o.put("weakSubjects", weak);
            o.put("strictMode", strictMode);
            o.put("showExplain", showExplain);
            o.put("childName", childName);
            o.put("overlayEnabled", overlayEnabled);
        } catch (Exception ignored) {
        }
        return o;
    }

    public void fromJson(JSONObject o) {
        grade = o.optInt("grade", grade);
        questionsPerGroup = o.optInt("questionsPerGroup", questionsPerGroup);
        passPercent = o.optInt("passPercent", passPercent);
        unlockMinutesPerGroup = o.optInt("unlockMinutesPerGroup", unlockMinutesPerGroup);
        maxGroupsPerDay = o.optInt("maxGroupsPerDay", maxGroupsPerDay);
        maxBonusMinutesPerDay = o.optInt("maxBonusMinutesPerDay", maxBonusMinutesPerDay);
        baseDailyMinutes = o.optInt("baseDailyMinutes", baseDailyMinutes);
        JSONArray arr = o.optJSONArray("freeWindows");
        if (arr != null) {
            List<String> list = new ArrayList<String>();
            for (int i = 0; i < arr.length(); i++) {
                String s = arr.optString(i, "");
                if (s.length() > 0) list.add(s);
            }
            freeWindows = list;
        }
        JSONArray weak = o.optJSONArray("weakSubjects");
        if (weak != null) {
            List<String> list = new ArrayList<String>();
            for (int i = 0; i < weak.length(); i++) {
                String s = weak.optString(i, "");
                if (s.length() > 0) list.add(s);
            }
            weakSubjects = list;
        }
        enabled = o.optBoolean("enabled", enabled);
        String pw = o.optString("parentPassword", parentPassword);
        if (pw != null && pw.length() > 0) parentPassword = pw;
        String tk = o.optString("adminToken", adminToken);
        if (tk != null && tk.length() > 0) adminToken = tk;
        String bu = o.optString("bankUrl", bankUrl);
        if (bu != null) bankUrl = bu;
        autoUpdateBank = o.optBoolean("autoUpdateBank", autoUpdateBank);
        useLocalBank = o.optBoolean("useLocalBank", useLocalBank);
        strictMode = o.optBoolean("strictMode", strictMode);
        showExplain = o.optBoolean("showExplain", showExplain);
        String cn = o.optString("childName", childName);
        if (cn != null && cn.length() > 0) childName = cn;
        overlayEnabled = o.optBoolean("overlayEnabled", overlayEnabled);
        normalize();
    }

    public void normalize() {
        grade = clamp(grade, 0, 12);
        questionsPerGroup = clamp(questionsPerGroup, 3, 20);
        passPercent = clamp(passPercent, 0, 100);
        unlockMinutesPerGroup = clamp(unlockMinutesPerGroup, 1, 180);
        maxGroupsPerDay = clamp(maxGroupsPerDay, 1, 30);
        maxBonusMinutesPerDay = clamp(maxBonusMinutesPerDay, 0, 720);
        baseDailyMinutes = clamp(baseDailyMinutes, 0, 720);
    }

    public static int clamp(int v, int lo, int hi) {
        return v < lo ? lo : (v > hi ? hi : v);
    }

    public static String newToken() {
        Random r = new Random();
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < 6; i++) sb.append((char) ('0' + r.nextInt(10)));
        return sb.toString();
    }
}
