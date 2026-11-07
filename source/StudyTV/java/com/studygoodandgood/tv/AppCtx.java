package com.studygoodandgood.tv;

import android.app.Application;
import android.content.Context;

/** 全局 Context（后台线程、HTTP 服务要用） */
public class AppCtx extends Application {

    private static AppCtx inst;

    @Override
    public void onCreate() {
        super.onCreate();
        inst = this;
    }

    public static Context get() {
        return inst;
    }
}
