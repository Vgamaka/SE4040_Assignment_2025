package com.evcharge.app.ui.stations;

import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import com.evcharge.app.R;

import java.util.ArrayList;
import java.util.List;

public final class DashboardStationAdapter extends RecyclerView.Adapter<DashboardStationAdapter.VH> {

    public static final class Row {
        public final String id;
        public final String title;
        public final String subtitle;
        public Row(String id, String title, String subtitle){
            this.id=id; this.title=title; this.subtitle=subtitle;
        }
    }

    public interface OnClick {
        void onStation(String stationId);
    }

    private final List<Row> rows = new ArrayList<>();
    private final OnClick click;

    public DashboardStationAdapter(OnClick click) {
        this.click = click;
    }

    public void setRows(List<Row> newRows) {
        rows.clear();
        if (newRows != null) rows.addAll(newRows);
        notifyDataSetChanged();
    }

    @NonNull @Override public VH onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        View v = LayoutInflater.from(parent.getContext())
                .inflate(R.layout.item_dashboard_station, parent, false);
        return new VH(v, click);
    }

    @Override public void onBindViewHolder(@NonNull VH holder, int position) {
        holder.bind(rows.get(position));
    }

    @Override public int getItemCount() { return rows.size(); }

    static final class VH extends RecyclerView.ViewHolder {
        private final TextView tvTitle, tvSub;
        private String id;
        VH(View v, OnClick click) {
            super(v);
            tvTitle = v.findViewById(R.id.tvTitle);
            tvSub = v.findViewById(R.id.tvSub);
            v.setOnClickListener(view -> {
                if (click != null && id != null) click.onStation(id);
            });
        }
        void bind(Row r) {
            id = r.id;
            tvTitle.setText(r.title != null ? r.title : "Station");
            tvSub.setText(r.subtitle != null ? r.subtitle : "");
        }
    }
}
