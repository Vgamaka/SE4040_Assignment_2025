package com.evcharge.app.ui.notifications;

import android.app.Dialog;
import android.content.Context;
import android.content.Intent;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.ProgressBar;
import android.widget.TextView;
import android.widget.Toast;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.appcompat.app.AlertDialog;
import androidx.fragment.app.DialogFragment;
import androidx.recyclerview.widget.LinearLayoutManager;
import androidx.recyclerview.widget.RecyclerView;

import com.evcharge.app.R;
import com.evcharge.app.core.net.ApiClient;
import com.evcharge.app.core.util.JsonUtils;
import com.evcharge.app.ui.booking.BookingDetailActivity;

import org.json.JSONArray;
import org.json.JSONObject;

import java.text.ParsePosition;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.TimeZone;

/**
 * Lists unread notifications (page 1, up to 20).
 * - Tap an item: mark as read; if bookingId in payload -> open BookingDetailActivity.
 * - "Mark all as read" button clears all.
 * - On dismiss: emits fragment result "notifDismiss" so Dashboard can refresh badge.
 */
public final class NotificationsDialog extends DialogFragment {

  private RecyclerView rv;
  private ProgressBar progress;
  private TextView tvEmpty, btnMarkAll;

  private final List<NotifRow> rows = new ArrayList<>();
  private Adapter adapter;

  @NonNull @Override
  public Dialog onCreateDialog(@Nullable Bundle savedInstanceState) {
    View content = LayoutInflater.from(requireContext())
      .inflate(R.layout.dialog_notifications, null, false);

    rv = content.findViewById(R.id.rvNotifs);
    progress = content.findViewById(R.id.progress);
    tvEmpty = content.findViewById(R.id.tvEmpty);
    btnMarkAll = content.findViewById(R.id.btnMarkAll);

    rv.setLayoutManager(new LinearLayoutManager(requireContext()));
    adapter = new Adapter(rows, (id, bookingId) -> onClickItem(id, bookingId));
    rv.setAdapter(adapter);

    btnMarkAll.setOnClickListener(v -> markAllRead());

    AlertDialog dlg = new AlertDialog.Builder(requireContext())
      .setView(content)
      .create();

    // Load after dialog is created to avoid UI timing glitches
    rv.post(this::load);
    return dlg;
  }

  @Override
  public void onDismiss(@NonNull android.content.DialogInterface dialog) {
    super.onDismiss(dialog);
    // Tell Dashboard to refresh the badge
    if (getParentFragmentManager() != null) {
      getParentFragmentManager().setFragmentResult("notifDismiss", new Bundle());
    }
  }

  // ------- Data model -------
  private static final class NotifRow {
    final String id;
    final String subject;
    final String message;
    final String bookingId;    // from payload.bookingId (if present)
    final String createdAtUtc;
    boolean read;              // not used now (we load unreadOnly), but we gray out after click

    NotifRow(String id, String subject, String message, String bookingId, String createdAtUtc, boolean read) {
      this.id = id; this.subject = subject; this.message = message;
      this.bookingId = bookingId; this.createdAtUtc = createdAtUtc; this.read = read;
    }
  }

  // ------- Networking -------
  private void load() {
    setBusy(true);
    new Thread(() -> {
      try {
        ApiClient api = new ApiClient(requireContext().getApplicationContext());
        // unreadOnly=true, first page, up to 20
        com.evcharge.app.core.net.HttpClient.Response r = api.notificationsListRaw(true, 1, 20);
        final List<NotifRow> tmp = new ArrayList<>();

        if (r != null && r.jsonObject != null) {
          JSONArray items = r.jsonObject.optJSONArray("items");
          if (items != null) {
            for (int i = 0; i < items.length(); i++) {
              JSONObject o = items.optJSONObject(i); if (o == null) continue;
              String id = JsonUtils.optString(o, "id");
              String subj = JsonUtils.optString(o, "subject");
              String msg = JsonUtils.optString(o, "message");
              String created = JsonUtils.optString(o, "createdAtUtc");
              String bookingId = null;
              JSONObject payload = o.optJSONObject("payload");
              if (payload != null) {
                bookingId = JsonUtils.optString(payload, "bookingId");
              }
              boolean read = o.has("readAtUtc") && !o.isNull("readAtUtc");
              if (id != null) tmp.add(new NotifRow(id, subj, msg, bookingId, created, read));
            }
          }
        }

        requireActivity().runOnUiThread(() -> {
          rows.clear(); rows.addAll(tmp);
          adapter.notifyDataSetChanged();
          tvEmpty.setVisibility(rows.isEmpty() ? View.VISIBLE : View.GONE);
          setBusy(false);
        });
      } catch (Exception e) {
        requireActivity().runOnUiThread(() -> {
          toast("Notifications error: " + e.getMessage());
          setBusy(false);
        });
      }
    }).start();
  }

