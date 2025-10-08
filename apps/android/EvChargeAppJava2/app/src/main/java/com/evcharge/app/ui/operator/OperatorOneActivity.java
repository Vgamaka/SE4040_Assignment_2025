package com.evcharge.app.ui.operator;

import android.Manifest;
import android.app.AlertDialog;
import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.os.Bundle;
import android.os.Vibrator;
import android.text.InputType;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.ArrayAdapter;
import android.widget.AutoCompleteTextView;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.TextView;
import android.widget.EditText;
import android.widget.Toast;

import androidx.activity.ComponentActivity;
import androidx.annotation.NonNull;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;
import androidx.swiperefreshlayout.widget.SwipeRefreshLayout;
import androidx.recyclerview.widget.RecyclerView;
import androidx.recyclerview.widget.LinearLayoutManager;

import com.evcharge.app.R;
import com.evcharge.app.core.net.ApiClient;
import com.evcharge.app.core.prefs.AppPrefs;
import com.evcharge.app.core.security.JwtStore;
import com.evcharge.app.core.util.JsonUtils;
import com.evcharge.app.ui.auth.LoginActivity;
import com.google.android.material.floatingactionbutton.FloatingActionButton;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.Locale;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

import androidx.camera.core.CameraSelector;
import androidx.camera.core.ImageAnalysis;
import androidx.camera.core.ImageProxy;
import androidx.camera.core.Preview;
import androidx.camera.lifecycle.ProcessCameraProvider;
import androidx.camera.view.PreviewView;

import com.google.common.util.concurrent.ListenableFuture;
import com.google.zxing.BinaryBitmap;
import com.google.zxing.Result;
import com.google.zxing.common.HybridBinarizer;
import com.google.zxing.qrcode.QRCodeReader;

import java.nio.ByteBuffer;
import java.util.concurrent.Executor;
import java.util.concurrent.Executors;

// Maps
import com.google.android.gms.maps.CameraUpdateFactory;
import com.google.android.gms.maps.GoogleMap;
import com.google.android.gms.maps.MapView;
import com.google.android.gms.maps.model.LatLng;
import com.google.android.gms.maps.OnMapReadyCallback;
import com.google.android.gms.maps.model.MarkerOptions;

public final class OperatorOneActivity extends ComponentActivity implements OnMapReadyCallback {

  // UI
  private AutoCompleteTextView stationSearch;
  private Button btnLogoutOp;
  private MapView mapView;
  private GoogleMap gmap;
  private SwipeRefreshLayout swipe;
  private RecyclerView rv;
  private ProgressBar progress;
  private LinearLayout filterRow;
  private FloatingActionButton fabScan;

  // Scan overlay
  private View scanOverlay;
  private PreviewView previewView;
  private Button btnScanClose;

  // Data
  private final Executor cameraExecutor = Executors.newSingleThreadExecutor();
  private volatile boolean scanning = false;
  private volatile boolean handledScan = false;

  private ApiClient api;
  private AppPrefs prefs;

  private final List<String> statusFilters = List.of(
    "Pending", "Approved", "Rejected", "Cancelled", "NoShow", "Aborted", "Expired", "CheckedIn", "Completed"
  );
  private String selectedStatus = "Approved";

  private static final int REQ_CAMERA = 2001;

  // Stations loaded from operatorStationIds
  private static final class Station {
    final String id;
    final String name;
    final double lat;
    final double lng;
    Station(String id, String name, double lat, double lng){ this.id=id; this.name=name; this.lat=lat; this.lng=lng; }
    @Override public String toString(){ return name != null ? name : id; }
  }
  private final Map<String, Station> stationsById = new HashMap<>();
  private final List<Station> stationList = new ArrayList<>();
  private Station selectedStation = null;

  // Inbox cache (raw)
  private JSONArray inboxRaw = new JSONArray();

  // Adapter
  private BookingAdapter adapter;

