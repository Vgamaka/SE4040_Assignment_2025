package com.evcharge.app.ui.booking;

import android.app.DatePickerDialog;
import android.app.TimePickerDialog;
import android.content.Intent;
import android.graphics.Bitmap;
import android.net.Uri;
import android.os.Bundle;
import android.widget.Button;
import android.widget.ImageView;
import android.widget.TextView;
import android.widget.Toast;

import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatActivity;

import com.evcharge.app.R;
import com.evcharge.app.core.net.ApiClient;
import com.evcharge.app.core.util.JsonUtils;
import com.evcharge.app.features.qr.QrRenderer;
import com.google.zxing.WriterException;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.Calendar;

public final class BookingDetailActivity extends AppCompatActivity {

  private String bookingId;

  private TextView tvStatus, tvMeta, tvQrToken;
  private ImageView imgQr;
  private Button btnIssueQr, btnModify, btnCancel, btnDirections;

  // For modify payload (defaults pulled from server if available)
  private String localDate;   // YYYY-MM-DD
  private String startTime;   // HH:mm
  private int minutes = 60;   // default if not present

  // For directions
  private String stationId;   // from booking detail
  private Double stationLat = null, stationLng = null;

  @Override
  protected void onCreate(@Nullable Bundle savedInstanceState) {
    super.onCreate(savedInstanceState);
    setContentView(R.layout.activity_booking_detail);

    bookingId = getIntent().getStringExtra("bookingId");

    tvStatus = findViewById(R.id.tvStatus);
    tvMeta = findViewById(R.id.tvMeta);
    tvQrToken = findViewById(R.id.tvQrToken);
    imgQr = findViewById(R.id.imgQr);
    btnIssueQr = findViewById(R.id.btnIssueQr);
    btnModify = findViewById(R.id.btnModify);
    btnCancel = findViewById(R.id.btnCancel);
    btnDirections = findViewById(R.id.btnDirections);

    btnIssueQr.setOnClickListener(v -> issueQr());
    btnModify.setOnClickListener(v -> startModifyFlow());
    btnCancel.setOnClickListener(v -> doCancel());
    btnDirections.setOnClickListener(v -> openDirections());
    btnDirections.setEnabled(false); // enabled once we resolve coords

    load();
  }

  private void load() {
    new Thread(() -> {
      ApiClient api = new ApiClient(getApplicationContext());
      ApiClient.Result r = api.bookingDetail(bookingId);
      runOnUiThread(() -> {
        if (!r.ok || r.json == null) {
          toast(r.message != null ? r.message : "Failed to load");
          return;
        }
        render(r.json);
      });
    }).start();
  }

  private void render(JSONObject o) {
    String status = JsonUtils.optString(o, "status");
    String station = JsonUtils.optString(o, "stationName");
    stationId = JsonUtils.optString(o, "stationId");
    if (stationId == null) stationId = JsonUtils.optString(o, "StationId");

    // Prefer localDate/startTime/minutes if your backend returns them
    localDate = JsonUtils.optString(o, "localDate");
    startTime = JsonUtils.optString(o, "startTime");
    minutes = o.optInt("minutes", minutes);

    // Fallbacks for display only
    String start = JsonUtils.optString(o, "startUtc");
    if (start == null) start = JsonUtils.optString(o, "startTimeUtc");
    String end = JsonUtils.optString(o, "endUtc");
    if (end == null) end = JsonUtils.optString(o, "endTimeUtc");

    String qrToken = JsonUtils.optString(o, "qrToken");

    tvStatus.setText("Status: " + (status != null ? status : "-"));
    String meta = (station != null ? station : "") +
      (start != null ? "\nStart: " + start : "") +
      (end != null ? "\nEnd: " + end : "");
    tvMeta.setText(meta.trim());

    if (qrToken != null && (status != null && status.toLowerCase().contains("approved"))) {
      drawQr(qrToken);
    } else {
      imgQr.setImageBitmap(null);
      tvQrToken.setText("");
    }

    // Resolve station coordinates for Directions button
    if (stationId != null) {
      fetchStationCoords(stationId);
    } else {
      btnDirections.setEnabled(false);
    }
  }

  private void fetchStationCoords(String id) {
    new Thread(() -> {
      try {
        ApiClient api = new ApiClient(getApplicationContext());
        com.evcharge.app.core.net.HttpClient.Response d = api.stationDetailRaw(id);
        Double lat = null, lng = null;

        if (d.jsonObject != null) {
          // Try direct fields
          if (d.jsonObject.has("lat")) lat = d.jsonObject.optDouble("lat", Double.NaN);
          if (d.jsonObject.has("lng")) lng = d.jsonObject.optDouble("lng", Double.NaN);
          if (lat != null && lat.isNaN()) lat = null;
          if (lng != null && lng.isNaN()) lng = null;

          // Fallback: Location.coordinates [lng,lat]
          if (lat == null || lng == null) {
            JSONObject loc = d.jsonObject.optJSONObject("Location");
            if (loc != null) {
              JSONArray coords = loc.optJSONArray("coordinates");
              if (coords != null && coords.length() >= 2) {
                double lng2 = coords.optDouble(0, Double.NaN);
                double lat2 = coords.optDouble(1, Double.NaN);
                if (!Double.isNaN(lat2) && !Double.isNaN(lng2)) {
                  lat = lat2;
                  lng = lng2;
                }
              }
            }
          }
        }

        final Double fLat = lat, fLng = lng;
        runOnUiThread(() -> {
          stationLat = fLat;
          stationLng = fLng;
          btnDirections.setEnabled(stationLat != null && stationLng != null);
        });
      } catch (Exception e) {
        runOnUiThread(() -> {
          stationLat = null; stationLng = null;
          btnDirections.setEnabled(false);
        });
      }
    }).start();
  }

