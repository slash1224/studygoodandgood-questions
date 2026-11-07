package com.studygoodandgood.tv;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.DialogInterface;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.Color;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import android.text.InputType;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.Window;
import android.view.WindowManager;
import android.widget.Button;
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import java.util.List;
import java.util.concurrent.CountDownLatch;

/**
 * 电视端主界面（同时也可以作为"电视桌面"）。
 * 普通提醒模式：家长打开本应用即可看到剩余时长，时间用完弹浮窗提醒。
 * 严格模式：在电视设置里把本应用设为桌面，孩子按 HOME 键就回到这里。
 */
public class MainActivity extends Activity implements AdminServer.Host {

    private Rules rules;
    private Stats stats;
    private QuestionBank bank;
    private TimeKeeper time;
    private Quotes quotes;
    private AdminServer server;
    private OverlayPanel overlay;

    private FrameLayout root;
    private LinearLayout homeView;
    private LinearLayout quizView;

    private TextView tvRemainTop, tvState, tvDetail, tvToday, tvQuote;
    private Button btnWatch, btnQuiz;

    private boolean inQuiz = false;
    private boolean resumed = false;
    /** 从浮窗点了"去答题"进来的，恢复界面后直接开始答题 */
    private boolean pendingQuiz = false;

    private List<Question> group;
    private int qIndex = 0;
    private int correctCount = 0;
    private boolean answered = false;
    private TextView tvFeedback;
    private Button btnNext;
    private Button[] opts = new Button[4];

    private final Handler ui = new Handler(Looper.getMainLooper());

    private final Runnable ticker = new Runnable() {
        public void run() {
            try {
                time.tick(rules, 3600);
                if (!inQuiz) refreshHomeTexts();
                updateOverlay();
            } catch (Exception ignored) {
            }
            ui.postDelayed(this, 1000);
        }
    };

    // ---------------- 生命周期 ----------------

    @Override
    protected void onCreate(Bundle b) {
        super.onCreate(b);
        fullScreen();
        rules = Rules.load(this);
        stats = new Stats(this);
        bank = new QuestionBank(this, stats);
        bank.setUseLocal(rules.useLocalBank);
        bank.setWeakSubjects(rules.weakSubjects);
        bank.load();
        time = new TimeKeeper(this);
        quotes = new Quotes(this);
        overlay = new OverlayPanel(this);

        root = new FrameLayout(this);
        root.setBackgroundColor(Ui.BG);
        setContentView(root);

        buildHome();
        showHome();

        server = new AdminServer(this);
        server.start();

        if (rules.autoUpdateBank) maybeAutoUpdateBank();
    }

