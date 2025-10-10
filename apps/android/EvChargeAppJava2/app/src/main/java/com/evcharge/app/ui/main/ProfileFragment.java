package com.evcharge.app.ui.main;

import android.app.AlertDialog;
import android.content.Intent;
import android.os.Bundle;
import android.util.Base64;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.EditText;
import android.widget.TextView;
import android.widget.Toast;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.fragment.app.Fragment;

import com.evcharge.app.R;
import com.evcharge.app.core.db.UserDao;
import com.evcharge.app.core.db.UserDao.UserRecord;
import com.evcharge.app.core.net.ApiClient;
import com.evcharge.app.core.prefs.AppPrefs;
import com.evcharge.app.core.security.JwtStore;
import com.evcharge.app.core.util.JsonUtils;
import com.evcharge.app.ui.auth.LoginActivity;

import org.json.JSONObject;

public final class ProfileFragment extends Fragment {

  private TextView tvNic;
  private EditText etFullName, etEmail, etPhone, etLine1, etLine2, etCity;
  private Button btnSave, btnDeactivate, btnLogout;

  private String currentNic = null;

  @Nullable
  @Override
  public View onCreateView(@NonNull LayoutInflater inflater, @Nullable ViewGroup container, @Nullable Bundle savedInstanceState) {
    View v = inflater.inflate(R.layout.fragment_profile, container, false);

    tvNic = v.findViewById(R.id.tvNic);
    etFullName = v.findViewById(R.id.etFullName);
    etEmail = v.findViewById(R.id.etEmail);
    etPhone = v.findViewById(R.id.etPhone);
    etLine1 = v.findViewById(R.id.etLine1);
    etLine2 = v.findViewById(R.id.etLine2);
    etCity = v.findViewById(R.id.etCity);

    btnSave = v.findViewById(R.id.btnSave);
    btnDeactivate = v.findViewById(R.id.btnDeactivate);
    btnLogout = v.findViewById(R.id.btnLogout);

    btnSave.setOnClickListener(view -> doSave());
    btnDeactivate.setOnClickListener(view -> confirmDeactivate());
    btnLogout.setOnClickListener(view -> {
      // Clear auth + active NIC
      new JwtStore(requireContext().getApplicationContext()).clear();
      new AppPrefs(requireContext().getApplicationContext()).clearActiveNic();
      // Optional: also remove local snapshot row for this NIC
      try {
        if (currentNic != null && !currentNic.isEmpty()) {
          new UserDao(requireContext().getApplicationContext()).deleteByIdKey(currentNic);
        }
      } catch (Exception ignored) {}

      Intent i = new Intent(requireContext(), LoginActivity.class);
      i.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_NEW_TASK);
      startActivity(i);
      requireActivity().finish();
    });

