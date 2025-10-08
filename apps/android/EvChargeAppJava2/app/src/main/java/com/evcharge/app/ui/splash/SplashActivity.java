package com.evcharge.app.ui.splash;

import android.content.Intent;
import android.os.Bundle;

import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatActivity;

import com.evcharge.app.core.prefs.AppPrefs;
import com.evcharge.app.core.security.JwtStore;
import com.evcharge.app.ui.auth.LoginActivity;
import com.evcharge.app.ui.main.MainActivity;
import com.evcharge.app.ui.onboarding.OnboardingActivity;
import com.evcharge.app.ui.operator.OperatorOneActivity;

/**
 * Splash router:
 *  - First run (not onboarded) -> OnboardingActivity
 *  - If onboarded and JWT valid:
 *        Operator role -> OperatorOneActivity
 *        Owner role    -> MainActivity
 *        (anything else) -> LoginActivity
 *  - Else -> LoginActivity
 */
public final class SplashActivity extends AppCompatActivity {

  @Override
  protected void onCreate(@Nullable Bundle savedInstanceState) {
    super.onCreate(savedInstanceState);

    AppPrefs prefs = new AppPrefs(getApplicationContext());
    JwtStore jwt = new JwtStore(getApplicationContext());

    final boolean hasOnboarded = prefs.hasOnboarded();
    final boolean jwtOk = jwt.isValid();

    Class<?> next;
    if (!hasOnboarded) {
      next = OnboardingActivity.class;
    } else if (jwtOk) {
      boolean isOperator = jwt.hasRole("Operator");
      boolean isOwner    = jwt.hasRole("Owner");

      if (isOperator) {
        next = OperatorOneActivity.class;
      } else if (isOwner) {
        next = MainActivity.class;
      } else {
        // Valid token but unsupported roles → force fresh login
        jwt.clear();
        next = LoginActivity.class;
      }
    } else {
      next = LoginActivity.class;
    }

    Intent i = new Intent(this, next);
    i.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_NEW_TASK);
    startActivity(i);
    finish();
  }
}
