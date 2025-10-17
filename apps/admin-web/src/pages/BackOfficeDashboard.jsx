import { useEffect, useMemo, useState } from "react";
import {
  getBackOfficeMe,
  listMyStations,
  listBackOfficeOperators,
  createOperator,
  attachOperatorStations,
} from "../services/api";
import api from "../services/api";
import { useAuth } from "../context/AuthContext";
import {
  Plus,
  Pencil,
  Power,
  MapPin,
  Settings,
  Zap,
  Clock,
  X,
  LogOut,
  Users,
  BarChart3,
  UserCircle2,
  Check,
  Search,
} from "lucide-react";
import { GoogleMap, Marker, useJsApiLoader } from "@react-google-maps/api";

/* ------------------------------- Helpers ------------------------------- */

const mapContainerStyle = { width: "100%", height: "100%" };
const mapZoom = 14;

function Badge({ tone = "slate", children }) {
  const tones = {
    slate: "bg-slate-100 text-slate-700",
    green: "bg-emerald-100 text-emerald-700",
    amber: "bg-amber-100 text-amber-700",
    red: "bg-rose-100 text-rose-700",
    blue: "bg-blue-100 text-blue-700",
    purple: "bg-purple-100 text-purple-700",
  };
  return (
    <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-bold ${tones[tone]}`}>
      {children}
    </span>
  );
}

function SectionCard({ title, icon, subtitle, right, children }) {
  return (
    <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6">
      <div className="flex items-start sm:items-center justify-between gap-4 mb-6">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-blue-500 to-blue-600 flex items-center justify-center shadow-lg shadow-blue-500/30">
            {icon}
          </div>
          <div>
            <h2 className="text-xl font-bold text-slate-800">{title}</h2>
            {subtitle ? <p className="text-slate-500 text-sm">{subtitle}</p> : null}
          </div>
        </div>
        {right}
      </div>
      {children}
    </div>
  );
}

function Empty({ title = "Nothing here yet", hint }) {
  return (
    <div className="flex flex-col items-center justify-center py-16">
      <div className="w-20 h-20 rounded-full bg-slate-100 flex items-center justify-center mb-4">
        <svg className="w-10 h-10 text-slate-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2" />
        </svg>
      </div>
      <p className="text-slate-600 font-medium">{title}</p>
      {hint ? <p className="text-slate-500 text-sm mt-1">{hint}</p> : null}
    </div>
  );
}

/* ----------------------------- Stations Tab ---------------------------- */

function StationsTab() {
  const [stations, setStations] = useState([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [currentIdx, setCurrentIdx] = useState(0);

  const [showAdd, setShowAdd] = useState(false);
  const [markerPos, setMarkerPos] = useState(null);
  const [creating, setCreating] = useState(false);
  const [edit, setEdit] = useState(null);

  const [filters, setFilters] = useState({ q: "", status: "" });

  const { isLoaded } = useJsApiLoader({
    googleMapsApiKey: import.meta.env.VITE_GOOGLE_MAPS_API_KEY,
  });

  useEffect(() => {
    (async () => {
      try {
        setLoading(true);
        // We call the BackOffice-scoped list (server returns only owned stations)
        const { items = [] } = await listMyStations({ page: 1, pageSize: 200 });
        setStations(items);
        setError("");
      } catch (e) {
        console.error(e);
        setError("Failed to load stations.");
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const filtered = useMemo(() => {
    const q = filters.q.trim().toLowerCase();
    const s = filters.status;
    return stations.filter((st) => {
      if (q && !`${st.name}`.toLowerCase().includes(q)) return false;
      if (s && st.status !== s) return false;
      return true;
    });
  }, [stations, filters]);

  const current = filtered[currentIdx] ?? filtered[0];

  const handleToggleStatus = async (id, active) => {
    try {
      setSaving(true);
      if (active) await api.put(`/Station/${id}/deactivate`);
      else await api.put(`/Station/${id}/activate`);
      // refresh
      const { items = [] } = await listMyStations({ page: 1, pageSize: 200 });
      setStations(items);
    } catch (e) {
      console.error(e);
      alert("Failed to toggle status.");
    } finally {
      setSaving(false);
    }
  };

  const handleMapClick = (e) => setMarkerPos({ lat: e.latLng.lat(), lng: e.latLng.lng() });

  const [newStation, setNewStation] = useState({
    name: "",
    type: "",
    connectors: 1,
    autoApproveEnabled: true,
    defaultSlotMinutes: 60,
    hoursTimezone: "UTC",
    pricingModel: "flat",
    pricingBase: 0,
    pricingPerHour: 0,
    pricingPerKwh: 0,
    pricingTaxPct: 0,
  });

  const createStation = async () => {
    if (!markerPos) return alert("Please click on the map to pin the station location.");
    try {
      setCreating(true);
      const payload = {
        name: newStation.name,
        type: newStation.type,
        connectors: Number(newStation.connectors),
        autoApproveEnabled: !!newStation.autoApproveEnabled,
        defaultSlotMinutes: Number(newStation.defaultSlotMinutes),
        hoursTimezone: newStation.hoursTimezone,
        lat: markerPos.lat,
        lng: markerPos.lng,
        pricing: {
          model: newStation.pricingModel,
          base: Number(newStation.pricingBase),
          perHour: Number(newStation.pricingPerHour),
          perKwh: Number(newStation.pricingPerKwh),
          taxPct: Number(newStation.pricingTaxPct),
        },
      };
      await api.post("/Station", payload);
      setShowAdd(false);
      setMarkerPos(null);
      setNewStation({
        name: "",
        type: "",
        connectors: 1,
        autoApproveEnabled: true,
        defaultSlotMinutes: 60,
        hoursTimezone: "UTC",
        pricingModel: "flat",
        pricingBase: 0,
        pricingPerHour: 0,
        pricingPerKwh: 0,
        pricingTaxPct: 0,
      });
      const { items = [] } = await listMyStations({ page: 1, pageSize: 200 });
      setStations(items);
      setCurrentIdx(0);
    } catch (e) {
      console.error(e);
      alert("Failed to create station.");
    } finally {
      setCreating(false);
    }
  };

  const saveEdit = async () => {
    if (!edit) return;
    try {
      setSaving(true);
      const {
        id,
        name,
        type,
        connectors,
        autoApproveEnabled,
        defaultSlotMinutes,
        hoursTimezone,
        pricing,
      } = edit;
      await api.put(`/Station/${id}`, {
        name,
        type,
        connectors,
        autoApproveEnabled,
        defaultSlotMinutes,
        hoursTimezone,
        pricing,
      });
      setEdit(null);
      const { items = [] } = await listMyStations({ page: 1, pageSize: 200 });
      setStations(items);
    } catch (e) {
      console.error(e);
      alert("Failed to save changes.");
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-12 text-center">
        <div className="animate-spin mx-auto mb-3 h-8 w-8 border-2 border-slate-300 border-t-blue-600 rounded-full" />
        <div className="text-slate-600 font-medium">Loading stations…</div>
      </div>
    );
  }
  if (error) {
    return (
      <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-8 text-center text-rose-600 font-semibold">
        {error}
      </div>
    );
  }
  if (!stations.length) {
    return (
      <SectionCard
        title="Stations"
        icon={<Zap className="w-5 h-5 text-white" />}
        right={
          <button
            onClick={() => setShowAdd(true)}
            className="px-4 py-2 bg-gradient-to-r from-blue-600 to-blue-700 text-white rounded-xl font-medium shadow-md hover:shadow-lg hover:scale-105 transition"
          >
            <span className="inline-flex items-center gap-2"><Plus className="w-4 h-4" /> Add Station</span>
          </button>
        }
      >
        <Empty title="No stations yet" hint="Create your first charging station to get started." />
        {/* Add Modal */}
        {showAdd && (
          <AddStationModal
            isLoaded={isLoaded}
            markerPos={markerPos}
            onMapClick={handleMapClick}
            newStation={newStation}
            setNewStation={setNewStation}
            creating={creating}
            onClose={() => setShowAdd(false)}
            onCreate={createStation}
          />
        )}
      </SectionCard>
    );
  }

  return (
    <>
      <SectionCard
        title="Stations"
        subtitle="Manage your charging stations"
        icon={<Zap className="w-5 h-5 text-white" />}
        right={
          <div className="flex items-center gap-2">
            <div className="hidden sm:flex items-center gap-2 bg-slate-50 border border-slate-200 rounded-xl px-3 h-10">
              <Search className="w-4 h-4 text-slate-500" />
              <input
                className="bg-transparent outline-none text-sm"
                placeholder="Search by name…"
                value={filters.q}
                onChange={(e) => setFilters({ ...filters, q: e.target.value })}
              />
            </div>
            <select
              className="px-3 h-10 bg-slate-50 border border-slate-200 rounded-xl text-sm"
              value={filters.status}
              onChange={(e) => {
                setFilters({ ...filters, status: e.target.value });
                setCurrentIdx(0);
              }}
            >
              <option value="">All</option>
              <option value="Active">Active</option>
              <option value="Inactive">Inactive</option>
            </select>
            <button
              onClick={() => setShowAdd(true)}
              className="px-4 h-10 bg-gradient-to-r from-blue-600 to-blue-700 text-white rounded-xl font-medium shadow-md hover:shadow-lg hover:scale-105 transition"
            >
              <span className="inline-flex items-center gap-2"><Plus className="w-4 h-4" /> Add New Station</span>
            </button>
          </div>
        }
      >
        {filtered.length === 0 ? (
          <Empty title="No stations match your filters" />
        ) : (
          <>
            {/* Pager + title */}
            <div className="flex items-center justify-between mb-4">
              <button
                onClick={() =>
                  setCurrentIdx((p) => (p === 0 ? filtered.length - 1 : p - 1))
                }
                className="px-3 py-2 bg-slate-100 rounded-xl hover:bg-slate-200"
                aria-label="Previous"
              >
                ◀
              </button>
              <div className="text-center">
                <h3 className="text-2xl font-bold text-slate-900">{current?.name}</h3>
                <div className="mt-1 flex items-center justify-center gap-3 text-sm text-slate-500">
                  <span>{currentIdx + 1} of {filtered.length}</span>
                  <Badge tone={current?.status === "Active" ? "green" : "slate"}>
                    <span className={`w-1.5 h-1.5 rounded-full ${current?.status === "Active" ? "bg-emerald-500" : "bg-slate-500"}`} />
                    {current?.status || "Unknown"}
                  </Badge>
                </div>
              </div>
              <button
                onClick={() =>
                  setCurrentIdx((p) => (p === filtered.length - 1 ? 0 : p + 1))
                }
                className="px-3 py-2 bg-slate-100 rounded-xl hover:bg-slate-200"
                aria-label="Next"
              >
                ▶
              </button>
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
              {/* Info left */}
              <div className="space-y-4">
                <InfoTile
                  tone="from-blue-50 to-blue-100 border-blue-200"
                  icon={<Zap className="w-5 h-5 text-white" />}
                  title="Station Type"
                  value={current?.type}
                />
                <InfoTile
                  tone="from-purple-50 to-purple-100 border-purple-200"
                  icon={<Settings className="w-5 h-5 text-white" />}
                  title="Connectors"
                  value={current?.connectors}
                />
                <InfoTile
                  tone="from-amber-50 to-amber-100 border-amber-200"
                  icon={<Clock className="w-5 h-5 text-white" />}
                  title="Default Slot"
                  value={`${current?.defaultSlotMinutes} min`}
                />

                <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-sm">
                  <div className="flex items-center justify-between mb-3">
                    <div className="flex items-center gap-2">
                      <MapPin className="w-5 h-5 text-slate-600" />
                      <span className="font-semibold text-slate-900">Pricing</span>
                    </div>
                    <Badge tone="slate">{current?.pricing?.model || "—"}</Badge>
                  </div>
                  <div className="space-y-2 text-sm">
                    <Row label="Base" value={`$${current?.pricing?.base ?? 0}`} />
                    <Row label="Per Hour" value={`$${current?.pricing?.perHour ?? 0}`} />
                    <Row label="Per kWh" value={`$${current?.pricing?.perKwh ?? 0}`} />
                    <div className="pt-2 border-t border-slate-200">
                      <Row label="Tax" value={`${current?.pricing?.taxPct ?? 0}%`} />
                    </div>
                  </div>
                </div>

                <div className="flex flex-col gap-2">
                  <button
                    onClick={() => setEdit(current)}
                    className="w-full px-4 py-3 bg-gradient-to-r from-blue-600 to-blue-700 text-white rounded-xl font-semibold shadow-lg hover:shadow-xl hover:scale-105 transition flex items-center justify-center gap-2"
                  >
                    <Pencil className="w-4 h-4" /> Edit Station
                  </button>
                  <button
                    disabled={saving}
                    onClick={() => handleToggleStatus(current.id, current.status === "Active")}
                    className={`w-full px-4 py-3 rounded-xl font-semibold shadow-lg hover:shadow-xl hover:scale-105 transition flex items-center justify-center gap-2 ${
                      current?.status === "Active"
                        ? "bg-gradient-to-r from-rose-500 to-rose-600 text-white"
                        : "bg-gradient-to-r from-emerald-500 to-emerald-600 text-white"
                    } ${saving ? "opacity-60 cursor-not-allowed hover:scale-100" : ""}`}
                  >
                    <Power className="w-4 h-4" />
                    {saving ? "Processing…" : current?.status === "Active" ? "Deactivate" : "Activate"}
                  </button>
                </div>
              </div>

              {/* Map right */}
              <div className="lg:col-span-2">
                <div className="rounded-xl overflow-hidden border border-slate-200 h-[420px] shadow-md">
                  {isLoaded ? (
                    <GoogleMap
                      mapContainerStyle={mapContainerStyle}
                      center={{ lat: current?.lat, lng: current?.lng }}
                      zoom={mapZoom}
                    >
                      {current && <Marker position={{ lat: current.lat, lng: current.lng }} />}
                    </GoogleMap>
                  ) : (
                    <div className="h-full w-full grid place-items-center text-slate-500">
                      Google Maps key not configured
                    </div>
                  )}
                </div>
              </div>
            </div>
          </>
        )}
      </SectionCard>

      {/* Add Station Modal */}
      {showAdd && (
        <AddStationModal
          isLoaded={isLoaded}
          markerPos={markerPos}
          onMapClick={handleMapClick}
          newStation={newStation}
          setNewStation={setNewStation}
          creating={creating}
          onClose={() => setShowAdd(false)}
          onCreate={createStation}
        />
      )}

      {/* Edit Station Modal */}
      {edit && (
        <EditStationModal
          edit={edit}
          setEdit={setEdit}
          saving={saving}
          onSave={saveEdit}
        />
      )}
    </>
  );
}

function InfoTile({ tone, icon, title, value }) {
  return (
    <div className={`bg-gradient-to-br ${tone} rounded-xl p-4 border`}>
      <div className="flex items-center gap-3">
        <div className="p-2.5 bg-gradient-to-br from-slate-600 to-slate-700 rounded-xl shadow-lg">
          {icon}
        </div>
        <div>
          <p className="text-xs text-slate-700 font-medium">{title}</p>
          <p className="text-lg font-bold text-slate-900">{value ?? "—"}</p>
        </div>
      </div>
    </div>
  );
}
function Row({ label, value }) {
  return (
    <div className="flex items-center justify-between">
      <span className="text-slate-600">{label}</span>
      <span className="font-bold text-slate-900">{value}</span>
    </div>
  );
}

function AddStationModal({
  isLoaded,
  markerPos,
  onMapClick,
  newStation,
  setNewStation,
  creating,
  onCreate,
  onClose,
}) {
  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 p-4 grid place-items-center">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-4xl overflow-hidden">
        <div className="p-6 bg-gradient-to-r from-emerald-500 to-emerald-600 text-white flex items-center justify-between">
          <div>
            <h3 className="text-xl font-bold">Add New Station</h3>
            <p className="text-emerald-100 text-sm">Create a new charging station</p>
          </div>
          <button onClick={onClose} className="p-2 rounded-xl hover:bg-white/20">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-6 space-y-6 max-h-[80vh] overflow-auto">
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            <Field label="Station Name">
              <input
                className="px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl outline-none"
                value={newStation.name}
                onChange={(e) => setNewStation({ ...newStation, name: e.target.value })}
                placeholder="e.g. Downtown Hub"
              />
            </Field>
            <Field label="Station Type">
              <select
                className="px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl"
                value={newStation.type}
                onChange={(e) => setNewStation({ ...newStation, type: e.target.value })}
              >
                <option value="">Select Type</option>
                <option value="AC">AC</option>
                <option value="DC">DC</option>
              </select>
            </Field>
            <Field label="Connectors">
              <input
                type="number"
                className="px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl"
                value={newStation.connectors}
                onChange={(e) => setNewStation({ ...newStation, connectors: Number(e.target.value) })}
              />
            </Field>
            <Field label="Auto Approve">
              <select
                className="px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl"
                value={String(newStation.autoApproveEnabled)}
                onChange={(e) => setNewStation({ ...newStation, autoApproveEnabled: e.target.value === "true" })}
              >
                <option value="true">Enabled</option>
                <option value="false">Disabled</option>
              </select>
            </Field>
            <Field label="Default Slot (minutes)">
              <input
                type="number"
                className="px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl"
                value={newStation.defaultSlotMinutes}
                onChange={(e) => setNewStation({ ...newStation, defaultSlotMinutes: Number(e.target.value) })}
              />
            </Field>
            <Field label="Timezone">
              <input
                className="px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl"
                value={newStation.hoursTimezone}
                onChange={(e) => setNewStation({ ...newStation, hoursTimezone: e.target.value })}
                placeholder="UTC"
              />
            </Field>
          </div>

          <div className="bg-slate-50 border border-slate-200 rounded-xl p-4">
            <h4 className="font-bold text-slate-900 mb-3 flex items-center gap-2">
              <DollarSignIcon /> Pricing
            </h4>
            <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-5 gap-4">
              <Field label="Model">
                <select
                  className="px-4 py-3 bg-white border border-slate-200 rounded-xl"
                  value={newStation.pricingModel}
                  onChange={(e) => setNewStation({ ...newStation, pricingModel: e.target.value })}
                >
                  <option value="flat">Flat</option>
                  <option value="hourly">Hourly</option>
                  <option value="kwh">Per kWh</option>
                </select>
              </Field>
              <Field label="Base ($)">
                <input
                  type="number"
                  className="px-4 py-3 bg-white border border-slate-200 rounded-xl"
                  value={newStation.pricingBase}
                  onChange={(e) => setNewStation({ ...newStation, pricingBase: Number(e.target.value) })}
                />
              </Field>
              <Field label="Per Hour ($)">
                <input
                  type="number"
                  className="px-4 py-3 bg-white border border-slate-200 rounded-xl"
                  value={newStation.pricingPerHour}
                  onChange={(e) => setNewStation({ ...newStation, pricingPerHour: Number(e.target.value) })}
                />
              </Field>
              <Field label="Per kWh ($)">
                <input
                  type="number"
                  className="px-4 py-3 bg-white border border-slate-200 rounded-xl"
                  value={newStation.pricingPerKwh}
                  onChange={(e) => setNewStation({ ...newStation, pricingPerKwh: Number(e.target.value) })}
                />
              </Field>
              <Field label="Tax (%)">
                <input
                  type="number"
                  className="px-4 py-3 bg-white border border-slate-200 rounded-xl"
                  value={newStation.pricingTaxPct}
                  onChange={(e) => setNewStation({ ...newStation, pricingTaxPct: Number(e.target.value) })}
                />
              </Field>
            </div>
          </div>

          <div>
            <label className="mb-2 text-sm font-semibold text-slate-700 flex items-center gap-2">
              <MapPin className="w-4 h-4" />
              Station Location (click on map to pin)
            </label>
            <div className="rounded-xl overflow-hidden border border-slate-200 h-72 shadow-md">
              {isLoaded ? (
                <GoogleMap
                  mapContainerStyle={{ width: "100%", height: "100%" }}
                  center={markerPos || { lat: 6.9271, lng: 79.8612 }}
                  zoom={mapZoom}
                  onClick={onMapClick}
                >
                  {markerPos && <Marker position={markerPos} />}
                </GoogleMap>
              ) : (
                <div className="w-full h-full grid place-items-center text-slate-500">
                  Google Maps key not configured
                </div>
              )}
            </div>
            {markerPos && (
              <p className="text-sm text-emerald-600 mt-2 flex items-center gap-2 font-medium">
                <Check className="w-4 h-4" />
                Location pinned: {markerPos.lat.toFixed(4)}, {markerPos.lng.toFixed(4)}
              </p>
            )}
          </div>
        </div>

        <div className="flex justify-end gap-3 p-6 pt-0 border-t border-slate-200">
          <button onClick={onClose} className="px-6 py-3 bg-slate-100 text-slate-700 rounded-xl font-semibold hover:bg-slate-200">
            Cancel
          </button>
          <button
            disabled={creating}
            onClick={onCreate}
            className="px-6 py-3 bg-gradient-to-r from-emerald-600 to-emerald-700 text-white rounded-xl font-semibold shadow-lg hover:shadow-xl hover:scale-105 disabled:opacity-60 disabled:hover:scale-100"
          >
            {creating ? "Creating…" : "Create Station"}
          </button>
        </div>
      </div>
    </div>
  );
}

function EditStationModal({ edit, setEdit, saving, onSave }) {
  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 grid place-items-center p-4">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg overflow-hidden">
        <div className="p-6 bg-gradient-to-r from-blue-500 to-blue-600 text-white">
          <h3 className="text-xl font-bold">Edit Station</h3>
          <p className="text-blue-100 text-sm mt-1">Update station details</p>
        </div>

        <div className="p-6 space-y-4">
          <Field label="Name">
            <input
              className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl"
              value={edit.name}
              onChange={(e) => setEdit({ ...edit, name: e.target.value })}
            />
          </Field>

          <Field label="Type">
            <select
              className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl"
              value={edit.type}
              onChange={(e) => setEdit({ ...edit, type: e.target.value })}
            >
              <option value="AC">AC</option>
              <option value="DC">DC</option>
            </select>
          </Field>

          <Field label="Connectors">
            <input
              type="number"
              className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl"
              value={edit.connectors}
              onChange={(e) => setEdit({ ...edit, connectors: Number(e.target.value) })}
            />
          </Field>

          <Field label="Auto Approve">
            <select
              className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl"
              value={String(edit.autoApproveEnabled)}
              onChange={(e) => setEdit({ ...edit, autoApproveEnabled: e.target.value === "true" })}
            >
              <option value="true">Enabled</option>
              <option value="false">Disabled</option>
            </select>
          </Field>

          <Field label="Default Slot Minutes">
            <input
              type="number"
              className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl"
              value={edit.defaultSlotMinutes}
              onChange={(e) => setEdit({ ...edit, defaultSlotMinutes: Number(e.target.value) })}
            />
          </Field>

          <Field label="Pricing Model">
            <select
              className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl"
              value={edit.pricing?.model || ""}
              onChange={(e) => setEdit({ ...edit, pricing: { ...edit.pricing, model: e.target.value } })}
            >
              <option value="">Select</option>
              <option value="flat">Flat</option>
              <option value="hourly">Hourly</option>
              <option value="kwh">Per kWh</option>
            </select>
          </Field>
        </div>

        <div className="flex gap-3 p-6 pt-0">
          <button onClick={() => setEdit(null)} className="flex-1 px-4 py-3 bg-slate-100 text-slate-700 rounded-xl font-semibold hover:bg-slate-200">
            Cancel
          </button>
          <button
            disabled={saving}
            onClick={onSave}
            className="flex-1 px-4 py-3 bg-gradient-to-r from-blue-600 to-blue-700 text-white rounded-xl font-semibold shadow-lg hover:shadow-xl hover:scale-105 disabled:opacity-60 disabled:hover:scale-100"
          >
            {saving ? "Saving…" : "Save Changes"}
          </button>
        </div>
      </div>
    </div>
  );
}

function Field({ label, children }) {
  return (
    <div className="flex flex-col">
      <label className="mb-2 text-sm font-semibold text-slate-700">{label}</label>
      {children}
    </div>
  );
}

function DollarSignIcon() {
  return (
    <svg className="w-5 h-5 text-slate-700" fill="none" stroke="currentColor" viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8V4m0 12v4" />
    </svg>
  );
}

/* ---------------------------- Operators Tab ---------------------------- */

function OperatorsTab() {
  const [operators, setOperators] = useState([]);
  const [stations, setStations] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showCreate, setShowCreate] = useState(false);
  const [creating, setCreating] = useState(false);
  const [assignModal, setAssignModal] = useState(null); // { nic, stationIds: [] }

  const [filters, setFilters] = useState({ q: "" });

  useEffect(() => {
    (async () => {
      try {
        setLoading(true);
        const [{ items: ops = [] }, { items: sts = [] }] = await Promise.all([
          listBackOfficeOperators({ page: 1, pageSize: 200 }),
          listMyStations({ page: 1, pageSize: 200 }),
        ]);
        setOperators(ops);
        setStations(sts);
      } catch (e) {
        console.error(e);
        alert("Failed to load operators.");
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const filtered = useMemo(() => {
    const q = filters.q.trim().toLowerCase();
    return operators.filter((o) => !q || `${o.fullName} ${o.email}`.toLowerCase().includes(q));
  }, [operators, filters]);

  const [form, setForm] = useState({ fullName: "", email: "", phone: "", password: "", stationIds: [] });

  const create = async () => {
    try {
      setCreating(true);
      await createOperator({
        fullName: form.fullName,
        email: form.email,
        phone: form.phone || undefined,
        password: form.password,
        stationIds: form.stationIds,
      });
      setShowCreate(false);
      setForm({ fullName: "", email: "", phone: "", password: "", stationIds: [] });
      const { items: ops = [] } = await listBackOfficeOperators({ page: 1, pageSize: 200 });
      setOperators(ops);
    } catch (e) {
      console.error(e);
      alert("Failed to create operator.");
    } finally {
      setCreating(false);
    }
  };

  const saveAssignment = async () => {
    try {
      await attachOperatorStations(assignModal.nic, assignModal.stationIds);
      setAssignModal(null);
      const { items: ops = [] } = await listBackOfficeOperators({ page: 1, pageSize: 200 });
      setOperators(ops);
    } catch (e) {
      console.error(e);
      alert("Failed to save assignments.");
    }
  };

  if (loading) {
    return (
      <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-12 text-center">
        <div className="animate-spin mx-auto mb-3 h-8 w-8 border-2 border-slate-300 border-top-blue-600 rounded-full" />
        <div className="text-slate-600 font-medium">Loading operators…</div>
      </div>
    );
  }

  return (
    <>
      <SectionCard
        title="Operators"
        subtitle="Manage operator accounts and station assignments"
        icon={<Users className="w-5 h-5 text-white" />}
        right={
          <div className="flex items-center gap-2">
            <div className="hidden sm:flex items-center gap-2 bg-slate-50 border border-slate-200 rounded-xl px-3 h-10">
              <Search className="w-4 h-4 text-slate-500" />
              <input
                className="bg-transparent outline-none text-sm"
                placeholder="Search by name or email…"
                value={filters.q}
                onChange={(e) => setFilters({ ...filters, q: e.target.value })}
              />
            </div>
            <button
              onClick={() => setShowCreate(true)}
              className="px-4 h-10 bg-gradient-to-r from-blue-600 to-blue-700 text-white rounded-xl font-medium shadow-md hover:shadow-lg hover:scale-105 transition"
            >
              <span className="inline-flex items-center gap-2"><Plus className="w-4 h-4" /> New Operator</span>
            </button>
          </div>
        }
      >
        {filtered.length === 0 ? (
          <Empty title="No operators found" hint="Create your first operator." />
        ) : (
          <div className="overflow-hidden rounded-xl border border-slate-200">
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="bg-gradient-to-r from-slate-50 to-slate-100 border-b border-slate-200">
                    <th className="px-6 py-3 text-left text-xs font-bold text-slate-700 uppercase tracking-wider">Full name</th>
                    <th className="px-6 py-3 text-left text-xs font-bold text-slate-700 uppercase tracking-wider">Email</th>
                    <th className="px-6 py-3 text-left text-xs font-bold text-slate-700 uppercase tracking-wider">Phone</th>
                    <th className="px-6 py-3 text-left text-xs font-bold text-slate-700 uppercase tracking-wider">Stations</th>
                    <th className="px-6 py-3 text-left text-xs font-bold text-slate-700 uppercase tracking-wider">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200">
                  {filtered.map((op) => (
                    <tr key={op.nic} className="hover:bg-slate-50">
                      <td className="px-6 py-3 text-sm font-medium text-slate-900">{op.fullName || "—"}</td>
                      <td className="px-6 py-3 text-sm text-slate-700">{op.email || "—"}</td>
                      <td className="px-6 py-3 text-sm text-slate-700">{op.phone || "—"}</td>
                      <td className="px-6 py-3 text-sm">
                        <div className="flex flex-wrap gap-1">
                          {(op.stationNames || op.stations || []).map((n, i) => (
                            <Badge key={i} tone="purple">{n.name || n}</Badge>
                          ))}
                        </div>
                      </td>
                      <td className="px-6 py-3">
                        <button
                          onClick={() =>
                            setAssignModal({
                              nic: op.nic,
                              stationIds: (op.stationIds || op.stations?.map((s) => s.id) || []),
                            })
                          }
                          className="px-3 py-1.5 bg-gradient-to-r from-blue-600 to-blue-700 text-white rounded-lg text-xs font-semibold shadow-md hover:shadow-lg hover:scale-105 transition"
                        >
                          Assign Stations
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </SectionCard>

      {/* Create Operator Modal */}
      {showCreate && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 grid place-items-center p-4">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg overflow-hidden">
            <div className="p-6 bg-gradient-to-r from-blue-500 to-blue-600 text-white flex items-center justify-between">
              <h3 className="text-xl font-bold">New Operator</h3>
              <button onClick={() => setShowCreate(false)} className="p-2 rounded-xl hover:bg-white/20">
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-6 space-y-4">
              <Field label="Full name">
                <input className="px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl"
                  value={form.fullName}
                  onChange={(e) => setForm({ ...form, fullName: e.target.value })}
                />
              </Field>
              <Field label="Email">
                <input className="px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl"
                  value={form.email}
                  onChange={(e) => setForm({ ...form, email: e.target.value })}
                />
              </Field>
              <Field label="Phone (optional)">
                <input className="px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl"
                  value={form.phone}
                  onChange={(e) => setForm({ ...form, phone: e.target.value })}
                />
              </Field>
              <Field label="Password">
                <input type="password" className="px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl"
                  value={form.password}
                  onChange={(e) => setForm({ ...form, password: e.target.value })}
                />
              </Field>
              <Field label="Assign stations">
                <div className="bg-slate-50 border border-slate-200 rounded-xl p-3 max-h-40 overflow-auto">
                  {stations.map((s) => {
                    const checked = form.stationIds.includes(s.id);
                    return (
                      <label key={s.id} className="flex items-center gap-2 py-1">
                        <input
                          type="checkbox"
                          checked={checked}
                          onChange={(e) => {
                            setForm((f) => {
                              const next = new Set(f.stationIds);
                              if (e.target.checked) next.add(s.id);
                              else next.delete(s.id);
                              return { ...f, stationIds: [...next] };
                            });
                          }}
                        />
                        <span className="text-sm">{s.name}</span>
                      </label>
                    );
                  })}
                </div>
              </Field>
            </div>

            <div className="flex justify-end gap-3 p-6 pt-0">
              <button onClick={() => setShowCreate(false)} className="px-6 py-3 bg-slate-100 text-slate-700 rounded-xl font-semibold hover:bg-slate-200">Cancel</button>
              <button
                disabled={creating}
                onClick={create}
                className="px-6 py-3 bg-gradient-to-r from-blue-600 to-blue-700 text-white rounded-xl font-semibold shadow-lg hover:shadow-xl hover:scale-105 disabled:opacity-60"
              >
                {creating ? "Creating…" : "Create"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Assign Modal */}
      {assignModal && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 grid place-items-center p-4">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md overflow-hidden">
            <div className="p-6 bg-gradient-to-r from-indigo-500 to-indigo-600 text-white flex items-center justify-between">
              <h3 className="text-xl font-bold">Assign Stations</h3>
              <button onClick={() => setAssignModal(null)} className="p-2 rounded-xl hover:bg-white/20">
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="p-6 space-y-2 max-h-[60vh] overflow-auto">
              {stations.map((s) => {
                const checked = assignModal.stationIds.includes(s.id);
                return (
                  <label key={s.id} className="flex items-center gap-2 py-1">
                    <input
                      type="checkbox"
                      checked={checked}
                      onChange={(e) => {
                        setAssignModal((m) => {
                          const next = new Set(m.stationIds);
                          if (e.target.checked) next.add(s.id);
                          else next.delete(s.id);
                          return { ...m, stationIds: [...next] };
                        });
                      }}
                    />
                    <span className="text-sm">{s.name}</span>
                  </label>
                );
              })}
            </div>
            <div className="flex justify-end gap-3 p-6 pt-0">
              <button onClick={() => setAssignModal(null)} className="px-6 py-3 bg-slate-100 text-slate-700 rounded-xl font-semibold hover:bg-slate-200">Cancel</button>
              <button onClick={saveAssignment} className="px-6 py-3 bg-gradient-to-r from-indigo-600 to-indigo-700 text-white rounded-xl font-semibold shadow-lg hover:shadow-xl hover:scale-105">Save</button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}

/* ----------------------------- Reports Tab ----------------------------- */
/* Keeping it simple: KPIs + a revenue-by-station table (no extra chart libs) */

function ReportsTab() {
  const [stations, setStations] = useState([]);
  const [chosenStation, setChosenStation] = useState("");
  const [from, setFrom] = useState(() => new Date(new Date().setDate(new Date().getDate() - 30)).toISOString().slice(0, 10));
  const [to, setTo] = useState(() => new Date().toISOString().slice(0, 10));
  const [loading, setLoading] = useState(false);
  const [kpi, setKpi] = useState(null);
  const [rank, setRank] = useState([]);

  useEffect(() => {
    (async () => {
      try {
        const { items = [] } = await listMyStations({ page: 1, pageSize: 200 });
        setStations(items);
      } catch (e) {
        console.error(e);
      }
    })();
  }, []);

  const refresh = async () => {
    try {
      setLoading(true);
      // GET /Reports/summary and /Reports/revenue/by-station
      const params = {
        fromUtc: new Date(from).toISOString(),
        toUtc: new Date(to).toISOString(),
      };
      const [summaryRes, rankRes] = await Promise.all([
        api.get("/Reports/summary", { params: { ...params, stationId: chosenStation || undefined } }),
        api.get("/Reports/revenue/by-station", { params }),
      ]);
      setKpi(summaryRes.data || null);
      setRank(rankRes.data || []);
    } catch (e) {
      console.error(e);
      alert("Failed to load reports.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    refresh();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <SectionCard
      title="Reports & Summary"
      subtitle="Business KPIs and revenue by station"
      icon={<BarChart3 className="w-5 h-5 text-white" />}
      right={
        <div className="flex items-center gap-2">
          <input
            type="date"
            className="px-3 h-10 bg-slate-50 border border-slate-200 rounded-xl text-sm"
            value={from}
            onChange={(e) => setFrom(e.target.value)}
          />
          <input
            type="date"
            className="px-3 h-10 bg-slate-50 border border-slate-200 rounded-xl text-sm"
            value={to}
            onChange={(e) => setTo(e.target.value)}
          />
          <select
            className="px-3 h-10 bg-slate-50 border border-slate-200 rounded-xl text-sm"
            value={chosenStation}
            onChange={(e) => setChosenStation(e.target.value)}
          >
            <option value="">All Stations</option>
            {stations.map((s) => (
              <option value={s.id} key={s.id}>{s.name}</option>
            ))}
          </select>
          <button
            onClick={refresh}
            className="px-4 h-10 bg-gradient-to-r from-blue-600 to-blue-700 text-white rounded-xl font-medium shadow-md hover:shadow-lg hover:scale-105 transition"
          >
            Refresh
          </button>
        </div>
      }
    >
      {loading ? (
        <div className="py-10 text-center text-slate-600">Loading…</div>
      ) : (
        <>
          {/* KPI Cards */}
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
            <Kpi label="Bookings" value={kpi?.totalBookings ?? "—"} />
            <Kpi label="Approved %" value={kpi?.approvedRatePct != null ? `${kpi.approvedRatePct}%` : "—"} />
            <Kpi label="Revenue" value={kpi?.revenue != null ? `$${kpi.revenue.toLocaleString()}` : "—"} />
            <Kpi label="Avg Utilization" value={kpi?.avgUtilizationPct != null ? `${kpi.avgUtilizationPct}%` : "—"} />
          </div>

          {/* Revenue by Station */}
          <div className="overflow-hidden rounded-xl border border-slate-200">
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="bg-gradient-to-r from-slate-50 to-slate-100 border-b border-slate-200">
                    <th className="px-6 py-3 text-left text-xs font-bold text-slate-700 uppercase tracking-wider">Station</th>
                    <th className="px-6 py-3 text-right text-xs font-bold text-slate-700 uppercase tracking-wider">Revenue</th>
                    <th className="px-6 py-3 text-right text-xs font-bold text-slate-700 uppercase tracking-wider">Bookings</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200">
                  {(rank || []).map((r) => (
                    <tr key={r.stationId} className="hover:bg-slate-50">
                      <td className="px-6 py-3 text-sm font-medium text-slate-900">{r.stationName}</td>
                      <td className="px-6 py-3 text-sm text-right font-bold text-slate-900">${(r.revenue ?? 0).toLocaleString()}</td>
                      <td className="px-6 py-3 text-sm text-right text-slate-700">{r.bookings ?? 0}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}
    </SectionCard>
  );
}

function Kpi({ label, value }) {
  return (
    <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-5">
      <div className="text-xs font-medium text-slate-500 uppercase tracking-wider">{label}</div>
      <div className="text-2xl font-extrabold mt-1">{value}</div>
    </div>
  );
}

/* ------------------------------ Profile Tab ---------------------------- */

function ProfileTab() {
  const [me, setMe] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        const data = await getBackOfficeMe();
        setMe(data || null);
      } catch (e) {
        console.error(e);
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  if (loading) {
    return (
      <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-12 text-center">
        <div className="animate-spin mx-auto mb-3 h-8 w-8 border-2 border-slate-300 border-top-blue-600 rounded-full" />
        <div className="text-slate-600 font-medium">Loading profile…</div>
      </div>
    );
  }

  if (!me) {
    return <Empty title="Could not load profile" />;
  }

  const profile = me?.backOfficeProfile || me;

  return (
    <SectionCard
      title="My Profile"
      subtitle="Business & contact details"
      icon={<UserCircle2 className="w-5 h-5 text-white" />}
    >
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <Info label="Business Name" value={profile?.businessName || "—"} />
        <Info label="Contact Email" value={profile?.contactEmail || me?.email || "—"} />
        <Info label="Contact Phone" value={profile?.contactPhone || me?.phone || "—"} />
        <Info label="Default Timezone" value={profile?.hoursTimezone || "—"} />
        <Info label="Auto-Approve Default" value={String(profile?.autoApproveEnabled ?? "—")} />
        <Info label="Default Slot (mins)" value={String(profile?.defaultSlotMinutes ?? "—")} />
      </div>
    </SectionCard>
  );
}

function Info({ label, value }) {
  return (
    <div className="bg-white rounded-xl p-4 border border-slate-200">
      <div className="text-xs font-medium text-slate-500 uppercase tracking-wider">{label}</div>
      <div className="text-lg font-bold text-slate-900 mt-1">{value}</div>
    </div>
  );
}

/* --------------------------- BackOffice Page --------------------------- */

export default function BackOfficeDashboard() {
  const { logout } = useAuth();
  const [tab, setTab] = useState("stations"); // stations | operators | reports | profile

  const tabs = [
    { key: "stations", label: "Stations", icon: <Zap className="w-4 h-4" /> },
    { key: "operators", label: "Operators", icon: <Users className="w-4 h-4" /> },
    { key: "reports", label: "Reports", icon: <BarChart3 className="w-4 h-4" /> },
    { key: "profile", label: "Profile", icon: <UserCircle2 className="w-4 h-4" /> },
  ];

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 via-blue-50 to-slate-100">
      <div className="w-full mx-auto px-4 sm:px-6 lg:px-8 py-6">
        {/* Top bar */}
        <div className="flex items-center justify-between mb-6">
          <h1 className="text-2xl font-semibold">BackOffice</h1>
          <button
            onClick={logout}
            className="inline-flex items-center gap-2 bg-gradient-to-r from-rose-500 to-rose-600 text-white px-4 py-2 rounded-xl shadow-md hover:shadow-lg hover:scale-105 transition"
          >
            <LogOut className="w-4 h-4" />
            Logout
          </button>
        </div>

        {/* Section header */}
        <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6 mb-6">
          <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
            <div>
              <h2 className="text-2xl sm:text-3xl font-bold bg-gradient-to-r from-slate-800 to-slate-600 bg-clip-text text-transparent">
                Station Management
              </h2>
              <p className="text-slate-500 mt-1">
                Manage your charging stations and monitor performance
              </p>
            </div>
            <div className="flex items-center bg-slate-50 border border-slate-200 rounded-xl p-1">
              {tabs.map((t) => (
                <button
                  key={t.key}
                  onClick={() => setTab(t.key)}
                  className={`px-3 py-2 rounded-lg text-sm font-medium inline-flex items-center gap-2 transition ${
                    tab === t.key ? "bg-white shadow border border-slate-200" : "text-slate-600 hover:bg-white/60"
                  }`}
                >
                  {t.icon}
                  {t.label}
                </button>
              ))}
            </div>
          </div>
        </div>

        {/* Active tab */}
        <div className="space-y-6">
          {tab === "stations" && <StationsTab />}
          {tab === "operators" && <OperatorsTab />}
          {tab === "reports" && <ReportsTab />}
          {tab === "profile" && <ProfileTab />}
        </div>
      </div>
    </div>
  );
}
