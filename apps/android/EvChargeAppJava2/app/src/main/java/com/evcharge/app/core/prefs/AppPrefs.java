package com.evcharge.app.core.prefs;

import android.content.Context;
import android.content.SharedPreferences;

import org.json.JSONArray;
import org.json.JSONException;

import java.util.ArrayList;
import java.util.List;

/**
 * Thin SharedPreferences wrapper for simple, non-sensitive app flags.
 * - Onboarding flag
 * - Currently active user id_key (Owner NIC or Operator email)
 * - Active role
 * - Last login timestamp (UTC ISO-8601 string)
 * - Active NIC helpers (Owner-only convenience)
 * - Operator snapshot (email, nic, stationIds, expiresAtUtc)
 *
 * JWT/token storage lives in JwtStore (separate).
 */
public final class AppPrefs {

    private static final String PREFS_NAME = "app_prefs";

    private static final String KEY_HAS_ONBOARDED   = "has_onboarded";
    private static final String KEY_ACTIVE_ID_KEY   = "active_id_key";   // NIC (Owner) or email (Operator)
    private static final String KEY_ACTIVE_ROLE     = "active_role";     // "Owner" or "Operator"
    private static final String KEY_LAST_LOGIN_UTC  = "last_login_utc";  // ISO-8601 string

    // Explicit NIC cache (Owner convenience)
    private static final String KEY_ACTIVE_NIC      = "active_nic";

    // ---- Operator snapshot ----
    private static final String KEY_OP_EMAIL              = "op_email";
    private static final String KEY_OP_NIC                = "op_nic";
    private static final String KEY_OP_STATION_IDS_JSON   = "op_station_ids_json";
    private static final String KEY_OP_EXPIRES_AT_UTC     = "op_expires_at_utc";

    private final SharedPreferences prefs;

    public AppPrefs(Context context) {
        Context appCtx = context.getApplicationContext();
        this.prefs = appCtx.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
    }

    // --- Onboarding ---
    public boolean hasOnboarded() { return prefs.getBoolean(KEY_HAS_ONBOARDED, false); }
    public void setHasOnboarded(boolean value) { prefs.edit().putBoolean(KEY_HAS_ONBOARDED, value).apply(); }

    // --- Active user snapshot (lightweight cache only) ---
    public void setActiveUser(String idKey, String role) {
        SharedPreferences.Editor e = prefs.edit();
        if (idKey == null) e.remove(KEY_ACTIVE_ID_KEY); else e.putString(KEY_ACTIVE_ID_KEY, idKey);
        if (role == null)  e.remove(KEY_ACTIVE_ROLE);   else e.putString(KEY_ACTIVE_ROLE, role);
        e.apply();
    }
    public String getActiveIdKey() { return prefs.getString(KEY_ACTIVE_ID_KEY, null); }
    public String getActiveRole()  { return prefs.getString(KEY_ACTIVE_ROLE, null); }

    // --- Last login timestamp (UTC ISO-8601) ---
    public void setLastLoginUtc(String isoUtc) {
        if (isoUtc == null) { prefs.edit().remove(KEY_LAST_LOGIN_UTC).apply(); }
        else { prefs.edit().putString(KEY_LAST_LOGIN_UTC, isoUtc).apply(); }
    }
    public String getLastLoginUtc() { return prefs.getString(KEY_LAST_LOGIN_UTC, null); }

    // --- Active NIC helpers (Owner-only convenience) ---
    public void setActiveNic(String nic) {
        if (nic == null || nic.trim().isEmpty()) { clearActiveNic(); return; }
        String trimmed = nic.trim();
        prefs.edit()
                .putString(KEY_ACTIVE_NIC, trimmed)
                .putString(KEY_ACTIVE_ID_KEY, trimmed)
                .apply();
    }
    public String getActiveNic() {
        String nic = prefs.getString(KEY_ACTIVE_NIC, null);
        if (nic != null && !nic.trim().isEmpty()) return nic.trim();
        String idKey = prefs.getString(KEY_ACTIVE_ID_KEY, null);
        if (idKey != null && !idKey.contains("@")) return idKey.trim();
        return null;
    }
    public void clearActiveNic() {
        SharedPreferences.Editor e = prefs.edit().remove(KEY_ACTIVE_NIC);
        String idKey = prefs.getString(KEY_ACTIVE_ID_KEY, null);
        if (idKey != null && !idKey.contains("@")) e.remove(KEY_ACTIVE_ID_KEY);
        e.apply();
    }

    // --- Operator snapshot (email, nic, stationIds, expiresAtUtc) ---
    public void setOperatorSnapshot(String email, String nic, JSONArray stationIds, String expiresAtUtc) {
        SharedPreferences.Editor e = prefs.edit();
        if (email == null || email.trim().isEmpty()) e.remove(KEY_OP_EMAIL); else e.putString(KEY_OP_EMAIL, email.trim());
        if (nic == null || nic.trim().isEmpty())     e.remove(KEY_OP_NIC);   else e.putString(KEY_OP_NIC, nic.trim());
        if (stationIds == null) e.remove(KEY_OP_STATION_IDS_JSON);
        else e.putString(KEY_OP_STATION_IDS_JSON, stationIds.toString());
        if (expiresAtUtc == null || expiresAtUtc.trim().isEmpty()) e.remove(KEY_OP_EXPIRES_AT_UTC);
        else e.putString(KEY_OP_EXPIRES_AT_UTC, expiresAtUtc.trim());
        e.apply();
    }

    public String getOperatorEmail() { return prefs.getString(KEY_OP_EMAIL, null); }
    public String getOperatorNic()   { return prefs.getString(KEY_OP_NIC, null); }
    public String getOperatorExpiresAtUtc() { return prefs.getString(KEY_OP_EXPIRES_AT_UTC, null); }

    public JSONArray getOperatorStationIdsJson() {
        try {
            String s = prefs.getString(KEY_OP_STATION_IDS_JSON, null);
            return (s == null || s.isEmpty()) ? null : new JSONArray(s);
        } catch (JSONException e) { return null; }
    }

    public List<String> getOperatorStationIdsList() {
        JSONArray j = getOperatorStationIdsJson();
        List<String> out = new ArrayList<>();
        if (j == null) return out;
        for (int i = 0; i < j.length(); i++) out.add(j.optString(i, ""));
        return out;
    }

    public void clearOperatorSnapshot() {
        prefs.edit()
                .remove(KEY_OP_EMAIL)
                .remove(KEY_OP_NIC)
                .remove(KEY_OP_STATION_IDS_JSON)
                .remove(KEY_OP_EXPIRES_AT_UTC)
                .apply();
    }

    // --- Clear helpers ---
    /** Clears only the active-user snapshot (keeps onboarding flag). */
    public void clearActiveUser() {
        prefs.edit()
                .remove(KEY_ACTIVE_ID_KEY)
                .remove(KEY_ACTIVE_ROLE)
                .remove(KEY_LAST_LOGIN_UTC)
                .remove(KEY_ACTIVE_NIC)
                .apply();
    }

    /** Clears everything but preserves hasOnboarded=true if it was already set. */
    public void clearAllExceptOnboarding() {
        boolean onboarded = hasOnboarded();
        prefs.edit().clear().apply();
        if (onboarded) setHasOnboarded(true);
    }
}
