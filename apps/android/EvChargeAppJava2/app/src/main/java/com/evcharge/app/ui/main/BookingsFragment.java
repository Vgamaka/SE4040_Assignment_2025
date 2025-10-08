package com.evcharge.app.ui.main;

import android.app.DatePickerDialog;
import android.content.SharedPreferences;
import android.content.Intent;
import android.os.Bundle;
import android.text.TextUtils;
import android.view.*;
import android.widget.*;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.fragment.app.Fragment;
import androidx.recyclerview.widget.DividerItemDecoration;
import androidx.recyclerview.widget.LinearLayoutManager;
import androidx.recyclerview.widget.RecyclerView;
import androidx.swiperefreshlayout.widget.SwipeRefreshLayout;

import com.evcharge.app.R;
import com.evcharge.app.core.net.ApiClient;
import com.evcharge.app.core.util.JsonUtils;
import com.evcharge.app.ui.booking.BookingDetailActivity;
import com.evcharge.app.ui.booking.BookingListAdapter;
import com.evcharge.app.ui.booking.CreateBookingActivity;

import org.json.JSONArray;
import org.json.JSONObject;

import java.time.*;
import java.time.format.DateTimeFormatter;
import java.time.format.DateTimeParseException;
import java.time.temporal.WeekFields;
import java.util.*;

public final class BookingsFragment extends Fragment {

  private static final String PREFS_NAME           = "app_prefs"; // reuse same prefs namespace
  private static final String KEY_FILTER_STATUS    = "bookings_filter_status";
  private static final String KEY_FILTER_FROM      = "bookings_filter_from"; // yyyy-MM-dd
  private static final String KEY_FILTER_TO        = "bookings_filter_to";   // yyyy-MM-dd

  private Spinner spStatus;
  private Button btnFrom, btnTo, btnRefresh, btnCreate;
  private RecyclerView rv;
  private SwipeRefreshLayout swr;

  private BookingListAdapter adapter;

  private final String[] statusOptions = new String[]{
    "All", "Pending", "Approved", "Rejected", "Cancelled", "CheckedIn", "Completed", "NoShow"
  };

  private LocalDate filterFrom = null;
  private LocalDate filterTo = null;

  private final DateTimeFormatter btnFmt   = DateTimeFormatter.ofPattern("yyyy-MM-dd");
  private final DateTimeFormatter whenDate = DateTimeFormatter.ofPattern("dd MMM yyyy");
  private final DateTimeFormatter whenTime = DateTimeFormatter.ofPattern("HH:mm");

  @Nullable
  @Override
  public View onCreateView(@NonNull LayoutInflater inflater,
                           @Nullable ViewGroup container,
                           @Nullable Bundle savedInstanceState) {
    View v = inflater.inflate(R.layout.fragment_bookings, container, false);

    spStatus   = v.findViewById(R.id.spStatus);
    btnFrom    = v.findViewById(R.id.btnFrom);
    btnTo      = v.findViewById(R.id.btnTo);
    btnRefresh = v.findViewById(R.id.btnRefresh);
    btnCreate  = v.findViewById(R.id.btnCreateBooking);
    rv         = v.findViewById(R.id.rvBookings);
    swr        = v.findViewById(R.id.swrBookings);

    ArrayAdapter<String> sAdapter = new ArrayAdapter<>(requireContext(), android.R.layout.simple_spinner_dropdown_item, statusOptions);
    spStatus.setAdapter(sAdapter);

    // Load persisted filters (status + date range), then update UI
    loadFilters();
    updateDateButtons();

    // Persist on status change
    spStatus.setOnItemSelectedListener(new AdapterView.OnItemSelectedListener() {
      boolean first = true; // avoid double-trigger during initial setSelection
      @Override public void onItemSelected(AdapterView<?> parent, View view, int position, long id) {
        if (first) { first = false; return; }
        persistFilters();
      }
      @Override public void onNothingSelected(AdapterView<?> parent) {}
    });

    btnFrom.setOnClickListener(vw -> {
      showDatePicker(true);
    });
    btnTo.setOnClickListener(vw -> {
      showDatePicker(false);
    });

    btnRefresh.setOnClickListener(vw -> {
      persistFilters();
      loadAndRender();
    });

    btnCreate.setOnClickListener(vw ->
      startActivity(new Intent(requireContext(), CreateBookingActivity.class)));

    rv.setLayoutManager(new LinearLayoutManager(requireContext()));
    rv.addItemDecoration(new DividerItemDecoration(requireContext(), LinearLayoutManager.VERTICAL));
    adapter = new BookingListAdapter(bookingId -> {
      Intent i = new Intent(requireContext(), BookingDetailActivity.class);
      i.putExtra("bookingId", bookingId);
      startActivity(i);
    });
    rv.setAdapter(adapter);

    // Pull-to-refresh
    if (swr != null) {
      swr.setOnRefreshListener(this::loadAndRender);
    }

    loadAndRender();
    return v;
  }

