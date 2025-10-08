package com.evcharge.app.ui.booking;

import android.app.DatePickerDialog;
import android.app.TimePickerDialog;
import android.os.Bundle;
import android.view.View;
import android.widget.AdapterView;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.EditText;
import android.widget.Spinner;
import android.widget.Toast;

import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatActivity;

import com.evcharge.app.R;
import com.evcharge.app.core.net.ApiClient;
import com.evcharge.app.core.util.JsonUtils;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Calendar;
import java.util.List;

public final class CreateBookingActivity extends AppCompatActivity {

  private static final class StationRow {
    final String id; final String name;
    StationRow(String id, String name){ this.id = id; this.name = (name != null ? name : id); }
    @Override public String toString(){ return name; }
  }

  private Spinner spStation, spMinutes;
  private Button btnPickDate, btnPickTime, btnReview;
  private EditText etNotes;

  private final List<StationRow> stations = new ArrayList<>();
  private ArrayAdapter<StationRow> stationAdapter;

  private final Integer[] allowedMinutes = new Integer[]{30,45,60,90,120};
  private ArrayAdapter<Integer> minutesAdapter;

  // picked local date/time
  private final Calendar pickCal = Calendar.getInstance();

  // presets from StationDetail → CreateBooking
  private String presetStationId = null;
  private String presetStationName = null;
  private Integer presetMinutes = null;

  // track if minutes are fixed by server
  private boolean minutesFixed = false;

  @Override
  protected void onCreate(@Nullable Bundle savedInstanceState) {
    super.onCreate(savedInstanceState);
    setContentView(R.layout.activity_create_booking);

    spStation = findViewById(R.id.spStation);
    spMinutes = findViewById(R.id.spMinutes);
    btnPickDate = findViewById(R.id.btnPickDate);
    btnPickTime = findViewById(R.id.btnPickTime);
    btnReview = findViewById(R.id.btnReview);
    etNotes = findViewById(R.id.etNotes);

    // Read presets (optional)
    if (getIntent() != null) {
      presetStationId = getIntent().getStringExtra("presetStationId");
      presetStationName = getIntent().getStringExtra("presetStationName");
      if (getIntent().hasExtra("presetMinutes")) {
        int pm = getIntent().getIntExtra("presetMinutes", -1);
        if (pm > 0) presetMinutes = pm;
      }
    }

    stationAdapter = new ArrayAdapter<>(this, android.R.layout.simple_spinner_dropdown_item, stations);
    spStation.setAdapter(stationAdapter);

    minutesAdapter = new ArrayAdapter<>(this, android.R.layout.simple_spinner_dropdown_item, allowedMinutes);
    spMinutes.setAdapter(minutesAdapter);
    spMinutes.setSelection(defaultMinutesIndex()); // default 60 or closest to preset
    spMinutes.setEnabled(true);
    minutesFixed = false;

    // When the user changes station, fetch DefaultSlotMinutes and lock the duration if present
    spStation.setOnItemSelectedListener(new AdapterView.OnItemSelectedListener() {
      @Override public void onItemSelected(AdapterView<?> parent, View view, int position, long id) {
        StationRow sel = (StationRow) spStation.getSelectedItem();
        if (sel != null) fetchAndApplySlot(sel.id);
      }
      @Override public void onNothingSelected(AdapterView<?> parent) { /* no-op */ }
    });

    btnPickDate.setOnClickListener(v -> showDatePicker());
    btnPickTime.setOnClickListener(v -> showTimePicker());
    btnReview.setOnClickListener(v -> goReview());

    loadStations();
    updateDateTimeButtons();
  }

  /** choose 60 by default, or the closest to presetMinutes if provided */
  private int defaultMinutesIndex() {
    int defaultIndex = 2; // 60
    if (presetMinutes != null) {
      int bestIdx = 0; int bestDiff = Integer.MAX_VALUE;
      for (int i=0;i<allowedMinutes.length;i++) {
        int diff = Math.abs(allowedMinutes[i] - presetMinutes);
        if (diff < bestDiff) { bestDiff = diff; bestIdx = i; }
      }
      defaultIndex = bestIdx;
    }
    return defaultIndex;
  }

