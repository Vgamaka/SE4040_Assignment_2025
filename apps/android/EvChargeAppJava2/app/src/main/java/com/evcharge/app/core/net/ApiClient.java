package com.evcharge.app.core.net;

import android.content.Context;

import com.evcharge.app.BuildConfig;
import com.evcharge.app.core.prefs.AppPrefs;
import com.evcharge.app.core.security.JwtStore;
import com.evcharge.app.core.util.JsonUtils;

import org.json.JSONObject;

import java.util.HashMap;
import java.util.Map;
import java.util.UUID;

public final class ApiClient {

  private final Context appCtx;
  private final String baseUrl;
  private final HttpClient http;
  private final JwtStore jwt;
  private final AppPrefs prefs;

  private static final long CLOCK_SKEW_MS = 30_000L; // 30s skew (aligns with JwtStore)

  public ApiClient(Context context) {
    this.appCtx = context.getApplicationContext();
    this.baseUrl = BuildConfig.BASE_URL.endsWith("/")
      ? BuildConfig.BASE_URL.substring(0, BuildConfig.BASE_URL.length() - 1)
      : BuildConfig.BASE_URL;
    this.http = new HttpClient();
    this.jwt = new JwtStore(appCtx);
    this.prefs = new AppPrefs(appCtx);
  }

  // ===== Models =====
  public static final class Result {
    public final boolean ok; public final int code; public final String message; public final String body; public final JSONObject json;
    public Result(boolean ok, int code, String message, String body, JSONObject json) { this.ok = ok; this.code = code; this.message = message; this.body = body; this.json = json; }
    public static Result success(HttpClient.Response r) { String msg = (r.jsonObject != null ? JsonUtils.extractMessage(r.jsonObject) : null); return new Result(true, r.code, msg, r.body, r.jsonObject); }
    public static Result failure(HttpClient.Response r) { String msg = (r.jsonObject != null ? JsonUtils.extractMessage(r.jsonObject) : null); if (msg == null || msg.isEmpty()) msg = r.body; return new Result(false, r.code, msg, r.body, r.jsonObject); }
  }
  public static final class LoginResult { public final boolean ok; public final int code; public final String jwt; public final String message;
    public LoginResult(boolean ok, int code, String jwt, String message) { this.ok = ok; this.code = code; this.jwt = jwt; this.message = message; } }

  // ===== Helpers =====
  private String url(String path) { if (path == null || path.isEmpty()) return baseUrl; if (path.startsWith("/")) return baseUrl + path; return baseUrl + "/" + path; }

  private Map<String,String> authHeaders() {
    Map<String,String> h = new HashMap<>();
    String token = jwt.getToken();
    if (token != null && !token.isEmpty()) h.put("Authorization", "Bearer " + token);
    return h;
  }

  /** JSON + Authorization headers (Accept is set in HttpClient; Content-Type set when body exists) */
  private Map<String,String> jsonAuthHeaders() {
    Map<String,String> h = HttpClient.headers();
    String token = jwt.getToken();
    if (token != null && !token.isEmpty()) h.put("Authorization", "Bearer " + token);
    return h;
  }

  private static void addIdempotencyKey(Map<String,String> h) { if (h != null) h.put("Idempotency-Key", UUID.randomUUID().toString()); }

  // ---- Auth freshness guard ----
  /** Public helper for activities to query current freshness quickly. */
  public boolean isAuthFresh() { return authFreshnessGuard() == null; }

