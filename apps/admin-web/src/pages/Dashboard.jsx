import { useEffect, useState } from "react";
import api from "../services/api";

export default function Dashboard() {
  const [stats, setStats] = useState({ pending: 0, approvedFuture: 0, stations: 0 });
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    (async () => {
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
        setLoading(false);
      }
    })();
  }, []);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold text-gray-900">Dashboard</h1>
          <p className="text-gray-500 mt-1">Overview of your charging station management</p>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <StatCard
          title="Pending Reservations"
          value={stats.pending}
          icon="⏳"
          gradient="from-amber-500 to-orange-600"
          loading={loading}
        />
        <StatCard
          title="Approved (Future)"
          value={stats.approvedFuture}
          icon="✓"
          gradient="from-emerald-500 to-teal-600"
          loading={loading}
        />
        <StatCard
          title="Active Stations"
          value={stats.stations}
          icon="⚡"
          gradient="from-blue-500 to-indigo-600"
          loading={loading}
        />
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