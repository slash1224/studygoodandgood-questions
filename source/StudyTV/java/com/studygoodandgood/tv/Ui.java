package com.studygoodandgood.tv;

import android.content.Context;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.Drawable;
import android.graphics.drawable.GradientDrawable;
import android.graphics.drawable.StateListDrawable;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.TextView;

/** 界面小工具：电视上一切都要"大"、要能被遥控器焦点选中 */
public class Ui {

    public static final int BG = 0xFF0D1520;
    public static final int CARD = 0xFF172433;
    public static final int CARD_SOFT = 0xFF1E2C3E;
    public static final int TEXT = 0xFFE8F1F8;
    public static final int SUB = 0xFF93A6BA;
    public static final int ACCENT = 0xFF3D8BFD;
    public static final int GREEN = 0xFF2BB673;
    public static final int RED = 0xFFE05A5A;
    public static final int YELLOW = 0xFFF2B632;
    public static final int PURPLE = 0xFF8E7CF7;

    public static int dp(Context c, float v) {
        return Math.round(v * c.getResources().getDisplayMetrics().density);
    }

    public static LinearLayout column(Context c) {
        LinearLayout l = new LinearLayout(c);
        l.setOrientation(LinearLayout.VERTICAL);
        return l;
    }

    public static LinearLayout row(Context c) {
        LinearLayout l = new LinearLayout(c);
        l.setOrientation(LinearLayout.HORIZONTAL);
        l.setGravity(Gravity.CENTER_VERTICAL);
        return l;
    }

    public static TextView tv(Context c, String text, float sp, int color, boolean bold) {
        TextView t = new TextView(c);
        t.setText(text);
        t.setTextSize(sp);
        t.setTextColor(color);
        if (bold) t.setTypeface(Typeface.create("sans-serif-medium", Typeface.BOLD));
        t.setLineSpacing(dp(c, 2), 1.0f);
        return t;
    }

    public static Drawable roundBg(Context c, int color, float radiusDp, int strokeColor, float strokeDp) {
        GradientDrawable g = new GradientDrawable();
        g.setShape(GradientDrawable.RECTANGLE);
        g.setColor(color);
        g.setCornerRadius(dp(c, radiusDp));
        if (strokeDp > 0) g.setStroke(dp(c, strokeDp), strokeColor);
        return g;
    }

    /** 带遥控器焦点高亮的按钮背景 */
    public static Drawable btnBg(Context c, int color, float radiusDp) {
        int light = mix(color, 0xFFFFFFFF, 0.18f);
        int focusedBorder = 0xFFFFD166;
        GradientDrawable normal = new GradientDrawable();
        normal.setColor(color);
        normal.setCornerRadius(dp(c, radiusDp));
        GradientDrawable focus = new GradientDrawable();
        focus.setColor(light);
        focus.setCornerRadius(dp(c, radiusDp));
        focus.setStroke(dp(c, 3), focusedBorder);
        StateListDrawable sl = new StateListDrawable();
        sl.addState(new int[]{android.R.attr.state_focused}, focus);
        sl.addState(new int[]{android.R.attr.state_pressed}, focus);
        sl.addState(new int[]{}, normal);
        return sl;
    }

    public static int mix(int a, int b, float ratio) {
        int ar = (a >> 16) & 0xff, ag = (a >> 8) & 0xff, ab = a & 0xff;
        int br = (b >> 16) & 0xff, bg = (b >> 8) & 0xff, bb = b & 0xff;
        int r = (int) (ar + (br - ar) * ratio);
        int g = (int) (ag + (bg - ag) * ratio);
        int bl = (int) (ab + (bb - ab) * ratio);
        return 0xFF000000 | (r << 16) | (g << 8) | bl;
    }

    public static Button btn(Context c, String text, float sp, int color) {
        Button b = new Button(c);
        b.setText(text);
        b.setTextSize(sp);
        b.setTextColor(Color.WHITE);
        b.setAllCaps(false);
        b.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        b.setBackground(btnBg(c, color, 14));
        b.setPadding(dp(c, 26), dp(c, 14), dp(c, 26), dp(c, 14));
        b.setMinHeight(dp(c, 64));
        b.setMinWidth(dp(c, 140));
        b.setGravity(Gravity.CENTER);
        b.setFocusable(true);
        b.setFocusableInTouchMode(true);
        b.setElevation(0);
        return b;
    }

    public static Button smallBtn(Context c, String text, int color) {
        Button b = btn(c, text, 15f, color);
        b.setPadding(dp(c, 16), dp(c, 8), dp(c, 16), dp(c, 8));
        b.setMinHeight(dp(c, 44));
        b.setMinWidth(dp(c, 90));
        return b;
    }

    public static View gap(Context c, int heightDp) {
        View v = new View(c);
        v.setLayoutParams(new LinearLayout.LayoutParams(1, dp(c, heightDp)));
        return v;
    }

    public static LinearLayout.LayoutParams lp(int w, int h) {
        return new LinearLayout.LayoutParams(w, h);
    }

    public static LinearLayout.LayoutParams lpWeight(float weight) {
        return new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, weight);
    }
}