  private void loadStations() {
    new Thread(() -> {
      try {
        ApiClient api = new ApiClient(getApplicationContext());
        com.evcharge.app.core.net.HttpClient.Response r = api.stationsAllRaw();

        if (!(r.code >= 200 && r.code < 300)) {
          runOnUiThread(() -> toast("Stations failed: " + r.code));
          return;
        }

        // Accept either: [ ... ]  OR  { items/data/results/stations: [ ... ] }
        JSONArray arr = r.jsonArray;
        JSONObject root = r.jsonObject;

        if (arr == null) {
          if (root == null && r.body != null && !r.body.trim().isEmpty()) {
            // last resort: try to parse as array first, then object
            try { arr = new JSONArray(r.body); } catch (JSONException ignored) {
              try { root = new JSONObject(r.body); } catch (JSONException ignored2) { /* leave null */ }
            }
          }
          if (arr == null && root != null) {
            // common wrappers
            String[] keys = new String[]{"items","data","results","stations","value"};
            for (String k : keys) {
              JSONArray candidate = root.optJSONArray(k);
              if (candidate != null) { arr = candidate; break; }
            }
            // if still null, try first array we can find
            if (arr == null) {
              for (java.util.Iterator<String> it = root.keys(); it.hasNext(); ) {
                String k = it.next();
                Object v = root.opt(k);
                if (v instanceof JSONArray) { arr = (JSONArray) v; break; }
              }
            }
          }
        }

        if (arr == null) {
          String sample = (r.body != null ? r.body : "no body");
          final String msg = "Stations parse error (200). Shape not an array. Sample: " + sample;
          runOnUiThread(() -> toast(msg.length() > 200 ? msg.substring(0,200) + "…" : msg));
          return;
        }

        List<StationRow> tmp = new ArrayList<>();
        for (int i=0;i<arr.length();i++){
          JSONObject o = arr.optJSONObject(i); if (o==null) continue;
          String id = JsonUtils.optString(o, "id"); if (id==null) id = JsonUtils.optString(o, "stationId");
          String name = JsonUtils.optString(o, "name"); if (name==null) name = JsonUtils.optString(o, "stationName");
          if (id!=null) tmp.add(new StationRow(id,name));
        }

        List<StationRow> finalList = tmp;
        runOnUiThread(() -> {
          stations.clear();
          stations.addAll(finalList);

          // If a preset station is supplied but not found in the list, add it as a temporary option.
          if (presetStationId != null) {
            int foundIdx = -1;
            for (int i = 0; i < stations.size(); i++) {
              if (presetStationId.equals(stations.get(i).id)) { foundIdx = i; break; }
            }
            if (foundIdx == -1) {
              stations.add(0, new StationRow(presetStationId, presetStationName != null ? presetStationName : presetStationId));
            }
          }

          stationAdapter.notifyDataSetChanged();

          // Preselect & maybe lock the spinner if presetStationId exists
          int idx = 0;
          if (presetStationId != null) {
            for (int i=0;i<stations.size();i++) {
              if (presetStationId.equals(stations.get(i).id)) { idx = i; break; }
            }
            spStation.setSelection(idx);
            spStation.setEnabled(false); // station fixed when coming from StationDetail
          }

          // Fetch slot for the initially selected station (handles both preset & default selection)
          StationRow sel = (StationRow) spStation.getSelectedItem();
          if (sel != null) fetchAndApplySlot(sel.id);

          toast("Loaded " + stations.size() + " station(s)");
        });
      } catch (Exception e) {
        runOnUiThread(() -> toast("Network error: " + e.getMessage()));
      }
    }).start();
  }

  /** Fetch DefaultSlotMinutes for a station and apply it to the minutes spinner. */
  private void fetchAndApplySlot(String stationId) {
    new Thread(() -> {
      Integer slot = null;
      try {
        ApiClient api = new ApiClient(getApplicationContext());
        com.evcharge.app.core.net.HttpClient.Response d = api.stationDetailRaw(stationId);
        if (d.jsonObject != null) {
          int s1 = d.jsonObject.optInt("defaultSlotMinutes", 0);
          int s2 = d.jsonObject.optInt("DefaultSlotMinutes", 0);
          int s = (s1 > 0 ? s1 : s2);
          if (s > 0) slot = s;
        }
      } catch (Exception ignored) {}

      final Integer slotFinal = slot;
      runOnUiThread(() -> applySlotMinutes(slotFinal));
    }).start();
  }

