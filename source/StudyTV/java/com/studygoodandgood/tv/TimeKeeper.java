package com.studygoodandgood.tv;

import android.content.Context;
import android.content.SharedPreferences;
import android.os.SystemClock;

import org.json.JSONObject;

import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.List;
import java.util.Locale;

/**
 * 今日可用时长账本。
 * 可信计时：只认 SystemClock.elapsedRealtime()（看电视关机/待机时不走表），
 * 改系统时间不会多送时长。
 *
 * 每日额度 = 基础时长 + 答题赚取时长 + 家长加时
 */
public class TimeKeeper {

    private static final String PREF = "sgag_usage";

    private final Context ctx;

    private String date = "";
    private long usedSec = 0;
    private long earnedSec = 0;
    private long parentBonusSec = 0;
    private int groupsDone = 0;
    private int correctToday = 0;
    private int answeredToday = 0;
    private long lastElapsed = 0;
    /** 不足 1 秒的毫秒余量，避免每秒 tick 都抹掉零头导致计时变慢 */
    private long msRemainder = 0;
    private int saveCountdown = 0;

    public TimeKeeper(Context ctx) {
        this.ctx = ctx;
        load();
        resetIfNewDay();
        lastElapsed = SystemClock.elapsedRealtime();
        save();
    }

    // ---------------- 持久化 ----------------

    private void load() {
        String raw = ctx.getSharedPreferences(PREF, Context.MODE_PRIVATE).getString("json", null);
        if (raw == null || raw.length() == 0) return;
        try {
            JSONObject o = new JSONObject(raw);
            date = o.optString("date", "");
            usedSec = o.optLong("used", 0);
            earnedSec = o.optLong("earned", 0);
            parentBonusSec = o.optLong("parentBonus", 0);
            groupsDone = o.optInt("groups", 0);
            correctToday = o.optInt("correct", 0);
            answeredToday = o.optInt("answered", 0);
        } catch (Exception ignored) {
        }
    }

    public void save() {
        try {
            JSONObject o = new JSONObject();
            o.put("date", date);
            o.put("used", usedSec);
            o.put("earned", earnedSec);
            o.put("parentBonus", parentBonusSec);
            o.put("groups", groupsDone);
            o.put("correct", correctToday);
            o.put("answered", answeredToday);
            ctx.getSharedPreferences(PREF, Context.MODE_PRIVATE)
                    .edit().putString("json", o.toString()).apply();
            saveCountdown = 0;
        } catch (Exception ignored) {
        }
    }

    public static String today() {
        return new SimpleDateFormat("yyyy-MM-dd", Locale.US).format(new Date());
    }

    private void resetIfNewDay() {
        String t = today();
        if (!t.equals(date)) {
            date = t;
            usedSec = 0;
            earnedSec = 0;
            parentBonusSec = 0;
            groupsDone = 0;
            correctToday = 0;
            answeredToday = 0;
            save();
        }
    }

    // ---------------- 额度 ----------------

    public long budgetSeconds(Rules r) {
        return (long) r.baseDailyMinutes * 60L + earnedSec + parentBonusSec;
    }

    public long usedSeconds() {
        return usedSec;
    }

    public long remainingSeconds(Rules r) {
        long rem = budgetSeconds(r) - usedSec;
        return rem < 0 ? 0 : rem;
    }

    public int remainingMinutes(Rules r) {
        long s = remainingSeconds(r);
        return (int) ((s + 59) / 60);
    }

    public int groupsDone() {
        return groupsDone;
    }

    public int earnedMinutes() {
        return (int) ((earnedSec + parentBonusSec) / 60);
    }

    // ---------------- 自由时段 ----------------

