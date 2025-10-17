import { useEffect, useMemo, useState } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import {
  getStationById,
  getStationSchedule,
  upsertStationSchedule,
} from "../../services/api";
import ScheduleEditor from "../../../components/stations/ScheduleEditor";

export default function SchedulePage() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [station, setStation] = useState(null);
  const [initialSchedule, setInitialSchedule] = useState(null);
  const [schedule, setSchedule] = useState(null);

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const dirty = useMemo(() => {
    try {
      return JSON.stringify(schedule) !== JSON.stringify(initialSchedule);
    } catch {
      return false;
    }
  }, [schedule, initialSchedule]);

  useEffect(() => {
    let mounted = true;
    (async () => {
      setLoading(true);
      setError("");
      try {
        // Load basic station (to show name + TZ)
        const st = await getStationById(id);
        if (!mounted) return;
        setStation(st);

        // Load schedule (may not exist initially)
        const sch = await getStationSchedule(id).catch(() => null);
        const normalized = normalizeScheduleForEditor(sch);
        if (!mounted) return;
        setInitialSchedule(normalized);
        setSchedule(normalized);
      } catch (e) {
        if (!mounted) return;
        setError(e?.message || "Failed to load schedule.");
      } finally {
        if (mounted) setLoading(false);
      }
    })();
    return () => {
      mounted = false;
    };
  }, [id]);

  const onSave = async () => {
    setSaving(true);
    setError("");
    setSuccess("");
    try {
      // Validate before sending
      const issues = validate(schedule);
      if (issues.length) {
        setError(`Fix the following issues:\n• ${issues.join("\n• ")}`);
        setSaving(false);
        return;
      }

      const dto = toUpsertDto(schedule);
      await upsertStationSchedule(id, dto);

      setInitialSchedule(schedule);
      setSuccess("Schedule saved successfully.");
    } catch (e) {
      setError(e?.message || "Failed to save schedule.");
    } finally {
      setSaving(false);
    }
  };

  const onReset = () => {
    setSchedule(initialSchedule);
    setError("");
    setSuccess("");
  };

  if (loading) {
    return (
      <div className="p-4">
        <div className="text-gray-600">Loading...</div>
      </div>
    );
  }

  if (!station) {
    return (
      <div className="p-4">
        <div className="text-red-600">Station not found.</div>
        <button
          onClick={() => navigate(-1)}
          className="mt-3 px-3 py-2 border rounded"
        >
          Go back
        </button>
      </div>
    );
  }

  return (
    <div className="p-4 max-w-5xl mx-auto">
      <div className="mb-4 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">
            Schedule — {station.name}
          </h1>
          <p className="text-sm text-gray-600">
            Timezone: <span className="font-medium">{station.hoursTimezone}</span>{" "}
            · Default slot: <span className="font-medium">{station.defaultSlotMinutes} min</span>
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Link
            to={`/backoffice/stations/${id}/edit`}
            className="px-3 py-2 border rounded hover:bg-gray-50"
          >
            Back to Station
          </Link>
        </div>
      </div>

      {error && (
        <pre className="whitespace-pre-wrap mb-3 p-3 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm">
          {error}
        </pre>
      )}
      {success && (
        <div className="mb-3 p-3 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm">
          {success}
        </div>
      )}

      <div className="bg-white rounded-xl border p-4">
        <ScheduleEditor value={schedule} onChange={setSchedule} disabled={saving} />
      </div>

      <div className="mt-4 flex items-center gap-2">
        <button
          onClick={onSave}
          disabled={saving || !dirty}
          className="px-4 py-2 rounded bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-50"
        >
          {saving ? "Saving..." : "Save"}
        </button>
        <button
          onClick={onReset}
          disabled={saving || !dirty}
          className="px-4 py-2 rounded border hover:bg-gray-50 disabled:opacity-50"
        >
          Reset
        </button>
      </div>
    </div>
  );
}

/* ================= Helpers ================= */