  /** Apply minutes spinner contents depending on slot (fixed if provided, else flexible). */
  private void applySlotMinutes(Integer slotFromServer) {
    if (slotFromServer != null && slotFromServer > 0) {
      // Lock to a single option (e.g., 60) and disable spinner
      Integer[] single = new Integer[]{slotFromServer};
      minutesAdapter = new ArrayAdapter<>(this, android.R.layout.simple_spinner_dropdown_item, single);
      spMinutes.setAdapter(minutesAdapter);
      spMinutes.setSelection(0);
      spMinutes.setEnabled(false);
      minutesFixed = true;
    } else {
      // Keep the flexible list and enable spinner
      minutesAdapter = new ArrayAdapter<>(this, android.R.layout.simple_spinner_dropdown_item, allowedMinutes);
      spMinutes.setAdapter(minutesAdapter);
      spMinutes.setSelection(defaultMinutesIndex());
      spMinutes.setEnabled(true);
      minutesFixed = false;
    }
  }

  private void showDatePicker() {
    int y = pickCal.get(Calendar.YEAR), m = pickCal.get(Calendar.MONTH), d = pickCal.get(Calendar.DAY_OF_MONTH);
    new DatePickerDialog(this, (view, year, month, dayOfMonth) -> {
      pickCal.set(Calendar.YEAR, year);
      pickCal.set(Calendar.MONTH, month);
      pickCal.set(Calendar.DAY_OF_MONTH, dayOfMonth);
      updateDateTimeButtons();
    }, y, m, d).show();
  }

  private void showTimePicker() {
    int h = pickCal.get(Calendar.HOUR_OF_DAY), min = pickCal.get(Calendar.MINUTE);
    new TimePickerDialog(this, (view, hourOfDay, minute) -> {
      pickCal.set(Calendar.HOUR_OF_DAY, hourOfDay);
      pickCal.set(Calendar.MINUTE, minute);
      updateDateTimeButtons();
    }, h, min, true).show();
  }

  private void updateDateTimeButtons() {
    btnPickDate.setText(formatLocalDate(pickCal)); // YYYY-MM-DD
    btnPickTime.setText(formatLocalHm(pickCal));   // HH:mm
  }

  private static String formatLocalDate(Calendar c) {
    return String.format("%04d-%02d-%02d",
      c.get(Calendar.YEAR), c.get(Calendar.MONTH)+1, c.get(Calendar.DAY_OF_MONTH));
  }

  private static String formatLocalHm(Calendar c) {
    return String.format("%02d:%02d",
      c.get(Calendar.HOUR_OF_DAY), c.get(Calendar.MINUTE));
  }

  private void goReview() {
    if (stations.isEmpty()) { toast("Pick a station"); return; }
    StationRow sel = (StationRow) spStation.getSelectedItem();
    if (sel == null) { toast("Pick a station"); return; }

    Integer minutesObj = (Integer) spMinutes.getSelectedItem();
    if (minutesObj == null) { toast("Pick a duration"); return; }
    int minutes = minutesObj;

    String notes = etNotes.getText().toString().trim();

    // Build payload EXACTLY as backend expects
    JSONObject payload = new JSONObject();
    try {
      payload.put("stationId", sel.id);
      payload.put("localDate", formatLocalDate(pickCal)); // YYYY-MM-DD
      payload.put("startTime", formatLocalHm(pickCal));   // HH:mm
      payload.put("minutes", minutes);
      if (!notes.isEmpty()) payload.put("notes", notes);
    } catch (Exception ignore){}

    android.content.Intent i = new android.content.Intent(this, BookingSummaryActivity.class);
    i.putExtra("payload", payload.toString());
    i.putExtra("stationName", sel.name);
    startActivity(i);
  }

  private void toast(String m){ Toast.makeText(this, m, Toast.LENGTH_SHORT).show(); }
}
