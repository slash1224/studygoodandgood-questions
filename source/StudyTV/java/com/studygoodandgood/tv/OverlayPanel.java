package com.studygoodandgood.tv;

import android.content.Context;
import android.graphics.Color;
import android.os.Build;
import android.view.Gravity;
import android.view.View;
import android.view.WindowManager;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.TextView;

/** 时间到之后的强制浮窗（盖在任何视频 App 之上），点一下就能回到答题页 */
public class OverlayPanel {

    public interface Action {
        void onGoQuiz();
    }

    private final Context ctx;
    private final WindowManager wm;
    private View view;
    private TextView tvTitle, tvSub, tvQuote;
    private boolean failed = false;
    private long lastTry = 0L;

    public OverlayPanel(Context ctx) {
        this.ctx = ctx;
        this.wm = (WindowManager) ctx.getSystemService(Context.WINDOW_SERVICE);
    }

    public boolean isShown() {
        return view != null;
    }

    public void hide() {
        if (view != null) {
            try {
                wm.removeView(view);
            } catch (Exception ignored) {
            }
            view = null;
        }
    }

    public void show(String title, String sub, String quote, final Action action) {
        if (view == null) {
            long now = System.currentTimeMillis();
            if (failed && now - lastTry < 30000L) return;
            lastTry = now;
            if (Build.VERSION.SDK_INT >= 23 && !android.provider.Settings.canDrawOverlays(ctx)) {
                failed = true;
                return;
            }
            try {
                view = build(action);
                wm.addView(view, params());
                failed = false;
            } catch (Exception e) {
                failed = true;
                view = null;
                return;
            }
        }
        tvTitle.setText(title);
        tvSub.setText(sub);
        if (quote != null) tvQuote.setText(quote);
    }

    private WindowManager.LayoutParams params() {
        WindowManager.LayoutParams p = new WindowManager.LayoutParams();
        p.type = Build.VERSION.SDK_INT >= 26
                ? WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY
                : WindowManager.LayoutParams.TYPE_PHONE;
        p.format = android.graphics.PixelFormat.TRANSLUCENT;
        p.flags = WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN
                | WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS
                | WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED;
        p.width = WindowManager.LayoutParams.MATCH_PARENT;
        p.height = WindowManager.LayoutParams.MATCH_PARENT;
        p.gravity = Gravity.TOP | Gravity.START;
        p.alpha = 1.0f;
        return p;
    }

    private View build(final Action action) {
        FrameLayout outer = new FrameLayout(ctx);
        outer.setBackgroundColor(0xF20D1520);

        LinearLayout col = Ui.column(ctx);
        col.setGravity(Gravity.CENTER_HORIZONTAL);
        col.setPadding(Ui.dp(ctx, 60), Ui.dp(ctx, 20), Ui.dp(ctx, 60), Ui.dp(ctx, 20));
        FrameLayout.LayoutParams flp = new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT);
        col.setLayoutParams(flp);

        tvTitle = Ui.tv(ctx, "今天的学习时间用完了", 46f, Ui.TEXT, true);
        tvTitle.setGravity(Gravity.CENTER);
        tvSub = Ui.tv(ctx, "答对几道题就可以继续看", 26f, Ui.SUB, false);
        tvSub.setGravity(Gravity.CENTER);
        tvQuote = Ui.tv(ctx, "", 22f, Ui.YELLOW, false);
        tvQuote.setGravity(Gravity.CENTER);

        android.widget.Button go = Ui.btn(ctx, "去答题（按遥控器确定键）", 24f, Ui.GREEN);
        go.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                action.onGoQuiz();
            }
        });

        col.addView(tvTitle);
        col.addView(Ui.gap(ctx, 18));
        col.addView(tvSub);
        col.addView(Ui.gap(ctx, 26));
        col.addView(go);
        col.addView(Ui.gap(ctx, 30));
        col.addView(tvQuote);

        outer.addView(col);
        outer.post(new Runnable() {
            public void run() {
                try {
                    go.requestFocus();
                } catch (Exception ignored) {
                }
            }
        });
        return outer;
    }
}