  @Override
  protected void onCreate(Bundle savedInstanceState) {
    super.onCreate(savedInstanceState);
    setContentView(R.layout.activity_operator_one);

    api = new ApiClient(getApplicationContext());
    prefs = new AppPrefs(getApplicationContext());

    stationSearch = findViewById(R.id.stationSearch);
    btnLogoutOp = findViewById(R.id.btnLogoutOp);
    mapView = findViewById(R.id.mapView);
    mapView.onCreate(savedInstanceState);
    mapView.getMapAsync(this);

    swipe = findViewById(R.id.swipe);
    rv = findViewById(R.id.recycler);
    progress = findViewById(R.id.progress);
    filterRow = findViewById(R.id.filterRow);
    fabScan = findViewById(R.id.fabScan);

    scanOverlay = findViewById(R.id.scanOverlay);
    previewView = findViewById(R.id.previewView);
    btnScanClose = findViewById(R.id.btnScanClose);

    // Guard auth
    if (!new JwtStore(getApplicationContext()).isValid()) {
      toast("Session expired. Please log in again.");
      startActivity(new Intent(this, LoginActivity.class));
      finish();
      return;
    }

    // Logout button
    btnLogoutOp.setOnClickListener(v -> confirmLogout());

    // Filters UI
    inflateFilters();

    // Recycler
    rv.setLayoutManager(new LinearLayoutManager(this));
    adapter = new BookingAdapter();
    rv.setAdapter(adapter);

    // Pull-to-refresh
    swipe.setOnRefreshListener(this::refreshInbox);

    // Scan
    fabScan.setOnClickListener(v -> startScanOrManual());
    btnScanClose.setOnClickListener(v -> stopScanning());

    // Stations
    loadStationsThenInbox();
  }

  // ==== Logout ====
  private void confirmLogout() {
    new AlertDialog.Builder(this)
      .setTitle("Logout?")
      .setMessage("You will be returned to the sign-in screen.")
      .setPositiveButton("Logout", (d, w) -> doLogout())
      .setNegativeButton("Cancel", null)
      .show();
  }

