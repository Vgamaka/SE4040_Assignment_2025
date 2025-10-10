package com.evcharge.app.ui.booking;

import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import com.evcharge.app.R;

import java.util.ArrayList;
import java.util.List;

public final class BookingListAdapter extends RecyclerView.Adapter<RecyclerView.ViewHolder> {

    public interface OnItemClick {
        void onClick(String bookingId);
    }

    public static final int TYPE_HEADER = 0;
    public static final int TYPE_ITEM   = 1;

    public static abstract class Row {}
    public static final class HeaderRow extends Row {
        public final String title;
        public HeaderRow(String title){ this.title = title; }
    }
    public static final class ItemRow extends Row {
        public final String id;
        public final String station;
        public final String when;
        public final String status;
        public ItemRow(String id, String station, String when, String status){
            this.id=id; this.station=station; this.when=when; this.status=status;
        }
    }

    private final List<Row> rows = new ArrayList<>();
    private final OnItemClick click;

    public BookingListAdapter(OnItemClick click) {
        this.click = click;
    }

    public void setRows(List<Row> newRows) {
        rows.clear();
        if (newRows != null) rows.addAll(newRows);
        notifyDataSetChanged();
    }

    @Override public int getItemViewType(int position) {
        return (rows.get(position) instanceof HeaderRow) ? TYPE_HEADER : TYPE_ITEM;
    }

    @Override public int getItemCount() { return rows.size(); }

    @NonNull
    @Override
    public RecyclerView.ViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int type) {
        LayoutInflater inf = LayoutInflater.from(parent.getContext());
        if (type == TYPE_HEADER) {
            View v = inf.inflate(R.layout.item_booking_header, parent, false);
            return new HeaderVH(v);
        } else {
            View v = inf.inflate(R.layout.item_booking_row, parent, false);
            return new ItemVH(v, click);
        }
    }

    @Override
    public void onBindViewHolder(@NonNull RecyclerView.ViewHolder vh, int pos) {
        Row r = rows.get(pos);
        if (vh instanceof HeaderVH && r instanceof HeaderRow) {
            ((HeaderVH) vh).bind((HeaderRow) r);
        } else if (vh instanceof ItemVH && r instanceof ItemRow) {
            ((ItemVH) vh).bind((ItemRow) r);
        }
    }

    // ---- VHs ----
    static final class HeaderVH extends RecyclerView.ViewHolder {
        private final TextView tv;
        HeaderVH(View v) { super(v); tv = v.findViewById(R.id.tvHeader); }
        void bind(HeaderRow r) { tv.setText(r.title); }
    }

    static final class ItemVH extends RecyclerView.ViewHolder {
        private final TextView tvStation, tvWhen, tvStatus;
        ItemVH(View v, OnItemClick click) {
            super(v);
            tvStation = v.findViewById(R.id.tvStation);
            tvWhen = v.findViewById(R.id.tvWhen);
            tvStatus = v.findViewById(R.id.tvStatus);
            v.setOnClickListener(view -> {
                Object tag = view.getTag();
                if (tag instanceof String && click != null) click.onClick((String) tag);
            });
        }
        void bind(ItemRow r) {
            itemView.setTag(r.id);
            tvStation.setText(r.station != null ? r.station : "Station");
            tvWhen.setText(r.when != null ? r.when : "");
            tvStatus.setText(r.status != null ? r.status : "");
        }
    }
}
