package com.evcharge.app.ui.main;

import android.content.Intent;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.ImageButton;
import android.widget.ProgressBar;
import android.widget.TextView;
import android.widget.Toast;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.fragment.app.Fragment;
import androidx.recyclerview.widget.LinearLayoutManager;
import androidx.recyclerview.widget.RecyclerView;

import com.evcharge.app.R;
import com.evcharge.app.core.net.ApiClient;
import com.evcharge.app.core.util.JsonUtils;
import com.evcharge.app.ui.booking.BookingDetailActivity;
import com.evcharge.app.ui.booking.BookingListAdapter;
import com.evcharge.app.ui.booking.CreateBookingActivity;
import com.evcharge.app.ui.stations.NearbyMapActivity;
import com.evcharge.app.ui.stations.StationDetailActivity;
import com.google.android.gms.maps.CameraUpdateFactory;
import com.google.android.gms.maps.GoogleMap;
import com.google.android.gms.maps.MapView;
import com.google.android.gms.maps.UiSettings;
import com.google.android.gms.maps.model.BitmapDescriptorFactory;
import com.google.android.gms.maps.model.LatLng;
import com.google.android.gms.maps.model.MarkerOptions;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Calendar;
import java.util.List;
import java.util.Locale;

public final class DashboardFragment extends Fragment {

  // UI
  private View cardMap;
  private MapView mapView;
  private View btnTabBookings, btnTabStations;
  private View panelBookings, panelStations;
  private ImageButton btnNotif;
  private TextView tvBadge, tvBookingsEmpty, tvStationsEmpty;
  private ProgressBar progressBookings, progressStations;
  private RecyclerView rvBookings, rvStations;

  // Adapters
  private BookingListAdapter bookingsAdapter;
  private StationAdapter stationsAdapter;

  // Maps
  private GoogleMap previewMap;

  @Nullable
  @Override
  public View onCreateView(@NonNull LayoutInflater inflater,
                           @Nullable ViewGroup container,
                           @Nullable Bundle savedInstanceState) {

    View v = inflater.inflate(R.layout.fragment_dashboard, container, false);

    // --- Map preview ---
    cardMap = v.findViewById(R.id.cardMap);
    mapView = v.findViewById(R.id.mapPreview);
    if (mapView != null) {
      mapView.onCreate(savedInstanceState);
      mapView.getMapAsync(gm -> {
        previewMap = gm;
        // Make preview behave like a thumbnail (non-interactive)
        UiSettings ui = gm.getUiSettings();
        ui.setAllGesturesEnabled(false);
        ui.setZoomControlsEnabled(false);
        ui.setMapToolbarEnabled(false);
        ui.setCompassEnabled(false);

        LatLng colombo = new LatLng(6.9271, 79.8612);
        gm.moveCamera(CameraUpdateFactory.newLatLngZoom(colombo, 12f));
        loadMapPreview(colombo.latitude, colombo.longitude);

        // Tapping the map preview opens the full map screen
        gm.setOnMapClickListener(latLng ->
          startActivity(new Intent(requireContext(), NearbyMapActivity.class)));
      });
    }

    if (cardMap != null) {
      cardMap.setOnClickListener(view ->
        startActivity(new Intent(requireContext(), NearbyMapActivity.class)));
    }

    // Tabs
    btnTabBookings = v.findViewById(R.id.btnTabBookings);
    btnTabStations = v.findViewById(R.id.btnTabStations);
    panelBookings = v.findViewById(R.id.panelBookings);
    panelStations = v.findViewById(R.id.panelStations);
    if (btnTabBookings != null) btnTabBookings.setOnClickListener(view -> showTab(true));
    if (btnTabStations != null) btnTabStations.setOnClickListener(view -> showTab(false));

    // Quick create booking
    View btnCreate = v.findViewById(R.id.btnCreateBookingQuick);
    if (btnCreate != null) btnCreate.setOnClickListener(view ->
      startActivity(new Intent(requireContext(), CreateBookingActivity.class)));

    // Notifications
    btnNotif = v.findViewById(R.id.btnNotif);
    tvBadge  = v.findViewById(R.id.tvNotifBadge);
    if (btnNotif != null) btnNotif.setOnClickListener(view ->
      new com.evcharge.app.ui.notifications.NotificationsDialog()
        .show(getParentFragmentManager(), "notifs"));
    getParentFragmentManager().setFragmentResultListener(
      "notifDismiss",
      getViewLifecycleOwner(),
      (requestKey, bundle) -> refreshBadge()
    );

    // Bookings list
    rvBookings = v.findViewById(R.id.rvBookings);
    tvBookingsEmpty = v.findViewById(R.id.tvBookingsEmpty);
    progressBookings = v.findViewById(R.id.progressBookings);
    if (rvBookings != null) {
      rvBookings.setLayoutManager(new LinearLayoutManager(requireContext()));
      bookingsAdapter = new BookingListAdapter(bookingId -> {
        Intent i = new Intent(requireContext(), BookingDetailActivity.class);
        i.putExtra("bookingId", bookingId);
        startActivity(i);
      });
      rvBookings.setAdapter(bookingsAdapter);
    }

    // Stations list
    rvStations = v.findViewById(R.id.rvStations);
    tvStationsEmpty = v.findViewById(R.id.tvStationsEmpty);
    progressStations = v.findViewById(R.id.progressStations);
    if (rvStations != null) {
      rvStations.setLayoutManager(new LinearLayoutManager(requireContext()));
      stationsAdapter = new StationAdapter(stationId -> {
        Intent i = new Intent(requireContext(), StationDetailActivity.class);
        i.putExtra("stationId", stationId);
        startActivity(i);
      });
      rvStations.setAdapter(stationsAdapter);
    }

    // default tab
    showTab(true);

    // initial content
    refreshBookingsToday();
    refreshStationsActive();
    refreshBadge();

    return v;
  }