  private void showDatePicker(boolean isFrom) {
    LocalDate base = (isFrom ? (filterFrom != null ? filterFrom : LocalDate.now())
      : (filterTo   != null ? filterTo   : LocalDate.now()));
    DatePickerDialog dlg = new DatePickerDialog(
      requireContext(),
      (view, year, month, dayOfMonth) -> {
        LocalDate picked = LocalDate.of(year, month + 1, dayOfMonth);
        if (isFrom) filterFrom = picked; else filterTo = picked;
        updateDateButtons();
        persistFilters();
      },
      base.getYear(), base.getMonthValue() - 1, base.getDayOfMonth()
    );
    dlg.show();
  }

  private void updateDateButtons() {
    btnFrom.setText(filterFrom != null ? btnFmt.format(filterFrom) : "From");
    btnTo.setText(filterTo != null ? btnFmt.format(filterTo) : "To");
  }

  private void loadAndRender() {
    btnRefresh.setEnabled(false);
    if (swr != null && !swr.isRefreshing()) swr.setRefreshing(true);

    new Thread(() -> {
      List<BookingListAdapter.Row> rows = new ArrayList<>();
      int total = 0;
      int kept = 0;
      try {
        ApiClient api = new ApiClient(requireContext().getApplicationContext());
        com.evcharge.app.core.net.HttpClient.Response resp = api.bookingMineRaw();
        if (!(resp.code >= 200 && resp.code < 300)) {
          uiToast("Failed: " + resp.code);
        } else {
          JSONArray arr = resp.jsonArray;
          if (arr == null && resp.body != null) {
            try { arr = new JSONArray(resp.body); } catch (Exception ignored){}
          }
          if (arr == null) {
            uiToast("No data");
          } else {
            total = arr.length();

            // 1) Collect stationIds needing names
            Set<String> ids = new HashSet<>();
            Map<String,String> idToName = new HashMap<>();
            for (int i=0;i<arr.length();i++){
              JSONObject o = arr.optJSONObject(i); if (o==null) continue;
              String stationName = JsonUtils.optString(o, "stationName");
              if (stationName == null) stationName = JsonUtils.optString(o, "StationName");
              if (stationName != null) {
                String sid = extractStationId(o);
                if (sid != null) idToName.put(sid, stationName);
              } else {
                String sid = extractStationId(o);
                if (sid != null) ids.add(sid);
              }
            }

            // 2) Resolve missing station names via /api/Station/{id}
            if (!ids.isEmpty()) {
              for (String sid : ids) {
                try {
                  com.evcharge.app.core.net.HttpClient.Response r2 = api.stationDetailRaw(sid);
                  JSONObject sObj = r2.jsonObject;
                  if (sObj == null && r2.body != null) {
                    try { sObj = new JSONObject(r2.body); } catch (Exception ignored){}
                  }
                  String nm = (sObj != null ? JsonUtils.optString(sObj, "name") : null);
                  if (nm == null && sObj != null) nm = JsonUtils.optString(sObj, "Name");
                  if (nm != null) idToName.put(sid, nm);
                } catch (Exception ignored){}
              }
            }

            // 3) Build sectioned rows using idToName & robust time parsing
            rows = buildSectionedRows(arr, idToName);
            for (BookingListAdapter.Row r : rows) if (r instanceof BookingListAdapter.ItemRow) kept++;
          }
        }
      } catch (Exception e) {
        uiToast("Network error: " + e.getMessage());
      }
      int finalTotal = total, finalKept = kept;
      List<BookingListAdapter.Row> finalRows = rows;
      requireActivity().runOnUiThread(() -> {
        adapter.setRows(finalRows);
        btnRefresh.setEnabled(true);
        if (swr != null) swr.setRefreshing(false);
        Toast.makeText(requireContext(), "Loaded " + finalTotal + ", after filters " + finalKept, Toast.LENGTH_SHORT).show();
      });
    }).start();
  }

