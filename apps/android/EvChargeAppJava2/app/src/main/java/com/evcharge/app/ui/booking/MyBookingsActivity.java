package com.evcharge.app.ui.booking;

import android.content.Intent;
import android.os.Bundle;

import androidx.annotation.Nullable;
import androidx.appcompat.app.AppCompatActivity;

import com.evcharge.app.ui.main.MainActivity;

public final class MyBookingsActivity extends AppCompatActivity {

  @Override
  protected void onCreate(@Nullable Bundle savedInstanceState) {
    super.onCreate(savedInstanceState);

    // Redirect immediately to MainActivity → Bookings tab/fragment
    Intent i = new Intent(this, MainActivity.class);
    i.putExtra("nav", "bookings"); // MainActivity should switch to fragment_bookings when it sees this
    i.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_SINGLE_TOP);
    startActivity(i);

    // Close this redirector activity
    finish();
  }
}
