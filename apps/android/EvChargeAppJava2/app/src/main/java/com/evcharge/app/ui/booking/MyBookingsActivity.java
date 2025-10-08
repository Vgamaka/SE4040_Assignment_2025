package com.evcharge.app.ui.booking;

import android.content.Intent;
import android.os.Bundle;
import android.widget.ArrayAdapter;
import android.widget.ListView;
import android.widget.Toast;

import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatActivity;

import com.evcharge.app.R;
import com.evcharge.app.core.net.ApiClient;
import com.evcharge.app.core.util.JsonUtils;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

public final class MyBookingsActivity extends AppCompatActivity {

    private static final class Row {
        final String id;
        final String title;
        Row(String id, String title){ this.id = id; this.title = title; }
        @Override public String toString(){ return title; }
    }

    private final List<Row> rows = new ArrayList<>();
    private ArrayAdapter<Row> adapter;

    @Override
    protected void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_my_bookings);

        ListView list = findViewById(R.id.listBookings);
        adapter = new ArrayAdapter<>(this, R.layout.item_booking, R.id.tvLine1, rows);
        list.setAdapter(adapter);

        list.setOnItemClickListener((parent, view, position, id) -> {
            Row r = rows.get(position);
            Intent i = new Intent(this, BookingDetailActivity.class);
            i.putExtra("bookingId", r.id);
            startActivity(i);
        });

        load();
    }

    private void load() {
        new Thread(() -> {
            try {
                ApiClient api = new ApiClient(getApplicationContext());
                com.evcharge.app.core.net.HttpClient.Response resp = api.bookingMineRaw();
                if (!(resp.code >= 200 && resp.code < 300) || resp.jsonArray == null) {
                    runOnUiThread(() -> toast("Failed: " + resp.code));
                    return;
                }
                JSONArray arr = resp.jsonArray;
                List<Row> tmp = new ArrayList<>();
                for (int i = 0; i < arr.length(); i++) {
                    JSONObject o = arr.optJSONObject(i);
                    if (o == null) continue;
                    String id = JsonUtils.optString(o, "id");
                    if (id == null) id = JsonUtils.optString(o, "bookingId");
                    String status = JsonUtils.optString(o, "status");
                    String station = JsonUtils.optString(o, "stationName");
                    String start = JsonUtils.optString(o, "startUtc");
                    if (start == null) start = JsonUtils.optString(o, "startTimeUtc");
                    String line1 = (station != null ? station : "Booking") + (status != null ? " · " + status : "");
                    if (start != null) line1 += " · " + start;
                    if (id != null) tmp.add(new Row(id, line1));
                }
                List<Row> finalList = tmp;
                runOnUiThread(() -> {
                    rows.clear();
                    rows.addAll(finalList);
                    adapter.notifyDataSetChanged();
                });
            } catch (Exception e) {
                runOnUiThread(() -> toast("Network error: " + e.getMessage()));
            }
        }).start();
    }

    private void toast(String m){ Toast.makeText(this, m, Toast.LENGTH_SHORT).show(); }
}
