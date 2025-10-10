import { useEffect, useState } from "react";
import api from "../services/api";
import { GoogleMap, Marker, useJsApiLoader } from "@react-google-maps/api";
import {
  Pencil,
  Plus,
  MapPin,
  Zap,
  Clock,
  DollarSign,
  ChevronLeft,
  ChevronRight,
  X,
  Check,
  Power,
  Settings,
} from "lucide-react";

const mapContainerStyle = { width: "100%", height: "100%" };
const mapZoom = 15;

export default function BackOfficeDashboard() {

  const [stations, setStations] = useState([]);
  const [currentIndex, setCurrentIndex] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [user, setUser] = useState(null);
  const [editData, setEditData] = useState(null);
  const [saving, setSaving] = useState(false);
  const [showAddForm, setShowAddForm] = useState(false);

  const [newStation, setNewStation] = useState({
    name: "",
    type: "",
    connectors: 0,
    autoApproveEnabled: true,
    defaultSlotMinutes: 0,
    hoursTimezone: "UTC",
    pricingModel: "Standard",
    pricingBase: 0,
    pricingPerHour: 0,
    pricingPerKwh: 0,
    pricingTaxPct: 0,
  });
  const [markerPos, setMarkerPos] = useState(null);
  const [creating, setCreating] = useState(false);

  const { isLoaded } = useJsApiLoader({
    googleMapsApiKey: import.meta.env.VITE_GOOGLE_MAPS_API_KEY,
  });

  useEffect(() => {
    const storedUser = JSON.parse(localStorage.getItem("user"));
    if (storedUser) setUser(storedUser);
    fetchStations(storedUser?.nic);
  }, []);

  const fetchStations = async (userNic) => {
    try {
      setLoading(true);

      const { data: activeData } = await api.get("/api/Station", {
        params: { status: "Active" },
      });
      const { data: inactiveData } = await api.get("/api/Station", {
        params: { status: "Inactive" },
      });

      const allStations = [
        ...(activeData.items || activeData),
        ...(inactiveData.items || inactiveData),
      ];
      const ownedStations = allStations.filter(
        (station) => station.backOfficeNic === userNic
      );

      setStations(ownedStations);
    } catch (err) {
      console.error("Error fetching stations:", err);
      setError("Failed to fetch stations.");
    } finally {
      setLoading(false);
    }
  };

  const handleEdit = (station) => setEditData({ ...station });

  const handleSave = async () => {
    if (!editData) return;
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
      } = editData;

      const updatePayload = {
        name,
        type,
        connectors,
        autoApproveEnabled,
        defaultSlotMinutes,
        hoursTimezone,
        pricing,
      };
      await api.put(`/api/Station/${id}`, updatePayload);
      setEditData(null);
      fetchStations(user?.nic);
    } catch (err) {
      console.error("Error updating station:", err);
      alert("Failed to update station.");
    } finally {
      setSaving(false);
    }
  };

  // ------------------ Navigation ------------------
  const handlePrev = () =>
    setCurrentIndex((prev) => (prev === 0 ? stations.length - 1 : prev - 1));
  const handleNext = () =>
    setCurrentIndex((prev) => (prev === stations.length - 1 ? 0 : prev + 1));

  const handleToggleStatus = async (stationId, action) => {
    try {
      setSaving(true);
      if (action === "deactivate") {
        await api.put(`/api/Station/${stationId}/deactivate`);
      } else {
        await api.put(`/api/Station/${stationId}/activate`);
      }
      fetchStations(user?.nic);
    } catch (err) {
      console.error(`Failed to ${action} station:`, err);
      alert(`Failed to ${action} station.`);
    } finally {
      setSaving(false);
    }
  };

  // ------------------ Add Station ------------------
  const handleMapClick = (e) =>
    setMarkerPos({ lat: e.latLng.lat(), lng: e.latLng.lng() });

  const handleCreateStation = async () => {
    if (!markerPos) return alert("Please pin the station on the map.");

    try {
      setCreating(true);
      const payload = {
        name: newStation.name,
        type: newStation.type,
        connectors: newStation.connectors,
        autoApproveEnabled: newStation.autoApproveEnabled,
        defaultSlotMinutes: newStation.defaultSlotMinutes,
        hoursTimezone: newStation.hoursTimezone,
        lat: markerPos.lat,
        lng: markerPos.lng,
        pricing: {
          model: newStation.pricingModel,
          base: newStation.pricingBase,
          perHour: newStation.pricingPerHour,
          perKwh: newStation.pricingPerKwh,
          taxPct: newStation.pricingTaxPct,
        },
      };
      await api.post("/api/Station", payload);
      setNewStation({
        name: "",
        type: "",
        connectors: 0,
        autoApproveEnabled: true,
        defaultSlotMinutes: 0,
        hoursTimezone: "UTC",
        pricingModel: "Standard",
        pricingBase: 0,
        pricingPerHour: 0,
        pricingPerKwh: 0,
        pricingTaxPct: 0,
      });
      setMarkerPos(null);
      setShowAddForm(false);
      fetchStations(user?.nic);
    } catch (err) {
      console.error("Failed to create station:", err);
      alert("Failed to create station.");
    } finally {
      setCreating(false);
    }
  };

  if (loading)
    return (
      <div className="flex flex-col items-center justify-center h-screen bg-gradient-to-br from-slate-50 via-blue-50 to-slate-100">
        <svg
          className="animate-spin h-12 w-12 text-blue-600 mb-4"
          fill="none"
          viewBox="0 0 24 24"
        >
          <circle
            className="opacity-25"
            cx="12"
            cy="12"
            r="10"
            stroke="currentColor"
            strokeWidth="4"
          ></circle>
          <path
            className="opacity-75"
            fill="currentColor"
            d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
          ></path>
        </svg>
        <p className="text-slate-600 font-medium">Loading stations...</p>
      </div>
    );
  if (error)
    return (
      <div className="flex items-center justify-center h-screen bg-gradient-to-br from-slate-50 via-red-50 to-slate-100">
        <div className="bg-white rounded-2xl shadow-lg border-l-4 border-red-500 p-6">
          <p className="text-red-600 font-semibold">{error}</p>
        </div>
      </div>
    );
  if (!stations.length)
    return (
      <div className="flex flex-col items-center justify-center h-screen bg-gradient-to-br from-slate-50 via-blue-50 to-slate-100">
        <div className="w-20 h-20 rounded-full bg-slate-100 flex items-center justify-center mb-4">
          <Zap className="w-10 h-10 text-slate-400" />
        </div>
        <p className="text-slate-600 font-medium">No stations found.</p>
      </div>
    );
  if (!isLoaded)
    return (
      <div className="flex flex-col items-center justify-center h-screen bg-gradient-to-br from-slate-50 via-blue-50 to-slate-100">
        <svg
          className="animate-spin h-12 w-12 text-blue-600 mb-4"
          fill="none"
          viewBox="0 0 24 24"
        >
          <circle
            className="opacity-25"
            cx="12"
            cy="12"
            r="10"
            stroke="currentColor"
            strokeWidth="4"
          ></circle>
          <path
            className="opacity-75"
            fill="currentColor"
            d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
          ></path>
        </svg>
        <p className="text-slate-600 font-medium">Loading map...</p>
      </div>
    );

  const currentStation = stations[currentIndex];

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 via-blue-50 to-slate-100">
      <div className="w-full mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {/* Header */}
        <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6 mb-6">
          <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
            <div>
              <h1 className="text-3xl font-bold bg-gradient-to-r from-slate-800 to-slate-600 bg-clip-text text-transparent">
                Station Management
              </h1>
              <p className="text-slate-500 mt-1">
                Manage your charging stations and monitor performance
              </p>
            </div>
            <button
              onClick={() => setShowAddForm(true)}
              className="px-5 py-2.5 bg-gradient-to-r from-blue-500 to-blue-600 text-white rounded-xl font-medium shadow-lg shadow-blue-500/30 hover:shadow-xl hover:shadow-blue-500/40 hover:scale-105 transition-all duration-200 flex items-center gap-2"
            >
              <Plus className="w-5 h-5" />
              Add New Station
            </button>
          </div>
        </div>

        {/* Station Navigator */}
        <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6 mb-6">
          <div className="flex items-center justify-between mb-6">
            <button
              onClick={handlePrev}
              className="p-3 bg-slate-100 text-slate-700 rounded-xl hover:bg-slate-200 transition-all"
            >
              <ChevronLeft className="w-5 h-5" />
            </button>

            <div className="text-center flex-1">
              <h2 className="text-2xl md:text-3xl font-bold text-slate-800 mb-1">
                {currentStation.name}
              </h2>
              <div className="flex items-center justify-center gap-2 text-sm text-slate-500">
                <span className="font-medium">
                  {currentIndex + 1} of {stations.length}
                </span>
                <span
                  className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-bold ${
                    currentStation.status === "Active"
                      ? "bg-emerald-100 text-emerald-700"
                      : "bg-slate-100 text-slate-600"
                  }`}
                >
                  <span
                    className={`w-1.5 h-1.5 rounded-full ${
                      currentStation.status === "Active"
                        ? "bg-emerald-500"
                        : "bg-slate-500"
                    }`}
                  ></span>
                  {currentStation.status}
                </span>
              </div>
            </div>

            <button
              onClick={handleNext}
              className="p-3 bg-slate-100 text-slate-700 rounded-xl hover:bg-slate-200 transition-all"
            >
              <ChevronRight className="w-5 h-5" />
            </button>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* Info Cards */}
            <div className="lg:col-span-1 space-y-4">
              <div className="bg-gradient-to-br from-blue-50 to-blue-100 rounded-xl p-4 border border-blue-200">
                <div className="flex items-center gap-3">
                  <div className="p-2.5 bg-gradient-to-br from-blue-500 to-blue-600 rounded-xl shadow-lg shadow-blue-500/30">
                    <Zap className="w-5 h-5 text-white" />
                  </div>
                  <div>
                    <p className="text-xs text-blue-700 font-medium">
                      Station Type
                    </p>
                    <p className="text-lg font-bold text-blue-900">
                      {currentStation.type}
                    </p>
                  </div>
                </div>
              </div>

              <div className="bg-gradient-to-br from-purple-50 to-purple-100 rounded-xl p-4 border border-purple-200">
                <div className="flex items-center gap-3">
                  <div className="p-2.5 bg-gradient-to-br from-purple-500 to-purple-600 rounded-xl shadow-lg shadow-purple-500/30">
                    <Settings className="w-5 h-5 text-white" />
                  </div>
                  <div>
                    <p className="text-xs text-purple-700 font-medium">
                      Connectors
                    </p>
                    <p className="text-lg font-bold text-purple-900">
                      {currentStation.connectors}
                    </p>
                  </div>
                </div>
              </div>

              <div className="bg-gradient-to-br from-amber-50 to-amber-100 rounded-xl p-4 border border-amber-200">
                <div className="flex items-center gap-3">
                  <div className="p-2.5 bg-gradient-to-br from-amber-500 to-amber-600 rounded-xl shadow-lg shadow-amber-500/30">
                    <Clock className="w-5 h-5 text-white" />
                  </div>
                  <div>
                    <p className="text-xs text-amber-700 font-medium">
                      Default Slot
                    </p>
                    <p className="text-lg font-bold text-amber-900">
                      {currentStation.defaultSlotMinutes} min
                    </p>
                  </div>
                </div>
              </div>

              <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-sm">
                <div className="flex items-center justify-between mb-3">
                  <div className="flex items-center gap-2">
                    <DollarSign className="w-5 h-5 text-slate-600" />
                    <span className="font-semibold text-slate-900">
                      Pricing
                    </span>
                  </div>
                  <span className="text-xs bg-slate-100 px-2.5 py-1 rounded-full text-slate-700 font-bold">
                    {currentStation.pricing?.model}
                  </span>
                </div>
                <div className="space-y-2 text-sm">
                  <div className="flex justify-between items-center">
                    <span className="text-slate-600">Base</span>
                    <span className="font-bold text-slate-900">
                      ${currentStation.pricing?.base}
                    </span>
                  </div>
                  <div className="flex justify-between items-center">
                    <span className="text-slate-600">Per Hour</span>
                    <span className="font-bold text-slate-900">
                      ${currentStation.pricing?.perHour}
                    </span>
                  </div>
                  <div className="flex justify-between items-center">
                    <span className="text-slate-600">Per kWh</span>
                    <span className="font-bold text-slate-900">
                      ${currentStation.pricing?.perKwh}
                    </span>
                  </div>
                  <div className="flex justify-between items-center pt-2 border-t border-slate-200">
                    <span className="text-slate-600">Tax</span>
                    <span className="font-bold text-slate-900">
                      {currentStation.pricing?.taxPct}%
                    </span>
                  </div>
                </div>
              </div>

              <div className="flex flex-col gap-2">
                <button
                  onClick={() => handleEdit(currentStation)}
                  className="w-full px-4 py-3 bg-gradient-to-r from-blue-600 to-blue-700 text-white rounded-xl font-semibold shadow-lg shadow-blue-500/30 hover:shadow-xl hover:shadow-blue-500/40 hover:scale-105 transition-all duration-200 flex items-center justify-center gap-2"
                >
                  <Pencil className="w-4 h-4" />
                  Edit Station
                </button>

                {currentStation.status === "Active" ? (
                  <button
                    onClick={() =>
                      handleToggleStatus(currentStation.id, "deactivate")
                    }
                    disabled={saving}
                    className="w-full px-4 py-3 bg-gradient-to-r from-red-500 to-red-600 text-white rounded-xl font-semibold shadow-lg shadow-red-500/30 hover:shadow-xl hover:shadow-red-500/40 hover:scale-105 disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:scale-100 transition-all duration-200 flex items-center justify-center gap-2"
                  >
                    <Power className="w-4 h-4" />
                    {saving ? "Processing..." : "Deactivate"}
                  </button>
                ) : (
                  <button
                    onClick={() =>
                      handleToggleStatus(currentStation.id, "activate")
                    }
                    disabled={saving}
                    className="w-full px-4 py-3 bg-gradient-to-r from-emerald-500 to-emerald-600 text-white rounded-xl font-semibold shadow-lg shadow-emerald-500/30 hover:shadow-xl hover:shadow-emerald-500/40 hover:scale-105 disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:scale-100 transition-all duration-200 flex items-center justify-center gap-2"
                  >
                    <Power className="w-4 h-4" />
                    {saving ? "Processing..." : "Activate"}
                  </button>
                )}
              </div>
            </div>

            {/* Map */}
            <div className="lg:col-span-2">
              <div className="rounded-xl overflow-hidden shadow-md border border-slate-200 h-full min-h-[400px]">
                <GoogleMap
                  mapContainerStyle={mapContainerStyle}
                  center={{ lat: currentStation.lat, lng: currentStation.lng }}
                  zoom={mapZoom}
                >
                  <Marker
                    position={{
                      lat: currentStation.lat,
                      lng: currentStation.lng,
                    }}
                  />
                </GoogleMap>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Add Station Modal */}
      {showAddForm && (
        <div className="fixed inset-0 flex items-center justify-center bg-black/60 backdrop-blur-sm z-50 p-4 overflow-y-auto">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-4xl transform transition-all my-8">
            <div className="p-6 rounded-t-2xl bg-gradient-to-r from-emerald-500 to-emerald-600 flex items-center justify-between">
              <div>
                <h3 className="text-xl font-bold text-white">
                  Add New Station
                </h3>
                <p className="text-emerald-100 text-sm mt-1">
                  Create a new charging station
                </p>
              </div>
              <button
                onClick={() => setShowAddForm(false)}
                className="p-2 text-white/80 hover:text-white hover:bg-white/20 rounded-xl transition-all"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-6 space-y-6 max-h-[calc(100vh-200px)] overflow-y-auto">
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                <div className="flex flex-col">
                  <label className="mb-2 text-sm font-semibold text-slate-700">
                    Station Name
                  </label>
                  <input
                    type="text"
                    placeholder="e.g. Downtown Hub"
                    value={newStation.name}
                    onChange={(e) =>
                      setNewStation({ ...newStation, name: e.target.value })
                    }
                    className="px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                  />
                </div>

                <div className="flex flex-col">
                  <label className="mb-2 text-sm font-semibold text-slate-700">
                    Station Type
                  </label>
                  <select
                    value={newStation.type}
                    onChange={(e) =>
                      setNewStation({ ...newStation, type: e.target.value })
                    }
                    className="px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                  >
                    <option value="">Select Type</option>
                    <option value="AC">AC Charging</option>
                    <option value="DC">DC Fast Charging</option>
                  </select>
                </div>

                <div className="flex flex-col">
                  <label className="mb-2 text-sm font-semibold text-slate-700">
                    Connectors
                  </label>
                  <input
                    type="number"
                    placeholder="Number of ports"
                    value={newStation.connectors}
                    onChange={(e) =>
                      setNewStation({
                        ...newStation,
                        connectors: Number(e.target.value),
                      })
                    }
                    className="px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                  />
                </div>

                <div className="flex flex-col">
                  <label className="mb-2 text-sm font-semibold text-slate-700">
                    Auto Approve
                  </label>
                  <select
                    value={newStation.autoApproveEnabled}
                    onChange={(e) =>
                      setNewStation({
                        ...newStation,
                        autoApproveEnabled: e.target.value === "true",
                      })
                    }
                    className="px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                  >
                    <option value="true">Enabled</option>
                    <option value="false">Disabled</option>
                  </select>
                </div>

                <div className="flex flex-col">
                  <label className="mb-2 text-sm font-semibold text-slate-700">
                    Default Slot (minutes)
                  </label>
                  <input
                    type="number"
                    placeholder="e.g. 60"
                    value={newStation.defaultSlotMinutes}
                    onChange={(e) =>
                      setNewStation({
                        ...newStation,
                        defaultSlotMinutes: Number(e.target.value),
                      })
                    }
                    className="px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                  />
                </div>
              </div>

              {/* Pricing Section */}
              <div className="bg-gradient-to-br from-slate-50 to-slate-100 rounded-xl p-6 border border-slate-200">
                <h4 className="font-bold text-slate-900 mb-4 flex items-center gap-2">
                  <DollarSign className="w-5 h-5" />
                  Pricing Configuration
                </h4>
                <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-5 gap-4">
                  <div className="flex flex-col">
                    <label className="mb-2 text-sm font-semibold text-slate-700">
                      Model
                    </label>
                    <select
                      value={newStation.pricingModel}
                      onChange={(e) =>
                        setNewStation({
                          ...newStation,
                          pricingModel: e.target.value,
                        })
                      }
                      className="px-4 py-3 bg-white border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                    >
                      <option value="">Select Model</option>
                      <option value="flat">Flat Rate</option>
                      <option value="hourly">Hourly</option>
                      <option value="kWh">Per kWh</option>
                    </select>
                  </div>
                  <div className="flex flex-col">
                    <label className="mb-2 text-sm font-semibold text-slate-700">
                      Base ($)
                    </label>
                    <input
                      type="number"
                      placeholder="0.00"
                      value={newStation.pricingBase}
                      onChange={(e) =>
                        setNewStation({
                          ...newStation,
                          pricingBase: Number(e.target.value),
                        })
                      }
                      className="px-4 py-3 bg-white border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                    />
                  </div>
                  <div className="flex flex-col">
                    <label className="mb-2 text-sm font-semibold text-slate-700">
                      Per Hour ($)
                    </label>
                    <input
                      type="number"
                      placeholder="0.00"
                      value={newStation.pricingPerHour}
                      onChange={(e) =>
                        setNewStation({
                          ...newStation,
                          pricingPerHour: Number(e.target.value),
                        })
                      }
                      className="px-4 py-3 bg-white border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                    />
                  </div>
                  <div className="flex flex-col">
                    <label className="mb-2 text-sm font-semibold text-slate-700">
                      Per kWh ($)
                    </label>
                    <input
                      type="number"
                      placeholder="0.00"
                      value={newStation.pricingPerKwh}
                      onChange={(e) =>
                        setNewStation({
                          ...newStation,
                          pricingPerKwh: Number(e.target.value),
                        })
                      }
                      className="px-4 py-3 bg-white border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                    />
                  </div>
                  <div className="flex flex-col">
                    <label className="mb-2 text-sm font-semibold text-slate-700">
                      Tax (%)
                    </label>
                    <input
                      type="number"
                      placeholder="0"
                      value={newStation.pricingTaxPct}
                      onChange={(e) =>
                        setNewStation({
                          ...newStation,
                          pricingTaxPct: Number(e.target.value),
                        })
                      }
                      className="px-4 py-3 bg-white border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                    />
                  </div>
                </div>
              </div>

              {/* Map Section */}
              <div>
                <label className="mb-2 text-sm font-semibold text-slate-700 flex items-center gap-2">
                  <MapPin className="w-4 h-4" />
                  Station Location (Click on map to pin)
                </label>
                <div className="rounded-xl overflow-hidden border border-slate-200 h-80 shadow-md">
                  <GoogleMap
                    mapContainerStyle={{ width: "100%", height: "100%" }}
                    center={markerPos || { lat: 6.9271, lng: 79.8612 }}
                    zoom={mapZoom}
                    onClick={handleMapClick}
                  >
                    {markerPos && <Marker position={markerPos} />}
                  </GoogleMap>
                </div>
                {markerPos && (
                  <p className="text-sm text-emerald-600 mt-2 flex items-center gap-2 font-medium">
                    <Check className="w-4 h-4" />
                    Location pinned: {markerPos.lat.toFixed(4)},{" "}
                    {markerPos.lng.toFixed(4)}
                  </p>
                )}
              </div>
            </div>

            <div className="flex justify-end gap-3 p-6 pt-0 border-t border-slate-200">
              <button
                onClick={() => setShowAddForm(false)}
                className="px-6 py-3 bg-slate-100 text-slate-700 rounded-xl font-semibold hover:bg-slate-200 transition-all"
              >
                Cancel
              </button>
              <button
                onClick={handleCreateStation}
                disabled={creating}
                className="px-6 py-3 bg-gradient-to-r from-emerald-600 to-emerald-700 text-white rounded-xl font-semibold shadow-lg shadow-emerald-500/30 hover:shadow-xl hover:shadow-emerald-500/40 hover:scale-105 disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:scale-100 transition-all duration-200 flex items-center gap-2"
              >
                {creating ? (
                  <>
                    <svg
                      className="animate-spin h-5 w-5"
                      fill="none"
                      viewBox="0 0 24 24"
                    >
                      <circle
                        className="opacity-25"
                        cx="12"
                        cy="12"
                        r="10"
                        stroke="currentColor"
                        strokeWidth="4"
                      ></circle>
                      <path
                        className="opacity-75"
                        fill="currentColor"
                        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                      ></path>
                    </svg>
                    Creating...
                  </>
                ) : (
                  <>
                    <Plus className="w-5 h-5" />
                    Create Station
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Edit Modal */}
      {editData && (
        <div className="fixed inset-0 flex items-center justify-center bg-black/60 backdrop-blur-sm z-50 p-4">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg transform transition-all">
            <div className="p-6 rounded-t-2xl bg-gradient-to-r from-blue-500 to-blue-600">
              <h3 className="text-xl font-bold text-white">Edit Station</h3>
              <p className="text-blue-100 text-sm mt-1">
                Update station information and settings
              </p>
            </div>

            <div className="p-6 space-y-4">
              <div>
                <label className="block text-sm font-semibold text-slate-700 mb-2">
                  Name
                </label>
                <input
                  type="text"
                  value={editData.name}
                  onChange={(e) =>
                    setEditData({ ...editData, name: e.target.value })
                  }
                  className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                />
              </div>

              <div>
                <label className="block text-sm font-semibold text-slate-700 mb-2">
                  Type
                </label>
                <select
                  value={editData.type}
                  onChange={(e) =>
                    setEditData({ ...editData, type: e.target.value })
                  }
                  className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                >
                  <option value="AC">AC</option>
                  <option value="DC">DC</option>
                </select>
              </div>

              <div>
                <label className="block text-sm font-semibold text-slate-700 mb-2">
                  Connectors
                </label>
                <input
                  type="number"
                  value={editData.connectors}
                  onChange={(e) =>
                    setEditData({
                      ...editData,
                      connectors: Number(e.target.value),
                    })
                  }
                  className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                />
              </div>

              <div>
                <label className="block text-sm font-semibold text-slate-700 mb-2">
                  Auto Approve
                </label>
                <select
                  value={editData.autoApproveEnabled}
                  onChange={(e) =>
                    setEditData({
                      ...editData,
                      autoApproveEnabled: e.target.value === "true",
                    })
                  }
                  className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                >
                  <option value="true">Enabled</option>
                  <option value="false">Disabled</option>
                </select>
              </div>

              <div>
                <label className="block text-sm font-semibold text-slate-700 mb-2">
                  Default Slot Minutes
                </label>
                <input
                  type="number"
                  value={editData.defaultSlotMinutes}
                  onChange={(e) =>
                    setEditData({
                      ...editData,
                      defaultSlotMinutes: Number(e.target.value),
                    })
                  }
                  className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                />
              </div>

              <div>
                <label className="block text-sm font-semibold text-slate-700 mb-2">
                  Pricing Model
                </label>
                <select
                  value={editData.pricing?.model || ""}
                  onChange={(e) =>
                    setEditData({
                      ...editData,
                      pricing: { ...editData.pricing, model: e.target.value },
                    })
                  }
                  className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                >
                  <option value="">Select Model</option>
                  <option value="flat">Flat Rate</option>
                  <option value="hourly">Hourly</option>
                  <option value="kwh">Per kWh</option>
                </select>
              </div>
            </div>

            <div className="flex gap-3 p-6 pt-0">
              <button
                onClick={() => setEditData(null)}
                className="flex-1 px-4 py-3 bg-slate-100 text-slate-700 rounded-xl font-semibold hover:bg-slate-200 transition-all"
              >
                Cancel
              </button>
              <button
                onClick={handleSave}
                disabled={saving}
                className="flex-1 px-4 py-3 bg-gradient-to-r from-blue-600 to-blue-700 text-white rounded-xl font-semibold shadow-lg shadow-blue-500/30 hover:shadow-xl hover:shadow-blue-500/40 hover:scale-105 disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:scale-100 transition-all duration-200"
              >
                {saving ? (
                  <span className="flex items-center justify-center gap-2">
                    <svg
                      className="animate-spin h-5 w-5"
                      fill="none"
                      viewBox="0 0 24 24"
                    >
                      <circle
                        className="opacity-25"
                        cx="12"
                        cy="12"
                        r="10"
                        stroke="currentColor"
                        strokeWidth="4"
                      ></circle>
                      <path
                        className="opacity-75"
                        fill="currentColor"
                        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                      ></path>
                    </svg>
                    Saving...
                  </span>
                ) : (
                  "Save Changes"
                )}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}