  // ---------- Map preview loader ----------
  private void loadMapPreview(double lat, double lng) {
    new Thread(() -> {
      try {
        ApiClient api = new ApiClient(requireContext().getApplicationContext());
        com.evcharge.app.core.net.HttpClient.Response r =
          api.stationsNearbyRaw(lat, lng, 5, "AC");
        JSONArray arr = (r != null ? r.jsonArray : null);
        if (arr == null && r != null && r.body != null) {
          try { arr = new JSONArray(r.body); } catch (Exception ignored) {}
        }
        if (previewMap == null || arr == null) return;

        for (int i = 0; i < Math.min(arr.length(), 5); i++) {
          JSONObject o = arr.optJSONObject(i); if (o == null) continue;

          String status = JsonUtils.optString(o, "status");
          if (status == null) status = JsonUtils.optString(o, "Status");
          if (status == null || !"active".equalsIgnoreCase(status)) continue;

          double sLat = o.optDouble("lat", Double.NaN);
          double sLng = o.optDouble("lng", Double.NaN);
          if (Double.isNaN(sLat) || Double.isNaN(sLng)) continue;

          String name = JsonUtils.optString(o, "name");
          if (name == null) name = "Station";

          final String title = name;
          final double fl = sLat, flng = sLng;
          requireActivity().runOnUiThread(() -> {
            if (previewMap != null) {
              previewMap.addMarker(new MarkerOptions()
                .position(new LatLng(fl, flng))
                .title(title)
                .icon(BitmapDescriptorFactory.defaultMarker(BitmapDescriptorFactory.HUE_GREEN)));
            }
          });
        }
      } catch (Exception ignored) {}
    }).start();
  }

  private void showTab(boolean bookings) {
    if (panelBookings != null) panelBookings.setVisibility(bookings ? View.VISIBLE : View.GONE);
    if (panelStations != null) panelStations.setVisibility(bookings ? View.GONE : View.VISIBLE);
    if (btnTabBookings != null) btnTabBookings.setSelected(bookings);
    if (btnTabStations != null) btnTabStations.setSelected(!bookings);
  }