  /** Returns a non-null 401 Result if auth is stale; otherwise null. */
  private Result authFreshnessGuard() {
    // 1) Check JWT exp from token
    if (!jwt.isValid()) {
      return new Result(false, 401, "Session expired — please log in again.", null, null);
    }

    // 2) Check server-issued expiry we cached at login
    String role = prefs.getActiveRole();
    String expiresAtUtc = null;
    if ("Operator".equalsIgnoreCase(role)) {
      expiresAtUtc = prefs.getOperatorExpiresAtUtc();         // set from login response
    } else if ("Owner".equalsIgnoreCase(role)) {
      expiresAtUtc = prefs.getLastLoginUtc();                 // we mirrored expiresAtUtc here on login
    }

    if (expiresAtUtc != null && !expiresAtUtc.trim().isEmpty()) {
      Long ms = tryParseIsoToMillis(expiresAtUtc.trim());
      if (ms != null) {
        long now = System.currentTimeMillis();
        if (ms <= (now + CLOCK_SKEW_MS)) {
          return new Result(false, 401, "Session expired — please log in again.", null, null);
        }
      }
      // If unparsable, be lenient (fall back to JWT validity only)
    }

    // 3) Optional: reject any unknown roles explicitly
    if (role != null && !role.equalsIgnoreCase("Owner") && !role.equalsIgnoreCase("Operator")) {
      return new Result(false, 403, "Unsupported role", null, null);
    }

    return null;
  }

  private static Long tryParseIsoToMillis(String iso) {
    try {
      // Handles 2025-10-08T16:14:07.059889Z and variants
      return java.time.Instant.parse(iso).toEpochMilli();
    } catch (Exception ignored1) {
      try {
        return java.time.OffsetDateTime.parse(iso).toInstant().toEpochMilli();
      } catch (Exception ignored2) {
        try {
          // best-effort fallback for plain seconds (append Z if missing)
          String s = iso.endsWith("Z") ? iso : (iso + "Z");
          java.text.SimpleDateFormat f = new java.text.SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss'Z'", java.util.Locale.US);
          f.setTimeZone(java.util.TimeZone.getTimeZone("UTC"));
          java.util.Date d = f.parse(s);
          return (d != null) ? d.getTime() : null;
        } catch (Exception ignored3) {
          return null;
        }
      }
    }
  }

  // ===== Auth =====
  public LoginResult login(String username, String password) {
    try {
      JSONObject body = new JSONObject(); body.put("username", username); body.put("password", password);
      HttpClient.Response r = http.post(url("/api/Auth/login"), body, HttpClient.headers());
      if (r.is2xx()) {
        String token = null;
        if (r.body != null) {
          String raw = r.body.trim();
          if (raw.length() >= 2 && raw.charAt(0) == '"' && raw.charAt(raw.length()-1) == '"') raw = raw.substring(1, raw.length()-1);
          if (JsonUtils.looksLikeJwt(raw)) token = raw;
        }
        if (token == null && r.jsonObject != null) {
          token = JsonUtils.optString(r.jsonObject, "token");
          if (token == null) token = JsonUtils.optString(r.jsonObject, "accessToken");
          if (token == null) token = JsonUtils.optString(r.jsonObject, "jwt");
        }
        if (token != null && JsonUtils.looksLikeJwt(token)) {
          jwt.save(token);
          // Persist user snapshot based on response (Operator-friendly)
          try {
            JSONObject j = r.jsonObject;
            String expiresAtUtc = (j != null ? JsonUtils.optString(j, "expiresAtUtc") : null);
            String nic = (j != null ? JsonUtils.optString(j, "nic") : null);
            String email = (j != null ? JsonUtils.optString(j, "email") : null);
            org.json.JSONArray roles = (j != null ? j.optJSONArray("roles") : null);
            org.json.JSONArray opStationIds = (j != null ? j.optJSONArray("operatorStationIds") : null);

            boolean isOperator = false;
            if (roles != null) {
              for (int i = 0; i < roles.length(); i++) {
                String role = roles.optString(i, "");
                if ("Operator".equalsIgnoreCase(role)) { isOperator = true; break; }
              }
            }
            // Heuristic fallback: email logins are operators in this app
            if (!isOperator && username != null && username.contains("@")) isOperator = true;

            if (isOperator) {
              // Active identity: email for operator
              prefs.setActiveUser((email != null ? email : username), "Operator");
              prefs.setOperatorSnapshot(email, nic, opStationIds, expiresAtUtc);
              if (expiresAtUtc != null) prefs.setLastLoginUtc(expiresAtUtc);
              // Ensure Owner NIC cache doesn't leak across roles
              prefs.clearActiveNic();
            } else {
              // Owner path: mirror NIC behavior
              if (username != null && username.indexOf('@') < 0) {
                prefs.setActiveUser(username.trim(), "Owner");
                prefs.setActiveNic(username.trim());
              } else {
                prefs.setActiveUser(username, "Owner");
              }
              if (expiresAtUtc != null) prefs.setLastLoginUtc(expiresAtUtc);
              prefs.clearOperatorSnapshot();
            }
          } catch (Exception ignored) {}
          return new LoginResult(true, r.code, token, "OK");
        }
        return new LoginResult(false, r.code, null, "Login succeeded but token not found in response.");
      }
      String msg = (r.jsonObject != null ? JsonUtils.extractMessage(r.jsonObject) : r.body);
      return new LoginResult(false, r.code, null, (msg != null ? msg : "Login failed"));
    } catch (Exception e) { return new LoginResult(false, 0, null, "Network error: " + e.getMessage()); }
  }

