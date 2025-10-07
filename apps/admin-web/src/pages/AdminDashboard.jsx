import { useState, useEffect } from "react";
import { useAuth } from "../context/AuthContext";
import api from "../services/api";

export default function AdminDashboard() {
  const { token, username, logout } = useAuth();
  const [formData, setFormData] = useState({
    fullName: "",
    email: "",
    phone: "",
    password: "",
  });
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(false);

  const [backOffices, setBackOffices] = useState([]);
  const [statusFilter, setStatusFilter] = useState("");
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [loadingBackOffices, setLoadingBackOffices] = useState(false);

  const [noteModal, setNoteModal] = useState({
    show: false,
    nic: "",
    action: "",
    note: "",
  });

  // -------------------- CREATE ADMIN --------------------
  const handleChange = (e) => {
    setFormData({
      ...formData,
      [e.target.name]: e.target.value,
    });
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    setMessage("");

    try {
      const { data } = await api.post("/api/Admin/admins", formData, {
        headers: { Authorization: `Bearer ${token}` },
      });

      setMessage(`✅ Admin created successfully: ${data.fullName}`);
      setFormData({ fullName: "", email: "", phone: "", password: "" });
    } catch (error) {
      if (error.response?.status === 409) {
        setMessage("⚠️ An admin with this email or phone already exists.");
      } else if (error.response?.status === 400) {
        setMessage("❌ Invalid data. Please check your inputs.");
      } else {
        setMessage("❌ Something went wrong. Please try again.");
      }
    } finally {
      setLoading(false);
    }
  };

  // -------------------- FETCH BACKOFFICES --------------------
  const fetchBackOffices = async () => {
    setLoadingBackOffices(true);
    try {
      const { data } = await api.get("/api/Admin/backoffices", {
        headers: { Authorization: `Bearer ${token}` },
        params: { status: statusFilter || null, page, pageSize: 10 },
      });

      setBackOffices(data.items || []);
      setTotal(data.total || 0);
    } catch (error) {
      console.error(error);
      setMessage("❌ Failed to fetch BackOffice applications.");
    } finally {
      setLoadingBackOffices(false);
    }
  };

  useEffect(() => {
    fetchBackOffices();
  }, [statusFilter, page]);

  // -------------------- APPROVE / REJECT --------------------
  const handleDecision = async (nic, action, notes = "") => {
    const endpoint = `/api/Admin/backoffices/${nic}/${action}`;
    try {
      await api.put(
        endpoint,
        { notes },
        {
          headers: { Authorization: `Bearer ${token}` },
        }
      );
      setMessage(`✅ Application ${action}d successfully.`);
      fetchBackOffices();
    } catch (error) {
      if (error.response?.status === 404) {
        setMessage("❌ Application not found.");
      } else if (error.response?.status === 400) {
        setMessage("⚠️ Invalid notes or request data.");
      } else {
        setMessage("❌ Failed to update application.");
      }
    } finally {
      setNoteModal({ show: false, nic: "", action: "", note: "" });
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 via-blue-50 to-slate-100">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {/* Header */}
        <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6 mb-6">
          <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
            <div>
              <h1 className="text-3xl font-bold bg-gradient-to-r from-slate-800 to-slate-600 bg-clip-text text-transparent">
                Admin Dashboard
              </h1>
              <p className="text-slate-500 mt-1 flex items-center gap-2">
                <span className="w-2 h-2 bg-emerald-500 rounded-full animate-pulse"></span>
                Logged in as <span className="font-semibold text-slate-700">{username}</span>
              </p>
            </div>
            <button
              onClick={logout}
              className="px-5 py-2.5 bg-gradient-to-r from-red-500 to-red-600 text-white rounded-xl font-medium shadow-lg shadow-red-500/30 hover:shadow-xl hover:shadow-red-500/40 hover:scale-105 transition-all duration-200"
            >
              Logout
            </button>
          </div>
        </div>

        {/* Message Banner */}
        {message && (
          <div className="mb-6 p-4 bg-white border-l-4 border-blue-500 rounded-xl shadow-sm animate-in slide-in-from-top">
            <p className="text-sm font-medium text-slate-700">{message}</p>
          </div>
        )}

        <div className="grid grid-cols-1 xl:grid-cols-3 gap-6">
          {/* --- Section 1: Create Admin Form --- */}
          <div className="xl:col-span-1">
            <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6 sticky top-8">
              <div className="flex items-center gap-3 mb-6">
                <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-blue-500 to-blue-600 flex items-center justify-center shadow-lg shadow-blue-500/30">
                  <svg className="w-5 h-5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                  </svg>
                </div>
                <h2 className="text-xl font-bold text-slate-800">Create Admin</h2>
              </div>

              <form onSubmit={handleSubmit} className="space-y-4">
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-2">Full Name</label>
                  <input
                    type="text"
                    name="fullName"
                    placeholder="John Doe"
                    value={formData.fullName}
                    onChange={handleChange}
                    required
                    className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                  />
                </div>

                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-2">Email Address</label>
                  <input
                    type="email"
                    name="email"
                    placeholder="john@example.com"
                    value={formData.email}
                    onChange={handleChange}
                    required
                    className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                  />
                </div>

                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-2">Phone Number</label>
                  <input
                    type="text"
                    name="phone"
                    placeholder="+1 (555) 000-0000"
                    value={formData.phone}
                    onChange={handleChange}
                    required
                    className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                  />
                </div>

                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-2">Password</label>
                  <input
                    type="password"
                    name="password"
                    placeholder="••••••••"
                    value={formData.password}
                    onChange={handleChange}
                    required
                    className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                  />
                </div>

                <button
                  type="submit"
                  disabled={loading}
                  className="w-full px-4 py-3 bg-gradient-to-r from-blue-600 to-blue-700 text-white rounded-xl font-semibold shadow-lg shadow-blue-500/30 hover:shadow-xl hover:shadow-blue-500/40 hover:scale-105 disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:scale-100 transition-all duration-200"
                >
                  {loading ? (
                    <span className="flex items-center justify-center gap-2">
                      <svg className="animate-spin h-5 w-5" fill="none" viewBox="0 0 24 24">
                        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                      </svg>
                      Creating...
                    </span>
                  ) : (
                    "Create Admin"
                  )}
                </button>
              </form>
            </div>
          </div>

          {/* --- Section 2: BackOffice Applications --- */}
          <div className="xl:col-span-2">
            <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6">
              <div className="flex items-center gap-3 mb-6">
                <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-500 to-emerald-600 flex items-center justify-center shadow-lg shadow-emerald-500/30">
                  <svg className="w-5 h-5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                  </svg>
                </div>
                <h2 className="text-xl font-bold text-slate-800">BackOffice Applications</h2>
              </div>

              <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 mb-6">
                <div className="flex items-center gap-3">
                  <label className="text-sm font-medium text-slate-700">Status:</label>
                  <select
                    value={statusFilter}
                    onChange={(e) => setStatusFilter(e.target.value)}
                    className="px-4 py-2 bg-slate-50 border border-slate-200 rounded-xl text-sm font-medium focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                  >
                    <option value="">All Applications</option>
                    <option value="Pending">Pending</option>
                    <option value="Approved">Approved</option>
                    <option value="Rejected">Rejected</option>
                  </select>
                </div>

                <button
                  onClick={fetchBackOffices}
                  className="px-5 py-2.5 bg-gradient-to-r from-emerald-500 to-emerald-600 text-white rounded-xl font-medium shadow-lg shadow-emerald-500/30 hover:shadow-xl hover:shadow-emerald-500/40 hover:scale-105 transition-all duration-200 flex items-center gap-2"
                >
                  <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                  </svg>
                  Refresh
                </button>
              </div>

              {loadingBackOffices ? (
                <div className="flex flex-col items-center justify-center py-16">
                  <svg className="animate-spin h-10 w-10 text-blue-600 mb-4" fill="none" viewBox="0 0 24 24">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                  </svg>
                  <p className="text-slate-500 font-medium">Loading applications...</p>
                </div>
              ) : backOffices.length === 0 ? (
                <div className="flex flex-col items-center justify-center py-16">
                  <div className="w-20 h-20 rounded-full bg-slate-100 flex items-center justify-center mb-4">
                    <svg className="w-10 h-10 text-slate-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                    </svg>
                  </div>
                  <p className="text-slate-500 font-medium">No applications found</p>
                </div>
              ) : (
                <div className="overflow-hidden rounded-xl border border-slate-200">
                  <div className="overflow-x-auto">
                    <table className="w-full">
                      <thead>
                        <tr className="bg-gradient-to-r from-slate-50 to-slate-100 border-b border-slate-200">
                          <th className="px-6 py-4 text-left text-xs font-bold text-slate-700 uppercase tracking-wider">#</th>
                          <th className="px-6 py-4 text-left text-xs font-bold text-slate-700 uppercase tracking-wider">NIC</th>
                          <th className="px-6 py-4 text-left text-xs font-bold text-slate-700 uppercase tracking-wider">Applicant</th>
                          <th className="px-6 py-4 text-left text-xs font-bold text-slate-700 uppercase tracking-wider">Status</th>
                          <th className="px-6 py-4 text-left text-xs font-bold text-slate-700 uppercase tracking-wider">Created</th>
                          <th className="px-6 py-4 text-left text-xs font-bold text-slate-700 uppercase tracking-wider">Actions</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-200">
                        {backOffices.map((item, index) => (
                          <tr key={index} className="hover:bg-slate-50 transition-colors">
                            <td className="px-6 py-4 text-sm font-medium text-slate-900">{(page - 1) * 10 + index + 1}</td>
                            <td className="px-6 py-4 text-sm font-mono text-slate-700 bg-slate-50 rounded-lg">{item.nic}</td>
                            <td className="px-6 py-4 text-sm font-medium text-slate-900">{item.fullName || "N/A"}</td>
                            <td className="px-6 py-4">
                              <span
                                className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-bold ${
                                  item.status === "Approved"
                                    ? "bg-emerald-100 text-emerald-700"
                                    : item.status === "Pending"
                                    ? "bg-amber-100 text-amber-700"
                                    : "bg-rose-100 text-rose-700"
                                }`}
                              >
                                <span className={`w-1.5 h-1.5 rounded-full ${
                                  item.status === "Approved"
                                    ? "bg-emerald-500"
                                    : item.status === "Pending"
                                    ? "bg-amber-500"
                                    : "bg-rose-500"
                                }`}></span>
                                {item.status}
                              </span>
                            </td>
                            <td className="px-6 py-4 text-sm text-slate-600">
                              {new Date(item.createdAtUtc).toLocaleDateString('en-US', { 
                                month: 'short', 
                                day: 'numeric', 
                                year: 'numeric',
                                hour: '2-digit',
                                minute: '2-digit'
                              })}
                            </td>
                            <td className="px-6 py-4">
                              {item.status === "Pending" && (
                                <div className="flex gap-2">
                                  <button
                                    onClick={() =>
                                      setNoteModal({
                                        show: true,
                                        nic: item.nic,
                                        action: "approve",
                                        note: "",
                                      })
                                    }
                                    className="px-4 py-2 bg-gradient-to-r from-blue-500 to-blue-600 text-white rounded-lg text-xs font-semibold shadow-md shadow-blue-500/30 hover:shadow-lg hover:shadow-blue-500/40 hover:scale-105 transition-all duration-200"
                                  >
                                    Approve
                                  </button>
                                  <button
                                    onClick={() =>
                                      setNoteModal({
                                        show: true,
                                        nic: item.nic,
                                        action: "reject",
                                        note: "",
                                      })
                                    }
                                    className="px-4 py-2 bg-gradient-to-r from-rose-500 to-rose-600 text-white rounded-lg text-xs font-semibold shadow-md shadow-rose-500/30 hover:shadow-lg hover:shadow-rose-500/40 hover:scale-105 transition-all duration-200"
                                  >
                                    Reject
                                  </button>
                                </div>
                              )}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}

              {/* Pagination */}
              <div className="flex flex-col sm:flex-row justify-between items-center gap-4 mt-6 pt-6 border-t border-slate-200">
                <p className="text-sm text-slate-600 font-medium">
                  Showing <span className="font-bold text-slate-900">{backOffices.length}</span> of <span className="font-bold text-slate-900">{total}</span> applications
                </p>
                <div className="flex items-center gap-2">
                  <button
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    disabled={page === 1}
                    className="px-4 py-2 bg-slate-100 text-slate-700 rounded-lg font-medium hover:bg-slate-200 disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:bg-slate-100 transition-all"
                  >
                    Previous
                  </button>
                  <span className="px-4 py-2 bg-gradient-to-r from-blue-600 to-blue-700 text-white rounded-lg font-bold shadow-lg shadow-blue-500/30">
                    {page}
                  </span>
                  <button
                    onClick={() => setPage((p) => p + 1)}
                    disabled={page * 10 >= total}
                    className="px-4 py-2 bg-slate-100 text-slate-700 rounded-lg font-medium hover:bg-slate-200 disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:bg-slate-100 transition-all"
                  >
                    Next
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* --- Notes Modal --- */}
      {noteModal.show && (
        <div className="fixed inset-0 flex items-center justify-center bg-black/60 backdrop-blur-sm z-50 p-4 animate-in fade-in">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md transform transition-all animate-in zoom-in-95">
            <div className={`p-6 rounded-t-2xl ${
              noteModal.action === "approve" 
                ? "bg-gradient-to-r from-blue-500 to-blue-600" 
                : "bg-gradient-to-r from-rose-500 to-rose-600"
            }`}>
              <h3 className="text-xl font-bold text-white">
                {noteModal.action === "approve" ? "Approve Application" : "Reject Application"}
              </h3>
              <p className="text-blue-100 text-sm mt-1">
                NIC: <span className="font-mono font-semibold">{noteModal.nic}</span>
              </p>
            </div>
            
            <div className="p-6">
              <label className="block text-sm font-medium text-slate-700 mb-2">
                Add notes (optional)
              </label>
              <textarea
                placeholder="Enter your comments here..."
                value={noteModal.note}
                onChange={(e) =>
                  setNoteModal({ ...noteModal, note: e.target.value })
                }
                className="w-full px-4 py-3 bg-slate-50 border border-slate-200 rounded-xl focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all resize-none"
                rows="4"
              />
            </div>

            <div className="flex gap-3 p-6 pt-0">
              <button
                onClick={() => setNoteModal({ show: false, nic: "", action: "", note: "" })}
                className="flex-1 px-4 py-3 bg-slate-100 text-slate-700 rounded-xl font-semibold hover:bg-slate-200 transition-all"
              >
                Cancel
              </button>
              <button
                onClick={() =>
                  handleDecision(noteModal.nic, noteModal.action, noteModal.note)
                }
                className={`flex-1 px-4 py-3 text-white rounded-xl font-semibold shadow-lg hover:shadow-xl hover:scale-105 transition-all duration-200 ${
                  noteModal.action === "approve"
                    ? "bg-gradient-to-r from-blue-600 to-blue-700 shadow-blue-500/30 hover:shadow-blue-500/40"
                    : "bg-gradient-to-r from-rose-600 to-rose-700 shadow-rose-500/30 hover:shadow-rose-500/40"
                }`}
              >
                Confirm {noteModal.action === "approve" ? "Approval" : "Rejection"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}