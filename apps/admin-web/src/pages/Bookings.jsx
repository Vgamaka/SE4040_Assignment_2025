import { useEffect, useState } from "react";
import api from "../services/api";

export default function Bookings() {
  const [bookings, setBookings] = useState([]);
  const [stations, setStations] = useState([]);
  const [owners, setOwners] = useState([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState("All");
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [editMode, setEditMode] = useState(null);
  const [editForm, setEditForm] = useState({});
  const [form, setForm] = useState({
    ownerNIC: "",
    stationId: "",
    startUtc: "",
    endUtc: ""
  });
  const [isSubmitting, setIsSubmitting] = useState(false);

  const loadData = async () => {
    setLoading(true);
    try {
      const [bookingsRes, stationsRes, ownersRes] = await Promise.all([
        api.get("/api/booking"),
        api.get("/api/station"),
        api.get("/api/evowner")
      ]);
      setBookings(bookingsRes.data);
      setStations(stationsRes.data.filter(s => s.isActive));
      setOwners(ownersRes.data.filter(o => o.isActive));
    } catch (error) {
      console.error("Failed to load data:", error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const createBooking = async () => {
    if (!form.ownerNIC || !form.stationId || !form.startUtc || !form.endUtc) {
      alert("Please fill all required fields");
      return;
    }

    setIsSubmitting(true);
    try {
      await api.post("/api/booking", form);
      setForm({ ownerNIC: "", stationId: "", startUtc: "", endUtc: "" });
      setShowCreateModal(false);
      await loadData();
    } catch (error) {
      console.error("Failed to create booking:", error);
      alert(error.response?.data?.message || "Failed to create booking");
    } finally {
      setIsSubmitting(false);
    }
  };

  const approveBooking = async (id) => {
    try {
      await api.put(`/api/booking/${id}/approve`);
      await loadData();
    } catch (error) {
      console.error("Failed to approve booking:", error);
      alert(error.response?.data?.message || "Failed to approve booking");
    }
  };

  const cancelBooking = async (id) => {
    if (!confirm("Are you sure you want to cancel this booking?")) return;
    try {
      await api.put(`/api/booking/${id}/cancel`);
      await loadData();
    } catch (error) {
      console.error("Failed to cancel booking:", error);
      alert(error.response?.data?.message || "Failed to cancel booking");
    }
  };

  const startEdit = (booking) => {
    setEditMode(booking.id);
    setEditForm({
      startUtc: new Date(booking.startUtc).toISOString().slice(0, 16),
      endUtc: new Date(booking.endUtc).toISOString().slice(0, 16)
    });
  };

  const saveEdit = async (id) => {
    try {
      await api.put(`/api/booking/${id}`, editForm);
      setEditMode(null);
      setEditForm({});
      await loadData();
    } catch (error) {
      console.error("Failed to update booking:", error);
      alert(error.response?.data?.message || "Failed to update booking");
    }
  };

  const cancelEdit = () => {
    setEditMode(null);
    setEditForm({});
  };

  const deleteBooking = async (id) => {
    if (!confirm("Are you sure you want to delete this booking? This action cannot be undone.")) return;
    try {
      await api.delete(`/api/booking/${id}`);
      await loadData();
    } catch (error) {
      console.error("Failed to delete booking:", error);
      alert(error.response?.data?.message || "Failed to delete booking");
    }
  };

  const filteredBookings = bookings.filter(b => {
    if (filter === "All") return true;
    return b.status === filter;
  });

  const getStatusColor = (status) => {
    switch (status) {
      case "Pending": return "bg-yellow-100 text-yellow-800 border-yellow-200";
      case "Approved": return "bg-green-100 text-green-800 border-green-200";
      case "Completed": return "bg-blue-100 text-blue-800 border-blue-200";
      case "Cancelled": return "bg-red-100 text-red-800 border-red-200";
      default: return "bg-gray-100 text-gray-800 border-gray-200";
    }
  };

  const stats = {
    total: bookings.length,
    pending: bookings.filter(b => b.status === "Pending").length,
    approved: bookings.filter(b => b.status === "Approved").length,
    completed: bookings.filter(b => b.status === "Completed").length
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-gray-50 to-gray-100 p-6">
      <div className="max-w-7xl mx-auto space-y-8">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-4xl font-bold text-gray-900">Bookings Management</h1>
            <p className="text-gray-600 mt-2">Manage charging station reservations</p>
          </div>
          <button
            onClick={() => setShowCreateModal(true)}
            className="bg-gradient-to-r from-blue-600 to-blue-700 hover:from-blue-700 hover:to-blue-800 text-white px-6 py-3 rounded-lg font-semibold shadow-lg hover:shadow-xl transition-all duration-200"
          >
            + Create Booking
          </button>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
          <StatCard title="Total Bookings" value={stats.total} icon="📊" gradient="from-purple-500 to-purple-600" />
          <StatCard title="Pending" value={stats.pending} icon="⏳" gradient="from-yellow-500 to-orange-500" />
          <StatCard title="Approved" value={stats.approved} icon="✓" gradient="from-green-500 to-emerald-600" />
          <StatCard title="Completed" value={stats.completed} icon="🎉" gradient="from-blue-500 to-indigo-600" />
        </div>

        <div className="bg-white rounded-2xl shadow-xl border border-gray-200 overflow-hidden">
          <div className="bg-gradient-to-r from-gray-900 to-gray-800 px-8 py-6">
            <h2 className="text-2xl font-bold text-white flex items-center gap-3">
              <span className="text-3xl">📅</span>
              All Bookings
            </h2>
            <div className="flex gap-2 mt-4 flex-wrap">
              {["All", "Pending", "Approved", "Completed", "Cancelled"].map(status => (
                <button
                  key={status}
                  onClick={() => setFilter(status)}
                  className={`px-4 py-2 rounded-lg font-medium text-sm transition-all ${
                    filter === status
                      ? "bg-white text-gray-900"
                      : "bg-gray-700 text-gray-300 hover:bg-gray-600"
                  }`}
                >
                  {status}
                </button>
              ))}
            </div>
          </div>

          <div className="p-8">
            {loading ? (
              <div className="space-y-4">
                {[1, 2, 3].map(i => (
                  <div key={i} className="bg-gray-100 rounded-xl p-6 animate-pulse">
                    <div className="h-6 bg-gray-200 rounded w-1/3 mb-3"></div>
                    <div className="h-4 bg-gray-200 rounded w-1/2"></div>
                  </div>
                ))}
              </div>
            ) : filteredBookings.length === 0 ? (
              <div className="text-center py-12 bg-gray-50 rounded-xl border-2 border-dashed border-gray-300">
                <div className="text-6xl mb-4">📅</div>
                <p className="text-gray-600 font-medium">No bookings found</p>
                <p className="text-gray-500 text-sm mt-1">
                  {filter === "All" 
                    ? "Create your first booking using the button above"
                    : `No ${filter.toLowerCase()} bookings`}
                </p>
              </div>
            ) : (
              <div className="grid gap-4">
                {filteredBookings.map(booking => (
                  <div
                    key={booking.id}
                    className="group bg-gradient-to-br from-white to-gray-50 border border-gray-200 rounded-xl p-6 hover:shadow-lg transition-all duration-200"
                  >
                    {editMode === booking.id ? (
                      <div className="space-y-4">
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                          <div>
                            <label className="block text-sm font-medium text-gray-700 mb-2">Start Time</label>
                            <input
                              type="datetime-local"
                              className="w-full border border-gray-300 px-4 py-2 rounded-lg focus:ring-2 focus:ring-blue-500 outline-none"
                              value={editForm.startUtc}
                              onChange={e => setEditForm({...editForm, startUtc: e.target.value})}
                            />
                          </div>
                          <div>
                            <label className="block text-sm font-medium text-gray-700 mb-2">End Time</label>
                            <input
                              type="datetime-local"
                              className="w-full border border-gray-300 px-4 py-2 rounded-lg focus:ring-2 focus:ring-blue-500 outline-none"
                              value={editForm.endUtc}
                              onChange={e => setEditForm({...editForm, endUtc: e.target.value})}
                            />
                          </div>
                        </div>
                        <div className="flex gap-2">
                          <button
                            onClick={() => saveEdit(booking.id)}
                            className="bg-green-600 hover:bg-green-700 text-white px-6 py-2 rounded-lg font-medium transition-colors"
                          >
                            Save Changes
                          </button>
                          <button
                            onClick={cancelEdit}
                            className="bg-gray-500 hover:bg-gray-600 text-white px-6 py-2 rounded-lg font-medium transition-colors"
                          >
                            Cancel
                          </button>
                        </div>
                      </div>
                    ) : (
                      <div className="flex items-start gap-4">
                        <div className="flex-shrink-0 w-14 h-14 bg-gradient-to-br from-indigo-500 to-purple-600 rounded-xl flex items-center justify-center text-white font-bold text-xl shadow-md">
                          <span>⚡</span>
                        </div>
                        
                        <div className="flex-1 min-w-0">
                          <div className="flex items-center gap-3 mb-3">
                            <h4 className="font-bold text-gray-900 text-lg">
                              Booking #{booking.id.slice(0, 8)}
                            </h4>
                            <span className={`px-3 py-1 rounded-full text-xs font-semibold border ${getStatusColor(booking.status)}`}>
                              {booking.status}
                            </span>
                          </div>
                          
                          <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-sm">
                            <div className="space-y-2">
                              <div className="flex items-center gap-2 text-gray-700">
                                <span className="font-semibold">👤 Owner:</span>
                                <span>{booking.ownerNIC}</span>
                              </div>
                              <div className="flex items-center gap-2 text-gray-700">
                                <span className="font-semibold">🏢 Station:</span>
                                <span>{booking.stationId}</span>
                              </div>
                            </div>
                            <div className="space-y-2">
                              <div className="flex items-center gap-2 text-gray-700">
                                <span className="font-semibold">🕐 Start:</span>
                                <span>{new Date(booking.startUtc).toLocaleString()}</span>
                              </div>
                              <div className="flex items-center gap-2 text-gray-700">
                                <span className="font-semibold">🕐 End:</span>
                                <span>{new Date(booking.endUtc).toLocaleString()}</span>
                              </div>
                            </div>
                          </div>

                          {booking.qrToken && (
                            <div className="mt-3 flex items-center gap-2 text-xs text-gray-500 bg-gray-50 px-3 py-2 rounded-lg">
                              <span className="font-semibold">🔐 QR Token:</span>
                              <span className="truncate">{booking.qrToken}</span>
                            </div>
                          )}
                        </div>
                        
                        <div className="flex-shrink-0 flex flex-wrap gap-2 opacity-0 group-hover:opacity-100 transition-opacity">
                          {booking.status === "Pending" && (
                            <>
                              <button
                                onClick={() => approveBooking(booking.id)}
                                className="px-4 py-2 bg-green-50 border border-green-200 text-green-700 rounded-lg hover:bg-green-100 transition-colors font-medium text-sm"
                              >
                                Approve
                              </button>
                              <button
                                onClick={() => startEdit(booking)}
                                className="px-4 py-2 bg-blue-50 border border-blue-200 text-blue-700 rounded-lg hover:bg-blue-100 transition-colors font-medium text-sm"
                              >
                                Edit
                              </button>
                            </>
                          )}
                          {(booking.status === "Pending" || booking.status === "Approved") && (
                            <button
                              onClick={() => cancelBooking(booking.id)}
                              className="px-4 py-2 bg-orange-50 border border-orange-200 text-orange-700 rounded-lg hover:bg-orange-100 transition-colors font-medium text-sm"
                            >
                              Cancel
                            </button>
                          )}
                          <button
                            onClick={() => deleteBooking(booking.id)}
                            className="px-4 py-2 bg-red-50 border border-red-200 text-red-700 rounded-lg hover:bg-red-100 transition-colors font-medium text-sm"
                          >
                            Delete
                          </button>
                        </div>
                      </div>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>

      {showCreateModal && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl shadow-2xl max-w-2xl w-full max-h-[90vh] overflow-auto">
            <div className="bg-gradient-to-r from-blue-600 to-indigo-600 px-8 py-6 rounded-t-2xl">
              <h3 className="text-2xl font-bold text-white">Create New Booking</h3>
              <p className="text-blue-100 mt-1">Schedule a charging station reservation</p>
            </div>
            
            <div className="p-8 space-y-6">
              <div>
                <label className="block text-sm font-semibold text-gray-700 mb-2">EV Owner *</label>
                <select
                  className="w-full border border-gray-300 px-4 py-3 rounded-lg focus:ring-2 focus:ring-blue-500 outline-none bg-white"
                  value={form.ownerNIC}
                  onChange={e => setForm({...form, ownerNIC: e.target.value})}
                >
                  <option value="">Select Owner</option>
                  {owners.map(o => (
                    <option key={o.nic} value={o.nic}>{o.name || o.nic} - {o.email}</option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-sm font-semibold text-gray-700 mb-2">Charging Station *</label>
                <select
                  className="w-full border border-gray-300 px-4 py-3 rounded-lg focus:ring-2 focus:ring-blue-500 outline-none bg-white"
                  value={form.stationId}
                  onChange={e => setForm({...form, stationId: e.target.value})}
                >
                  <option value="">Select Station</option>
                  {stations.map(s => (
                    <option key={s.id} value={s.id}>{s.name} - {s.location}</option>
                  ))}
                </select>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-semibold text-gray-700 mb-2">Start Time *</label>
                  <input
                    type="datetime-local"
                    className="w-full border border-gray-300 px-4 py-3 rounded-lg focus:ring-2 focus:ring-blue-500 outline-none"
                    value={form.startUtc}
                    onChange={e => setForm({...form, startUtc: e.target.value})}
                  />
                </div>
                <div>
                  <label className="block text-sm font-semibold text-gray-700 mb-2">End Time *</label>
                  <input
                    type="datetime-local"
                    className="w-full border border-gray-300 px-4 py-3 rounded-lg focus:ring-2 focus:ring-blue-500 outline-none"
                    value={form.endUtc}
                    onChange={e => setForm({...form, endUtc: e.target.value})}
                  />
                </div>
              </div>

              <div className="flex gap-3 pt-4">
                <button
                  onClick={createBooking}
                  disabled={isSubmitting}
                  className="flex-1 bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-700 hover:to-indigo-700 text-white px-6 py-3 rounded-lg font-semibold shadow-lg hover:shadow-xl transition-all duration-200 disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {isSubmitting ? "Creating..." : "Create Booking"}
                </button>
                <button
                  onClick={() => setShowCreateModal(false)}
                  className="px-6 py-3 border border-gray-300 text-gray-700 rounded-lg font-semibold hover:bg-gray-50 transition-colors"
                >
                  Cancel
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function StatCard({ title, value, icon, gradient }) {
  return (
    <div className="relative group">
      <div className={`absolute inset-0 bg-gradient-to-br ${gradient} rounded-2xl opacity-75 group-hover:opacity-100 transition-opacity duration-300`}></div>
      <div className="relative bg-white rounded-2xl p-6 shadow-lg hover:shadow-xl transition-shadow duration-300 border border-gray-100">
        <div className="flex items-start justify-between">
          <div className="flex-1">
            <p className="text-sm font-medium text-gray-600 uppercase tracking-wide">{title}</p>
            <p className={`mt-3 text-4xl font-bold bg-gradient-to-br ${gradient} bg-clip-text text-transparent`}>
              {value}
            </p>
          </div>
          <div className={`flex-shrink-0 w-12 h-12 rounded-xl bg-gradient-to-br ${gradient} flex items-center justify-center text-2xl shadow-md`}>
            {icon}
          </div>
        </div>
      </div>
    </div>
  );
}