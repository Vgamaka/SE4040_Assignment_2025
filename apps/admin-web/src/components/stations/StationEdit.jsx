import { useEffect, useState, useMemo } from "react";
import { useNavigate, useParams, Link } from "react-router-dom";
import StationForm from "../../../components/stations/StationForm";
import { getStationById, updateStation } from "../../../services/api";

export default function StationEdit() {
  const { id } = useParams();
  const nav = useNavigate();

  const [initial, setInitial] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [updatedOk, setUpdatedOk] = useState(false);

  // fetch station
  useEffect(() => {
    let alive = true;
    (async () => {
      setLoading(true);
      setError("");
      try {
        const data = await getStationById(id);
        if (!alive) return;
        setInitial({
          id: data.id,
          name: data.name,
          type: data.type ?? "AC",
          connectors: data.connectors ?? 1,
          autoApproveEnabled: data.autoApproveEnabled ?? false,
          lat: data.lat ?? 0,
          lng: data.lng ?? 0,
          defaultSlotMinutes: data.defaultSlotMinutes ?? 60,
          hoursTimezone: data.hoursTimezone ?? "Asia/Colombo",
          pricing: {
            model: data.pricing?.model ?? "flat",
            base: data.pricing?.base ?? 0,
            perHour: data.pricing?.perHour ?? 0,
            perKwh: data.pricing?.perKwh ?? 0,
            taxPct: data.pricing?.taxPct ?? 0,
          },
        });
      } catch (e) {
        console.error(e);
        const msg = e?.response?.data?.message || e?.message || "Failed to load station";
        setError(msg);
      } finally {
        if (alive) setLoading(false);
      }
    })();
    return () => { alive = false; };
  }, [id]);

  const title = useMemo(() => initial?.name ? `Edit: ${initial.name}` : "Edit Station", [initial]);

  const handleUpdate = async (payload) => {
    setSaving(true);
    setError("");
    setUpdatedOk(false);
    try {
      await updateStation(id, {
        name: payload.name,
        type: payload.type,
        connectors: payload.connectors,
        autoApproveEnabled: payload.autoApproveEnabled,
        lat: payload.lat,
        lng: payload.lng,
        defaultSlotMinutes: payload.defaultSlotMinutes,
        hoursTimezone: payload.hoursTimezone,
        pricing: payload.pricing,
      });
      setUpdatedOk(true);
      // Soft refresh details after update
      const fresh = await getStationById(id);
      setInitial((prev) => ({ ...prev, ...fresh, pricing: { ...fresh.pricing } }));
    } catch (e) {
      console.error(e);
      const msg = e?.response?.data?.message || e?.message || "Update failed";
      setError(msg);
    } finally {
      setSaving(false);
      setTimeout(() => setUpdatedOk(false), 2500);
    }
  };

  return (
    <div className="p-4 max-w-4xl mx-auto">
      <div className="mb-4 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{title}</h1>
          <p className="text-sm text-gray-600">Update details. You can also manage the schedule for this station.</p>
        </div>
        <div className="flex gap-2">
          <Link
            to={`/backoffice/stations/${id}/schedule`}
            className="bg-indigo-600 hover:bg-indigo-700 text-white px-3 py-2 rounded"
          >
            Edit Schedule
          </Link>
          <button
            onClick={() => nav("/backoffice")}
            className="border px-3 py-2 rounded"
          >
            Back
          </button>
        </div>
      </div>

      {loading && <div className="text-gray-600">Loading…</div>}
      {error && <div className="mb-3 text-sm text-red-600">{error}</div>}

      {!loading && initial && (
        <div className="bg-white border rounded-xl shadow p-4">
          {updatedOk && (
            <div className="mb-3 text-sm text-green-700 bg-green-50 border border-green-200 rounded px-3 py-2">
              Station updated successfully.
            </div>
          )}
          <StationForm initial={initial} onSubmit={handleUpdate} submitting={saving} />
        </div>
      )}
    </div>
  );
}
