package com.studygoodandgood.tv;

import android.content.Context;

import java.io.BufferedReader;
import java.io.File;
import java.io.FileInputStream;
import java.io.InputStreamReader;
import java.util.ArrayList;
import java.util.List;
import java.util.Random;

/** 锁屏/首页随机名言（家长可在 quotes.txt 里自定义，一行一句） */
public class Quotes {

    private static final String[] DEFAULT = new String[]{
            "少壮不努力，老大徒伤悲。",
            "业精于勤荒于嬉，行成于思毁于随。",
            "书山有路勤为径，学海无涯苦作舟。",
            "今天的努力，是明天的底气。",
            "你现在偷的懒，都会变成未来的坑。",
            "不怕慢，就怕站。",
            "题做错了不要紧，改对了才重要。",
            "每天进步一点点，一年就是一大步。",
            "读书是为了有更多选择的权利。",
            "自律的人，运气都不会太差。",
            "聪明出于勤奋，天才在于积累。",
            "把不会的题弄懂，比刷一百道会的题有用。",
            "时间对每个人都是公平的，差别在于怎么用。",
            "先苦后甜，不是鸡汤，是规律。",
            "认真做完一道题，胜过草草看十道。",
            "你的努力，爸爸妈妈都看在眼里。",
            "学如逆水行舟，不进则退。",
            "读万卷书，行万里路。",
            "黑发不知勤学早，白首方悔读书迟。",
            "一寸光阴一寸金，寸金难买寸光阴。",
            "不积跬步，无以至千里。",
            "天道酬勤，功不唐捐。",
            "千里之行，始于足下。",
            "少年易老学难成，一寸光阴不可轻。",
            "会当凌绝顶，一览众山小。",
            "宝剑锋从磨砺出，梅花香自苦寒来。",
            "与其羡慕别人，不如雕刻自己。",
            "坚持不是一件容易的事，但坚持下来的人都很了不起。",
            "把今天的事做完，明天才轻松。",
            "学习不是为了考试，是为了看懂这个世界。"
    };

    private final Context ctx;
    private final List<String> all = new ArrayList<String>();
    private final Random rnd = new Random();

    public Quotes(Context ctx) {
        this.ctx = ctx;
        reload();
    }

    public void reload() {
        all.clear();
        for (String s : DEFAULT) all.add(s);
        try {
            File f = new File(ctx.getFilesDir(), "quotes.txt");
            if (f.exists() && f.length() > 0) {
                BufferedReader r = new BufferedReader(new InputStreamReader(new FileInputStream(f), "UTF-8"));
                String line;
                while ((line = r.readLine()) != null) {
                    String t = line.trim();
                    if (t.length() >= 4) all.add(t);
                }
                r.close();
            }
        } catch (Exception ignored) {
        }
    }

    public String random() {
        if (all.isEmpty()) return "";
        return all.get(rnd.nextInt(all.size()));
    }
}
