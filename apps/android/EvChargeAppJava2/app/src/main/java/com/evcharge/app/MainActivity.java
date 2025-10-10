package com.evcharge.app;

import android.os.Bundle;
import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatActivity;

/** Legacy template activity (unused). Keep as a stub to avoid build errors. */
public final class MainActivity extends AppCompatActivity {
    @Override
    protected void onCreate(@Nullable Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        // Not used; immediately finish to ensure no accidental navigation here.
        finish();
    }
}
