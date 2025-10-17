import { useMemo } from "react";

/**
 * Reusable schedule editor used by the Station Schedule page.
 *
 * Props:
 * - value: {
 *     weekly: { mon: DayRange[], tue:[], ... sun:[] },
 *     exceptions: [{ date: "YYYY-MM-DD", closed: true }, ...],
 *     capacityOverrides: [{ date: "YYYY-MM-DD", connectors: 2 }, ...]
 *   }
 * - onChange(nextValue)
 * - disabled?: boolean
 *
 * DayRange = { start: "HH:mm", end: "HH:mm" }
 *
 * Shape aligns with backend DTO (StationScheduleUpsertRequest):
 *  {
 *    weekly: { mon:[{start,end}], ... },
 *    exceptions: [{date, closed}],
 *    capacityOverrides: [{date, connectors}]
 *  }
 */
export default function ScheduleEditor({ value, onChange, disabled = false }) {
  const v = useMemo(() => normalize(value), [value]);

  const update = (next) => onChange?.(normalize(next));

  // ---- Weekly ranges ----
  const addRange = (dayKey) => {
    const next = clone(v);
    (next.weekly[dayKey] ||= []).push({ start: "09:00", end: "17:00" });
    update(next);
  };

  const removeRange = (dayKey, idx) => {
    const next = clone(v);
    next.weekly[dayKey].splice(idx, 1);
    update(next);
  };

  const setRange = (dayKey, idx, field, val) => {
    const next = clone(v);
    next.weekly[dayKey][idx][field] = val;
    update(next);
  };

  // ---- Exceptions (closed dates) ----
  const addException = () => {
    const next = clone(v);
    next.exceptions.push({ date: "", closed: true });
    update(next);
  };

  const setException = (idx, field, val) => {
    const next = clone(v);
    next.exceptions[idx][field] = field === "closed" ? !!val : val;
    update(next);
  };

  const removeException = (idx) => {
    const next = clone(v);
    next.exceptions.splice(idx, 1);
    update(next);
  };

  // ---- Capacity overrides ----
  const addOverride = () => {
    const next = clone(v);
    next.capacityOverrides.push({ date: "", connectors: 1 });
    update(next);
  };

  const setOverride = (idx, field, val) => {
    const next = clone(v);
    if (field === "connectors") {
      const n = parseInt(val || "0", 10);
      next.capacityOverrides[idx].connectors = isNaN(n) ? 0 : Math.max(0, n);
    } else {
      next.capacityOverrides[idx][field] = val;
    }
    update(next);
  };

  const removeOverride = (idx) => {
    const next = clone(v);
    next.capacityOverrides.splice(idx, 1);
    update(next);
  };

  return (
    <div className="space-y-8">
      {/* Weekly schedule */}
      <section>
        <h2 className="text-lg font-semibold mb-2">Weekly schedule</h2>
        <p className="text-sm text-gray-600 mb-4">
          Add one or more open ranges per day (HH:mm, 24h). Ranges must not overlap.
        </p>

        <div className="grid md:grid-cols-2 gap-4">
          {DAYS.map((d) => {
            const dayKey = d.key;
            return (
              <div key={dayKey} className="border rounded-xl p-3">
                <div className="flex items-center justify-between mb-2">
                  <h3 className="font-medium">{d.label}</h3>
                  <button
                    type="button"
                    onClick={() => addRange(dayKey)}
                    disabled={disabled}
                    className="text-sm bg-gray-100 hover:bg-gray-200 px-2 py-1 rounded disabled:opacity-50"
                  >
                    + Add range
                  </button>
                </div>

                {(v.weekly[dayKey] || []).length === 0 && (
                  <div className="text-sm text-gray-500">No ranges</div>
                )}

                <div className="space-y-2">
                  {(v.weekly[dayKey] || []).map((r, idx) => (
                    <div key={idx} className="flex items-center gap-2">
                      <input
                        type="time"
                        value={r.start}
                        onChange={(e) => setRange(dayKey, idx, "start", e.target.value)}
                        disabled={disabled}
                        className="border rounded px-2 py-1"
                      />
                      <span className="text-gray-500">to</span>
                      <input
                        type="time"
                        value={r.end}
                        onChange={(e) => setRange(dayKey, idx, "end", e.target.value)}
                        disabled={disabled}
                        className="border rounded px-2 py-1"
                      />
                      <button
                        type="button"
                        onClick={() => removeRange(dayKey, idx)}
                        disabled={disabled}
                        className="ml-auto text-sm text-red-600 hover:underline disabled:opacity-50"
                      >
                        Remove
                      </button>
                    </div>
                  ))}
                </div>
              </div>
            );
          })}
        </div>
      </section>

      {/* Exceptions */}
      <section>
        <div className="flex items-center justify-between mb-2">
          <h2 className="text-lg font-semibold">Exceptions (Closed days)</h2>
          <button
            type="button"
            onClick={addException}
            disabled={disabled}
            className="text-sm bg-gray-100 hover:bg-gray-200 px-2 py-1 rounded disabled:opacity-50"
          >
            + Add exception
          </button>
        </div>
        {(v.exceptions || []).length === 0 && (
          <div className="text-sm text-gray-500 mb-2">No exception dates</div>
        )}
        <div className="space-y-2">
          {v.exceptions.map((ex, idx) => (
            <div key={idx} className="flex items-center gap-2">
              <input
                type="date"
                value={ex.date}
                onChange={(e) => setException(idx, "date", e.target.value)}
                disabled={disabled}
                className="border rounded px-2 py-1"
              />
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={!!ex.closed}
                  onChange={(e) => setException(idx, "closed", e.target.checked)}
                  disabled={disabled}
                />
                Closed
              </label>
              <button
                type="button"
                onClick={() => removeException(idx)}
                disabled={disabled}
                className="ml-auto text-sm text-red-600 hover:underline disabled:opacity-50"
              >
                Remove
              </button>
            </div>
          ))}
        </div>
      </section>

      {/* Capacity overrides */}
      <section>
        <div className="flex items-center justify-between mb-2">
          <h2 className="text-lg font-semibold">Capacity overrides</h2>
          <button
            type="button"
            onClick={addOverride}
            disabled={disabled}
            className="text-sm bg-gray-100 hover:bg-gray-200 px-2 py-1 rounded disabled:opacity-50"
          >
            + Add override
          </button>
        </div>

        {(v.capacityOverrides || []).length === 0 && (
          <div className="text-sm text-gray-500 mb-2">No overrides</div>
        )}

        <div className="space-y-2">
          {v.capacityOverrides.map((ov, idx) => (
            <div key={idx} className="flex items-center gap-2">
              <input
                type="date"
                value={ov.date}
                onChange={(e) => setOverride(idx, "date", e.target.value)}
                disabled={disabled}
                className="border rounded px-2 py-1"
              />
              <input
                type="number"
                min={0}
                value={ov.connectors}
                onChange={(e) => setOverride(idx, "connectors", e.target.value)}
                disabled={disabled}
                className="w-28 border rounded px-2 py-1"
                placeholder="Connectors"
              />
              <button
                type="button"
                onClick={() => removeOverride(idx)}
                disabled={disabled}
                className="ml-auto text-sm text-red-600 hover:underline disabled:opacity-50"
              >
                Remove
              </button>
            </div>
          ))}
        </div>

        <p className="text-xs text-gray-500 mt-2">
          Note: Overrides set the number of available connectors for a specific date. Use 0 to force-closed via override,
          or prefer the “Exceptions” section with “Closed” for clarity.
        </p>
      </section>
    </div>
  );
}