  // ===== Owner registration =====
  public Result registerOwner(String nic, String fullName, String email, String phone, String password, String addressLine1, String addressLine2, String city) {
    try {
      JSONObject b = new JSONObject();
      b.put("nic", nic); b.put("fullName", fullName); b.put("email", email); b.put("phone", phone); b.put("password", password);
      b.put("addressLine1", addressLine1); b.put("addressLine2", addressLine2); b.put("city", city);
      HttpClient.Response r = http.post(url("/api/EvOwner"), b, HttpClient.headers()); // anonymous endpoint: no auth
      return r.is2xx() ? Result.success(r) : Result.failure(r);
    } catch (Exception e) { return new Result(false, 0, "Network error: " + e.getMessage(), null, null); }
  }

  // ===== Generic authed helpers (now guarded) =====
  public Result getAuthed(String path) {
    Result g = authFreshnessGuard(); if (g != null) return g;
    try { HttpClient.Response r = http.get(url(path), jsonAuthHeaders()); return r.is2xx()? Result.success(r): Result.failure(r); }
    catch (Exception e){ return new Result(false,0,"Network error: "+e.getMessage(),null,null); }
  }

  public Result postAuthed(String path, JSONObject body) {
    Result g = authFreshnessGuard(); if (g != null) return g;
    try { HttpClient.Response r = http.post(url(path), body, jsonAuthHeaders()); return r.is2xx()? Result.success(r): Result.failure(r); }
    catch (Exception e){ return new Result(false,0,"Network error: "+e.getMessage(),null,null); }
  }

  public Result postAuthedIdempotent(String path, JSONObject body) {
    Result g = authFreshnessGuard(); if (g != null) return g;
    try { Map<String,String> h = jsonAuthHeaders(); addIdempotencyKey(h); HttpClient.Response r = http.post(url(path), body, h); return r.is2xx()? Result.success(r): Result.failure(r); }
    catch (Exception e){ return new Result(false,0,"Network error: "+e.getMessage(),null,null); }
  }

  public Result putAuthed(String path, JSONObject body) {
    Result g = authFreshnessGuard(); if (g != null) return g;
    try { HttpClient.Response r = http.put(url(path), body, jsonAuthHeaders()); return r.is2xx()? Result.success(r): Result.failure(r); }
    catch (Exception e){ return new Result(false,0,"Network error: "+e.getMessage(),null,null); }
  }

  public Result deleteAuthed(String path, JSONObject body) {
    Result g = authFreshnessGuard(); if (g != null) return g;
    try { HttpClient.Response r = http.delete(url(path), body, jsonAuthHeaders()); return r.is2xx()? Result.success(r): Result.failure(r); }
    catch (Exception e){ return new Result(false,0,"Network error: "+e.getMessage(),null,null); }
  }

