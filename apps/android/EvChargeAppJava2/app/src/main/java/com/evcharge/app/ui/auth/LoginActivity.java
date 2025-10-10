package com.evcharge.app.ui.auth;

import android.content.Intent;
import android.os.Bundle;
import android.view.View;
import android.widget.Button;
import android.widget.EditText;
import android.widget.Toast;

import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatActivity;

import com.evcharge.app.BuildConfig;
import com.evcharge.app.R;
import com.evcharge.app.core.net.ApiClient;
import com.evcharge.app.core.prefs.AppPrefs;
import com.evcharge.app.core.security.JwtStore;
import com.evcharge.app.ui.main.MainActivity;
import com.evcharge.app.ui.operator.OperatorOneActivity;

public final class LoginActivity extends AppCompatActivity {

  private EditText etUsername, etPassword;
  private Button btnLogin, btnGoToSignup;
  private View progressOverlay;

  @Override
  protected void onCreate(@Nullable Bundle savedInstanceState) {
    super.onCreate(savedInstanceState);
    setContentView(R.layout.activity_login);

    etUsername = findViewById(R.id.etUsername);
    etPassword = findViewById(R.id.etPassword);
    btnLogin = findViewById(R.id.btnLogin);
    btnGoToSignup = findViewById(R.id.btnGoToSignup);

    // Show current flavor & base URL for sanity
    Toast.makeText(this,
      "FLAVOR=" + BuildConfig.FLAVOR + "  BASE_URL=" + BuildConfig.BASE_URL,
      Toast.LENGTH_LONG).show();

    progressOverlay = new View(this);
    progressOverlay.setClickable(true);
    progressOverlay.setFocusable(true);
    progressOverlay.setBackgroundColor(0x88000000);
    addContentView(progressOverlay, new android.widget.FrameLayout.LayoutParams(
      android.widget.FrameLayout.LayoutParams.MATCH_PARENT,
      android.widget.FrameLayout.LayoutParams.MATCH_PARENT));
    progressOverlay.setVisibility(View.GONE);

    // Allow login on ANY flavor (localEmu/localLan/iis)
    btnLogin.setOnClickListener(v -> doLogin());

    btnGoToSignup.setOnClickListener(v ->
      startActivity(new Intent(this, SignupActivity.class)));
  }

  private void doLogin() {
    final String username = etUsername.getText().toString().trim();
    final String password = etPassword.getText().toString();

    if (username.isEmpty()) { toast("Enter username (NIC or Email)"); return; }
    if (password.isEmpty()) { toast("Enter password"); return; }

    setLoading(true);
    new Thread(() -> {
      ApiClient api = new ApiClient(getApplicationContext());
      ApiClient.LoginResult res = api.login(username, password);
      runOnUiThread(() -> {
        setLoading(false);
        if (!res.ok) {
          String msg = (res.message != null && !res.message.isEmpty()) ? res.message : "Login failed";
          toast(msg + (res.code > 0 ? " (code " + res.code + ")" : ""));
          return;
        }

        // Enforce role: only Owner or Operator may proceed
        JwtStore jwt = new JwtStore(getApplicationContext());
        boolean isOwner = jwt.hasRole("Owner");
        boolean isOperator = jwt.hasRole("Operator");

        // Fallback heuristic for older tokens: email → Operator, NIC → Owner
        if (!isOwner && !isOperator) {
          if (username.contains("@")) isOperator = true; else isOwner = true;
        }

        if (!isOwner && !isOperator) {
          // Unsupported role → hard stop and clear token
          new JwtStore(getApplicationContext()).clear();
          new AppPrefs(getApplicationContext()).clearActiveUser();
          toast("This account role is not supported. Only Owner or Operator are allowed.");
          return;
        }

        toast("Login successful");
        Intent i = new Intent(this, isOperator ? OperatorOneActivity.class : MainActivity.class);
        i.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_NEW_TASK);
        startActivity(i);
        finish();
      });
    }).start();
  }

  private void toast(String m) { Toast.makeText(this, m, Toast.LENGTH_SHORT).show(); }

  private void setLoading(boolean loading) {
    btnLogin.setEnabled(!loading);
    btnGoToSignup.setEnabled(!loading);
    progressOverlay.setVisibility(loading ? View.VISIBLE : View.GONE);
  }
}
