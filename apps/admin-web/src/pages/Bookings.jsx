import { useEffect, useState } from "react";
import api from "../services/api";

export default function Bookings() {
  const [items, setItems] = useState([]);

  const load = async () => {
    const { data } = await api.get("/api/booking");
    setItems(data);
  };
  useEffect(() => { load(); }, []);

  const approve = async (id) => { await api.put(`/api/booking/${id}/approve`); await load(); };
  const cancel  = async (id) => { await api.put(`/api/booking/${id}/cancel`); await load(); };

  return (
    <div className="space-y-6">
      <h2 className="text-lg font-semibold">Bookings</h2>
      <div className="grid gap-3">
        {items.map(b => (
          <div key={b.id} className="bg-white border rounded p-3">
            <div className="font-medium">{b.ownerNIC} → {b.stationId}</div>
            <div className="text-sm text-gray-600">
              {new Date(b.reservationDateTime).toLocaleString()} • {b.status}
            </div>
            <div className="mt-2 flex gap-2">
              {b.status === "Pending" && (
                <>
                  <button className="text-sm border px-3 py-1 rounded" onClick={()=>approve(b.id)}>Approve</button>
                  <button className="text-sm border px-3 py-1 rounded" onClick={()=>cancel(b.id)}>Cancel</button>
                </>
              )}
              {b.qrCode && <span className="text-xs text-gray-500">QR: {b.qrCode}</span>}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