  // ---------- Bookings (today only) ----------
  private void refreshBookingsToday() {
    setBusyBookings(true);
    new Thread(() -> {
      try {
        ApiClient api = new ApiClient(requireContext().getApplicationContext());
        com.evcharge.app.core.net.HttpClient.Response resp = api.bookingMineRaw();
        if (!(resp.code >= 200 && resp.code < 300) || resp.jsonArray == null) {
          requireActivity().runOnUiThread(() -> {
            toast("Bookings failed: " + resp.code);
            setBusyBookings(false);
          });
          return;
        }
        JSONArray arr = resp.jsonArray;

        String todayYmd = ymd(Calendar.getInstance());
        List<BookingListAdapter.Row> rows = new ArrayList<>();
        rows.add(new BookingListAdapter.HeaderRow("Today"));

        for (int i = 0; i < arr.length(); i++) {
          JSONObject o = arr.optJSONObject(i); if (o == null) continue;

          String id = JsonUtils.optString(o, "id");
          if (id == null) id = JsonUtils.optString(o, "bookingId");
          if (id == null) continue;

          String station = JsonUtils.optString(o, "stationName");
          if (station == null) {
            String stId = JsonUtils.optString(o, "stationId");
            if (stId == null) {
              JSONObject stObj = o.optJSONObject("StationId");
              if (stObj != null) stId = JsonUtils.optString(stObj, "$oid");
            }
            if (stId != null) {
              try {
                com.evcharge.app.core.net.HttpClient.Response d = api.stationDetailRaw(stId);
                if (d.jsonObject != null) {
                  station = JsonUtils.optString(d.jsonObject, "name");
                }
              } catch (Exception ignored) {}
            }
          }
          if (station == null) station = "Station";

          String status = JsonUtils.optString(o, "status");
          if (status == null) status = "-";

          String local = JsonUtils.optString(o, "slotStartLocal");
          if (local == null) local = JsonUtils.optString(o, "SlotStartLocal");

          String dateYmd = null, hm = null;
          if (local != null && local.length() >= 16) {
            dateYmd = local.substring(0, 10);
            hm = local.substring(11, 16);
          } else {
            String startUtc = JsonUtils.optString(o, "slotStartUtc");
            if (startUtc == null) startUtc = JsonUtils.optString(o, "SlotStartUtc");
            if (startUtc != null && startUtc.length() >= 16) {
              dateYmd = startUtc.substring(0, 10);
              hm = startUtc.substring(11, 16) + "Z";
            }
          }

          if (todayYmd.equals(dateYmd)) {
            String when = dateYmd + " · " + (hm != null ? hm : "??:??");
            rows.add(new BookingListAdapter.ItemRow(id, station, when, status));
          }
        }

        final boolean emptyToday = rows.size() <= 1;
        requireActivity().runOnUiThread(() -> {
          bookingsAdapter.setRows(rows);
          tvBookingsEmpty.setVisibility(emptyToday ? View.VISIBLE : View.GONE);
          setBusyBookings(false);
        });
      } catch (Exception e) {
        requireActivity().runOnUiThread(() -> {
          toast("Bookings error: " + e.getMessage());
          setBusyBookings(false);
        });
      }
    }).start();
  }

  private void setBusyBookings(boolean b) {
    if (!isAdded()) return;
    requireActivity().runOnUiThread(() -> {
      progressBookings.setVisibility(b ? View.VISIBLE : View.GONE);
      rvBookings.setAlpha(b ? 0.4f : 1f);
    });
  }

  // ---------- Stations (active only) ----------
  private void refreshStationsActive() {
    setBusyStations(true);
    new Thread(() -> {
      try {
        ApiClient api = new ApiClient(requireContext().getApplicationContext());
        com.evcharge.app.core.net.HttpClient.Response r = api.stationsAllRaw();

        JSONArray arr = r.jsonArray;
        JSONObject root = r.jsonObject;
        if (arr == null) {
          if (root == null && r.body != null && !r.body.trim().isEmpty()) {
            try { arr = new JSONArray(r.body); } catch (Exception ignored) {
              try { root = new JSONObject(r.body); } catch (Exception ignored2) {}
            }
          }
          if (arr == null && root != null) {
            String[] keys = new String[]{"items","data","results","stations","value"};
            for (String k : keys) {
              JSONArray cand = root.optJSONArray(k);
              if (cand != null) { arr = cand; break; }
            }
            if (arr == null) {
              for (java.util.Iterator<String> it = root.keys(); it.hasNext();) {
                String k = it.next();
                Object v = root.opt(k);
                if (v instanceof JSONArray) { arr = (JSONArray) v; break; }
              }
            }
          }
        }

        final List<String> stationIds = new ArrayList<>();
        final List<String> stationNames = new ArrayList<>();

        if (arr != null) {
          for (int i = 0; i < arr.length(); i++) {
            JSONObject o = arr.optJSONObject(i); if (o == null) continue;

            String id = JsonUtils.optString(o,"id");
            if (id == null) {
              JSONObject m = o.optJSONObject("_id");
              if (m != null) id = JsonUtils.optString(m, "$oid");
            }
            if (id == null) id = JsonUtils.optString(o, "stationId");
            if (id == null) continue;

            String name = JsonUtils.optString(o,"name");
            if (name == null) name = JsonUtils.optString(o,"Name");
            if (name == null) name = "Station";

            String status = JsonUtils.optString(o,"status");
            if (status == null) status = JsonUtils.optString(o,"Status");

            if (status == null) {
              try {
                com.evcharge.app.core.net.HttpClient.Response d = api.stationDetailRaw(id);
                if (d.jsonObject != null) {
                  status = JsonUtils.optString(d.jsonObject,"status");
                  if (status == null) status = JsonUtils.optString(d.jsonObject,"Status");
                  String dn = JsonUtils.optString(d.jsonObject,"name");
                  if (dn != null) name = dn;
                }
              } catch (Exception ignored) {}
            }

            if (status != null && "active".equalsIgnoreCase(status)) {
              stationIds.add(id);
              stationNames.add(name);
            }
          }
        }

        requireActivity().runOnUiThread(() -> {
          stationsAdapter.setRows(stationIds, stationNames);
          tvStationsEmpty.setVisibility(stationIds.isEmpty() ? View.VISIBLE : View.GONE);
          setBusyStations(false);
        });
      } catch (Exception e) {
        requireActivity().runOnUiThread(() -> {
          toast("Stations error: " + e.getMessage());
          setBusyStations(false);
        });
      }
    }).start();
  }