  private void startModifyFlow() {
    // If server didn't provide localDate/startTime, initialize with "now"
    final Calendar c = Calendar.getInstance();
    int initYear, initMonth, initDay, initHour, initMin;

    if (localDate != null && startTime != null && localDate.matches("\\d{4}-\\d{2}-\\d{2}") && startTime.matches("\\d{2}:\\d{2}")) {
      String[] d = localDate.split("-");
      String[] t = startTime.split(":");
      initYear = Integer.parseInt(d[0]);
      initMonth = Integer.parseInt(d[1]) - 1;
      initDay = Integer.parseInt(d[2]);
      initHour = Integer.parseInt(t[0]);
      initMin = Integer.parseInt(t[1]);
    } else {
      initYear = c.get(Calendar.YEAR);
      initMonth = c.get(Calendar.MONTH);
      initDay = c.get(Calendar.DAY_OF_MONTH);
      initHour = c.get(Calendar.HOUR_OF_DAY);
      initMin = c.get(Calendar.MINUTE);
    }

    // Date → then Time → then PUT
    new DatePickerDialog(this, (view, y, m, d) -> {
      String pickedDate = String.format("%04d-%02d-%02d", y, m + 1, d);
      new TimePickerDialog(this, (view2, hh, mm) -> {
        String pickedTime = String.format("%02d:%02d", hh, mm);
        doModify(pickedDate, pickedTime);
      }, initHour, initMin, true).show();
    }, initYear, initMonth, initDay).show();
  }

  private void doModify(String newDate, String newTime) {
    new Thread(() -> {
      ApiClient api = new ApiClient(getApplicationContext());
      try {
        JSONObject payload = new JSONObject();
        payload.put("localDate", newDate);
        payload.put("startTime", newTime);
        payload.put("minutes", minutes); // keep same duration
        ApiClient.Result r = api.bookingModify(bookingId, payload);
        runOnUiThread(() -> {
          String detail = (r.body != null ? r.body : (r.message != null ? r.message : ""));
          toast("Modify: " + r.code + (detail.isEmpty() ? "" : " · " + shrink(detail, 120)));
          if (r.ok) load(); // reload detail
        });
      } catch (Exception e) {
        runOnUiThread(() -> toast("Modify error: " + e.getMessage()));
      }
    }).start();
  }

  private void doCancel() {
    new Thread(() -> {
      ApiClient api = new ApiClient(getApplicationContext());
      ApiClient.Result r = api.bookingCancel(bookingId);
      runOnUiThread(() -> {
        String detail = (r.body != null ? r.body : (r.message != null ? r.message : ""));
        toast("Cancel: " + r.code + (detail.isEmpty() ? "" : " · " + shrink(detail, 120)));
        if (r.ok) finish(); // close detail on success
      });
    }).start();
  }

  private void issueQr() {
    new Thread(() -> {
      ApiClient api = new ApiClient(getApplicationContext());
      ApiClient.Result r = api.qrIssue(bookingId);
      runOnUiThread(() -> {
        if (!r.ok || r.json == null) {
          toast(r.message != null ? r.message : "Could not issue QR");
          return;
        }
        String token = JsonUtils.optString(r.json, "qrToken");
        if (token != null) {
          drawQr(token);
        } else {
          toast("No token in response");
        }
      });
    }).start();
  }

  private void drawQr(String token) {
    try {
      Bitmap bmp = QrRenderer.bitmapFrom(token, 800);
      imgQr.setImageBitmap(bmp);
      tvQrToken.setText(token);
    } catch (WriterException e) {
      toast("QR error: " + e.getMessage());
    }
  }

  private void openDirections() {
    if (stationLat == null || stationLng == null) {
      toast("Location unavailable");
      return;
    }
    String label = "EV Station";
    String geoUri = String.format(java.util.Locale.US,
      "geo:%f,%f?q=%f,%f(%s)", stationLat, stationLng, stationLat, stationLng,
      java.net.URLEncoder.encode(label));
    Intent intent = new Intent(Intent.ACTION_VIEW, Uri.parse(geoUri));
    intent.setPackage("com.google.android.apps.maps");
    try {
      startActivity(intent);
    } catch (Exception e) {
      // Fallback to universal web URL
      String web = String.format(java.util.Locale.US,
        "https://www.google.com/maps/dir/?api=1&destination=%f,%f&travelmode=driving", stationLat, stationLng);
      startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse(web)));
    }
  }

  private static String shrink(String s, int max) {
    return (s != null && s.length() > max) ? s.substring(0, max) + "…" : (s != null ? s : "");
  }

  private void toast(String m){ Toast.makeText(this, m, Toast.LENGTH_LONG).show(); }
}
