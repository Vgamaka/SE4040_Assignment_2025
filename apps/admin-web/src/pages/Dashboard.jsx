import { useEffect, useState } from "react";
import api from "../services/api";

export default function Dashboard() {
  const [stats, setStats] = useState({ pending: 0, approvedFuture: 0, stations: 0 });

  useEffect(() => {
    (async () => {
      const [b, s] = await Promise.all([
        api.get("/api/booking"),
        api.get("/api/station"),
      ]);
      const now = new Date();
      const pending = b.data.filter(x => x.status === "Pending").length;
      const approvedFuture = b.data.filter(x => x.status === "Approved" && new Date(x.reservationDateTime) > now).length;
      setStats({ pending, approvedFuture, stations: s.data.length });
    })();
  }, []);

  return (
    <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
      <Card title="Pending Reservations" value={stats.pending} />
      <Card title="Approved (Future)" value={stats.approvedFuture} />
      <Card title="Stations" value={stats.stations} />
    </div>
  );
}

function Card({ title, value }) {
  return (
    <div className="bg-white border rounded p-4">
      <div className="text-sm text-gray-600">{title}</div>
      <div className="text-3xl font-semibold">{value}</div>
    </div>
  );
}
