package com.evcharge.app.ui.auth;

import android.os.Bundle;
import android.util.Patterns;
import android.view.View;
import android.widget.Button;
import android.widget.EditText;
import android.widget.Toast;

import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatActivity;

import com.evcharge.app.R;
import com.evcharge.app.core.net.ApiClient;

public final class SignupActivity extends AppCompatActivity {

    private EditText etNic, etFullName, etEmail, etPhone, etPassword, etAddr1, etAddr2, etCity;
    private Button btnSignup;
    private View progressOverlay;

    @Override
    protected void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_signup);

        etNic = findViewById(R.id.etNic);
        etFullName = findViewById(R.id.etFullName);
        etEmail = findViewById(R.id.etEmail);
        etPhone = findViewById(R.id.etPhone);
        etPassword = findViewById(R.id.etPassword);
        etAddr1 = findViewById(R.id.etAddr1);
        etAddr2 = findViewById(R.id.etAddr2);
        etCity = findViewById(R.id.etCity);
        btnSignup = findViewById(R.id.btnSignup);

        progressOverlay = new View(this);
        progressOverlay.setClickable(true);
        progressOverlay.setFocusable(true);
        progressOverlay.setBackgroundColor(0x88000000);
        addContentView(progressOverlay, new android.widget.FrameLayout.LayoutParams(
                android.widget.FrameLayout.LayoutParams.MATCH_PARENT,
                android.widget.FrameLayout.LayoutParams.MATCH_PARENT));
        progressOverlay.setVisibility(View.GONE);

        btnSignup.setOnClickListener(v -> doSignup());
    }

    private void doSignup() {
        final String nic = etNic.getText().toString().trim();
        final String fullName = etFullName.getText().toString().trim();
        final String email = etEmail.getText().toString().trim();
        final String phone = etPhone.getText().toString().trim();
        final String password = etPassword.getText().toString();
        final String addr1 = etAddr1.getText().toString().trim();
        final String addr2 = etAddr2.getText().toString().trim();
        final String city = etCity.getText().toString().trim();

        if (nic.isEmpty()) { toast("Enter NIC"); return; }
        if (fullName.isEmpty()) { toast("Enter full name"); return; }
        if (email.isEmpty()) { toast("Enter email"); return; }
        if (!Patterns.EMAIL_ADDRESS.matcher(email).matches()) { toast("Enter a valid email"); return; }
        if (phone.isEmpty()) { toast("Enter phone"); return; }
        if (password.isEmpty()) { toast("Enter password"); return; }
        if (addr1.isEmpty()) { toast("Enter address line 1"); return; }
        if (city.isEmpty()) { toast("Enter city"); return; }

        setLoading(true);

        new Thread(() -> {
            ApiClient api = new ApiClient(getApplicationContext());
            ApiClient.Result res = api.registerOwner(
                    nic, fullName, email, phone, password, addr1, addr2, city
            );

            runOnUiThread(() -> {
                setLoading(false);
                if (res.ok) {
                    toast("Account created. Please log in.");
                    finish();
                } else {
                    String msg = (res.message != null && !res.message.isEmpty()) ? res.message : "Signup failed";
                    toast(msg + (res.code > 0 ? " (code " + res.code + ")" : ""));
                }
            });
        }).start();
    }

    private void toast(String m) { Toast.makeText(this, m, Toast.LENGTH_SHORT).show(); }

    private void setLoading(boolean loading) {
        btnSignup.setEnabled(!loading);
        progressOverlay.setVisibility(loading ? View.VISIBLE : View.GONE);
    }
}