  // ===== Bookings =====
  public HttpClient.Response bookingMineRaw() throws Exception {
    // Raw method kept as-is (callers should verify isAuthFresh() first)
    return http.get(url("/api/Booking/mine"), jsonAuthHeaders());
  }
  public Result bookingDetail(String bookingId) {
    Result g = authFreshnessGuard(); if (g != null) return g;
    try { HttpClient.Response r = http.get(url("/api/Booking/"+bookingId), jsonAuthHeaders()); return r.is2xx()? Result.success(r): Result.failure(r);}
    catch (Exception e){ return new Result(false,0,"Network error: "+e.getMessage(),null,null);}
  }
  public Result bookingCreate(JSONObject payload) {
    Result g = authFreshnessGuard(); if (g != null) return g;
    try { Map<String,String> h = jsonAuthHeaders(); addIdempotencyKey(h); HttpClient.Response r = http.post(url("/api/Booking"), payload, h); return r.is2xx()? Result.success(r): Result.failure(r);}
    catch (Exception e){ return new Result(false,0,"Network error: "+e.getMessage(),null,null);}
  }
  public Result bookingModify(String bookingId, JSONObject payload) {
    Result g = authFreshnessGuard(); if (g != null) return g;
    try { HttpClient.Response r = http.put(url("/api/Booking/"+bookingId), payload, jsonAuthHeaders()); return r.is2xx()? Result.success(r): Result.failure(r);}
    catch (Exception e){ return new Result(false,0,"Network error: "+e.getMessage(),null,null);}
  }
  public Result bookingCancel(String bookingId) {
    Result g = authFreshnessGuard(); if (g != null) return g;
    try { HttpClient.Response r = http.post(url("/api/Booking/"+bookingId+"/cancel"), new JSONObject(), jsonAuthHeaders()); return r.is2xx()? Result.success(r): Result.failure(r);}
    catch (Exception e){ return new Result(false,0,"Network error: "+e.getMessage(),null,null);}
  }
  public Result qrIssue(String bookingId) {
    Result g = authFreshnessGuard(); if (g != null) return g;
    try {
      HttpClient.Response r = http.post(url("/api/Qr/issue/"+bookingId), new JSONObject(), jsonAuthHeaders());
      if (!r.is2xx()) return Result.failure(r);
      String token = null;
      if (r.body != null) {
        String raw = r.body.trim();
        if (raw.length()>=2 && raw.charAt(0)=='"' && raw.charAt(raw.length()-1)=='"') raw = raw.substring(1, raw.length()-1);
        if (JsonUtils.looksLikeJwt(raw) || raw.length()>16) token = raw;
      }
      if (token == null && r.jsonObject != null) {
        token = JsonUtils.optString(r.jsonObject, "token");
        if (token == null) token = JsonUtils.optString(r.jsonObject, "qrToken");
      }
      if (token != null) {
        JSONObject out = new JSONObject(); out.put("qrToken", token);
        HttpClient.Response fake = new HttpClient.Response(r.code, out.toString(), out, null, r.headers);
        return Result.success(fake);
      }
      return Result.failure(r);
    } catch (Exception e){ return new Result(false,0,"Network error: "+e.getMessage(),null,null); }
  }

  // ===== Stations =====
  public HttpClient.Response stationsAllRaw() throws Exception {
    return http.get(url("/api/Station"), HttpClient.headers());
  }

  /** GET /api/Station/nearby?lat=..&lng=..&radiusKm=..&type=AC|DC → JSONArray */
  public HttpClient.Response stationsNearbyRaw(double lat, double lng, double radiusKm, String type) throws Exception {
    String q = String.format(java.util.Locale.US,
      "/api/Station/nearby?lat=%f&lng=%f&radiusKm=%f%s",
      lat, lng, radiusKm, (type != null && !type.isEmpty() ? "&type=" + java.net.URLEncoder.encode(type, "UTF-8") : ""));
    return http.get(url(q), HttpClient.headers());
  }

  /** GET /api/Station/{id} → JSONObject (public) */
  public HttpClient.Response stationDetailRaw(String id) throws Exception {
    return http.get(url("/api/Station/" + id), HttpClient.headers());
  }

  /** GET /api/Station/{id}/schedule → JSONObject (public) */
  public HttpClient.Response stationScheduleRaw(String id) throws Exception {
    return http.get(url("/api/Station/" + id + "/schedule"), HttpClient.headers());
  }