  private List<BookingListAdapter.Row> buildSectionedRows(JSONArray arr, Map<String,String> idToName) {
    String selStatus = (String) spStatus.getSelectedItem();
    boolean filterByStatus = selStatus != null && !"All".equalsIgnoreCase(selStatus);

    ZoneId zone = ZoneId.systemDefault();
    LocalDate today = LocalDate.now(zone);
    LocalDate yesterday = today.minusDays(1);

    WeekFields wf = WeekFields.ISO;
    LocalDate weekStart = today.with(wf.dayOfWeek(), 1);
    LocalDate weekEnd   = today.with(wf.dayOfWeek(), 7);

    List<BookingListAdapter.ItemRow> todayList = new ArrayList<>();
    List<BookingListAdapter.ItemRow> yList     = new ArrayList<>();
    List<BookingListAdapter.ItemRow> weekList  = new ArrayList<>();
    List<BookingListAdapter.ItemRow> upcoming  = new ArrayList<>();
    List<BookingListAdapter.ItemRow> older     = new ArrayList<>();
    List<BookingListAdapter.ItemRow> other     = new ArrayList<>();

    for (int i=0;i<arr.length();i++) {
      JSONObject o = arr.optJSONObject(i);
      if (o == null) continue;
      String id = JsonUtils.optString(o, "id");
      if (id == null) id = JsonUtils.optString(o, "bookingId");
      if (id == null) id = JsonUtils.optString(o, "BookingId");
      if (id == null) id = JsonUtils.optString(o, "BookingCode");
      if (id == null) continue;

      String station = JsonUtils.optString(o, "stationName");
      if (station == null) station = JsonUtils.optString(o, "StationName");
      if (station == null) {
        String sid = extractStationId(o);
        if (sid != null && idToName.containsKey(sid)) {
          station = idToName.get(sid);
        } else if (sid != null) {
          station = "Station " + (sid.length() > 6 ? sid.substring(0,6) : sid);
        } else {
          station = "Station";
        }
      }

      String status  = JsonUtils.optString(o, "status");
      if (status == null) status = JsonUtils.optString(o, "Status");

      LocalDateTime ldt = parseStartLocalRobust(o, zone);
      String whenDisplay = displayWhenString(o, ldt);

      if (filterByStatus && (status == null || !selStatus.equalsIgnoreCase(status))) continue;

      if (ldt == null) {
        other.add(new BookingListAdapter.ItemRow(id, station, whenDisplay, status));
        continue;
      }

      LocalDate d = ldt.toLocalDate();

      if (filterFrom != null && d.isBefore(filterFrom)) continue;
      if (filterTo   != null && d.isAfter(filterTo))   continue;

      BookingListAdapter.ItemRow item = new BookingListAdapter.ItemRow(
        id, station,
        whenDate.format(d) + " · " + whenTime.format(ldt.toLocalTime()),
        status
      );

      if (d.equals(today)) {
        todayList.add(item);
      } else if (d.equals(yesterday)) {
        yList.add(item);
      } else if (!d.isBefore(weekStart) && !d.isAfter(weekEnd)) {
        weekList.add(item);
      } else if (d.isAfter(weekEnd)) {
        upcoming.add(item);
      } else {
        older.add(item);
      }
    }

    List<BookingListAdapter.Row> out = new ArrayList<>();
    if (!todayList.isEmpty())    { out.add(new BookingListAdapter.HeaderRow("Today"));      out.addAll(todayList); }
    if (!yList.isEmpty())        { out.add(new BookingListAdapter.HeaderRow("Yesterday"));  out.addAll(yList); }
    if (!weekList.isEmpty())     { out.add(new BookingListAdapter.HeaderRow("This Week"));  out.addAll(weekList); }
    if (!upcoming.isEmpty())     { out.add(new BookingListAdapter.HeaderRow("Upcoming"));   out.addAll(upcoming); }
    if (!older.isEmpty())        { out.add(new BookingListAdapter.HeaderRow("Older"));      out.addAll(older); }
    if (!other.isEmpty())        { out.add(new BookingListAdapter.HeaderRow("Other"));      out.addAll(other); }
    return out;
  }

