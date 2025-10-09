package com.evcharge.app.ui.splash;

import android.content.Intent;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.View;

import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatActivity;

import com.evcharge.app.R;
import com.evcharge.app.core.prefs.AppPrefs;
import com.evcharge.app.core.security.JwtStore;
import com.evcharge.app.ui.auth.LoginActivity;
import com.evcharge.app.ui.main.MainActivity;
import com.evcharge.app.ui.onboarding.OnboardingActivity;
import com.evcharge.app.ui.operator.OperatorOneActivity;

/**
 * Shows a short 3-stage intro sequence (after system splash), then routes.
 *   Stage 1 -> Stage 2 -> Stage 3 -> navigate (unchanged business logic).
 */
public final class SplashActivity extends AppCompatActivity {

  private final Handler handler = new Handler(Looper.getMainLooper());
  private View stage1, stage2, stage3;

  // Durations (tweak if you want a faster/slower sequence)
  private static final long STAGE_DURATION = 800L;
  private static final long FADE_DURATION  = 300L;


  // Keep a flag to avoid double navigation if the Activity is destroyed
  private boolean navigated = false;

  @Override
  protected void onCreate(@Nullable Bundle savedInstanceState) {
    super.onCreate(savedInstanceState);

    // 1) Inflate our in-app sequence view (system splash has already shown)
    setContentView(R.layout.splash_sequence);

    stage1 = findViewById(R.id.stage1);
    stage2 = findViewById(R.id.stage2);
    stage3 = findViewById(R.id.stage3);

    // 2) Precompute where to go (your existing logic — unchanged)
    final Class<?> next = computeNextScreen();

    // 3) Play the three quick stages, then route
    playSequenceThenNavigate(next);
  }

  private Class<?> computeNextScreen() {
    AppPrefs prefs = new AppPrefs(getApplicationContext());
    JwtStore jwt = new JwtStore(getApplicationContext());

    final boolean hasOnboarded = prefs.hasOnboarded();
    final boolean jwtOk = jwt.isValid();

    if (!hasOnboarded) {
      return OnboardingActivity.class;
    }
    if (jwtOk) {
      boolean isOperator = jwt.hasRole("Operator");
      boolean isOwner    = jwt.hasRole("Owner");
      if (isOperator) return OperatorOneActivity.class;
      if (isOwner)    return MainActivity.class;

      // Valid token but unsupported roles → force fresh login
      jwt.clear();
      return LoginActivity.class;
    }
    return LoginActivity.class;
  }

  private void playSequenceThenNavigate(Class<?> next) {
    // Start with stage1 visible (XML already sets it visible/alpha=1)
    // Fade to stage2
    handler.postDelayed(() -> crossfade(stage1, stage2, () -> {
      // Hold stage2 briefly, then fade to stage3
      handler.postDelayed(() -> crossfade(stage2, stage3, () -> {
        // Hold stage3 briefly, then navigate
        handler.postDelayed(() -> navigate(next), STAGE_DURATION);
      }), STAGE_DURATION);
    }), STAGE_DURATION);
  }

  private void crossfade(View out, View in, Runnable endAction) {
    in.setVisibility(View.VISIBLE);
    in.setAlpha(0f);
    in.animate()
      .alpha(1f)
      .setDuration(FADE_DURATION)
      .withStartAction(() -> {
        out.animate()
          .alpha(0f)
          .setDuration(FADE_DURATION)
          .withEndAction(() -> out.setVisibility(View.GONE))
          .start();
      })
      .withEndAction(endAction)
      .start();
  }

  private void navigate(Class<?> next) {
    if (navigated || isFinishing() || isDestroyed()) return;
    navigated = true;
    Intent i = new Intent(this, next);
    i.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_NEW_TASK);
    startActivity(i);
    finish();
  }

  @Override
  protected void onDestroy() {
    super.onDestroy();
    handler.removeCallbacksAndMessages(null);
  }
}
