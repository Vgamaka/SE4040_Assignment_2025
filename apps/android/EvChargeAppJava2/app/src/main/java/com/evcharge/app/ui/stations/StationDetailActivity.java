package com.evcharge.app.ui.stations;

import android.content.Intent;
import android.os.Bundle;
import android.widget.Button;
import android.widget.TextView;
import android.widget.Toast;

import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatActivity;

import com.evcharge.app.R;
import com.evcharge.app.core.net.ApiClient;
import com.evcharge.app.core.util.JsonUtils;
import com.evcharge.app.ui.booking.CreateBookingActivity;

import org.json.JSONArray;
import org.json.JSONObject;

public final class StationDetailActivity extends AppCompatActivity {

    private String stationId;
    private String stationName;
    private int defaultSlotMinutes = 60;

    private TextView tvName, tvMeta, tvSchedule, tvPricing;
    private Button btnBookHere;

    @Override
    protected void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_station_detail);

        stationId = getIntent().getStringExtra("stationId");

        tvName = findViewById(R.id.tvName);
        tvMeta = findViewById(R.id.tvMeta);
        tvSchedule = findViewById(R.id.tvSchedule);
        tvPricing = findViewById(R.id.tvPricing);
        btnBookHere = findViewById(R.id.btnBookHere);
        btnBookHere.setOnClickListener(v -> {
            Intent i = new Intent(this, CreateBookingActivity.class);
            i.putExtra("presetStationId", stationId);
            i.putExtra("presetStationName", stationName);
            i.putExtra("presetMinutes", defaultSlotMinutes);
            startActivity(i);
        });

        load();
    }

    private void load() {
        new Thread(() -> {
            ApiClient api = new ApiClient(getApplicationContext());
            try {
                // Detail
                com.evcharge.app.core.net.HttpClient.Response d = api.stationDetailRaw(stationId);
                if (!(d.code >= 200 && d.code < 300) || d.jsonObject == null) {
                    runOnUiThread(() -> toast("Detail failed: " + d.code));
                    return;
                }
                JSONObject detail = d.jsonObject;

                // Schedule
                com.evcharge.app.core.net.HttpClient.Response s = api.stationScheduleRaw(stationId);
                JSONObject schedule = s.jsonObject; // may be null

                runOnUiThread(() -> render(detail, schedule));
            } catch (Exception e) {
                runOnUiThread(() -> toast("Error: " + e.getMessage()));
            }
        }).start();
    }

    private void render(JSONObject detail, JSONObject schedule) {
        // Names with both API-style and DB-style keys
        stationName = or(JsonUtils.optString(detail, "name"), JsonUtils.optString(detail, "Name"));
        String type = or(JsonUtils.optString(detail, "type"), JsonUtils.optString(detail, "Type"));
        int connectors = detail.optInt("connectors",
                detail.optInt("Connectors", -1));
        String status = or(JsonUtils.optString(detail, "status"), JsonUtils.optString(detail, "Status"));
        defaultSlotMinutes = detail.optInt("defaultSlotMinutes",
                detail.optInt("DefaultSlotMinutes", 60));

        tvName.setText(stationName != null ? stationName : "Station");
        String meta = "";
        if (type != null) meta += "Type: " + type;
        if (connectors >= 0) meta += (meta.isEmpty() ? "" : " · ") + "Connectors: " + connectors;
        if (status != null) meta += (meta.isEmpty() ? "" : " · ") + "Status: " + status;
        tvMeta.setText(meta);

        // Pricing: handle either plain numbers or Mongo $numberDecimal
        JSONObject pricing = detail.optJSONObject("pricing");
        if (pricing == null) pricing = detail.optJSONObject("Pricing");
        if (pricing != null) {
            String model = or(JsonUtils.optString(pricing, "model"), JsonUtils.optString(pricing, "Model"));
            double base = readDecimal(pricing, "base", "Base");
            double perHour = readDecimal(pricing, "perHour", "PerHour");
            double perKwh = readDecimal(pricing, "perKwh", "PerKwh");
            double taxPct = readDecimal(pricing, "taxPct", "TaxPct");
            String p = "Pricing (" + (model != null ? model : "n/a") + "): "
                    + "Base " + stripTrailing(base) + "; "
                    + "PerHour " + stripTrailing(perHour) + "; "
                    + "PerKwh " + stripTrailing(perKwh) + "; "
                    + "Tax " + stripTrailing(taxPct) + "%";
            tvPricing.setText(p);
        } else {
            tvPricing.setText("Pricing: n/a");
        }

        if (schedule != null) {
            StringBuilder sb = new StringBuilder();
            JSONObject weekly = schedule.optJSONObject("weekly");
            if (weekly != null) {
                sb.append("Weekly Hours:\n");
                String[] days = {"mon","tue","wed","thu","fri","sat","sun"};
                String[] labels = {"Mon","Tue","Wed","Thu","Fri","Sat","Sun"};
                for (int i=0;i<days.length;i++) {
                    JSONArray ar = weekly.optJSONArray(days[i]);
                    if (ar != null && ar.length() > 0) {
                        sb.append(labels[i]).append(": ");
                        for (int j=0;j<ar.length();j++) {
                            JSONObject r = ar.optJSONObject(j);
                            if (r != null) {
                                if (j>0) sb.append(", ");
                                sb.append(r.optString("start")).append("-").append(r.optString("end"));
                            }
                        }
                        sb.append("\n");
                    } else {
                        sb.append(labels[i]).append(": closed\n");
                    }
                }
            }
            JSONArray ex = schedule.optJSONArray("exceptions");
            if (ex != null && ex.length() > 0) {
                sb.append("\nExceptions:\n");
                for (int i=0;i<ex.length();i++) {
                    JSONObject e = ex.optJSONObject(i);
                    if (e == null) continue;
                    String date = e.optString("date");
                    boolean closed = e.optBoolean("closed", false);
                    sb.append(date).append(": ").append(closed ? "Closed" : "Open").append("\n");
                }
            }
            tvSchedule.setText(sb.toString().trim());
        } else {
            tvSchedule.setText("No schedule configured.");
        }
    }

    private static String or(String a, String b) { return a != null ? a : b; }

    private static double readDecimal(JSONObject o, String lower, String upper) {
        if (o == null) return 0;
        if (o.has(lower)) {
            Object v = o.opt(lower);
            if (v instanceof Number) return ((Number)v).doubleValue();
            if (v instanceof JSONObject) {
                String s = ((JSONObject) v).optString("$numberDecimal", null);
                if (s != null) try { return Double.parseDouble(s); } catch (Exception ignored){}
            }
        }
        if (o.has(upper)) {
            Object v = o.opt(upper);
            if (v instanceof Number) return ((Number)v).doubleValue();
            if (v instanceof JSONObject) {
                String s = ((JSONObject) v).optString("$numberDecimal", null);
                if (s != null) try { return Double.parseDouble(s); } catch (Exception ignored){}
            }
        }
        return 0;
    }

    private static String stripTrailing(double d) {
        String s = String.valueOf(d);
        if (s.endsWith(".0")) return s.substring(0, s.length()-2);
        return s;
    }

    private void toast(String m){ Toast.makeText(this, m, Toast.LENGTH_SHORT).show(); }
}