function normalizeScheduleForEditor(apiResponse) {
  // apiResponse matches StationScheduleResponse:
  // {
  //   weekly: { Mon:[{Start,End}], ... },
  //   exceptions: [{Date, Closed}],
  //   capacityOverrides: [{Date, Connectors}],
  //   updatedAtUtc
  // }
  if (!apiResponse) {
    return {
      weekly: { mon: [], tue: [], wed: [], thu: [], fri: [], sat: [], sun: [] },
      exceptions: [],
      capacityOverrides: [],
    };
  }

  const w = apiResponse.weekly || {};
  const mapDay = (arr) =>
    (arr || []).map((r) => ({
      start: r.start ?? r.Start ?? "",
      end: r.end ?? r.End ?? "",
    }));

  return {
    weekly: {
      mon: mapDay(w.mon || w.Mon),
      tue: mapDay(w.tue || w.Tue),
      wed: mapDay(w.wed || w.Wed),
      thu: mapDay(w.thu || w.Thu),
      fri: mapDay(w.fri || w.Fri),
      sat: mapDay(w.sat || w.Sat),
      sun: mapDay(w.sun || w.Sun),
    },
    exceptions: (apiResponse.exceptions || []).map((e) => ({
      date: e.date ?? e.Date ?? "",
      closed: !!(e.closed ?? e.Closed ?? true),
    })),
    capacityOverrides: (apiResponse.capacityOverrides || []).map((c) => ({
      date: c.date ?? c.Date ?? "",
      connectors: Number(c.connectors ?? c.Connectors ?? 0),
    })),
  };
}

function toUpsertDto(v) {
  const mapDay = (arr) =>
    (arr || []).map((r) => ({
      Start: (r.start || "").trim(),
      End: (r.end || "").trim(),
    }));

  return {
    weekly: {
      Mon: mapDay(v.weekly?.mon),
      Tue: mapDay(v.weekly?.tue),
      Wed: mapDay(v.weekly?.wed),
      Thu: mapDay(v.weekly?.thu),
      Fri: mapDay(v.weekly?.fri),
      Sat: mapDay(v.weekly?.sat),
      Sun: mapDay(v.weekly?.sun),
    },
    exceptions: (v.exceptions || []).map((e) => ({
      Date: (e.date || "").trim(),
      Closed: !!e.closed,
    })),
    capacityOverrides: (v.capacityOverrides || []).map((c) => ({
      Date: (c.date || "").trim(),
      Connectors: Number(c.connectors || 0),
    })),
  };
}

// --- validation ---
const timeRe = /^([01]\d|2[0-3]):[0-5]\d$/;

function validate(v) {
  const issues = [];

  // Weekly: validate format, start<end, and no overlaps per day
  const days = ["mon", "tue", "wed", "thu", "fri", "sat", "sun"];
  for (const d of days) {
    const ranges = (v.weekly?.[d] || []).map((r, i) => ({
      ...r,
      i,
      s: r.start,
      e: r.end,
    }));

    for (const r of ranges) {
      if (!timeRe.test(r.s || "")) issues.push(`${label(d)}: invalid start ${r.s}`);
      if (!timeRe.test(r.e || "")) issues.push(`${label(d)}: invalid end ${r.e}`);
      if (timeRe.test(r.s || "") && timeRe.test(r.e || "")) {
        if (toMin(r.s) >= toMin(r.e)) issues.push(`${label(d)}: start must be before end (${r.s}–${r.e})`);
      }
    }

    // Overlap check
    const sorted = ranges
      .filter((r) => timeRe.test(r.s || "") && timeRe.test(r.e || "") && toMin(r.s) < toMin(r.e))
      .sort((a, b) => toMin(a.s) - toMin(b.s));

    for (let i = 1; i < sorted.length; i++) {
      const prev = sorted[i - 1];
      const cur = sorted[i];
      if (toMin(cur.s) < toMin(prev.e)) {
        issues.push(`${label(d)}: range ${cur.s}-${cur.e} overlaps ${prev.s}-${prev.e}`);
      }
    }
  }

  // Exceptions: dates required
  for (const ex of v.exceptions || []) {
    if (!(ex.date || "").trim()) issues.push(`Exceptions: date required`);
  }

  // Overrides: date+non-negative connectors
  for (const ov of v.capacityOverrides || []) {
    if (!(ov.date || "").trim()) issues.push(`Overrides: date required`);
    const n = Number(ov.connectors);
    if (!Number.isFinite(n) || n < 0) issues.push(`Overrides: connectors must be ≥ 0`);
  }

  return Array.from(new Set(issues));
}

function toMin(hhmm) {
  const [h, m] = (hhmm || "0:0").split(":").map((x) => parseInt(x, 10));
  return h * 60 + m;
}
function label(d) {
  return (
    {
      mon: "Monday",
      tue: "Tuesday",
      wed: "Wednesday",
      thu: "Thursday",
      fri: "Friday",
      sat: "Saturday",
      sun: "Sunday",
    }[d] || d
  );
}
