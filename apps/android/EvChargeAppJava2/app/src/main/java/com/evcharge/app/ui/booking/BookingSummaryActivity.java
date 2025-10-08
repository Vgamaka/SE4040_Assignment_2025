package com.evcharge.app.ui.booking;

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

import org.json.JSONObject;

public final class BookingSummaryActivity extends AppCompatActivity {

  private TextView tvSummary;
  private Button btnConfirm;
  private JSONObject payload;

  @Override
  protected void onCreate(@Nullable Bundle savedInstanceState) {
    super.onCreate(savedInstanceState);
    setContentView(R.layout.activity_booking_summary);

    tvSummary = findViewById(R.id.tvSummary);
    btnConfirm = findViewById(R.id.btnConfirm);

    String payloadStr = getIntent().getStringExtra("payload");
    String stationName = getIntent().getStringExtra("stationName");
    payload = JsonUtils.parseObject(payloadStr);

    String date = payload != null ? JsonUtils.optString(payload, "localDate") : null;
    String time = payload != null ? JsonUtils.optString(payload, "startTime") : null;
    String minutes = payload != null ? String.valueOf(payload.optInt("minutes", 60)) : "60";

    tvSummary.setText("Station: " + stationName
      + "\nDate: " + date
      + "\nStart: " + time
      + "\nDuration: " + minutes + " mins"
      + "\n\nPayload JSON:\n" + payloadStr);

    btnConfirm.setOnClickListener(v -> doCreate());
  }

  private void doCreate() {
    if (payload == null) { toast("Invalid payload"); return; }

    btnConfirm.setEnabled(false);

    new Thread(() -> {
      ApiClient api = new ApiClient(getApplicationContext());
      ApiClient.Result r = api.bookingCreate(payload);

      runOnUiThread(() -> {
        btnConfirm.setEnabled(true);

        // Always show what happened
        String detail = (r.body != null ? r.body : (r.message != null ? r.message : ""));
        toast("Create result: " + r.code + (detail.isEmpty() ? "" : " · " + shrink(detail, 140)));

        if (r.ok) {
          // Navigate to My Bookings so you can verify immediately
          startActivity(new Intent(this, MyBookingsActivity.class));
          finish();
        }
      });
    }).start();
  }

  private static String shrink(String s, int max) { return (s != null && s.length() > max) ? s.substring(0, max) + "…" : (s != null ? s : ""); }

  private void toast(String m){ Toast.makeText(this, m, Toast.LENGTH_LONG).show(); }
}