  private void setBusyStations(boolean b) {
    if (!isAdded()) return;
    requireActivity().runOnUiThread(() -> {
      progressStations.setVisibility(b ? View.VISIBLE : View.GONE);
      rvStations.setAlpha(b ? 0.4f : 1f);
    });
  }

  // ---------- Notifications badge ----------
  private void refreshBadge() {
    new Thread(() -> {
      try {
        ApiClient api = new ApiClient(requireContext().getApplicationContext());
        com.evcharge.app.core.net.HttpClient.Response r = api.notificationsListRaw(true, 1, 1);
        int total = 0;
        if (r != null && r.jsonObject != null) total = r.jsonObject.optInt("total", 0);
        final int badge = Math.max(0, total);
        requireActivity().runOnUiThread(() -> {
          tvBadge.setVisibility(badge > 0 ? View.VISIBLE : View.GONE);
          tvBadge.setText(String.valueOf(badge));
        });
      } catch (Exception ignored) {}
    }).start();
  }

  private static String ymd(Calendar c) {
    return String.format(Locale.US, "%04d-%02d-%02d",
      c.get(Calendar.YEAR), c.get(Calendar.MONTH) + 1, c.get(Calendar.DAY_OF_MONTH));
  }

  private void toast(String m) {
    if (!isAdded()) return;
    Toast.makeText(requireContext(), m, Toast.LENGTH_SHORT).show();
  }

  // ---- tiny stations list adapter for Stations panel ----
  private static final class StationAdapter extends RecyclerView.Adapter<StationAdapter.VH> {
    interface OnClick { void onClick(String stationId); }
    private final List<String> ids = new ArrayList<>();
    private final List<String> names = new ArrayList<>();
    private final OnClick click;

    StationAdapter(OnClick click){ this.click = click; }

    void setRows(List<String> ids, List<String> names){
      this.ids.clear(); this.names.clear();
      if (ids != null) this.ids.addAll(ids);
      if (names != null) this.names.addAll(names);
      notifyDataSetChanged();
    }

    @NonNull @Override public VH onCreateViewHolder(@NonNull ViewGroup p, int vType) {
      View v = LayoutInflater.from(p.getContext()).inflate(R.layout.item_station_row, p, false);
      return new VH(v, click);
    }

    @Override public void onBindViewHolder(@NonNull VH h, int pos) {
      String id = ids.get(pos);
      String name = names.get(pos);
      h.bind(id, name);
    }

    @Override public int getItemCount() { return Math.min(ids.size(), names.size()); }

    static final class VH extends RecyclerView.ViewHolder {
      private final TextView tv;
      VH(@NonNull View v, OnClick click) {
        super(v);
        tv = v.findViewById(R.id.tvStationName);
        v.setOnClickListener(view -> {
          Object tag = view.getTag();
          if (tag instanceof String && click != null) click.onClick((String) tag);
        });
      }
      void bind(String id, String name){
        itemView.setTag(id);
        tv.setText(name != null ? name : "Station");
      }
    }
  }

  // ---- MapView lifecycle wiring ----
  @Override public void onResume() {
    super.onResume();
    if (mapView != null) mapView.onResume();
  }
  @Override public void onPause() {
    if (mapView != null) mapView.onPause();
    super.onPause();
  }
  @Override public void onDestroyView() {
    if (mapView != null) mapView.onDestroy();
    super.onDestroyView();
  }
  @Override public void onLowMemory() {
    super.onLowMemory();
    if (mapView != null) mapView.onLowMemory();
  }
}