    private void fullScreen() {
        try {
            requestWindowFeature(Window.FEATURE_NO_TITLE);
            getWindow().setFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN,
                    WindowManager.LayoutParams.FLAG_FULLSCREEN);
            getWindow().getDecorView().setSystemUiVisibility(
                    View.SYSTEM_UI_FLAG_FULLSCREEN
                            | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                            | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
                            | View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                            | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN);
        } catch (Exception ignored) {
        }
    }

    @Override
    protected void onResume() {
        super.onResume();
        resumed = true;
        time.save();
        if (bank.count() == 0) bank.load();
        showHome();
        ui.removeCallbacks(ticker);
        ui.post(ticker);
        if (pendingQuiz) {
            pendingQuiz = false;
            startQuiz();
        }
    }

    @Override
    protected void onPause() {
        super.onPause();
        resumed = false;
        time.save();
        updateOverlay();
    }

    @Override
    protected void onDestroy() {
        ui.removeCallbacks(ticker);
        time.save();
        if (server != null) server.stop();
        if (overlay != null) overlay.hide();
        super.onDestroy();
    }

    @Override
    public void onBackPressed() {
        if (inQuiz) {
            showHome();
        }
        // 首页按返回键不退出（避免孩子一按就离开管控界面）
    }

    // ---------------- 首页 ----------------

    private void buildHome() {
        homeView = Ui.column(this);
        homeView.setPadding(Ui.dp(this, 56), Ui.dp(this, 30), Ui.dp(this, 56), Ui.dp(this, 26));

        LinearLayout top = Ui.row(this);
        TextView title = Ui.tv(this, "学习好电视", 28f, Ui.TEXT, true);
        top.addView(title, Ui.lpWeight(1));

        tvRemainTop = Ui.tv(this, "", 24f, Ui.YELLOW, true);
        top.addView(tvRemainTop);

        Button btnParent = Ui.smallBtn(this, "家长管理", Ui.CARD_SOFT);
        btnParent.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                openParentGate();
            }
        });
        LinearLayout.LayoutParams plp = Ui.lp(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        plp.leftMargin = Ui.dp(this, 18);
        top.addView(btnParent, plp);

        homeView.addView(top);

        LinearLayout card = Ui.column(this);
        card.setBackground(Ui.roundBg(this, Ui.CARD, 22, 0, 0));
        card.setPadding(Ui.dp(this, 44), Ui.dp(this, 40), Ui.dp(this, 44), Ui.dp(this, 40));
        card.setGravity(Gravity.CENTER_VERTICAL);
        LinearLayout.LayoutParams clp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, 0, 1f);
        clp.topMargin = Ui.dp(this, 26);
        homeView.addView(card, clp);

        tvState = Ui.tv(this, "", 46f, Ui.TEXT, true);
        card.addView(tvState);

        tvDetail = Ui.tv(this, "", 24f, Ui.SUB, false);
        LinearLayout.LayoutParams dlp = Ui.lp(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        dlp.topMargin = Ui.dp(this, 16);
        card.addView(tvDetail, dlp);

        LinearLayout btns = Ui.row(this);
        LinearLayout.LayoutParams blp = Ui.lp(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        blp.topMargin = Ui.dp(this, 34);
        card.addView(btns, blp);

        btnWatch = Ui.btn(this, "继续看电视", 24f, Ui.ACCENT);
        btnWatch.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                openAppList();
            }
        });
        btns.addView(btnWatch);

        btnQuiz = Ui.btn(this, "开始答题", 24f, Ui.GREEN);
        btnQuiz.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                startQuiz();
            }
        });
        LinearLayout.LayoutParams qlp = Ui.lp(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        qlp.leftMargin = Ui.dp(this, 22);
        btns.addView(btnQuiz, qlp);

        LinearLayout footer = Ui.column(this);
        LinearLayout.LayoutParams flp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        flp.topMargin = Ui.dp(this, 20);
        homeView.addView(footer, flp);

        tvToday = Ui.tv(this, "", 18f, Ui.SUB, false);
        footer.addView(tvToday);

        tvQuote = Ui.tv(this, "", 20f, Ui.YELLOW, false);
        LinearLayout.LayoutParams qplp = Ui.lp(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        qplp.topMargin = Ui.dp(this, 10);
        tvQuote.setSingleLine(false);
        footer.addView(tvQuote, qplp);
    }

    private void showHome() {
        inQuiz = false;
        root.removeAllViews();
        root.addView(homeView, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
        tvQuote.setText("—— " + quotes.random());
        refreshHomeTexts();
        if (time.isLocked(rules)) {
            btnQuiz.requestFocus();
        } else if (btnWatch.getVisibility() == View.VISIBLE) {
            btnWatch.requestFocus();
        } else {
            btnQuiz.requestFocus();
        }
        updateOverlay();
    }

    private void refreshHomeTexts() {
        boolean free = time.inFreeWindow(rules);
        boolean locked = time.isLocked(rules);
        int remain = time.remainingMinutes(rules);
        String name = rules.childName == null ? "宝贝" : rules.childName;

        if (free) {
            tvRemainTop.setText("自由时段");
            tvState.setText("现在是自由时段");
            tvDetail.setText("这段时间可以放心看电视，答不答题都可以。");
        } else if (locked) {
            tvRemainTop.setText("已用完");
            tvState.setText(name + "，今天的观看时间用完啦");
            tvDetail.setText("答对 " + rules.questionsPerGroup + " 道题（正确率 " + rules.passPercent
                    + "% 以上）可以换取 " + rules.unlockMinutesPerGroup + " 分钟观看时间。");
        } else {
            tvRemainTop.setText("剩余 " + fmtMin(remain));
            tvState.setText("还能看 " + fmtMin(remain));
            tvDetail.setText("基础 " + rules.baseDailyMinutes + " 分钟 + 答题奖励 " + time.earnedMinutes()
                    + " 分钟。用完需要答题才能继续。");
        }
        btnWatch.setVisibility(locked ? View.GONE : View.VISIBLE);
        btnWatch.setText(time.isLocked(rules) ? "继续看电视" : "选择应用继续看电视");

        tvToday.setText("今日：完成 " + time.groupsDone() + " 组 · 累计奖励 " + time.earnedMinutes()
                + " 分钟 · 题库 " + bank.count() + " 题" + bankTips());
        btnQuiz.setText(locked ? "开始答题解锁" : "开始答题赚时长");
    }

    private String bankTips() {
        int wb = stats.wrongBookCount(bank.list());
        return wb > 0 ? " · 错题待巩固 " + wb + " 道" : "";
    }

    private static String fmtMin(int m) {
        if (m <= 0) return "0 分钟";
        if (m < 60) return m + " 分钟";
        return (m / 60) + " 小时 " + (m % 60 == 0 ? "" : (m % 60) + " 分钟");
    }

    // ---------------- 答题 ----------------

    private void startQuiz() {
        group = bank.pickGroup(rules.grade, rules.questionsPerGroup);
        if (group == null || group.isEmpty()) {
            toast("题库为空，请先在家长管理里更新题库");
            return;
        }
        qIndex = 0;
        correctCount = 0;
        answered = false;
        inQuiz = true;
        showQuestion();
    }

    private void showQuestion() {
        answered = false;
        Question q = group.get(qIndex);

        LinearLayout col = Ui.column(this);
        col.setPadding(Ui.dp(this, 60), Ui.dp(this, 18), Ui.dp(this, 60), Ui.dp(this, 18));
        col.setBackgroundColor(Ui.BG);

        LinearLayout top = Ui.row(this);
        TextView prog = Ui.tv(this, "第 " + (qIndex + 1) + " / " + group.size() + " 题", 20f, Ui.SUB, false);
        top.addView(prog, Ui.lpWeight(1));
        TextView tag = Ui.tv(this, q.subjectCn() + " · 难度 " + q.difficulty, 18f, Ui.SUB, false);
        top.addView(tag);
        Button quit = Ui.smallBtn(this, "返回首页", Ui.CARD_SOFT);
        quit.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                showHome();
            }
        });
        LinearLayout.LayoutParams qlp2 = Ui.lp(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        qlp2.leftMargin = Ui.dp(this, 16);
        top.addView(quit, qlp2);
        col.addView(top);

        TextView text = Ui.tv(this, q.text, 30f, Ui.TEXT, true);
        LinearLayout.LayoutParams tlp = Ui.lp(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        tlp.topMargin = Ui.dp(this, 14);
        col.addView(text, tlp);

        LinearLayout optsBox = Ui.column(this);
        LinearLayout.LayoutParams olp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        olp.topMargin = Ui.dp(this, 12);
        col.addView(optsBox, olp);

        String[] letters = new String[]{"A", "B", "C", "D"};
        for (int i = 0; i < 4; i++) {
            final int idx = i;
            String content = i < q.options.size() ? (letters[i] + "   " + q.options.get(i)) : "";
            Button b = Ui.btn(this, content, 24f, Ui.CARD_SOFT);
            b.setGravity(Gravity.START | Gravity.CENTER_VERTICAL);
            b.setVisibility(i < q.options.size() ? View.VISIBLE : View.GONE);
            b.setOnClickListener(new View.OnClickListener() {
                public void onClick(View v) {
                    answer(idx);
                }
            });
            opts[i] = b;
            LinearLayout.LayoutParams blp2 = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT, Ui.dp(this, 58));
            blp2.topMargin = Ui.dp(this, 8);
            optsBox.addView(b, blp2);
        }

        tvFeedback = Ui.tv(this, "", 20f, Ui.SUB, false);
        LinearLayout.LayoutParams fblp = Ui.lp(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        fblp.topMargin = Ui.dp(this, 10);
        col.addView(tvFeedback, fblp);

        btnNext = Ui.btn(this, "下一题", 20f, Ui.ACCENT);
        btnNext.setVisibility(View.GONE);
        btnNext.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                nextQuestion();
            }
        });
        LinearLayout.LayoutParams nlp = Ui.lp(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        nlp.topMargin = Ui.dp(this, 6);
        col.addView(btnNext, nlp);

        quizView = col;
        root.removeAllViews();
        ScrollView sv = new ScrollView(this);
        sv.setFillViewport(true);
        sv.addView(col);
        root.addView(sv, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));

        if (opts[0] != null) opts[0].requestFocus();
    }

    private void answer(int idx) {
        if (answered) return;
        answered = true;
        Question q = group.get(qIndex);
        boolean right = idx == q.answer;
        if (right) correctCount++;
        stats.record(q, right);

        for (int i = 0; i < opts.length; i++) {
            if (opts[i] == null) continue;
            if (i == q.answer) {
                opts[i].setBackground(Ui.btnBg(this, Ui.GREEN, 14));
            } else if (i == idx) {
                opts[i].setBackground(Ui.btnBg(this, Ui.RED, 14));
            } else {
                opts[i].setBackground(Ui.btnBg(this, Ui.mix(Ui.CARD_SOFT, Ui.BG, 0.35f), 14));
            }
            opts[i].setEnabled(false);
            opts[i].setFocusable(false);
        }

        if (right) {
            tvFeedback.setTextColor(Ui.GREEN);
            tvFeedback.setText("答对了！" + (rules.showExplain && q.explain.length() > 0 ? "\n" + q.explain : ""));
        } else {
            tvFeedback.setTextColor(0xFFFF8A8A);
            String correct = q.answer < q.options.size() ? q.options.get(q.answer) : "";
            tvFeedback.setText("正确答案是 " + letter(q.answer) + "：" + correct
                    + (rules.showExplain && q.explain.length() > 0 ? "\n" + q.explain : ""));
        }
        btnNext.setVisibility(View.VISIBLE);
        btnNext.setText(qIndex + 1 >= group.size() ? "看结果" : "下一题");
        btnNext.requestFocus();
    }

    private static String letter(int i) {
        return i >= 0 && i < 4 ? new String[]{"A", "B", "C", "D"}[i] : "?";
    }

    private void nextQuestion() {
        if (qIndex + 1 >= group.size()) {
            showResult();
            return;
        }
        qIndex++;
        showQuestion();
    }

    private void showResult() {
        int total = group.size();
        int rate = total == 0 ? 0 : correctCount * 100 / total;
        int got = time.finishGroup(rules, correctCount, total);
        time.save();

        LinearLayout col = Ui.column(this);
        col.setPadding(Ui.dp(this, 80), Ui.dp(this, 40), Ui.dp(this, 80), Ui.dp(this, 40));
        col.setGravity(Gravity.CENTER_HORIZONTAL);
        col.setBackgroundColor(Ui.BG);

        String big;
        String detail;
        int color;
        if (got > 0) {
            big = "太棒了！+" + got + " 分钟";
            color = Ui.GREEN;
            detail = "本组正确率 " + rate + "%，可以继续看 " + fmtMin(time.remainingMinutes(rules))
                    + "。错题会更容易再出现，多练几遍就掌握了。";
        } else if (rate >= rules.passPercent) {
            big = "这组答得不错";
            color = Ui.YELLOW;
            detail = "正确率 " + rate + "%。今天的答题奖励已达上限或组数已用完，明天再来赚时长吧。";
        } else {
            big = "还差一点点";
            color = Ui.YELLOW;
            detail = "本组正确率 " + rate + "%，需要达到 " + rules.passPercent + "% 才能解锁 "
                    + rules.unlockMinutesPerGroup + " 分钟。看看解析，再来一组试试。";
        }

        TextView t1 = Ui.tv(this, big, 46f, color, true);
        t1.setGravity(Gravity.CENTER);
        TextView t2 = Ui.tv(this, detail, 24f, Ui.TEXT, false);
        t2.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams t2lp = Ui.lp(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        t2lp.topMargin = Ui.dp(this, 24);
        col.addView(t1);
        col.addView(t2, t2lp);

        LinearLayout btns = Ui.row(this);
        btns.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams blp = Ui.lp(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        blp.topMargin = Ui.dp(this, 40);
        col.addView(btns, blp);

        Button again = Ui.btn(this, "再答一组", 22f, Ui.ACCENT);
        again.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                startQuiz();
            }
        });
        btns.addView(again);

        Button home = Ui.btn(this, "回到首页", 22f, Ui.CARD_SOFT);
        home.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                showHome();
            }
        });
        LinearLayout.LayoutParams hlp = Ui.lp(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        hlp.leftMargin = Ui.dp(this, 18);
        btns.addView(home, hlp);
        again.requestFocus();

        root.removeAllViews();
        root.addView(col, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
        inQuiz = false;
        updateOverlay();
    }

    // ---------------- 浮窗 ----------------

    private void updateOverlay() {
        try {
            boolean need = rules.enabled && rules.overlayEnabled && !resumed
                    && !time.inFreeWindow(rules) && time.isLocked(rules);
            if (need) {
                String quote = overlay.isShown() ? null : "—— " + quotes.random();
                overlay.show("今天的学习时间用完了", "答对 " + rules.questionsPerGroup + " 道题就能继续看",
                        quote, new OverlayPanel.Action() {
                            public void onGoQuiz() {
                                pendingQuiz = true;
                                Intent i = new Intent(MainActivity.this, MainActivity.class);
                                i.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_SINGLE_TOP);
                                startActivity(i);
                            }
                        });
            } else {
                overlay.hide();
            }
        } catch (Exception ignored) {
        }
    }

    // ---------------- 选择应用 ----------------

    private void openAppList() {
        List<AppList.Entry> apps = AppList.load(this, getPackageName());
        if (apps.isEmpty()) {
            toast("没有找到可以打开的电视应用");
            return;
        }
        ScrollView sv = new ScrollView(this);
        LinearLayout col = Ui.column(this);
        col.setPadding(Ui.dp(this, 20), Ui.dp(this, 14), Ui.dp(this, 20), Ui.dp(this, 14));
        for (final AppList.Entry e : apps) {
            Button b = Ui.btn(this, e.label, 20f, Ui.CARD_SOFT);
            b.setGravity(Gravity.START | Gravity.CENTER_VERTICAL);
            b.setOnClickListener(new View.OnClickListener() {
                public void onClick(View v) {
                    try {
                        startActivity(e.intent);
                    } catch (Exception ex) {
                        toast("打不开 " + e.label);
                    }
                }
            });
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
            lp.topMargin = Ui.dp(this, 8);
            col.addView(b, lp);
        }
        sv.addView(col);
        new AlertDialog.Builder(this)
                .setTitle("选择要看的应用")
                .setView(sv)
                .setNegativeButton("取消", null)
                .show();
    }

    // ---------------- 家长面板 ----------------

    private void openParentGate() {
        LinearLayout box = Ui.column(this);
        box.setPadding(Ui.dp(this, 28), Ui.dp(this, 16), Ui.dp(this, 28), Ui.dp(this, 8));
        TextView hint = Ui.tv(this, "请输入家长密码（默认 123456）", 18f, Ui.SUB, false);
        EditText et = new EditText(this);
        et.setInputType(InputType.TYPE_CLASS_NUMBER | InputType.TYPE_NUMBER_VARIATION_PASSWORD);
        et.setTextSize(22);
        et.setHint("家长密码");
        LinearLayout.LayoutParams elp = Ui.lp(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        elp.topMargin = Ui.dp(this, 10);
        box.addView(hint);
        box.addView(et, elp);

        final AlertDialog dlg = new AlertDialog.Builder(this)
                .setTitle("家长验证")
                .setView(box)
                .setNegativeButton("取消", null)
                .setPositiveButton("进入", null)
                .create();
        dlg.show();
        dlg.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                String pw = et.getText().toString().trim();
                if (pw.equals(rules.parentPassword)) {
                    dlg.dismiss();
                    showParentPanel();
                } else {
                    et.setText("");
                    toast("密码不对");
                }
            }
        });
        et.requestFocus();
    }

    private void showParentPanel() {
        String url = lanUrl();
        ScrollView sv = new ScrollView(this);
        LinearLayout col = Ui.column(this);
        col.setPadding(Ui.dp(this, 30), Ui.dp(this, 16), Ui.dp(this, 30), Ui.dp(this, 16));

        TextView u = Ui.tv(this, url, 22f, Ui.ACCENT, true);
        u.setTextIsSelectable(true);
        col.addView(u);

        TextView t = Ui.tv(this, "手机和电视连同一个 WiFi，用手机浏览器打开上面的地址就能管理（改规则、加时长、看错题本）。访问码：" + rules.adminToken,
                16f, Ui.SUB, false);
        LinearLayout.LayoutParams tlp = Ui.lp(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        tlp.topMargin = Ui.dp(this, 10);
        col.addView(t, tlp);

        Bitmap qr = QrGen.make(url, 360);
        if (qr != null) {
            ImageView iv = new ImageView(this);
            iv.setImageBitmap(qr);
            LinearLayout.LayoutParams ilp = Ui.lp(Ui.dp(this, 220), Ui.dp(this, 220));
            ilp.topMargin = Ui.dp(this, 16);
            ilp.gravity = Gravity.CENTER_HORIZONTAL;
            col.addView(iv, ilp);
        }

        LinearLayout b1 = Ui.row(this);
        LinearLayout.LayoutParams b1lp = Ui.lp(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        b1lp.topMargin = Ui.dp(this, 14);
        col.addView(b1, b1lp);

        Button add = Ui.btn(this, "加 30 分钟", 18f, Ui.GREEN);
        add.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                time.addParentMinutes(30);
                toast("已加 30 分钟");
                refreshHomeTexts();
            }
        });
        b1.addView(add);

        Button upd = Ui.btn(this, "更新题库", 18f, Ui.ACCENT);
        upd.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                toast("正在更新题库…");
                bank.updateFromUrl(rules.bankUrl, new QuestionBank.Callback() {
                    public void done(boolean ok, String msg, int count) {
                        toast(msg);
                        ui.post(new Runnable() {
                            public void run() {
                                refreshHomeTexts();
                            }
                        });
                    }
                });
            }
        });
        LinearLayout.LayoutParams ulp = Ui.lp(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        ulp.leftMargin = Ui.dp(this, 12);
        b1.addView(upd, ulp);

        LinearLayout b2 = Ui.row(this);
        LinearLayout.LayoutParams b2lp = Ui.lp(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        b2lp.topMargin = Ui.dp(this, 8);
        col.addView(b2, b2lp);

        Button perm = Ui.btn(this, "授权浮窗覆盖", 18f, Ui.CARD_SOFT);
        perm.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                openOverlaySettings();
            }
        });
        b2.addView(perm);

        Button homeSet = Ui.btn(this, "设为电视桌面", 18f, Ui.CARD_SOFT);
        homeSet.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                openHomeSettings();
            }
        });
        LinearLayout.LayoutParams hlp = Ui.lp(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        hlp.leftMargin = Ui.dp(this, 12);
        b2.addView(homeSet, hlp);

        TextView tip = Ui.tv(this, "严格模式说明：在电视系统设置里把「学习好电视」设为默认桌面（或桌面选择里选它），"
                + "孩子按 HOME 键就回到这里；时间用完时浮窗会盖在任何视频应用上。"
                + "\n普通提醒模式：不需要设为桌面，家长自己打开本应用就能看到剩余时长。"
                + "\n当前模式：" + (rules.strictMode ? "严格模式（已在管理页开启）" : "普通提醒模式"), 16f, Ui.SUB, false);
        LinearLayout.LayoutParams tipLp = Ui.lp(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        tipLp.topMargin = Ui.dp(this, 16);
        col.addView(tip, tipLp);

        sv.addView(col);
        new AlertDialog.Builder(this)
                .setTitle("家长管理")
                .setView(sv)
                .setNegativeButton("关闭", null)
                .show();
    }

    private void openOverlaySettings() {
        if (Build.VERSION.SDK_INT >= 23) {
            try {
                Intent i = new Intent(Settings.ACTION_MANAGE_OVERLAY_PERMISSION,
                        Uri.parse("package:" + getPackageName()));
                startActivity(i);
                toast("请打开「显示在其他应用上层」权限");
            } catch (Exception e) {
                toast("这台电视找不到悬浮窗权限设置，可能不需要授权");
            }
        } else {
            toast("这台电视系统较老，浮窗权限默认已开");
        }
    }

    private void openHomeSettings() {
        try {
            Intent i = new Intent(Settings.ACTION_HOME_SETTINGS);
            startActivity(i);
            toast("请选择「学习好电视」作为桌面");
        } catch (Exception e) {
            try {
                Intent i2 = new Intent(Intent.ACTION_MAIN);
                i2.addCategory(Intent.CATEGORY_HOME);
                startActivity(Intent.createChooser(i2, "选择电视桌面"));
            } catch (Exception e2) {
                toast("这台电视不支持设置默认桌面，可用普通提醒模式");
            }
        }
    }

    // ---------------- AdminServer.Host ----------------

    public Rules rules() {
        return rules;
    }

    public TimeKeeper time() {
        return time;
    }

    public QuestionBank bank() {
        return bank;
    }

    public Stats stats() {
        return stats;
    }

    public String lanUrl() {
        String ip = NetUtil.localIpv4();
        if (ip == null) ip = "电视IP";
        return "http://" + ip + ":" + (server == null ? 8787 : server.port()) + "/";
    }

    public void onRulesSaved() {
        ui.post(new Runnable() {
            public void run() {
                rules.save(MainActivity.this);
                bank.setUseLocal(rules.useLocalBank);
                bank.setWeakSubjects(rules.weakSubjects);
                refreshHomeTexts();
                updateOverlay();
            }
        });
    }

    public QuestionBank.LocalResult applyLocalBank(String json) {
        QuestionBank.LocalResult res = bank.applyLocalJson(json);
        ui.post(new Runnable() {
            public void run() {
                refreshHomeTexts();
            }
        });
        return res;
    }

    public QuestionBank.LocalResult clearLocalBank() {
        QuestionBank.LocalResult res = bank.clearLocal();
        ui.post(new Runnable() {
            public void run() {
                refreshHomeTexts();
            }
        });
        return res;
    }

    public String updateBankNow() {
        final String[] msg = new String[]{"题库更新超时"};
        final CountDownLatch latch = new CountDownLatch(1);
        bank.updateFromUrl(rules.bankUrl, new QuestionBank.Callback() {
            public void done(boolean ok, String m, int count) {
                msg[0] = m;
                latch.countDown();
            }
        });
        try {
            latch.await(30, java.util.concurrent.TimeUnit.SECONDS);
        } catch (Exception ignored) {
        }
        ui.post(new Runnable() {
            public void run() {
                refreshHomeTexts();
            }
        });
        return msg[0];
    }

    // ---------------- 其它 ----------------

    private void maybeAutoUpdateBank() {
        try {
            android.content.SharedPreferences sp =
                    getSharedPreferences("sgag_meta", MODE_PRIVATE);
            long last = sp.getLong("lastBankUpdate", 0L);
            if (System.currentTimeMillis() - last < 3L * 86400000L) return;
            bank.updateFromUrl(rules.bankUrl, new QuestionBank.Callback() {
                public void done(boolean ok, String msg, int count) {
                    if (ok) {
                        getSharedPreferences("sgag_meta", MODE_PRIVATE).edit()
                                .putLong("lastBankUpdate", System.currentTimeMillis()).apply();
                        ui.post(new Runnable() {
                            public void run() {
                                refreshHomeTexts();
                            }
                        });
                    }
                }
            });
        } catch (Exception ignored) {
        }
    }

    private void toast(String s) {
        Toast.makeText(this, s, Toast.LENGTH_SHORT).show();
    }
}
