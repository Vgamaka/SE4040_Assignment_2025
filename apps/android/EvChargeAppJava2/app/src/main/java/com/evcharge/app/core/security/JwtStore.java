package com.evcharge.app.core.security;

import android.content.Context;
import android.content.SharedPreferences;
import android.util.Base64;

import org.json.JSONArray;
import org.json.JSONObject;

import java.nio.charset.StandardCharsets;
import java.net.HttpURLConnection;

/**
 * Stores and validates the JWT. No external libs.
 * - Persists token
 * - Extracts exp (epoch seconds) and roles from JWT payload
 * - Validity check with small clock skew
 */
public final class JwtStore {

    private static final String PREFS_NAME = "jwt_store";

    private static final String KEY_TOKEN = "token";
    private static final String KEY_EXP_EPOCH_SEC = "exp_epoch_sec";
    private static final String KEY_ROLES_JSON = "roles_json";

    private static final long CLOCK_SKEW_SEC = 30L;

    private final SharedPreferences prefs;

    public JwtStore(Context context) {
        this.prefs = context.getApplicationContext()
                .getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
    }

    /** Save raw JWT and derived claims (exp, roles). */
    public void save(String jwt) {
        if (jwt == null || jwt.trim().isEmpty()) return;

        long exp = 0L;
        JSONArray roles = new JSONArray();

        try {
            JSONObject payload = decodePayload(jwt);
            if (payload != null) {
                // exp: seconds since epoch
                exp = payload.optLong("exp", 0L);

                // Roles can be "role" (string or array) or "roles" (array)
                Object roleClaim = null;
                if (payload.has("roles")) roleClaim = payload.opt("roles");
                else if (payload.has("role")) roleClaim = payload.opt("role");

                if (roleClaim instanceof JSONArray) {
                    roles = (JSONArray) roleClaim;
                } else if (roleClaim instanceof String) {
                    roles.put((String) roleClaim);
                }
            }
        } catch (Exception ignored) { /* be lenient */ }

        SharedPreferences.Editor e = prefs.edit();
        e.putString(KEY_TOKEN, jwt);
        e.putLong(KEY_EXP_EPOCH_SEC, exp);
        e.putString(KEY_ROLES_JSON, roles.toString());
        e.apply();
    }

    /** Returns the raw token or null. */
    public String getToken() {
        return prefs.getString(KEY_TOKEN, null);
    }

    /** Returns exp in epoch seconds (0 if unknown). */
    public long getExpiryEpochSeconds() {
        return prefs.getLong(KEY_EXP_EPOCH_SEC, 0L);
    }

    /** Returns true if a token exists and exp is in the future (with small skew). */
    public boolean isValid() {
        String t = getToken();
        if (t == null || t.isEmpty()) return false;
        long exp = getExpiryEpochSeconds();
        if (exp <= 0L) return false;
        long nowSec = System.currentTimeMillis() / 1000L;
        return exp > (nowSec + CLOCK_SKEW_SEC);
    }

    /** Milliseconds remaining until expiry (can be negative). */
    public long getMillisUntilExpiry() {
        long nowSec = System.currentTimeMillis() / 1000L;
        long exp = getExpiryEpochSeconds();
        return (exp - nowSec) * 1000L;
    }

    /** Return roles as a String[] (empty if none). */
    public String[] getRoles() {
        try {
            String json = prefs.getString(KEY_ROLES_JSON, "[]");
            JSONArray arr = new JSONArray(json);
            String[] out = new String[arr.length()];
            for (int i = 0; i < arr.length(); i++) {
                out[i] = arr.optString(i, "");
            }
            return out;
        } catch (Exception e) {
            return new String[0];
        }
    }

    /** Simple case-insensitive role check. */
    public boolean hasRole(String role) {
        if (role == null) return false;
        String[] rs = getRoles();
        for (String r : rs) {
            if (role.equalsIgnoreCase(r)) return true;
        }
        return false;
    }

    /** Adds Authorization: Bearer <token> header if present. */
    public void addAuthHeader(HttpURLConnection conn) {
        String t = getToken();
        if (t != null && !t.isEmpty()) {
            conn.setRequestProperty("Authorization", "Bearer " + t);
        }
    }

    /** Clear everything. */
    public void clear() {
        prefs.edit().clear().apply();
    }

    // ---- helpers ----

    private static JSONObject decodePayload(String jwt) {
        try {
            String[] parts = jwt.split("\\.");
            if (parts.length < 2) return null;
            byte[] decoded = Base64.decode(parts[1], Base64.URL_SAFE | Base64.NO_WRAP | Base64.NO_PADDING);
            String json = new String(decoded, StandardCharsets.UTF_8);
            return new JSONObject(json);
        } catch (Exception e) {
            return null;
        }
    }
}