  private LocalDateTime parseStartLocalRobust(JSONObject o, ZoneId zone) {
    String s = JsonUtils.optString(o, "startUtc");
    if (s == null) s = JsonUtils.optString(o, "startTimeUtc");
    if (s == null) s = JsonUtils.optString(o, "slotStartUtc");
    if (s == null) s = JsonUtils.optString(o, "SlotStartUtc");

    if (!TextUtils.isEmpty(s)) {
      LocalDateTime ldt = tryParseInstantLike(s, zone);
      if (ldt != null) return ldt;
      ldt = tryCommonDateTimePatterns(s, zone);
      if (ldt != null) return ldt;
    }

    String d = JsonUtils.optString(o, "localDate");
    String t = JsonUtils.optString(o, "startTime");
    String slotLocal = JsonUtils.optString(o, "slotStartLocal");
    if (slotLocal == null) slotLocal = JsonUtils.optString(o, "SlotStartLocal");
    if (!TextUtils.isEmpty(slotLocal)) {
      try {
        return LocalDateTime.parse(slotLocal.replace(" ", "T"), DateTimeFormatter.ofPattern("yyyy-MM-dd'T'HH:mm"));
      } catch (Exception ignored) {}
    }
    if (!TextUtils.isEmpty(d) && !TextUtils.isEmpty(t)) {
      try {
        LocalDate ld = LocalDate.parse(d);
        java.time.LocalTime lt = java.time.LocalTime.parse(t);
        return LocalDateTime.of(ld, lt);
      } catch (Exception ignored) {}
    }

    return null;
  }

  private LocalDateTime tryParseInstantLike(String s, ZoneId zone) {
    try {
      if (s.endsWith("Z") || s.contains("+")) {
        Instant ins = Instant.parse(s.replace(" ", "T"));
        return LocalDateTime.ofInstant(ins, zone);
      }
    } catch (Exception ignored) {}
    try {
      return OffsetDateTime.parse(s.replace(" ", "T")).atZoneSameInstant(zone).toLocalDateTime();
    } catch (Exception ignored) {}
    try {
      return ZonedDateTime.parse(s.replace(" ", "T")).withZoneSameInstant(zone).toLocalDateTime();
    } catch (Exception ignored) {}
    return null;
  }

