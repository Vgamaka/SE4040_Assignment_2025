package com.evcharge.app.ui.stations;

import android.Manifest;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.os.Bundle;
import android.widget.Button;
import android.widget.Toast;

import androidx.activity.result.ActivityResultLauncher;
import androidx.activity.result.contract.ActivityResultContracts;
import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.content.ContextCompat;

import com.evcharge.app.R;
import com.evcharge.app.core.net.ApiClient;
import com.evcharge.app.core.util.JsonUtils;
import com.google.android.gms.maps.CameraUpdateFactory;
import com.google.android.gms.maps.GoogleMap;
import com.google.android.gms.maps.SupportMapFragment;
import com.google.android.gms.maps.model.*;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

public final class NearbyMapActivity extends AppCompatActivity {

    private GoogleMap map;

    private final ActivityResultLauncher<String> reqFineLocation =
            registerForActivityResult(new ActivityResultContracts.RequestPermission(), granted -> {
                if (map != null) tryEnableMyLocation();
            });

    @Override
    protected void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_nearby_map);

        SupportMapFragment smf = SupportMapFragment.newInstance();
        getSupportFragmentManager().beginTransaction()
                .replace(R.id.mapContainer, smf)
                .commit();

        smf.getMapAsync(gm -> {
            map = gm;
            map.getUiSettings().setZoomControlsEnabled(true);
            map.setOnInfoWindowClickListener(marker -> {
                Object tag = marker.getTag();
                if (tag instanceof String) {
                    String stationId = (String) tag;
                    Intent i = new Intent(this, StationDetailActivity.class);
                    i.putExtra("stationId", stationId);
                    startActivity(i);
                }
            });

            tryEnableMyLocation();
            LatLng colombo = new LatLng(6.9271, 79.8612);
            map.moveCamera(CameraUpdateFactory.newLatLngZoom(colombo, 13f));

            refreshNearby();
        });

        Button btnMy = findViewById(R.id.btnMyLocation);
        btnMy.setOnClickListener(v -> {
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION)
                    != PackageManager.PERMISSION_GRANTED) {
                reqFineLocation.launch(Manifest.permission.ACCESS_FINE_LOCATION);
            } else if (map != null) {
                map.setMyLocationEnabled(true);
                if (map.getMyLocation() != null) {
                    LatLng me = new LatLng(map.getMyLocation().getLatitude(), map.getMyLocation().getLongitude());
                    map.animateCamera(CameraUpdateFactory.newLatLngZoom(me, 15f));
                } else {
                    toast("Location not available yet");
                }
            }
        });

        Button btnRefresh = findViewById(R.id.btnRefresh);
        btnRefresh.setOnClickListener(v -> refreshNearby());
    }

    private void tryEnableMyLocation() {
        try {
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION)
                    == PackageManager.PERMISSION_GRANTED && map != null) {
                map.setMyLocationEnabled(true);
            }
        } catch (SecurityException ignored) {}
    }

    private void refreshNearby() {
        if (map == null) return;
        LatLng center = map.getCameraPosition().target;
        final double lat = center.latitude;
        final double lng = center.longitude;

        new Thread(() -> {
            try {
                ApiClient api = new ApiClient(getApplicationContext());
                com.evcharge.app.core.net.HttpClient.Response r =
                        api.stationsNearbyRaw(lat, lng, 5, "AC");
                if (!(r.code >= 200 && r.code < 300)) {
                    runOnUiThread(() -> toast("Nearby failed: " + r.code));
                    return;
                }

                JSONArray arr = r.jsonArray;
                if (arr == null && r.body != null && !r.body.trim().isEmpty()) {
                    try { arr = new JSONArray(r.body); } catch (Exception ignored) {}
                }
                if (arr == null) {
                    runOnUiThread(() -> toast("Nearby parse error"));
                    return;
                }

                final List<StationMarker> toShow = new ArrayList<>();
                LatLngBounds.Builder bounds = new LatLngBounds.Builder();
                boolean any = false;

                for (int i = 0; i < arr.length(); i++) {
                    JSONObject o = arr.optJSONObject(i); if (o == null) continue;
                    String id = JsonUtils.optString(o, "id");
                    String name = JsonUtils.optString(o, "name");

                    // Status may not be in nearby payload; try detail if missing
                    String status = JsonUtils.optString(o, "status");
                    if (status == null) status = JsonUtils.optString(o, "Status");

                    double sLat = o.optDouble("lat", Double.NaN);
                    double sLng = o.optDouble("lng", Double.NaN);

                    // If coords missing or status unknown, fetch detail
                    if ((Double.isNaN(sLat) || Double.isNaN(sLng) || status == null) && id != null) {
                        try {
                            com.evcharge.app.core.net.HttpClient.Response d = api.stationDetailRaw(id);
                            if (d.jsonObject != null) {
                                // status
                                if (status == null) {
                                    status = JsonUtils.optString(d.jsonObject, "status");
                                    if (status == null) status = JsonUtils.optString(d.jsonObject, "Status");
                                }
                                // coords: lat/lng OR Location.coordinates [lng,lat]
                                if (Double.isNaN(sLat) || Double.isNaN(sLng)) {
                                    sLat = d.jsonObject.optDouble("lat", sLat);
                                    sLng = d.jsonObject.optDouble("lng", sLng);
                                    if (Double.isNaN(sLat) || Double.isNaN(sLng)) {
                                        JSONObject loc = d.jsonObject.optJSONObject("Location");
                                        if (loc != null) {
                                            JSONArray coords = loc.optJSONArray("coordinates");
                                            if (coords != null && coords.length() >= 2) {
                                                double lng2 = coords.optDouble(0, Double.NaN);
                                                double lat2 = coords.optDouble(1, Double.NaN);
                                                if (!Double.isNaN(lat2) && !Double.isNaN(lng2)) {
                                                    sLat = lat2; sLng = lng2;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        } catch (Exception ignored) {}
                    }

                    // Only show Active
                    if (status == null || !"active".equalsIgnoreCase(status)) continue;

                    if (!Double.isNaN(sLat) && !Double.isNaN(sLng)) {
                        String snippet = "";
                        double distanceKm = o.optDouble("distanceKm", -1);
                        if (distanceKm >= 0) snippet = String.format(Locale.US, "%.1f km", distanceKm);
                        JSONArray summary = o.optJSONArray("availabilitySummary");
                        if (summary != null && summary.length() > 0) {
                            JSONObject first = summary.optJSONObject(0);
                            if (first != null) {
                                int slots = first.optInt("availableSlots", -1);
                                if (slots >= 0) snippet = (snippet.isEmpty() ? "" : snippet + " · ") + "Today: " + slots;
                            }
                        }
                        StationMarker sm = new StationMarker(id, name != null ? name : "Station", snippet, sLat, sLng);
                        toShow.add(sm);
                    }
                }

                runOnUiThread(() -> {
                    map.clear();
                    if (toShow.isEmpty()) { toast("Nearby: 0 station(s)"); return; }

                    LatLngBounds.Builder b = new LatLngBounds.Builder();
                    for (StationMarker sm : toShow) {
                        MarkerOptions mo = new MarkerOptions()
                                .position(new LatLng(sm.lat, sm.lng))
                                .title(sm.title)
                                .snippet(sm.snippet)
                                .icon(BitmapDescriptorFactory.defaultMarker(BitmapDescriptorFactory.HUE_GREEN));
                        Marker m = map.addMarker(mo);
                        if (m != null && sm.id != null) m.setTag(sm.id);
                        b.include(new LatLng(sm.lat, sm.lng));
                    }
                    map.setOnMapLoadedCallback(() ->
                            map.animateCamera(CameraUpdateFactory.newLatLngBounds(b.build(), 80))
                    );
                    toast("Nearby: " + toShow.size() + " station(s)");
                });

            } catch (Exception e) {
                runOnUiThread(() -> toast("Nearby error: " + e.getMessage()));
            }
        }).start();
    }

    private static final class StationMarker {
        final String id, title, snippet; final double lat, lng;
        StationMarker(String id, String title, String snippet, double lat, double lng) {
            this.id = id; this.title = title; this.snippet = snippet; this.lat = lat; this.lng = lng;
        }
    }

    private void toast(String m) { Toast.makeText(this, m, Toast.LENGTH_SHORT).show(); }
}