    public boolean inFreeWindow(Rules r) {
        if (r.freeWindows == null) return false;
        String now = new SimpleDateFormat("HH:mm", Locale.US).format(new Date());
        int nowMin = minutesOf(now);
        for (String w : r.freeWindows) {
            String[] p = w.split("-");
            if (p.length != 2) continue;
            int a = minutesOf(p[0].trim());
            int b = minutesOf(p[1].trim());
            if (a < 0 || b < 0) continue;
            if (a <= b) {
                if (nowMin >= a && nowMin < b) return true;
            } else {
                // 跨午夜，例如 22:00-02:00
                if (nowMin >= a || nowMin < b) return true;
            }
        }
        return false;
    }

    private static int minutesOf(String hhmm) {
        try {
            String[] p = hhmm.split(":");
            return Integer.parseInt(p[0].trim()) * 60 + Integer.parseInt(p[1].trim());
        } catch (Exception e) {
            return -1;
        }
    }

    // ---------------- 计时 ----------------

    /** 每秒调用一次；只按单调时钟扣减，关电视/待机不扣 */
    public void tick(Rules r, long maxChargeSec) {
        resetIfNewDay();
        long el = SystemClock.elapsedRealtime();   // 毫秒
        long deltaMs = el - lastElapsed;
        lastElapsed = el;
        if (deltaMs < 0) deltaMs = 0;      // 时钟异常，忽略
        long delta = (deltaMs + msRemainder) / 1000L;   // 毫秒换算成秒
        msRemainder = (deltaMs + msRemainder) % 1000L;
        if (delta > maxChargeSec) delta = maxChargeSec;

        if (r.enabled && !inFreeWindow(r)) {
            // 只在使用额度内计时；已锁定（浮窗挡着）时不再累计，否则加时也白加
            if (usedSec < budgetSeconds(r)) {
                usedSec += delta;
                if (usedSec > budgetSeconds(r)) usedSec = budgetSeconds(r);
            }
        }
        saveCountdown += (int) delta;
        if (saveCountdown >= 10) save();
    }

    public boolean isLocked(Rules r) {
        if (!r.enabled) return false;
        if (inFreeWindow(r)) return false;
        return remainingSeconds(r) <= 0;
    }

    // ---------------- 答题结算 --------------

    /** 答完一组：达标返回获得的分钟数，未达标返回 0 */
    public int finishGroup(Rules r, int correct, int total) {
        correctToday += correct;
        answeredToday += total;
        int rate = total == 0 ? 0 : (int) (correct * 100L / total);
        int got = 0;
        if (rate >= r.passPercent && groupsDone < r.maxGroupsPerDay) {
            groupsDone++;
            long capSec = (long) r.maxBonusMinutesPerDay * 60L;
            if (earnedSec + r.unlockMinutesPerGroup * 60L <= capSec) {
                earnedSec += r.unlockMinutesPerGroup * 60L;
                got = r.unlockMinutesPerGroup;
            } else if (earnedSec < capSec) {
                long left = capSec - earnedSec;
                earnedSec = capSec;
                got = (int) (left / 60);
            }
        }
        save();
        return got;
    }

    public void addParentMinutes(int minutes) {
        parentBonusSec += minutes * 60L;
        save();
    }

    public void resetToday() {
        usedSec = 0;
        earnedSec = 0;
        parentBonusSec = 0;
        groupsDone = 0;
        correctToday = 0;
        answeredToday = 0;
        save();
    }

    public JSONObject statusJson(Rules r) {
        JSONObject o = new JSONObject();
        try {
            o.put("remainingMinutes", remainingMinutes(r));
            o.put("remainingSeconds", remainingSeconds(r));
            o.put("usedMinutes", usedSec / 60);
            o.put("budgetMinutes", budgetSeconds(r) / 60);
            o.put("earnedMinutes", earnedSec / 60);
            o.put("parentBonusMinutes", parentBonusSec / 60);
            o.put("groupsDone", groupsDone);
            o.put("correct", correctToday);
            o.put("answered", answeredToday);
            o.put("freeWindow", inFreeWindow(r));
            o.put("locked", isLocked(r));
        } catch (Exception ignored) {
        }
        return o;
    }
}
