package com.evcharge.app.ui.main;

import android.os.Bundle;
import android.widget.Button;

import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatActivity;
import androidx.fragment.app.Fragment;

import com.evcharge.app.R;

public final class MainActivity extends AppCompatActivity {

    private Button btnTabDash, btnTabBookings, btnTabProfile;

    @Override
    protected void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        btnTabDash = findViewById(R.id.btnTabDash);
        btnTabBookings = findViewById(R.id.btnTabBookings);
        btnTabProfile = findViewById(R.id.btnTabProfile);

        // Default tab
        if (savedInstanceState == null) {
            show(new DashboardFragment());
            setActive(btnTabDash);
        }

        btnTabDash.setOnClickListener(v -> {
            show(new DashboardFragment());
            setActive(btnTabDash); clearActive(btnTabBookings, btnTabProfile);
        });

        btnTabBookings.setOnClickListener(v -> {
            show(new BookingsFragment());
            setActive(btnTabBookings); clearActive(btnTabDash, btnTabProfile);
        });

        btnTabProfile.setOnClickListener(v -> {
            show(new ProfileFragment());
            setActive(btnTabProfile); clearActive(btnTabDash, btnTabBookings);
        });
    }

    private void show(Fragment f) {
        getSupportFragmentManager()
                .beginTransaction()
                .replace(R.id.fragment_container, f)
                .commit();
    }

    private void setActive(Button b) {
        b.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFF5DD62C));
        b.setTextColor(0xFF000000);
    }

    private void clearActive(Button... buttons) {
        for (Button b : buttons) {
            b.setBackgroundTintList(android.content.res.ColorStateList.valueOf(0xFF202020));
            b.setTextColor(0xFFF8F8F8);
        }
    }
}
