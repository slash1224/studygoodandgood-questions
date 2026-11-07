package com.studygoodandgood.tv;

import android.content.Context;

import java.util.ArrayList;
import java.util.List;

/** 一道选择题（与电脑版 questions.json 完全同构） */
public class Question {
    public int id = 0;
    public String subject = "math";
    public int grade = 0;
    public int difficulty = 1;
    public String text = "";
    public List<String> options = new ArrayList<String>();
    public int answer = 0;
    public String explain = "";

    public String subjectCn() {
        if ("math".equals(subject)) return "数学";
        if ("chinese".equals(subject)) return "语文";
        if ("english".equals(subject)) return "英语";
        if ("coding".equals(subject)) return "编程";
        if ("physics".equals(subject)) return "物理";
        if ("chemistry".equals(subject)) return "化学";
        if ("biology".equals(subject)) return "生物";
        if ("history".equals(subject)) return "历史";
        if ("geography".equals(subject)) return "地理";
        if ("politics".equals(subject)) return "道法";
        return subject == null ? "" : subject;
    }
}
