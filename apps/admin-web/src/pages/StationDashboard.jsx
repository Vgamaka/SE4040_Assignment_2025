import { useEffect, useState } from "react";
import { useAuth } from "../context/AuthContext";
import api from "../services/api";
import { ToastContainer, toast } from 'react-toastify';
import 'react-toastify/dist/ReactToastify.css';

export default function StationDashboard() {
  const { logout } = useAuth();
  const user = JSON.parse(localStorage.getItem("user"));
  const stationId = user?.operatorStationIds?.[0];

  const [station, setStation] = useState(null);
  const [bookings, setBookings] = useState([]);
  const [loading, setLoading] = useState(false);

  const [form, setForm] = useState({
    localDate: "",
    startTime: "",
    minutes: "",
    notes: "",
  });

  const [filters, setFilters] = useState({
    status: "",
    fromUtc: "",
    toUtc: "",
  });

  // Edit modal state
  const [editModal, setEditModal] = useState({
    isOpen: false,
    bookingId: null,
    localDate: "",
    startTime: "",
    minutes: "",
    notes: "",
  });

  useEffect(() => {
    if (stationId) {
      fetchStationInfo();
    }
    fetchBookings();
  }, []);

  const fetchStationInfo = async () => {
    try {
      const res = await api.get(`/api/Station/${stationId}`);
      setStation(res.data);
    } catch (err) {
      console.error("Error fetching station info:", err);
      toast.error("Failed to fetch station information.", {
        position: "top-right",
        autoClose: 5000,
      });
    }
  };

  const fetchBookings = async (params = {}) => {
    try {
      setLoading(true);
      const res = await api.get("/api/Booking/mine", { params });
      setBookings(res.data);
    } catch (err) {
      console.error("Error fetching bookings:", err);
      toast.error("Failed to fetch bookings.", {
        position: "top-right",
        autoClose: 5000,
      });
    } finally {
      setLoading(false);
    }
  };

  const handleChange = (e) =>
    setForm({ ...form, [e.target.name]: e.target.value });

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      await api.post("/api/Booking", {
        ...form,
        stationId,
      });
      toast.success("Booking created successfully!", {
        position: "top-right",
        autoClose: 3000,
        hideProgressBar: false,
      });
      setForm({ localDate: "", startTime: "", minutes: "", notes: "" });
      fetchBookings();
    } catch (err) {
      console.error("Booking creation failed:", err);
      if (err.response?.status === 400)
        toast.error("Bad request – check inputs.", {
          position: "top-right",
          autoClose: 5000,
        });
      else if (err.response?.status === 409)
        toast.error("Conflict – slot full or station closed.", {
          position: "top-right",
          autoClose: 5000,
        });
      else toast.error("An unexpected error occurred.", {
        position: "top-right",
        autoClose: 5000,
      });
    }
  };

  const handleFilterChange = (e) =>
    setFilters({ ...filters, [e.target.name]: e.target.value });

  const applyFilters = () => {
    const queryParams = {};
    if (filters.status) queryParams.status = filters.status;
    if (filters.fromUtc)
      queryParams.fromUtc = new Date(filters.fromUtc).toISOString();
    if (filters.toUtc)
      queryParams.toUtc = new Date(filters.toUtc).toISOString();
    fetchBookings(queryParams);
  };

  const resetFilters = () => {
    setFilters({ status: "", fromUtc: "", toUtc: "" });
    fetchBookings();
  };

  const handleApprove = async (id) => {
    try {
      await api.put(`/api/Booking/${id}/approve`);
      toast.success("Booking approved successfully!", {
        position: "top-right",
        autoClose: 3000,
      });
      fetchBookings();
    } catch (err) {
      console.error("Error approving booking:", err);
      toast.error("Failed to approve booking.", {
        position: "top-right",
        autoClose: 5000,
      });
    }
  };

  const handleReject = async (id) => {
    try {
      await api.put(`/api/Booking/${id}/reject`);
      toast.success("Booking rejected successfully!", {
        position: "top-right",
        autoClose: 3000,
      });
      fetchBookings();
    } catch (err) {
      console.error("Error rejecting booking:", err);
      toast.error("Failed to reject booking.", {
        position: "top-right",
        autoClose: 5000,
      });
    }
  };

  const handleCancel = async (id) => {
    try {
      await api.post(`/api/Booking/${id}/cancel`);
      toast.success("Booking cancelled successfully!", {
        position: "top-right",
        autoClose: 3000,
      });
      fetchBookings();
    } catch (err) {
      console.error("Error cancelling booking:", err);
      toast.error("Failed to cancel booking.", {
        position: "top-right",
        autoClose: 5000,
      });
    }
  };

  // Edit functionality
  const openEditModal = (booking) => {
    const startDate = new Date(booking.slotStartUtc);
    const localDate = startDate.toISOString().split('T')[0];
    const startTime = startDate.toTimeString().slice(0, 5);

    setEditModal({
      isOpen: true,
      bookingId: booking.id,
      localDate: localDate,
      startTime: startTime,
      minutes: booking.slotMinutes.toString(),
      notes: booking.notes || "",
    });
  };

  const closeEditModal = () => {
    setEditModal({
      isOpen: false,
      bookingId: null,
      localDate: "",
      startTime: "",
      minutes: "",
      notes: "",
    });
  };

  const handleEditChange = (e) => {
    setEditModal({ ...editModal, [e.target.name]: e.target.value });
  };

  const handleEditSubmit = async (e) => {
    e.preventDefault();
    try {
      await api.put(`/api/Booking/${editModal.bookingId}`, {
        localDate: editModal.localDate,
        startTime: editModal.startTime,
        minutes: parseInt(editModal.minutes),
        notes: editModal.notes,
      });
      toast.success("Booking updated successfully!", {
        position: "top-right",
        autoClose: 3000,
        hideProgressBar: false,
      });
      closeEditModal();
      fetchBookings();
    } catch (err) {
      console.error("Booking update failed:", err);
      if (err.response?.status === 400)
        toast.error("Bad request – check inputs.", {
          position: "top-right",
          autoClose: 5000,
        });
      else if (err.response?.status === 403)
        toast.error("Forbidden – you cannot edit this booking.", {
          position: "top-right",
          autoClose: 5000,
        });
      else if (err.response?.status === 409)
        toast.error("Conflict – slot full or station closed.", {
          position: "top-right",
          autoClose: 5000,
        });
      else toast.error("An unexpected error occurred.", {
        position: "top-right",
        autoClose: 5000,
      });
    }
  };

  const getStatusColor = (status) => {
    const colors = {
      Pending: "bg-amber-100 text-amber-700",
      Approved: "bg-emerald-100 text-emerald-700",
      Cancelled: "bg-rose-100 text-rose-700",
      Completed: "bg-blue-100 text-blue-700",
      NoShow: "bg-gray-100 text-gray-700",
      CheckedIn: "bg-purple-100 text-purple-700",
    };
    return colors[status] || "bg-slate-100 text-slate-700";
  };

  const getStatusDot = (status) => {
    const colors = {
      Pending: "bg-amber-500",
      Approved: "bg-emerald-500",
      Cancelled: "bg-rose-500",
      Completed: "bg-blue-500",
      NoShow: "bg-gray-500",
      CheckedIn: "bg-purple-500",
    };
    return colors[status] || "bg-slate-500";
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 via-blue-50 to-slate-100">
      <div className="w-full mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {/* Header */}
        <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6 mb-6">
          <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
            <div>
              <h1 className="text-3xl font-bold bg-gradient-to-r from-slate-800 to-slate-600 bg-clip-text text-transparent">
                Station Dashboard
              </h1>
              <p className="text-slate-500 mt-1 flex items-center gap-2">
                <span className="w-2 h-2 bg-emerald-500 rounded-full animate-pulse"></span>
                Managing your charging station
              </p>
            </div>
            <button
              onClick={() => {
                try {
                  toast.info("Logging out...", {
                    position: "top-right",
                    autoClose: 2000,
                  });
                  // Slight delay for toast to be visible
                  setTimeout(() => {
                    logout();
                  }, 1000);
                } catch (error) {
                  toast.error("Logout failed. Please try again.", {
                    position: "top-right",
                    autoClose: 5000,
                  });
                  console.error("Logout error:", error);
                }
              }}
              className="px-5 py-2.5 bg-gradient-to-r from-red-500 to-red-600 text-white rounded-xl font-medium shadow-lg shadow-red-500/30 hover:shadow-xl hover:shadow-red-500/40 hover:scale-105 transition-all duration-200"
            >
              Logout
            </button>
          </div>
        </div>

        {/* Toast Container */}
        <ToastContainer
          position="top-right"
          autoClose={3000}
          hideProgressBar={false}
          newestOnTop
          closeOnClick
          rtl={false}
          pauseOnFocusLoss
          draggable
          pauseOnHover
        />

        {/* Station Info Card */}
        {station && (
          <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6 mb-6">
            <div className="flex items-center gap-3 mb-4">
              <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-blue-500 to-blue-600 flex items-center justify-center shadow-lg shadow-blue-500/30">
                <svg
                  className="w-5 h-5 text-white"
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M13 10V3L4 14h7v7l9-11h-7z"
                  />
                </svg>
              </div>
              <h2 className="text-xl font-bold text-slate-800">
                Station Information
              </h2>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              <div className="bg-slate-50 rounded-xl p-4">
                <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">
                  Station Name
                </p>
                <p className="text-lg font-bold text-slate-900">
                  {station.name}
                </p>
              </div>

              <div className="bg-slate-50 rounded-xl p-4">
                <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">
                  Type
                </p>
                <p className="text-lg font-bold text-slate-900">
                  {station.type}
                </p>
              </div>

              <div className="bg-slate-50 rounded-xl p-4">
                <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">
                  Status
                </p>
                <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-sm font-bold bg-emerald-100 text-emerald-700">
                  <span className="w-1.5 h-1.5 bg-emerald-500 rounded-full"></span>
                  {station.status}
                </span>
              </div>

              <div className="bg-slate-50 rounded-xl p-4">
                <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">
                  Connectors
                </p>
                <p className="text-lg font-bold text-slate-900">
                  {station.connectors}
                </p>
              </div>

              <div className="bg-slate-50 rounded-xl p-4">
                <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">
                  Default Slot
                </p>
                <p className="text-lg font-bold text-slate-900">
                  {station.defaultSlotMinutes} mins
                </p>
              </div>

              <div className="bg-slate-50 rounded-xl p-4">
                <p className="text-xs font-medium text-slate-500 uppercase tracking-wider mb-1">
                  Auto Approve
                </p>
                <p className="text-lg font-bold text-slate-900">
                  {station.autoApproveEnabled ? "Enabled" : "Disabled"}
                </p>
              </div>
            </div>
          </div>
        )}

        <div className="grid grid-cols-1 xl:grid-cols-3 gap-6">
          {/* Create Reservation Form */}
          <div className="xl:col-span-1">
            <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6">
              <div className="flex items-center gap-3 mb-6">
                <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-indigo-500 to-indigo-600 flex items-center justify-center shadow-lg shadow-indigo-500/30">
                  <svg
                    className="w-5 h-5 text-white"
                    fill="none"
                    stroke="currentColor"
                    viewBox="0 0 24 24"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M12 4v16m8-8H4"
                    />
                  </svg>
                </div>
                <h2 className="text-xl font-bold text-slate-800">
                  Create Reservation
                </h2>
              </div>

              <form onSubmit={handleSubmit} className="space-y-4">
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-2">
                    Date
                  </label>
                  <input
                    type="date"
                    name="localDate"
                    value={form.localDate}
                    onChange={handleChange}
                    className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-indigo-500 focus:border-transparent outline-none transition-all"
                    required
                  />
                </div>

                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-2">
                    Start Time
                  </label>
                  <input
                    type="time"
                    name="startTime"
                    value={form.startTime}
                    onChange={handleChange}
                    className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-indigo-500 focus:border-transparent outline-none transition-all"
                    required
                  />
                </div>

                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-2">
                    Duration (minutes)
                  </label>
                  <input
                    type="number"
                    name="minutes"
                    placeholder="60"
                    value={form.minutes}
                    onChange={handleChange}
                    className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-indigo-500 focus:border-transparent outline-none transition-all"
                    required
                  />
                </div>

                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-2">
                    Notes (optional)
                  </label>
                  <textarea
                    name="notes"
                    placeholder="Add any additional notes..."
                    value={form.notes}
                    onChange={handleChange}
                    rows="3"
                    className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-indigo-500 focus:border-transparent outline-none transition-all resize-none"
                  />
                </div>

                <button
                  type="submit"
                  className="w-full px-4 py-3 bg-gradient-to-r from-indigo-600 to-indigo-700 text-white rounded-xl font-semibold shadow-lg shadow-indigo-500/30 hover:shadow-xl hover:shadow-indigo-500/40 hover:scale-105 transition-all duration-200"
                >
                  Create Booking
                </button>
              </form>
            </div>
          </div>

          {/* Bookings Table */}
          <div className="xl:col-span-2">
            <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6">
              <div className="flex items-center gap-3 mb-6">
                <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-500 to-emerald-600 flex items-center justify-center shadow-lg shadow-emerald-500/30">
                  <svg
                    className="w-5 h-5 text-white"
                    fill="none"
                    stroke="currentColor"
                    viewBox="0 0 24 24"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"
                    />
                  </svg>
                </div>
                <h2 className="text-xl font-bold text-slate-800">
                  My Bookings
                </h2>
              </div>

              {/* Filters */}
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 mb-6">
                <select
                  name="status"
                  value={filters.status}
                  onChange={handleFilterChange}
                  className="px-4 py-2 bg-slate-50 border border-slate-200 rounded-xl text-sm font-medium focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                >
                  <option value="">All Statuses</option>
                  <option value="Pending">Pending</option>
                  <option value="Approved">Approved</option>
                  <option value="Cancelled">Cancelled</option>
                  <option value="Completed">Completed</option>
                  <option value="NoShow">No Show</option>
                  <option value="CheckedIn">Checked In</option>
                </select>

                <input
                  type="datetime-local"
                  name="fromUtc"
                  value={filters.fromUtc}
                  onChange={handleFilterChange}
                  className="px-4 py-2 bg-slate-50 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                />

                <input
                  type="datetime-local"
                  name="toUtc"
                  value={filters.toUtc}
                  onChange={handleFilterChange}
                  className="px-4 py-2 bg-slate-50 border border-slate-200 rounded-xl text-sm focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                />

                <div className="flex gap-2">
                  <button
                    onClick={applyFilters}
                    className="flex-1 px-4 py-2 bg-gradient-to-r from-emerald-500 to-emerald-600 text-white rounded-xl font-medium shadow-lg shadow-emerald-500/30 hover:shadow-xl hover:shadow-emerald-500/40 hover:scale-105 transition-all duration-200 flex items-center justify-center gap-2"
                  >
                    <svg
                      className="w-4 h-4"
                      fill="none"
                      stroke="currentColor"
                      viewBox="0 0 24 24"
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M3 4a1 1 0 011-1h16a1 1 0 011 1v2.586a1 1 0 01-.293.707l-6.414 6.414a1 1 0 00-.293.707V17l-4 4v-6.586a1 1 0 00-.293-.707L3.293 7.293A1 1 0 013 6.586V4z"
                      />
                    </svg>
                    Apply
                  </button>
                  <button
                    onClick={resetFilters}
                    className="px-4 py-2 bg-slate-100 text-slate-700 rounded-xl font-medium hover:bg-slate-200 transition-all"
                  >
                    Reset
                  </button>
                </div>
              </div>

              {/* Table */}
              {loading ? (
                <div className="flex flex-col items-center justify-center py-16">
                  <svg
                    className="animate-spin h-10 w-10 text-blue-600 mb-4"
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
                  <p className="text-slate-500 font-medium">
                    Loading bookings...
                  </p>
                </div>
              ) : bookings.length === 0 ? (
                <div className="flex flex-col items-center justify-center py-16">
                  <div className="w-20 h-20 rounded-full bg-slate-100 flex items-center justify-center mb-4">
                    <svg
                      className="w-10 h-10 text-slate-400"
                      fill="none"
                      stroke="currentColor"
                      viewBox="0 0 24 24"
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={2}
                        d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"
                      />
                    </svg>
                  </div>
                  <p className="text-slate-500 font-medium">
                    No bookings found
                  </p>
                </div>
              ) : (
                <div className="overflow-hidden rounded-xl border border-slate-200">
                  <div className="overflow-x-auto">
                    <table className="w-full">
                      <thead>
                        <tr className="bg-gradient-to-r from-slate-50 to-slate-100 border-b border-slate-200">
                          <th className="px-6 py-4 text-left text-xs font-bold text-slate-700 uppercase tracking-wider">
                            Booking Code
                          </th>
                          <th className="px-6 py-4 text-left text-xs font-bold text-slate-700 uppercase tracking-wider">
                            Start Time
                          </th>
                          <th className="px-6 py-4 text-left text-xs font-bold text-slate-700 uppercase tracking-wider">
                            Duration
                          </th>
                          <th className="px-6 py-4 text-left text-xs font-bold text-slate-700 uppercase tracking-wider">
                            Status
                          </th>
                          <th className="px-6 py-4 text-left text-xs font-bold text-slate-700 uppercase tracking-wider">
                            Actions
                          </th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-200">
                        {bookings.map((b) => (
                          <tr
                            key={b.id}
                            className="hover:bg-slate-50 transition-colors"
                          >
                            <td className="px-6 py-4 text-sm font-mono text-slate-700 bg-slate-50 rounded-lg">
                              {b.bookingCode}
                            </td>
                            <td className="px-6 py-4 text-sm text-slate-600">
                              {new Date(b.slotStartUtc).toLocaleString(
                                "en-US",
                                {
                                  month: "short",
                                  day: "numeric",
                                  year: "numeric",
                                  hour: "2-digit",
                                  minute: "2-digit",
                                }
                              )}
                            </td>
                            <td className="px-6 py-4 text-sm text-slate-700">
                              {b.slotMinutes} mins
                            </td>
                            <td className="px-6 py-4">
                              <span
                                className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-bold ${getStatusColor(
                                  b.status
                                )}`}
                              >
                                <span
                                  className={`w-1.5 h-1.5 rounded-full ${getStatusDot(
                                    b.status
                                  )}`}
                                ></span>
                                {b.status}
                              </span>
                            </td>
                            <td className="px-6 py-4">
                              {b.status === "Pending" ? (
                                <div className="flex gap-2">
                                  <button
                                    onClick={() => handleApprove(b.id)}
                                    className="px-3 py-1.5 bg-gradient-to-r from-emerald-500 to-emerald-600 text-white rounded-lg text-xs font-semibold shadow-md shadow-emerald-500/30 hover:shadow-lg hover:shadow-emerald-500/40 hover:scale-105 transition-all duration-200"
                                  >
                                    Approve
                                  </button>
                                  <button
                                    onClick={() => handleReject(b.id)}
                                    className="px-3 py-1.5 bg-gradient-to-r from-rose-500 to-rose-600 text-white rounded-lg text-xs font-semibold shadow-md shadow-rose-500/30 hover:shadow-lg hover:shadow-rose-500/40 hover:scale-105 transition-all duration-200"
                                  >
                                    Reject
                                  </button>
                                  <button
                                    onClick={() => openEditModal(b)}
                                    className="px-3 py-1.5 bg-gradient-to-r from-blue-500 to-blue-600 text-white rounded-lg text-xs font-semibold shadow-md shadow-blue-500/30 hover:shadow-lg hover:shadow-blue-500/40 hover:scale-105 transition-all duration-200"
                                  >
                                    Edit
                                  </button>
                                </div>
                              ) : b.status === "Approved" ? (
                                <div className="flex gap-2">
                                  <button
                                    onClick={() => handleCancel(b.id)}
                                    className="px-3 py-1.5 bg-gradient-to-r from-amber-500 to-amber-600 text-white rounded-lg text-xs font-semibold shadow-md shadow-amber-500/30 hover:shadow-lg hover:shadow-amber-500/40 hover:scale-105 transition-all duration-200"
                                  >
                                    Cancel
                                  </button>
                                  <button
                                    onClick={() => openEditModal(b)}
                                    className="px-3 py-1.5 bg-gradient-to-r from-blue-500 to-blue-600 text-white rounded-lg text-xs font-semibold shadow-md shadow-blue-500/30 hover:shadow-lg hover:shadow-blue-500/40 hover:scale-105 transition-all duration-200"
                                  >
                                    Edit
                                  </button>
                                </div>
                              ) : (
                                <span className="text-slate-400 text-sm">
                                  —
                                </span>
                              )}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Edit Modal */}
      {editModal.isOpen && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl shadow-2xl max-w-md w-full p-6">
            <div className="flex items-center justify-between mb-6">
              <h3 className="text-2xl font-bold text-slate-800">Edit Booking</h3>
              <button
                onClick={closeEditModal}
                className="text-slate-400 hover:text-slate-600 transition-colors"
              >
                <svg
                  className="w-6 h-6"
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M6 18L18 6M6 6l12 12"
                  />
                </svg>
              </button>
            </div>

            <form onSubmit={handleEditSubmit} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-2">
                  Date
                </label>
                <input
                  type="date"
                  name="localDate"
                  value={editModal.localDate}
                  onChange={handleEditChange}
                  className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                  required
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-slate-700 mb-2">
                  Start Time
                </label>
                <input
                  type="time"
                  name="startTime"
                  value={editModal.startTime}
                  onChange={handleEditChange}
                  className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                  required
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-slate-700 mb-2">
                  Duration (minutes)
                </label>
                <input
                  type="number"
                  name="minutes"
                  value={editModal.minutes}
                  onChange={handleEditChange}
                  className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                  required
                />
              </div>

              <div className="flex gap-3 pt-2">
                <button
                  type="button"
                  onClick={closeEditModal}
                  className="flex-1 px-4 py-3 bg-slate-100 text-slate-700 rounded-xl font-semibold hover:bg-slate-200 transition-all"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="flex-1 px-4 py-3 bg-gradient-to-r from-blue-600 to-blue-700 text-white rounded-xl font-semibold shadow-lg shadow-blue-500/30 hover:shadow-xl hover:shadow-blue-500/40 hover:scale-105 transition-all duration-200"
                >
                  Update Booking
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}