import { useEffect, useState } from "react";
import api from "../services/api";

export default function Stations() {
  const [stations, setStations] = useState([]);
  const [form, setForm] = useState({ name:"", location:"", type:"AC", availableSlots:2 });

  const load = async () => {
    const { data } = await api.get("/api/station");
    setStations(data);
  };
  useEffect(() => { load(); }, []);

  const create = async (e) => {
    e.preventDefault();
    await api.post("/api/station", { ...form, schedule: [] });
    setForm({ name:"", location:"", type:"AC", availableSlots:2 });
    await load();
  };

  const deactivate = async (id) => { await api.put(`/api/station/${id}/deactivate`); await load(); };

  return (
    <div className="space-y-6">
      <h2 className="text-lg font-semibold">Stations</h2>

      <form onSubmit={create} className="grid grid-cols-1 md:grid-cols-5 gap-3">
        <input className="border p-2 rounded" placeholder="Name"
          value={form.name} onChange={e=>setForm({...form, name:e.target.value})}/>
        <input className="border p-2 rounded" placeholder="Location"
          value={form.location} onChange={e=>setForm({...form, location:e.target.value})}/>
        <select className="border p-2 rounded"
          value={form.type} onChange={e=>setForm({...form, type:e.target.value})}>
          <option>AC</option><option>DC</option>
        </select>
        <input className="border p-2 rounded" type="number" min="1" placeholder="Slots"
          value={form.availableSlots} onChange={e=>setForm({...form, availableSlots:+e.target.value})}/>
        <button className="bg-black text-white px-3 py-2 rounded">Create</button>
      </form>

      <div className="grid gap-3">
        {stations.map(s => (
          <div key={s.id} className="bg-white border rounded p-3 flex items-center gap-3">
            <div className="flex-1">
              <div className="font-medium">{s.name}</div>
              <div className="text-sm text-gray-600">{s.location} • {s.type} • slots: {s.availableSlots}</div>
              <div className="text-sm">{s.isActive ? "Active" : "Inactive"}</div>
            </div>
            {s.isActive && (
              <button className="text-sm border px-3 py-1 rounded" onClick={()=>deactivate(s.id)}>
                Deactivate
              </button>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
