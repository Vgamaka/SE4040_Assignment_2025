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

    // Show only concise booking details
    tvSummary.setText(
      "Station: " + stationName +
        "\nDate: " + date +
        "\nStart: " + time +
        "\nDuration: " + minutes + " mins"
    );

    btnConfirm.setOnClickListener(v -> doCreate());
  }

  private void doCreate() {
    if (payload == null) {
      toast("Invalid booking details");
      return;
    }

    btnConfirm.setEnabled(false);

    new Thread(() -> {
      ApiClient api = new ApiClient(getApplicationContext());
      ApiClient.Result r = api.bookingCreate(payload);

      runOnUiThread(() -> {
        btnConfirm.setEnabled(true);

        if (r.ok) {
          toast("Booking created successfully");
          startActivity(new Intent(this, MyBookingsActivity.class));
          finish();
        } else {
          String msg = (r.message != null && !r.message.isEmpty())
            ? r.message
            : "Booking failed (" + r.code + ")";
          toast(msg);
        }
      });
    }).start();
  }

  private void toast(String m) {
    Toast.makeText(this, m, Toast.LENGTH_LONG).show();
  }
}