  // ===== Owners (Profile) =====
  /** GET /api/EvOwner/{nic} */
  public Result ownerGet(String nic) {
    Result g = authFreshnessGuard(); if (g != null) return g;
    try {
      HttpClient.Response r = http.get(url("/api/EvOwner/" + nic), jsonAuthHeaders());
      return r.is2xx()? Result.success(r): Result.failure(r);
    } catch (Exception e){ return new Result(false,0,"Network error: "+e.getMessage(),null,null); }
  }

  /** PUT /api/EvOwner/{nic} with { fullName, email, phone, addressLine1, addressLine2, city } */
  public Result ownerUpdate(String nic, JSONObject body) {
    Result g = authFreshnessGuard(); if (g != null) return g;
    try {
      HttpClient.Response r = http.put(url("/api/EvOwner/" + nic), body, jsonAuthHeaders());
      return r.is2xx()? Result.success(r): Result.failure(r);
    } catch (Exception e){ return new Result(false,0,"Network error: "+e.getMessage(),null,null); }
  }

  /** PUT /api/EvOwner/{nic}/deactivate */
  public Result ownerDeactivate(String nic) {
    Result g = authFreshnessGuard(); if (g != null) return g;
    try {
      HttpClient.Response r = http.put(url("/api/EvOwner/" + nic + "/deactivate"), new JSONObject(), jsonAuthHeaders());
      return r.is2xx()? Result.success(r): Result.failure(r);
    } catch (Exception e){ return new Result(false,0,"Network error: "+e.getMessage(),null,null); }
  }

  // ===== Operator =====
  /** GET /api/Operator/inbox?date=YYYY-MM-DD → JSONArray in body */
  public Result operatorInbox(String ymd) {
    Result g = authFreshnessGuard(); if (g != null) return g;
    try {
      String q = "/api/Operator/inbox" + (ymd != null && !ymd.isEmpty() ? ("?date=" + ymd) : "");
      HttpClient.Response r = http.get(url(q), jsonAuthHeaders());
      return r.is2xx()? Result.success(r): Result.failure(r);
    } catch (Exception e) { return new Result(false,0,"Network error: "+e.getMessage(),null,null); }
  }

  /** GET /api/Station/{id} with Authorization (operator flow) */
  public Result stationDetailAuthed(String stationId) {
    Result g = authFreshnessGuard(); if (g != null) return g;
    try {
      HttpClient.Response r = http.get(url("/api/Station/" + stationId), jsonAuthHeaders());
      return r.is2xx()? Result.success(r): Result.failure(r);
    } catch (Exception e) { return new Result(false,0,"Network error: "+e.getMessage(),null,null); }
  }

  // ===== Notifications =====
  /** GET /api/Notifications?unreadOnly=true&page=1&pageSize=20 → JSONObject (raw) */
  public HttpClient.Response notificationsListRaw(boolean unreadOnly, int page, int pageSize) throws Exception {
    // Raw method kept as-is; callers should check isAuthFresh() first
    String q = String.format(java.util.Locale.US,
      "/api/Notifications?unreadOnly=%s&page=%d&pageSize=%d",
      unreadOnly ? "true" : "false", Math.max(1, page), Math.max(1, pageSize));
    return http.get(url(q), jsonAuthHeaders());
  }

  /** PUT /api/Notifications/{id}/read → 204 */
  public Result notificationMarkRead(String id) {
    Result g = authFreshnessGuard(); if (g != null) return g;
    try {
      HttpClient.Response r = http.put(url("/api/Notifications/" + id + "/read"), new JSONObject(), jsonAuthHeaders());
      return r.is2xx() ? Result.success(r) : Result.failure(r);
    } catch (Exception e) { return new Result(false,0,"Network error: " + e.getMessage(),null,null); }
  }

  /** PUT /api/Notifications/read-all → { "updated": N } */
  public Result notificationsMarkAllRead() {
    Result g = authFreshnessGuard(); if (g != null) return g;
    try {
      HttpClient.Response r = http.put(url("/api/Notifications/read-all"), new JSONObject(), jsonAuthHeaders());
      return r.is2xx() ? Result.success(r) : Result.failure(r);
    } catch (Exception e) { return new Result(false,0,"Network error: " + e.getMessage(),null,null); }
  }
}
