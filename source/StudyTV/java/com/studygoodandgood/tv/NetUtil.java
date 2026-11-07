package com.studygoodandgood.tv;

import java.net.Inet4Address;
import java.net.InetAddress;
import java.net.NetworkInterface;
import java.util.Collections;
import java.util.Enumeration;

/** 获取电视在局域网里的 IPv4 地址（手机同 WiFi 打开这个地址就能管理） */
public class NetUtil {

    public static String localIpv4() {
        try {
            Enumeration<NetworkInterface> nis = NetworkInterface.getNetworkInterfaces();
            if (nis == null) return null;
            for (NetworkInterface ni : Collections.list(nis)) {
                if (!ni.isUp() || ni.isLoopback()) continue;
                String name = ni.getName() == null ? "" : ni.getName().toLowerCase();
                if (name.startsWith("p2p") || name.startsWith("dummy") || name.contains("rmnet")) continue;
                for (InetAddress addr : Collections.list(ni.getInetAddresses())) {
                    if (addr instanceof Inet4Address && !addr.isLoopbackAddress() && !addr.isLinkLocalAddress()) {
                        String ip = addr.getHostAddress();
                        if (ip != null && ip.startsWith("192.")) return ip;
                        if (ip != null && (ip.startsWith("10.") || ip.startsWith("172."))) return ip;
                        if (ip != null && !ip.equals("0.0.0.0")) return ip;
                    }
                }
            }
        } catch (Exception ignored) {
        }
        return null;
    }
}
