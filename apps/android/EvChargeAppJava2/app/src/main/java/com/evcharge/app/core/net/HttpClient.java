package com.evcharge.app.core.net;

import android.util.Log;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedInputStream;
import java.io.BufferedOutputStream;
import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * Minimal HTTP client using HttpURLConnection.
 * - JSON requests/responses
 * - Proper timeouts
 * - Returns Response with body and parsed JSON (object or array)
 */
public final class HttpClient {

    public static final int CONNECT_TIMEOUT_MS = 15000;
    public static final int READ_TIMEOUT_MS = 20000;

    public static final class Response {
        public final int code;
        public final String body;
        public final JSONObject jsonObject;   // null if not an object
        public final JSONArray jsonArray;     // null if not an array
        public final Map<String, List<String>> headers;

        Response(int code, String body, JSONObject obj, JSONArray arr, Map<String, List<String>> headers) {
            this.code = code;
            this.body = body;
            this.jsonObject = obj;
            this.jsonArray = arr;
            this.headers = (headers == null) ? Collections.emptyMap() : headers;
        }

        public boolean is2xx() { return code >= 200 && code < 300; }
        public boolean isJsonObject() { return jsonObject != null; }
        public boolean isJsonArray() { return jsonArray != null; }
    }

    public Response request(String method, String urlStr, JSONObject body, Map<String, String> headers) throws Exception {
        HttpURLConnection conn = null;
        try {
            URL url = new URL(urlStr);
            conn = (HttpURLConnection) url.openConnection();
            conn.setConnectTimeout(CONNECT_TIMEOUT_MS);
            conn.setReadTimeout(READ_TIMEOUT_MS);
            conn.setRequestMethod(method);
            conn.setUseCaches(false);

            // Defaults
            conn.setRequestProperty("Accept", "application/json");
            if (headers != null) {
                for (Map.Entry<String, String> e : headers.entrySet()) {
                    if (e.getKey() != null && e.getValue() != null) {
                        conn.setRequestProperty(e.getKey(), e.getValue());
                    }
                }
            }

            // Write body if present
            if (body != null) {
                byte[] bytes = body.toString().getBytes(StandardCharsets.UTF_8);
                conn.setDoOutput(true);
                conn.setRequestProperty("Content-Type", "application/json; charset=UTF-8");
                try (OutputStream os = new BufferedOutputStream(conn.getOutputStream())) {
                    os.write(bytes);
                    os.flush();
                }
            }

            int code = conn.getResponseCode();
            String resp = readAllSafe((code >= 200 && code < 400) ? conn.getInputStream() : conn.getErrorStream());
            JSONObject obj = null; JSONArray arr = null;
            if (resp != null && !resp.isEmpty()) {
                // Try object first, then array
                obj = tryParseObject(resp);
                if (obj == null) arr = tryParseArray(resp);
            }

            Map<String, List<String>> hdrs = conn.getHeaderFields();
            return new Response(code, resp, obj, arr, hdrs);
        } finally {
            if (conn != null) conn.disconnect();
        }
    }

    public Response get(String url, Map<String,String> headers) throws Exception {
        return request("GET", url, null, headers);
    }

    public Response post(String url, JSONObject body, Map<String,String> headers) throws Exception {
        return request("POST", url, body, headers);
    }

    public Response put(String url, JSONObject body, Map<String,String> headers) throws Exception {
        return request("PUT", url, body, headers);
    }

    public Response delete(String url, JSONObject body, Map<String,String> headers) throws Exception {
        return request("DELETE", url, body, headers);
    }

    // ---- helpers ----

    private static String readAllSafe(InputStream in) {
        if (in == null) return "";
        try (InputStream bis = new BufferedInputStream(in);
             ByteArrayOutputStream baos = new ByteArrayOutputStream()) {
            byte[] buf = new byte[4096];
            int n;
            while ((n = bis.read(buf)) > 0) {
                baos.write(buf, 0, n);
            }
            return baos.toString(StandardCharsets.UTF_8.name());
        } catch (Exception e) {
            Log.w("HttpClient", "readAllSafe error: " + e.getMessage());
            return "";
        }
    }

    private static JSONObject tryParseObject(String s) {
        try {
            return new JSONObject(s);
        } catch (Exception ignore) { return null; }
    }

    private static JSONArray tryParseArray(String s) {
        try {
            return new JSONArray(s);
        } catch (Exception ignore) { return null; }
    }

    public static Map<String,String> headers() {
        return new HashMap<>();
    }
}
