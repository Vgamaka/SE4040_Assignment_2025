package com.evcharge.app.ui.onboarding;

import android.content.Intent;
import android.os.Bundle;
import android.view.View;
import android.widget.Button;
import android.widget.TextView;

import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatActivity;

import com.evcharge.app.core.prefs.AppPrefs;
import com.evcharge.app.ui.auth.LoginActivity;
import com.evcharge.app.R;

public final class OnboardingActivity extends AppCompatActivity {

    private TextView tvTitle, tvBody, tvStep;
    private Button btnBack, btnSkip, btnNext;

    private int index = 0;

    private static final String[] TITLES = new String[] {
            "Find & Book Stations",
            "Server-Enforced Policies",
            "Use QR at the Station"
    };

    private static final String[] BODIES = new String[] {
            "Discover nearby charging stations and view details and schedules before booking.",
            "All business logic lives on the server: 7-day booking window, ≥12h modify/cancel cutoff, earliest check-in, and more.",
            "After approval, show your QR at the station to check in. Simple and fast."
    };

    @Override
    protected void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_onboarding);

        tvTitle = findViewById(R.id.tvTitle);
        tvBody  = findViewById(R.id.tvBody);
        tvStep  = findViewById(R.id.tvStep);
        btnBack = findViewById(R.id.btnBack);
        btnSkip = findViewById(R.id.btnSkip);
        btnNext = findViewById(R.id.btnNext);

        render();

        btnBack.setOnClickListener(v -> {
            if (index > 0) {
                index--;
                render();
            }
        });

        btnSkip.setOnClickListener(v -> finishOnboarding());

        btnNext.setOnClickListener(v -> {
            if (index < 2) {
                index++;
                render();
            } else {
                finishOnboarding();
            }
        });
    }

    private void render() {
        tvTitle.setText(TITLES[index]);
        tvBody.setText(BODIES[index]);
        tvStep.setText((index + 1) + " / 3");

        btnBack.setVisibility(index == 0 ? View.GONE : View.VISIBLE);
        btnNext.setText(index == 2 ? "Get Started" : "Next");
    }

    private void finishOnboarding() {
        new AppPrefs(getApplicationContext()).setHasOnboarded(true);
        Intent i = new Intent(this, LoginActivity.class);
        i.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_NEW_TASK);
        startActivity(i);
        finish();
    }
}
