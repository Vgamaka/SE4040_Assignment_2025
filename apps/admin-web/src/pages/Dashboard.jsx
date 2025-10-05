import { useEffect, useState } from "react";
import api from "../services/api";

export default function Dashboard() {
  const [stats, setStats] = useState({ pending: 0, approvedFuture: 0, stations: 0 });
  const [statsLoading, setStatsLoading] = useState(true);
  const [users, setUsers] = useState([]);
  const [usersLoading, setUsersLoading] = useState(true);
  const [form, setForm] = useState({ username: "", email: "", passwordHash: "", role: "StationOperator" });
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    loadStats();
    loadUsers();
  }, []);

  const loadStats = async () => {
    try {
      const [b, s] = await Promise.all([
        api.get("/api/booking"),
        api.get("/api/station"),
      ]);
      const now = new Date();
      const pending = b.data.filter(x => x.status === "Pending").length;
      const approvedFuture = b.data.filter(x => x.status === "Approved" && new Date(x.reservationDateTime) > now).length;
      setStats({ pending, approvedFuture, stations: s.data.length });
    } catch (error) {
      console.error("Failed to fetch dashboard data:", error);
    } finally {
      setStatsLoading(false);
    }
  };

  const loadUsers = async () => {
    try {
      const { data } = await api.get("/api/user");
      setUsers(data.map(u => ({...u, passwordHash: undefined})));
    } catch (error) {
      console.error("Failed to fetch users:", error);
    } finally {
      setUsersLoading(false);
    }
  };

  const createUser = async (e) => {
    e.preventDefault();
    if (!form.username || !form.email || !form.passwordHash) return;
    
    setIsSubmitting(true);
    try {
      await api.post("/api/user", form);
      setForm({ username: "", email: "", passwordHash: "", role: "StationOperator" });
      await loadUsers();
    } catch (error) {
      console.error("Failed to create user:", error);
    } finally {
      setIsSubmitting(false);
    }
  };

  const removeUser = async (id) => {
    if (!confirm("Are you sure you want to delete this user?")) return;
    try {
      await api.delete(`/api/user/${id}`);
      await loadUsers();
    } catch (error) {
      console.error("Failed to delete user:", error);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-gray-50 to-gray-100 p-6">
      <div className="max-w-7xl mx-auto space-y-8">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-4xl font-bold text-gray-900">Dashboard</h1>
            <p className="text-gray-600 mt-2">Overview of your charging station management</p>
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          <StatCard
            title="Pending Reservations"
            value={stats.pending}
            icon="⏳"
            gradient="from-amber-500 to-orange-600"
            loading={statsLoading}
          />
          <StatCard
            title="Approved (Future)"
            value={stats.approvedFuture}
            icon="✓"
            gradient="from-emerald-500 to-teal-600"
            loading={statsLoading}
          />
          <StatCard
            title="Active Stations"
            value={stats.stations}
            icon="⚡"
            gradient="from-blue-500 to-indigo-600"
            loading={statsLoading}
          />
        </div>

        <div className="bg-white rounded-2xl shadow-xl border border-gray-200 overflow-hidden">
          <div className="bg-gradient-to-r from-gray-900 to-gray-800 px-8 py-6">
            <h2 className="text-2xl font-bold text-white flex items-center gap-3">
              <span className="text-3xl">👥</span>
              User Management
            </h2>
            <p className="text-gray-300 mt-1">Create and manage system users</p>
          </div>

          <div className="p-8 bg-gradient-to-br from-gray-50 to-white border-b border-gray-200">
            <h3 className="text-lg font-semibold text-gray-900 mb-4">Create New User</h3>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4">
              <input
                className="border border-gray-300 px-4 py-3 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                placeholder="Username"
                value={form.username}
                onChange={e => setForm({...form, username: e.target.value})}
              />
              <input
                className="border border-gray-300 px-4 py-3 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                placeholder="Email"
                type="email"
                value={form.email}
                onChange={e => setForm({...form, email: e.target.value})}
              />
              <input
                className="border border-gray-300 px-4 py-3 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                placeholder="Password"
                type="password"
                value={form.passwordHash}
                onChange={e => setForm({...form, passwordHash: e.target.value})}
              />
              <select
                className="border border-gray-300 px-4 py-3 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all bg-white"
                value={form.role}
                onChange={e => setForm({...form, role: e.target.value})}
              >
                <option value="Backoffice">Backoffice</option>
                <option value="StationOperator">Station Operator</option>
              </select>
              <button
                onClick={createUser}
                disabled={isSubmitting}
                className="bg-gradient-to-r from-blue-600 to-blue-700 hover:from-blue-700 hover:to-blue-800 text-white px-6 py-3 rounded-lg font-semibold shadow-lg hover:shadow-xl transition-all duration-200 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isSubmitting ? "Creating..." : "Create User"}
              </button>
            </div>
          </div>

          <div className="p-8">
            <div className="flex items-center justify-between mb-6">
              <h3 className="text-lg font-semibold text-gray-900">
                Existing Users ({users.length})
              </h3>
            </div>
            
            {usersLoading ? (
              <div className="space-y-4">
                {[1, 2, 3].map(i => (
                  <div key={i} className="bg-gray-100 rounded-xl p-6 animate-pulse">
                    <div className="h-6 bg-gray-200 rounded w-1/4 mb-3"></div>
                    <div className="h-4 bg-gray-200 rounded w-1/2"></div>
                  </div>
                ))}
              </div>
            ) : users.length === 0 ? (
              <div className="text-center py-12 bg-gray-50 rounded-xl border-2 border-dashed border-gray-300">
                <div className="text-6xl mb-4">👤</div>
                <p className="text-gray-600 font-medium">No users found</p>
                <p className="text-gray-500 text-sm mt-1">Create your first user using the form above</p>
              </div>
            ) : (
              <div className="grid gap-4">
                {users.map(user => (
                  <div
                    key={user.id}
                    className="group bg-gradient-to-br from-white to-gray-50 border border-gray-200 rounded-xl p-6 hover:shadow-lg transition-all duration-200 hover:border-gray-300"
                  >
                    <div className="flex items-center gap-4">
                      <div className="flex-shrink-0 w-12 h-12 bg-gradient-to-br from-blue-500 to-indigo-600 rounded-full flex items-center justify-center text-white font-bold text-lg shadow-md">
                        {user.username.charAt(0).toUpperCase()}
                      </div>
                      
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-3">
                          <h4 className="font-semibold text-gray-900 text-lg truncate">
                            {user.username}
                          </h4>
                          <span className={`px-3 py-1 rounded-full text-xs font-semibold ${
                            user.role === "Backoffice"
                              ? "bg-purple-100 text-purple-700"
                              : "bg-blue-100 text-blue-700"
                          }`}>
                            {user.role === "StationOperator" ? "Station Operator" : user.role}
                          </span>
                        </div>
                        <p className="text-gray-600 text-sm mt-1 flex items-center gap-2">
                          <span>✉️</span>
                          <span className="truncate">{user.email}</span>
                        </p>
                      </div>
                      
                      <button
                        onClick={() => removeUser(user.id)}
                        className="flex-shrink-0 px-4 py-2 border border-red-200 text-red-600 rounded-lg hover:bg-red-50 hover:border-red-300 transition-all duration-200 font-medium text-sm opacity-0 group-hover:opacity-100"
                      >
                        Delete
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

function StatCard({ title, value, icon, gradient, loading }) {
  return (
    <div className="relative group">
      <div className={`absolute inset-0 bg-gradient-to-br ${gradient} rounded-2xl opacity-75 group-hover:opacity-100 transition-opacity duration-300`}></div>
      <div className="relative bg-white rounded-2xl p-6 shadow-lg hover:shadow-xl transition-shadow duration-300 border border-gray-100">
        <div className="flex items-start justify-between">
          <div className="flex-1">
            <p className="text-sm font-medium text-gray-600 uppercase tracking-wide">{title}</p>
            {loading ? (
              <div className="mt-3 h-10 w-24 bg-gray-200 rounded animate-pulse"></div>
            ) : (
              <p className={`mt-3 text-4xl font-bold bg-gradient-to-br ${gradient} bg-clip-text text-transparent`}>
                {value}
              </p>
            )}
          </div>
          <div className={`flex-shrink-0 w-12 h-12 rounded-xl bg-gradient-to-br ${gradient} flex items-center justify-center text-2xl shadow-md`}>
            {icon}
          </div>
        </div>
        <div className="mt-4 flex items-center text-sm">
          <div className={`h-1.5 flex-1 bg-gradient-to-r ${gradient} rounded-full opacity-20`}></div>
        </div>
      </div>
    </div>
  );
}