// ---------- helpers ----------

const DAYS = [
  { key: "mon", label: "Monday" },
  { key: "tue", label: "Tuesday" },
  { key: "wed", label: "Wednesday" },
  { key: "thu", label: "Thursday" },
  { key: "fri", label: "Friday" },
  { key: "sat", label: "Saturday" },
  { key: "sun", label: "Sunday" },
];

function normalize(raw) {
  const base = {
    weekly: {
      mon: [],
      tue: [],
      wed: [],
      thu: [],
      fri: [],
      sat: [],
      sun: [],
    },
    exceptions: [],
    capacityOverrides: [],
  };
  const v = raw || {};
  base.weekly = {
    mon: toRanges(v.weekly?.mon || v.weekly?.Mon),
    tue: toRanges(v.weekly?.tue || v.weekly?.Tue),
    wed: toRanges(v.weekly?.wed || v.weekly?.Wed),
    thu: toRanges(v.weekly?.thu || v.weekly?.Thu),
    fri: toRanges(v.weekly?.fri || v.weekly?.Fri),
    sat: toRanges(v.weekly?.sat || v.weekly?.Sat),
    sun: toRanges(v.weekly?.sun || v.weekly?.Sun),
  };
  base.exceptions = (v.exceptions || v.Exceptions || []).map((e) => ({
    date: e.date ?? e.Date ?? "",
    closed: Boolean(e.closed ?? e.Closed ?? true),
  }));
  base.capacityOverrides = (v.capacityOverrides || v.CapacityOverrides || []).map((c) => ({
    date: c.date ?? c.Date ?? "",
    connectors: Number(c.connectors ?? c.Connectors ?? 0),
  }));

  // strip empties
  Object.keys(base.weekly).forEach((k) => {
    base.weekly[k] = base.weekly[k].filter((r) => r.start && r.end);
  });
  base.exceptions = base.exceptions.filter((e) => e.date);
  base.capacityOverrides = base.capacityOverrides.filter((c) => c.date);
  return base;
}

function toRanges(list) {
  return (list || []).map((r) => ({
    start: r.start ?? r.Start ?? "",
    end: r.end ?? r.End ?? "",
  }));
}

function clone(o) {
  return JSON.parse(JSON.stringify(o || {}));
}