  private LocalDateTime tryCommonDateTimePatterns(String s, ZoneId zone) {
    String v = s.replace(" ", "T");
    String[] patterns = new String[]{
      "yyyy-MM-dd'T'HH:mm:ss.SSS",
      "yyyy-MM-dd'T'HH:mm:ss",
      "yyyy-MM-dd'T'HH:mm"
    };
    for (String p : patterns) {
      try {
        DateTimeFormatter f = DateTimeFormatter.ofPattern(p);
        LocalDateTime ldt = LocalDateTime.parse(v, f);
        return ldt;
      } catch (DateTimeParseException ignored) {}
    }
    return null;
  }

  private String displayWhenString(JSONObject o, LocalDateTime ldt) {
    if (ldt != null) {
      return whenDate.format(ldt.toLocalDate()) + " · " + whenTime.format(ldt.toLocalTime());
    }
    String s = JsonUtils.optString(o, "startUtc");
    if (s == null) s = JsonUtils.optString(o, "startTimeUtc");
    if (s == null) s = JsonUtils.optString(o, "slotStartUtc");
    if (s == null) s = JsonUtils.optString(o, "SlotStartUtc");
    if (!TextUtils.isEmpty(s)) return s;
    String d = JsonUtils.optString(o, "localDate");
    String t = JsonUtils.optString(o, "startTime");
    String l = JsonUtils.optString(o, "slotStartLocal");
    if (l == null) l = JsonUtils.optString(o, "SlotStartLocal");
    if (!TextUtils.isEmpty(l)) return l;
    if (!TextUtils.isEmpty(d) && !TextUtils.isEmpty(t)) return d + " · " + t;
    if (!TextUtils.isEmpty(d)) return d;
    return "(time unknown)";
  }

  private String extractStationId(JSONObject o) {
    String sid = JsonUtils.optString(o, "stationId");
    if (sid == null) sid = JsonUtils.optString(o, "StationId");
    if (sid != null) return sid;

    JSONObject node = o.optJSONObject("StationId");
    if (node != null) {
      String oid = JsonUtils.optString(node, "$oid");
      if (oid != null) return oid;
    }

    JSONObject st = o.optJSONObject("station");
    if (st != null) {
      String id = JsonUtils.optString(st, "id");
      if (id == null) id = JsonUtils.optString(st, "_id");
      if (id != null) return id;
      JSONObject _id = st.optJSONObject("_id");
      if (_id != null) {
        String oid = JsonUtils.optString(_id, "$oid");
        if (oid != null) return oid;
      }
    }

    return null;
  }

  private void loadFilters() {
    SharedPreferences p = requireContext().getSharedPreferences(PREFS_NAME, 0);
    String status = p.getString(KEY_FILTER_STATUS, "All");
    int idx = 0;
    for (int i = 0; i < statusOptions.length; i++) {
      if (statusOptions[i].equalsIgnoreCase(status)) { idx = i; break; }
    }
    spStatus.setSelection(idx);

    String from = p.getString(KEY_FILTER_FROM, null);
    String to   = p.getString(KEY_FILTER_TO, null);
    try { filterFrom = (from != null && !from.isEmpty()) ? LocalDate.parse(from) : null; } catch (Exception ignored) { filterFrom = null; }
    try { filterTo   = (to   != null && !to.isEmpty())   ? LocalDate.parse(to)   : null; } catch (Exception ignored) { filterTo   = null; }
  }

  private void persistFilters() {
    SharedPreferences p = requireContext().getSharedPreferences(PREFS_NAME, 0);
    String status = (String) spStatus.getSelectedItem();
    String from = (filterFrom != null ? btnFmt.format(filterFrom) : "");
    String to   = (filterTo   != null ? btnFmt.format(filterTo)   : "");
    p.edit()
      .putString(KEY_FILTER_STATUS, (status != null ? status : "All"))
      .putString(KEY_FILTER_FROM, from)
      .putString(KEY_FILTER_TO, to)
      .apply();
  }

  private void uiToast(String m) {
    requireActivity().runOnUiThread(() ->
      Toast.makeText(requireContext(), m, Toast.LENGTH_SHORT).show());
  }
}
