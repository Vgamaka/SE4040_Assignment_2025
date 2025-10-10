package com.evcharge.app.core.util;

import org.json.JSONArray;
import org.json.JSONObject;

import java.time.Instant;

public final class JsonUtils {

    private JsonUtils() {}

    public static String optString(JSONObject o, String key) {
        if (o == null) return null;
        String v = o.optString(key, null);
        return (v == null || v.equals("null") || v.isEmpty()) ? null : v;
    }

    public static boolean optBool(JSONObject o, String key, boolean defVal) {
        if (o == null) return defVal;
        try { return o.getBoolean(key); } catch (Exception e) { return defVal; }
    }

    public static JSONObject optObj(JSONObject o, String key) {
        if (o == null) return null;
        try { return o.getJSONObject(key); } catch (Exception e) { return null; }
    }

    public static JSONArray optArr(JSONObject o, String key) {
        if (o == null) return null;
        try { return o.getJSONArray(key); } catch (Exception e) { return null; }
    }

    public static JSONObject parseObject(String s) {
        try { return new JSONObject(s); } catch (Exception e) { return null; }
    }

    public static JSONArray parseArray(String s) {
        try { return new JSONArray(s); } catch (Exception e) { return null; }
    }

    /** Simple heuristic to detect a JWT: 3 dot-separated Base64URL parts. */
    public static boolean looksLikeJwt(String s) {
        if (s == null) return false;
        String[] parts = s.split("\\.");
        return parts.length >= 2 && s.startsWith("eyJ"); // most JWTs start with {" header -> "eyJ"
    }

    /** ISO-8601 in UTC, e.g., 2025-10-07T04:12:34Z */
    public static String nowUtcIso() {
        return Instant.now().toString();
    }

    /** Try to extract a message field from common server shapes. */
    public static String extractMessage(JSONObject o) {
        if (o == null) return null;
        String m = optString(o, "message");
        if (m != null) return m;
        m = optString(o, "error");
        if (m != null) return m;
        m = optString(o, "detail");
        if (m != null) return m;
        return null;
    }
}