  private void doLogout() {
    try { stopScanning(); } catch (Exception ignore) {}
    try { new JwtStore(getApplicationContext()).clear(); } catch (Exception ignore) {}
    try {
      AppPrefs p = new AppPrefs(getApplicationContext());
      p.clearActiveUser();
      p.clearActiveNic();
      p.clearOperatorSnapshot();
    } catch (Exception ignore) {}
    Intent i = new Intent(this, LoginActivity.class);
    i.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP | Intent.FLAG_ACTIVITY_NEW_TASK);
    startActivity(i);
    finish();
  }

  // ==== STATIONS & INBOX ====
  private void loadStationsThenInbox() {
    setBusy(true);

    stationList.clear();
    stationsById.clear();

    // Get station ids from prefs (set during login)
    List<String> ids = prefs.getOperatorStationIdsList();
    if (ids == null || ids.isEmpty()) {
      setBusy(false);
      toast("No stations assigned to this operator.");
      return;
    }

    // Load meta for each station (authed GET /api/Station/{id})
    new Thread(() -> {
      for (String id : ids) {
        try {
          ApiClient.Result r = api.stationDetailAuthed(id);
          if (r.ok && r.json != null) {
            String name = JsonUtils.optString(r.json, "name");
            double lat = 6.9271, lng = 79.8612;
            try {
              if (r.json.has("lat")) lat = r.json.optDouble("lat", lat);
              if (r.json.has("lng")) lng = r.json.optDouble("lng", lng);
            } catch (Exception ignored) {}
            Station s = new Station(id, name, lat, lng);
            stationsById.put(id, s);
            stationList.add(s);
          } else if (r.code == 401) {
            runOnUiThread(this::handleUnauthorized);
            return;
          }
        } catch (Exception ignored) {}
      }

      runOnUiThread(() -> {
        setBusy(false);
        bindStationDropdown();
        if (!stationList.isEmpty()) {
          setSelectedStation(stationList.get(0));
          refreshInbox();
        }
      });
    }).start();
  }

  private void bindStationDropdown() {
    List<String> names = new ArrayList<>();
    for (Station s : stationList) names.add(s.name != null ? s.name : s.id);
    ArrayAdapter<String> adapter = new ArrayAdapter<>(this, android.R.layout.simple_dropdown_item_1line, names);
    stationSearch.setAdapter(adapter);
    stationSearch.setOnItemClickListener((parent, view, position, id) -> {
      if (position >= 0 && position < stationList.size()) {
        setSelectedStation(stationList.get(position));
        refreshInbox();
      }
    });
    // Enable type-to-filter
    stationSearch.setOnFocusChangeListener((v, hasFocus) -> { if (hasFocus) stationSearch.showDropDown(); });
  }

  private void setSelectedStation(Station s) {
    selectedStation = s;
    stationSearch.setText(s.name != null ? s.name : s.id, false);
    updateMapPin();
  }

  private void updateMapPin() {
    if (gmap == null || selectedStation == null) return;
    gmap.clear();
    LatLng ll = new LatLng(selectedStation.lat, selectedStation.lng);
    gmap.addMarker(new MarkerOptions().position(ll).title(selectedStation.name));
    gmap.moveCamera(CameraUpdateFactory.newLatLngZoom(ll, 15f));
  }

  private void refreshInbox() {
    if (selectedStation == null) { swipe.setRefreshing(false); return; }
    final String today = todayYmd();

    new Thread(() -> {
      ApiClient.Result r = api.operatorInbox(today);
      runOnUiThread(() -> {
        swipe.setRefreshing(false);
        if (!r.ok) {
          if (r.code == 401) { handleUnauthorized(); return; }
          toast(r.message != null ? r.message : "Load failed");
          return;
        }
        // Expect JSONArray; parse from raw body for safety
        JSONArray inbox = tryParseArray(r.body);
        if (inbox == null) inbox = new JSONArray();

        inboxRaw = inbox;
        applyFiltersAndRender();
      });
    }).start();
  }

  private void applyFiltersAndRender() {
    if (selectedStation == null) return;

    List<JSONObject> filtered = new ArrayList<>();
    for (int i = 0; i < inboxRaw.length(); i++) {
      JSONObject b = inboxRaw.optJSONObject(i);
      if (b == null) continue;
      String sid = JsonUtils.optString(b, "stationId");
      String status = JsonUtils.optString(b, "status");
      if (selectedStation.id.equals(sid) && (selectedStatus == null || selectedStatus.equalsIgnoreCase(status))) {
        filtered.add(b);
      }
    }
    adapter.submit(filtered);
  }

  // ==== FILTER BUTTONS ====
  private void inflateFilters() {
    filterRow.removeAllViews();
    for (String s : statusFilters) {
      Button btn = new Button(this);
      btn.setText(s);
      btn.setMinHeight(dp(36));
      btn.setAllCaps(false);
      btn.setTextColor(0xFFFFFFFF);
      btn.setBackgroundColor(s.equals(selectedStatus) ? 0xFF337418 : 0xFF202020);
      LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.WRAP_CONTENT, LinearLayout.LayoutParams.WRAP_CONTENT);
      lp.setMargins(dp(6), dp(6), dp(6), dp(6));
      btn.setLayoutParams(lp);
      btn.setOnClickListener(v -> {
        selectedStatus = s;
        inflateFilters();
        applyFiltersAndRender();
      });
      filterRow.addView(btn);
    }
  }

  // ==== BOOKING DETAIL & EXCEPTION ====
  private void showBookingDialog(JSONObject booking) {
    String bookingId = JsonUtils.optString(booking, "id");
    String code = JsonUtils.optString(booking, "bookingCode");
    String status = JsonUtils.optString(booking, "status");
    String when = JsonUtils.optString(booking, "slotStartLocal");
    int minutes = booking.optInt("slotMinutes", 0);
    String nicMasked = JsonUtils.optString(booking, "ownerNicMasked");

    StringBuilder sb = new StringBuilder();
    if (code != null) sb.append("Code: ").append(code).append("\n");
    if (status != null) sb.append("Status: ").append(status).append("\n");
    if (when != null) sb.append("Start: ").append(when).append("\n");
    if (minutes > 0) sb.append("Duration: ").append(minutes).append(" min\n");
    if (nicMasked != null) sb.append("Owner: ").append(nicMasked).append("\n");

    new AlertDialog.Builder(this)
      .setTitle("Booking Details")
      .setMessage(sb.toString())
      .setPositiveButton("Report Exception", (d,w) -> promptException(bookingId))
      .setNegativeButton("Close", null)
      .show();
  }

  private void promptException(String bookingId) {
    LinearLayout root = new LinearLayout(this);
    root.setOrientation(LinearLayout.VERTICAL);
    final EditText etReason = new EditText(this);
    etReason.setHint("Reason");
    final EditText etNotes = new EditText(this);
    etNotes.setHint("Notes (optional)");
    etReason.setInputType(InputType.TYPE_CLASS_TEXT);
    etNotes.setInputType(InputType.TYPE_CLASS_TEXT);
    root.setPadding(dp(16), dp(8), dp(16), dp(8));
    root.addView(etReason);
    root.addView(etNotes);

    new AlertDialog.Builder(this)
      .setTitle("Report Exception")
      .setView(root)
      .setPositiveButton("Submit", (d,w) -> doException(bookingId, etReason.getText().toString().trim(), etNotes.getText().toString().trim()))
      .setNegativeButton("Cancel", null)
      .show();
  }

  private void doException(String bookingId, String reason, String notes) {
    if (bookingId == null || bookingId.isEmpty()) { toast("Missing bookingId"); return; }
    if (reason.isEmpty()) { toast("Enter a reason"); return; }

    setBusy(true);
    new Thread(() -> {
      try {
        JSONObject body = new JSONObject();
        body.put("bookingId", bookingId);
        body.put("reason", reason);
        body.put("notes", notes);
        ApiClient.Result r = api.postAuthed("/api/Operator/exception", body);
        runOnUiThread(() -> {
          setBusy(false);
          if (r.ok) {
            toast("Exception recorded");
            refreshInbox();
          } else if (r.code == 401) {
            handleUnauthorized();
          } else {
            toast(r.message != null ? r.message : "Failed: " + r.code);
          }
        });
      } catch (Exception e) {
        runOnUiThread(() -> {
          setBusy(false);
          toast("Network error: " + e.getMessage());
        });
      }
    }).start();
  }

  // ==== SCAN ====
  private void startScanOrManual() {
    if (ContextCompat.checkSelfPermission(this, Manifest.permission.CAMERA) != PackageManager.PERMISSION_GRANTED) {
      ActivityCompat.requestPermissions(this, new String[]{Manifest.permission.CAMERA}, REQ_CAMERA);
      promptManualToken();
      return;
    }
    startCameraOverlay();
  }
  private void startCameraOverlay() {
    scanOverlay.setVisibility(View.VISIBLE);
    handledScan = false;
    scanning = true;
    bindCamera();
  }
  private void stopScanning() {
    scanning = false;
    scanOverlay.setVisibility(View.GONE);
  }
  private void bindCamera() {
    ListenableFuture<ProcessCameraProvider> cameraProviderFuture = ProcessCameraProvider.getInstance(this);
    cameraProviderFuture.addListener(() -> {
      try {
        ProcessCameraProvider cameraProvider = cameraProviderFuture.get();
        Preview preview = new Preview.Builder().build();
        preview.setSurfaceProvider(previewView.getSurfaceProvider());
        ImageAnalysis analysis = new ImageAnalysis.Builder()
          .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
          .build();
        analysis.setAnalyzer(cameraExecutor, this::analyze);
        CameraSelector selector = CameraSelector.DEFAULT_BACK_CAMERA;
        cameraProvider.unbindAll();
        cameraProvider.bindToLifecycle(this, selector, preview, analysis);
      } catch (Exception e) {
        toast("Camera error: " + e.getMessage());
        stopScanning();
        promptManualToken();
      }
    }, ContextCompat.getMainExecutor(this));
  }

  private void analyze(@NonNull ImageProxy image) {
    if (!scanning || handledScan) { image.close(); return; }
    try {
      ByteBuffer buffer = image.getPlanes()[0].getBuffer();
      byte[] data = new byte[buffer.remaining()];
      buffer.get(data);
      int width = image.getWidth();
      int height = image.getHeight();
      com.google.zxing.LuminanceSource source = new com.google.zxing.PlanarYUVLuminanceSource(
        data, width, height, 0, 0, width, height, false);
      BinaryBitmap bitmap = new BinaryBitmap(new HybridBinarizer(source));
      Result result = new QRCodeReader().decode(bitmap);
      if (result != null) {
        handledScan = true;
        vibrateShort();
        String token = result.getText();
        runOnUiThread(() -> {
          stopScanning();
          onTokenCaptured(token);
        });
      }
    } catch (Exception ignore) {
    } finally { image.close(); }
  }

  private void promptManualToken() {
    final EditText et = new EditText(this);
    et.setHint("Paste QR token");
    et.setInputType(InputType.TYPE_CLASS_TEXT);
    new AlertDialog.Builder(this)
      .setTitle("Enter QR Token")
      .setView(et)
      .setPositiveButton("OK", (d, w) -> {
        String t = et.getText().toString().trim();
        if (!t.isEmpty()) onTokenCaptured(t);
      })
      .setNegativeButton("Cancel", null)
      .show();
  }

  private void onTokenCaptured(String qrToken) {
    // Verify then confirm flow
    setBusy(true);
    new Thread(() -> {
      try {
        JSONObject body = new JSONObject();
        body.put("qrToken", qrToken);
        ApiClient.Result vr = api.postAuthed("/api/Qr/verify", body);
        runOnUiThread(() -> {
          if (!vr.ok || vr.json == null) {
            setBusy(false);
            if (vr.code == 401) { handleUnauthorized(); return; }
            toast(vr.message != null ? vr.message : "Verify failed");
            return;
          }
          String bookingId = JsonUtils.optString(vr.json, "bookingId");
          String stationId = JsonUtils.optString(vr.json, "stationId");
          String status = JsonUtils.optString(vr.json, "status");
          String message = JsonUtils.optString(vr.json, "message");
          showConfirmDialog(qrToken, bookingId, stationId, status, message);
        });
      } catch (Exception e) {
        runOnUiThread(() -> {
          setBusy(false);
          toast("Network error: " + e.getMessage());
        });
      }
    }).start();
  }

  private void showConfirmDialog(String qrToken, String bookingId, String stationId, String status, String message) {
    StringBuilder sb = new StringBuilder();
    if (bookingId != null) sb.append("Booking: ").append(bookingId).append("\n");
    if (stationId != null) sb.append("Station: ").append(stationId).append("\n");
    if (status != null) sb.append("Status: ").append(status).append("\n");
    if (message != null && !message.isEmpty()) sb.append("Note: ").append(message).append("\n");

    new AlertDialog.Builder(this)
      .setTitle("Confirm Check-In?")
      .setMessage(sb.toString())
      .setPositiveButton("Confirm", (d, w) -> doConfirmScan(qrToken, bookingId))
      .setNegativeButton("Cancel", null)
      .show();
  }

  private void doConfirmScan(String qrToken, String bookingId) {
    if (qrToken == null || qrToken.isEmpty() || bookingId == null || bookingId.isEmpty()) {
      toast("Missing token or bookingId"); setBusy(false); return;
    }
    setBusy(true);
    new Thread(() -> {
      try {
        JSONObject body = new JSONObject();
        body.put("qrToken", qrToken);
        body.put("bookingId", bookingId);
        ApiClient.Result r = api.postAuthed("/api/Operator/scan", body);
        runOnUiThread(() -> {
          setBusy(false);
          if (r.ok) {
            toast("Check-in successful");
            refreshInbox();
          } else if (r.code == 401) {
            handleUnauthorized();
          } else {
            toast(r.message != null ? r.message : "Scan failed (" + r.code + ")");
          }
        });
      } catch (Exception e) {
        runOnUiThread(() -> {
          setBusy(false);
          toast("Network error: " + e.getMessage());
        });
      }
    }).start();
  }

  // ==== Helpers ====
  private void setBusy(boolean busy) {
    progress.setVisibility(busy ? View.VISIBLE : View.GONE);
    swipe.setEnabled(!busy);
    fabScan.setEnabled(!busy);
  }

  private void handleUnauthorized() {
    toast("Session expired. Please log in again.");
    startActivity(new Intent(this, LoginActivity.class));
    finish();
  }

  private void toast(String m) { Toast.makeText(this, m, Toast.LENGTH_LONG).show(); }
  private int dp(int v) { return Math.round(v * getResources().getDisplayMetrics().density); }
  private String todayYmd() { java.text.SimpleDateFormat f = new java.text.SimpleDateFormat("yyyy-MM-dd", java.util.Locale.US); return f.format(new java.util.Date()); }

  private void vibrateShort() {
    try {
      Vibrator v = (Vibrator) getSystemService(VIBRATOR_SERVICE);
      if (v != null) v.vibrate(50);
    } catch (Exception ignored) {}
  }

  // parse array from string if needed
  private static JSONArray tryParseArray(String s) {
    if (s == null || s.isEmpty()) return null;
    try { return new JSONArray(s); } catch (Exception ignore) { return null; }
  }

  // MapView lifecycle
  @Override public void onMapReady(GoogleMap googleMap) { this.gmap = googleMap; updateMapPin(); }
  @Override protected void onStart() { super.onStart(); mapView.onStart(); }
  @Override protected void onResume(){ super.onResume(); mapView.onResume(); }
  @Override protected void onPause() { mapView.onPause(); super.onPause(); }
  @Override protected void onStop()  { mapView.onStop();  super.onStop(); }
  @Override protected void onDestroy(){ mapView.onDestroy(); super.onDestroy(); }
  @Override public void onLowMemory(){ super.onLowMemory(); mapView.onLowMemory(); }

  // Permission result
  @Override
  public void onRequestPermissionsResult(int requestCode, @NonNull String[] permissions, @NonNull int[] grantResults) {
    super.onRequestPermissionsResult(requestCode, permissions, grantResults);
    if (requestCode == REQ_CAMERA) {
      if (grantResults.length > 0 && grantResults[0] == PackageManager.PERMISSION_GRANTED) {
        startCameraOverlay();
      } else {
        promptManualToken();
      }
    }
  }

  // ==== Recycler Adapter ====
  private final class BookingAdapter extends RecyclerView.Adapter<BookingVH> {
    private final List<JSONObject> items = new ArrayList<>();
    void submit(List<JSONObject> list){ items.clear(); items.addAll(list); notifyDataSetChanged(); }
    @NonNull @Override public BookingVH onCreateViewHolder(@NonNull android.view.ViewGroup parent, int viewType) {
      View v = LayoutInflater.from(parent.getContext()).inflate(R.layout.item_booking_card_operator, parent, false);
      return new BookingVH(v);
    }
    @Override public void onBindViewHolder(@NonNull BookingVH h, int pos) {
      JSONObject b = items.get(pos);
      h.bind(b);
    }
    @Override public int getItemCount(){ return items.size(); }
  }

  private final class BookingVH extends RecyclerView.ViewHolder {
    TextView tvCode, tvStatus, tvTime;
    View root;
    BookingVH(@NonNull View itemView) {
      super(itemView);
      root = itemView;
      tvCode = itemView.findViewById(R.id.tvCode);
      tvStatus = itemView.findViewById(R.id.tvStatus);
      tvTime = itemView.findViewById(R.id.tvTime);
    }
    void bind(JSONObject b) {
      tvCode.setText(JsonUtils.optString(b,"bookingCode"));
      tvStatus.setText(JsonUtils.optString(b,"status"));
      String when = JsonUtils.optString(b,"slotStartLocal");
      if (when == null) when = JsonUtils.optString(b, "slotStartUtc");
      tvTime.setText(when != null ? when : "—");
      root.setOnClickListener(v -> showBookingDialog(b));
    }
  }

  // ==== Map "Get Directions" via station header button (optional hook) ====
  public void onClickGetDirections(View view) {
    if (selectedStation == null) return;
    String url = String.format(Locale.US,
      "https://www.google.com/maps/dir/?api=1&destination=%f,%f&travelmode=driving",
      selectedStation.lat, selectedStation.lng);
    Intent i = new Intent(Intent.ACTION_VIEW, Uri.parse(url));
    i.setPackage("com.google.android.apps.maps");
    try { startActivity(i); } catch (ActivityNotFoundException e) {
      startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse(url)));
    }
  }
}