  private void markAllRead() {
    setBusy(true);
    new Thread(() -> {
      ApiClient api = new ApiClient(requireContext().getApplicationContext());
      ApiClient.Result r = api.notificationsMarkAllRead();
      requireActivity().runOnUiThread(() -> {
        setBusy(false);
        if (r.ok) {
          rows.clear();
          adapter.notifyDataSetChanged();
          tvEmpty.setVisibility(View.VISIBLE);
          toast("Marked all as read");
        } else {
          toast("Failed: " + r.code);
        }
      });
    }).start();
  }

  private void onClickItem(String id, @Nullable String bookingId) {
    // Optimistic UI
    int idx = -1;
    for (int i = 0; i < rows.size(); i++) {
      if (id.equals(rows.get(i).id)) { idx = i; break; }
    }
    if (idx >= 0) { rows.get(idx).read = true; adapter.notifyItemChanged(idx); }

    // Mark read on server
    new Thread(() -> {
      ApiClient api = new ApiClient(requireContext().getApplicationContext());
      api.notificationMarkRead(id); // ignore result (best-effort)
    }).start();

    // Deep link if bookingId present
    if (bookingId != null && !bookingId.isEmpty()) {
      try {
        Context ctx = requireContext();
        Intent i = new Intent(ctx, BookingDetailActivity.class);
        i.putExtra("bookingId", bookingId);
        startActivity(i);
      } catch (Exception ignored) {}
    }

    // Close dialog so badge refresh happens (onDismiss will emit the event)
    dismiss();
  }

  private void setBusy(boolean b) {
    progress.setVisibility(b ? View.VISIBLE : View.GONE);
    rv.setAlpha(b ? 0.5f : 1f);
    btnMarkAll.setEnabled(!b);
  }

  private void toast(String m) {
    Toast.makeText(requireContext(), m, Toast.LENGTH_SHORT).show();
  }

  // ------- Adapter -------
  private static final class Adapter extends RecyclerView.Adapter<Adapter.VH> {
    interface Click { void onClick(String id, @Nullable String bookingId); }
    private final List<NotifRow> rows; private final Click click;
    Adapter(List<NotifRow> rows, Click click){ this.rows = rows; this.click = click; }

    @NonNull @Override public VH onCreateViewHolder(@NonNull ViewGroup p, int vType) {
      View v = LayoutInflater.from(p.getContext()).inflate(R.layout.item_notif, p, false);
      return new VH(v, click);
    }

    @Override public void onBindViewHolder(@NonNull VH h, int pos) { h.bind(rows.get(pos)); }

    @Override public int getItemCount() { return rows.size(); }

    static final class VH extends RecyclerView.ViewHolder {
      private final TextView tvSubject, tvMsg, tvTime;
      private NotifRow bound;

      VH(@NonNull View v, Click click) {
        super(v);
        tvSubject = v.findViewById(R.id.tvSubject);
        tvMsg = v.findViewById(R.id.tvMsg);
        tvTime = v.findViewById(R.id.tvTime);
        v.setOnClickListener(view -> {
          if (bound != null) click.onClick(bound.id, bound.bookingId);
        });
      }

      void bind(NotifRow r) {
        bound = r;
        tvSubject.setText(r.subject != null ? r.subject : "(No subject)");
        tvMsg.setText(r.message != null ? r.message : "");
        tvTime.setText(relTime(r.createdAtUtc));

        // Dim if read (defensive; list is unread, but we dim after click)
        itemView.setAlpha(r.read ? 0.5f : 1f);
      }

      private static String relTime(@Nullable String isoUtc) {
        if (isoUtc == null) return "";
        // Parse ISO-8601 with Z
        SimpleDateFormat sdf = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.US);
        sdf.setTimeZone(TimeZone.getTimeZone("UTC"));
        long ts = sdf.parse(isoUtc, new ParsePosition(0)) != null
          ? sdf.parse(isoUtc, new ParsePosition(0)).getTime()
          : 0L;
        if (ts == 0L) return "";
        long now = System.currentTimeMillis();
        long diff = Math.max(0, now - ts);
        long m = diff / 60000L;
        if (m < 60) return m + "m ago";
        long h = m / 60;
        if (h < 24) return h + "h ago";
        long d = h / 24;
        return d + "d ago";
      }
    }
  }
}
