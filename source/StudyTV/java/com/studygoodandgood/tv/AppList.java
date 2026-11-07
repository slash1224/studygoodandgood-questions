package com.studygoodandgood.tv;

import android.content.Context;
import android.content.Intent;
import android.content.pm.ResolveInfo;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

/** 电视上已安装的、可以用遥控器打开的应用（严格模式下孩子从这里"继续看电视"） */
public class AppList {

    public static class Entry {
        public String label = "";
        public Intent intent;
    }

    public static List<Entry> load(Context ctx, String selfPackage) {
        List<Entry> out = new ArrayList<Entry>();
        Set<String> seen = new HashSet<String>();
        try {
            android.content.pm.PackageManager pm = ctx.getPackageManager();
            Intent leanback = new Intent(Intent.ACTION_MAIN);
            leanback.addCategory(Intent.CATEGORY_LEANBACK_LAUNCHER);
            Intent normal = new Intent(Intent.ACTION_MAIN);
            normal.addCategory(Intent.CATEGORY_LAUNCHER);

            List<ResolveInfo> ris = new ArrayList<ResolveInfo>();
            try {
                ris.addAll(pm.queryIntentActivities(leanback, 0));
            } catch (Exception ignored) {
            }
            try {
                ris.addAll(pm.queryIntentActivities(normal, 0));
            } catch (Exception ignored) {
            }

            for (ResolveInfo ri : ris) {
                if (ri == null || ri.activityInfo == null) continue;
                String pkg = ri.activityInfo.packageName;
                if (pkg == null || pkg.equals(selfPackage)) continue;
                if (pkg.equals("com.android.settings") || pkg.equals("com.android.systemui")) continue;
                if (seen.contains(pkg)) continue;
                seen.add(pkg);
                Entry e = new Entry();
                CharSequence lb = ri.loadLabel(pm);
                e.label = lb == null ? pkg : lb.toString();
                e.intent = new Intent(Intent.ACTION_MAIN);
                e.intent.addCategory(Intent.CATEGORY_LEANBACK_LAUNCHER);
                e.intent.setClassName(pkg, ri.activityInfo.name);
                e.intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                out.add(e);
            }
        } catch (Exception ignored) {
        }
        Collections.sort(out, new Comparator<Entry>() {
            public int compare(Entry a, Entry b) {
                return a.label.compareToIgnoreCase(b.label);
            }
        });
        return out;
    }
}
