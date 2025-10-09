package com.evcharge.app.ui.profile;

import android.os.Bundle;
import android.widget.Button;
import android.widget.EditText;
import android.widget.Toast;

import androidx.annotation.Nullable;
import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;

import com.evcharge.app.R;
import com.evcharge.app.core.net.ApiClient;
import com.evcharge.app.core.util.JsonUtils;

import org.json.JSONObject;

public final class OwnerProfileActivity extends AppCompatActivity {

  private EditText etNic, etFullName, etEmail, etPhone, etLine1, etLine2, etCity;
  private Button btnLoad, btnSave, btnDeactivate;

  @Override
  protected void onCreate(@Nullable Bundle savedInstanceState) {
    super.onCreate(savedInstanceState);
    setContentView(R.layout.activity_owner_profile);

    etNic = findViewById(R.id.etNic);
    etFullName = findViewById(R.id.etFullName);
    etEmail = findViewById(R.id.etEmail);
    etPhone = findViewById(R.id.etPhone);
    etLine1 = findViewById(R.id.etLine1);
    etLine2 = findViewById(R.id.etLine2);
    etCity = findViewById(R.id.etCity);

    btnLoad = findViewById(R.id.btnLoad);
    btnSave = findViewById(R.id.btnSave);
    btnDeactivate = findViewById(R.id.btnDeactivate);

    // Prefill NIC if passed from caller
    String nic = getIntent().getStringExtra("nic");
    if (nic != null && !nic.trim().isEmpty()) {
      etNic.setText(nic.trim());
    }

    btnLoad.setOnClickListener(v -> doLoad());
    btnSave.setOnClickListener(v -> doSave());
    btnDeactivate.setOnClickListener(v -> confirmDeactivate());

    // Auto-load if NIC was provided
    if (etNic.getText().length() > 0) {
      doLoad();
    }
  }

  private void doLoad() {
    final String nic = etNic.getText().toString().trim();
    if (nic.isEmpty()) { toast("Enter NIC"); return; }

    btnLoad.setEnabled(false);
    new Thread(() -> {
      ApiClient api = new ApiClient(getApplicationContext());
      ApiClient.Result r = api.ownerGet(nic);
      runOnUiThread(() -> {
        btnLoad.setEnabled(true);
        if (!r.ok || r.json == null) {
          toast(r.message != null ? r.message : "Load failed");
          return;
        }
        render(r.json);
      });
    }).start();
  }

  private void render(JSONObject o) {
    String nic = JsonUtils.optString(o, "nic");
    String fullName = JsonUtils.optString(o, "fullName");
    String email = JsonUtils.optString(o, "email");
    String phone = JsonUtils.optString(o, "phone");

    JSONObject addr = o.optJSONObject("address");
    String line1 = (addr != null ? JsonUtils.optString(addr, "line1") : null);
    String line2 = (addr != null ? JsonUtils.optString(addr, "line2") : null);
    String city  = (addr != null ? JsonUtils.optString(addr, "city") : null);

    if (nic != null) etNic.setText(nic);
    if (fullName != null) etFullName.setText(fullName);
    if (email != null) etEmail.setText(email);
    if (phone != null) etPhone.setText(phone);
    if (line1 != null) etLine1.setText(line1);
    if (line2 != null) etLine2.setText(line2);
    if (city  != null) etCity.setText(city);
  }

  private void doSave() {
    final String nic = etNic.getText().toString().trim();
    if (nic.isEmpty()) { toast("Enter NIC"); return; }

    JSONObject body = new JSONObject();
    try {
      body.put("fullName", etFullName.getText().toString().trim());
      body.put("email", etEmail.getText().toString().trim());
      body.put("phone", etPhone.getText().toString().trim());
      body.put("addressLine1", etLine1.getText().toString().trim());
      body.put("addressLine2", etLine2.getText().toString().trim());
      body.put("city", etCity.getText().toString().trim());
    } catch (Exception ignored) {}

    btnSave.setEnabled(false);
    new Thread(() -> {
      ApiClient api = new ApiClient(getApplicationContext());
      ApiClient.Result r = api.ownerUpdate(nic, body);
      runOnUiThread(() -> {
        btnSave.setEnabled(true);
        String detail = (r.body != null ? r.body : (r.message != null ? r.message : ""));
        toast("Save: " + r.code + (detail.isEmpty() ? "" : " · " + shrink(detail, 140)));
        if (r.ok) doLoad(); // refresh
      });
    }).start();
  }

  /** Confirmation dialog before deactivation **/
  private void confirmDeactivate() {
    final String nic = etNic.getText().toString().trim();
    if (nic.isEmpty()) { toast("Enter NIC"); return; }

    new AlertDialog.Builder(this, com.google.android.material.R.style.ThemeOverlay_Material3_Dialog_Alert)
      .setTitle("Confirm Deactivation")
      .setMessage("Are you sure you want to deactivate this account?\nYou can later request reactivation through BackOffice.")
      .setPositiveButton("Deactivate", (dialog, which) -> doDeactivate(nic))
      .setNegativeButton("Cancel", null)
      .show();
  }

  /** Actual API call **/
  private void doDeactivate(String nic) {
    btnDeactivate.setEnabled(false);
    new Thread(() -> {
      ApiClient api = new ApiClient(getApplicationContext());
      ApiClient.Result r = api.ownerDeactivate(nic);
      runOnUiThread(() -> {
        btnDeactivate.setEnabled(true);
        String detail = (r.body != null ? r.body : (r.message != null ? r.message : ""));
        toast("Deactivate: " + r.code + (detail.isEmpty() ? "" : " · " + shrink(detail, 140)));
        if (r.ok) finish(); // close activity
      });
    }).start();
  }

  private static String shrink(String s, int max) {
    return (s != null && s.length() > max) ? s.substring(0, max) + "…" : (s != null ? s : "");
  }

  private void toast(String m) {
    Toast.makeText(this, m, Toast.LENGTH_LONG).show();
  }
}