    return v;
  }

  @Override
  public void onResume() {
    super.onResume();
    resolveNicAndLoad();
  }

  private void resolveNicAndLoad() {
    // 1) Try AppPrefs
    AppPrefs prefs = new AppPrefs(requireContext().getApplicationContext());
    String nic = prefs.getActiveNic();

    // 2) Fallback: decode JWT for nic/sub (ignore emails)
    if (nic == null) {
      try {
        String token = new JwtStore(requireContext().getApplicationContext()).getToken();
        if (token != null && token.contains(".")) {
          String[] parts = token.split("\\.");
          if (parts.length >= 2) {
            String payload = new String(Base64.decode(parts[1], Base64.URL_SAFE | Base64.NO_WRAP | Base64.NO_PADDING));
            JSONObject p = JsonUtils.parseObject(payload);
            if (p != null) {
              String candidate = JsonUtils.optString(p, "nic");
              if (candidate == null) candidate = JsonUtils.optString(p, "sub");
              if (candidate != null && candidate.contains("@")) candidate = null; // not a NIC
              nic = candidate;
            }
          }
        }
      } catch (Exception ignored) {}
    }

    currentNic = nic;
    tvNic.setText("NIC: " + (currentNic != null ? currentNic : "unknown"));

    boolean enable = (currentNic != null && !currentNic.isEmpty());
    btnSave.setEnabled(enable);
    btnDeactivate.setEnabled(enable);

    // 3) Try local snapshot for instant UI
    if (enable) {
      try {
        UserDao dao = new UserDao(requireContext().getApplicationContext());
        UserRecord rec = dao.getByIdKey(currentNic);
        if (rec != null) {
          JSONObject pseudo = new JSONObject();
          try {
            if (rec.fullName != null) pseudo.put("fullName", rec.fullName);
            if (rec.phone != null) pseudo.put("phone", rec.phone);
            // email not cached locally; server will provide it
          } catch (Exception ignored) {}
          render(pseudo);
        }
      } catch (Exception ignored) {}
    }

    if (enable) {
      doLoad(currentNic);
    } else {
      toast("Could not determine NIC. Please log out and log in again as Owner.");
    }
  }

  private void doLoad(String nic) {
    new Thread(() -> {
      ApiClient api = new ApiClient(requireContext().getApplicationContext());
      ApiClient.Result r = api.ownerGet(nic);
      requireActivity().runOnUiThread(() -> {
        if (!r.ok || r.json == null) {
          toast(r.message != null ? r.message : "Load failed");
          return;
        }
        render(r.json);

        // Persist refreshed fields back to local snapshot (best-effort)
        try {
          UserDao dao = new UserDao(requireContext().getApplicationContext());
          UserRecord rec = new UserRecord();
          rec.idKey = currentNic;
          rec.role = "Owner";
          rec.fullName = JsonUtils.optString(r.json, "fullName");
          rec.phone = JsonUtils.optString(r.json, "phone");
          rec.status = null;        // unknown; server is source of truth
          rec.lastLoginUtc = null;  // keep previous login time; not updated here
          dao.insertOrReplace(rec);
        } catch (Exception ignored) {}
      });
    }).start();
  }

  private void render(JSONObject o) {
    String fullName = JsonUtils.optString(o, "fullName");
    String email = JsonUtils.optString(o, "email");
    String phone = JsonUtils.optString(o, "phone");

    JSONObject addr = o.optJSONObject("address");
    String line1 = (addr != null ? JsonUtils.optString(addr, "line1") : null);
    String line2 = (addr != null ? JsonUtils.optString(addr, "line2") : null);
    String city  = (addr != null ? JsonUtils.optString(addr, "city") : null);

    if (fullName != null) etFullName.setText(fullName); else etFullName.setText("");
    if (email != null) etEmail.setText(email); else etEmail.setText("");
    if (phone != null) etPhone.setText(phone); else etPhone.setText("");
    if (line1 != null) etLine1.setText(line1); else etLine1.setText("");
    if (line2 != null) etLine2.setText(line2); else etLine2.setText("");
    if (city  != null) etCity.setText(city); else etCity.setText("");
  }

  private void doSave() {
    if (currentNic == null || currentNic.isEmpty()) {
      toast("NIC unavailable");
      return;
    }

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
      ApiClient api = new ApiClient(requireContext().getApplicationContext());
      ApiClient.Result r = api.ownerUpdate(currentNic, body);
      requireActivity().runOnUiThread(() -> {
        btnSave.setEnabled(true);
        String detail = (r.body != null ? r.body : (r.message != null ? r.message : ""));
        toast("Save: " + r.code + (detail.isEmpty() ? "" : " · " + shrink(detail, 140)));
        if (r.ok) doLoad(currentNic);
      });
    }).start();
  }

  private void confirmDeactivate() {
    if (currentNic == null || currentNic.isEmpty()) {
      toast("NIC unavailable");
      return;
    }
    new AlertDialog.Builder(requireContext())
      .setTitle("Deactivate account?")
      .setMessage("This will deactivate the owner profile. Continue?")
      .setPositiveButton("Deactivate", (d, w) -> doDeactivate())
      .setNegativeButton("Cancel", null)
      .show();
  }

  private void doDeactivate() {
    btnDeactivate.setEnabled(false);
    new Thread(() -> {
      ApiClient api = new ApiClient(requireContext().getApplicationContext());
      ApiClient.Result r = api.ownerDeactivate(currentNic);
      requireActivity().runOnUiThread(() -> {
        btnDeactivate.setEnabled(true);
        String detail = (r.body != null ? r.body : (r.message != null ? r.message : ""));
        toast("Deactivate: " + r.code + (detail.isEmpty() ? "" : " · " + shrink(detail, 140)));
        if (r.ok) {
          // After deactivation, log out
          new JwtStore(requireContext().getApplicationContext()).clear();
          new AppPrefs(requireContext().getApplicationContext()).clearActiveNic();
          try {
            new UserDao(requireContext().getApplicationContext()).deleteByIdKey(currentNic);
          } catch (Exception ignored) {}
          Intent i = new Intent(requireContext(), LoginActivity.class);
          i.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_NEW_TASK);
          startActivity(i);
          requireActivity().finish();
        }
      });
    }).start();
  }

  private static String shrink(String s, int max) {
    return (s != null && s.length() > max) ? s.substring(0, max) + "…" : (s != null ? s : "");
  }

  private void toast(String m){
    Toast.makeText(requireContext(), m, Toast.LENGTH_LONG).show();
  }
}
