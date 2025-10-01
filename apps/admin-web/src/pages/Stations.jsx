import { useEffect, useState } from "react";
import api from "../services/api";

export default function Stations() {
  const [stations, setStations] = useState([]);
  const [form, setForm] = useState({ name:"", location:"", type:"AC", availableSlots:2 });
  const [loading, setLoading] = useState(false);

  const load = async () => {
    const { data } = await api.get("/api/station");
    setStations(data);
  };
  useEffect(() => { load(); }, []);

  const create = async (e) => {
    e.preventDefault();
    setLoading(true);
    try {
      await api.post("/api/station", { ...form, schedule: [] });
      setForm({ name:"", location:"", type:"AC", availableSlots:2 });
      await load();
    } finally {
      setLoading(false);
    }
  };

  const deactivate = async (id) => { 
    await api.put(`/api/station/${id}/deactivate`); 
    await load(); 
  };

  return (
    <div className="space-y-8">
      {/* Header */}
      <div>
        <h1 className="text-3xl font-bold text-gray-900">Charging Stations</h1>
        <p className="text-gray-500 mt-1">Manage your charging station network</p>
      </div>

      {/* Create Station Form */}
      <div className="bg-gradient-to-br from-blue-50 to-indigo-50 rounded-2xl p-6 border border-blue-100 shadow-sm">
        <h3 className="text-lg font-semibold text-gray-900 mb-4">Add New Station</h3>
        <form onSubmit={create} className="grid grid-cols-1 md:grid-cols-5 gap-4">
          <input 
            className="bg-white border border-gray-200 px-4 py-2.5 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all" 
            placeholder="Station Name"
            required
            value={form.name} 
            onChange={e=>setForm({...form, name:e.target.value})}
          />
          <input 
            className="bg-white border border-gray-200 px-4 py-2.5 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all" 
            placeholder="Location"
            required
            value={form.location} 
            onChange={e=>setForm({...form, location:e.target.value})}
          />
          <select 
            className="bg-white border border-gray-200 px-4 py-2.5 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
            value={form.type} 
            onChange={e=>setForm({...form, type:e.target.value})}
          >
            <option>AC</option>
            <option>DC</option>
          </select>
          <input 
            className="bg-white border border-gray-200 px-4 py-2.5 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all" 
            type="number" 
            min="1" 
            placeholder="Available Slots"
            required
            value={form.availableSlots} 
            onChange={e=>setForm({...form, availableSlots:+e.target.value})}
          />
          <button 
            disabled={loading}
            className="bg-gradient-to-r from-blue-600 to-indigo-600 text-white px-6 py-2.5 rounded-xl font-medium hover:from-blue-700 hover:to-indigo-700 transition-all shadow-md hover:shadow-lg disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {loading ? "Creating..." : "Create Station"}
          </button>
        </form>
      </div>

      {/* Stations List */}
      <div>
        <div className="flex items-center justify-between mb-4">
          <h3 className="text-xl font-semibold text-gray-900">All Stations</h3>
          <span className="text-sm text-gray-500 bg-gray-100 px-3 py-1 rounded-full">
            {stations.length} {stations.length === 1 ? 'station' : 'stations'}
          </span>
        </div>
        
        <div className="grid gap-4">
          {stations.map(s => (
            <div 
              key={s.id} 
              className="group bg-white rounded-2xl p-5 border border-gray-200 hover:border-blue-300 hover:shadow-lg transition-all duration-300"
            >
              <div className="flex items-center gap-4">
                {/* Icon */}
                <div className={`flex-shrink-0 w-14 h-14 rounded-xl flex items-center justify-center text-2xl shadow-sm ${
                  s.isActive 
                    ? 'bg-gradient-to-br from-emerald-500 to-teal-600' 
                    : 'bg-gradient-to-br from-gray-400 to-gray-500'
                }`}>
                  ⚡
                </div>

                {/* Content */}
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <h4 className="text-lg font-semibold text-gray-900 truncate">{s.name}</h4>
                    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${
                      s.isActive 
                        ? 'bg-emerald-100 text-emerald-700' 
                        : 'bg-gray-100 text-gray-600'
                    }`}>
                      {s.isActive ? '● Active' : '○ Inactive'}
                    </span>
                  </div>
                  
                  <div className="flex flex-wrap items-center gap-3 text-sm text-gray-600">
                    <span className="flex items-center gap-1">
                      <span className="text-gray-400">📍</span>
                      {s.location}
                    </span>
                    <span className={`px-2 py-0.5 rounded-md font-medium ${
                      s.type === 'DC' 
                        ? 'bg-purple-100 text-purple-700' 
                        : 'bg-blue-100 text-blue-700'
                    }`}>
                      {s.type}
                    </span>
                    <span className="flex items-center gap-1">
                      <span className="text-gray-400">🔌</span>
                      {s.availableSlots} {s.availableSlots === 1 ? 'slot' : 'slots'}
                    </span>
                  </div>
                </div>

                {/* Action Button */}
                {s.isActive && (
                  <button 
                    className="flex-shrink-0 px-4 py-2 text-sm font-medium text-red-600 bg-red-50 hover:bg-red-100 rounded-xl transition-colors border border-red-200"
                    onClick={()=>deactivate(s.id)}
                  >
                    Deactivate
                  </button>
                )}
              </div>
            </div>
          ))}

          {stations.length === 0 && (
            <div className="text-center py-12 bg-gray-50 rounded-2xl border-2 border-dashed border-gray-200">
              <div className="text-4xl mb-2">⚡</div>
              <p className="text-gray-600 font-medium">No stations yet</p>
              <p className="text-gray-400 text-sm mt-1">Create your first charging station above</p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}