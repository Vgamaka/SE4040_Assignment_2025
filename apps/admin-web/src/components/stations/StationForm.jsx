import { useEffect, useState } from "react";

const TYPE_OPTIONS = [
  { value: "AC", label: "AC" },
  { value: "DC", label: "DC" },
];

const SLOT_MIN_OPTIONS = [30, 45, 60, 90, 120];

export default function StationForm({ initial, onSubmit, submitting }) {
  const [form, setForm] = useState({
    name: "",
    type: "AC",
    connectors: 1,
    autoApproveEnabled: false,
    lat: 0,
    lng: 0,
    defaultSlotMinutes: 60,
    hoursTimezone: "Asia/Colombo",
    pricing: {
      model: "flat",
      base: 0,
      perHour: 0,
      perKwh: 0,
      taxPct: 0,
    },
  });

  useEffect(() => {
    if (initial) {
      setForm({
        name: initial.name ?? "",
        type: initial.type ?? "AC",
        connectors: initial.connectors ?? 1,
        autoApproveEnabled: initial.autoApproveEnabled ?? false,
        lat: initial.lat ?? 0,
        lng: initial.lng ?? 0,
        defaultSlotMinutes: initial.defaultSlotMinutes ?? 60,
        hoursTimezone: initial.hoursTimezone ?? "Asia/Colombo",
        pricing: {
          model: initial.pricing?.model ?? "flat",
          base: initial.pricing?.base ?? 0,
          perHour: initial.pricing?.perHour ?? 0,
          perKwh: initial.pricing?.perKwh ?? 0,
          taxPct: initial.pricing?.taxPct ?? 0,
        },
      });
    }
  }, [initial]);

  const set = (key, val) => setForm((f) => ({ ...f, [key]: val }));
  const setPricing = (key, val) =>
    setForm((f) => ({ ...f, pricing: { ...f.pricing, [key]: val } }));

  const handleSubmit = (e) => {
    e.preventDefault();
    // minimal client-side validation
    if (!form.name.trim()) {
      alert("Name is required");
      return;
    }
    if (form.connectors < 1) {
      alert("Connectors must be >= 1");
      return;
    }
    if (form.lat < -90 || form.lat > 90 || form.lng < -180 || form.lng > 180) {
      alert("Lat/Lng out of range");
      return;
    }
    onSubmit?.(form);
  };

  return (
    <form onSubmit={handleSubmit} className="grid gap-4">
      <div className="grid md:grid-cols-2 gap-4">
        <div>
          <label className="text-sm">Name</label>
          <input
            className="w-full border rounded px-2 py-1.5"
            value={form.name}
            onChange={(e) => set("name", e.target.value)}
            required
          />
        </div>
        <div>
          <label className="text-sm">Type</label>
          <select
            className="w-full border rounded px-2 py-1.5"
            value={form.type}
            onChange={(e) => set("type", e.target.value)}
          >
            {TYPE_OPTIONS.map((t) => (
              <option key={t.value} value={t.value}>
                {t.label}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label className="text-sm">Connectors</label>
          <input
            type="number"
            min={1}
            className="w-full border rounded px-2 py-1.5"
            value={form.connectors}
            onChange={(e) => set("connectors", Number(e.target.value))}
          />
        </div>

        <div>
          <label className="text-sm">Auto-Approve Enabled</label>
          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={form.autoApproveEnabled}
              onChange={(e) => set("autoApproveEnabled", e.target.checked)}
            />
            <span className="text-sm text-gray-600">Auto approve booking requests</span>
          </div>
        </div>

        <div>
          <label className="text-sm">Latitude</label>
          <input
            type="number"
            step="any"
            className="w-full border rounded px-2 py-1.5"
            value={form.lat}
            onChange={(e) => set("lat", Number(e.target.value))}
          />
        </div>

        <div>
          <label className="text-sm">Longitude</label>
          <input
            type="number"
            step="any"
            className="w-full border rounded px-2 py-1.5"
            value={form.lng}
            onChange={(e) => set("lng", Number(e.target.value))}
          />
        </div>

        <div>
          <label className="text-sm">Default Slot Minutes</label>
          <select
            className="w-full border rounded px-2 py-1.5"
            value={form.defaultSlotMinutes}
            onChange={(e) => set("defaultSlotMinutes", Number(e.target.value))}
          >
            {SLOT_MIN_OPTIONS.map((m) => (
              <option key={m} value={m}>
                {m}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label className="text-sm">Timezone</label>
          <input
            className="w-full border rounded px-2 py-1.5"
            value={form.hoursTimezone}
            onChange={(e) => set("hoursTimezone", e.target.value)}
            placeholder="e.g., Asia/Colombo"
          />
        </div>
      </div>

      <div className="border-t pt-2">
        <div className="font-medium mb-2">Pricing</div>
        <div className="grid md:grid-cols-5 gap-4">
          <div>
            <label className="text-sm">Model</label>
            <select
              className="w-full border rounded px-2 py-1.5"
              value={form.pricing.model}
              onChange={(e) => setPricing("model", e.target.value)}
            >
              <option value="flat">flat</option>
              <option value="hourly">hourly</option>
              <option value="kwh">kwh</option>
            </select>
          </div>
          <div>
            <label className="text-sm">Base</label>
            <input
              type="number"
              step="0.01"
              className="w-full border rounded px-2 py-1.5"
              value={form.pricing.base}
              onChange={(e) => setPricing("base", Number(e.target.value))}
            />
          </div>
          <div>
            <label className="text-sm">Per Hour</label>
            <input
              type="number"
              step="0.01"
              className="w-full border rounded px-2 py-1.5"
              value={form.pricing.perHour}
              onChange={(e) => setPricing("perHour", Number(e.target.value))}
            />
          </div>
          <div>
            <label className="text-sm">Per kWh</label>
            <input
              type="number"
              step="0.01"
              className="w-full border rounded px-2 py-1.5"
              value={form.pricing.perKwh}
              onChange={(e) => setPricing("perKwh", Number(e.target.value))}
            />
          </div>
          <div>
            <label className="text-sm">Tax %</label>
            <input
              type="number"
              step="0.01"
              className="w-full border rounded px-2 py-1.5"
              value={form.pricing.taxPct}
              onChange={(e) => setPricing("taxPct", Number(e.target.value))}
            />
          </div>
        </div>
      </div>

      <div className="flex gap-2 justify-end">
        <button type="submit" className="bg-blue-600 text-white px-4 py-2 rounded" disabled={submitting}>
          {submitting ? "Saving…" : "Save Station"}
        </button>
      </div>
    </form>
  );
}
