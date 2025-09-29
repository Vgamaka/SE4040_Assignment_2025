import { useEffect, useState } from "react";
import api from "../services/api";

export default function Users() {
  const [users, setUsers] = useState([]);
  const [form, setForm] = useState({ username: "", email: "", passwordHash: "", role: "StationOperator" });

  const load = async () => {
    const { data } = await api.get("/api/user");
    // hide hashes if backend still returns them
    setUsers(data.map(u => ({...u, passwordHash: undefined})));
  };
  useEffect(() => { load(); }, []);

  const createUser = async (e) => {
    e.preventDefault();
    await api.post("/api/user", form);
    setForm({ username: "", email: "", passwordHash: "", role: "StationOperator" });
    await load();
  };

  const remove = async (id) => { await api.delete(`/api/user/${id}`); await load(); };

  return (
    <div className="space-y-6">
      <h2 className="text-lg font-semibold">Users</h2>

      <form onSubmit={createUser} className="grid grid-cols-1 md:grid-cols-5 gap-3">
        <input className="border p-2 rounded" placeholder="Username"
          value={form.username} onChange={e=>setForm({...form, username:e.target.value})} />
        <input className="border p-2 rounded" placeholder="Email"
          value={form.email} onChange={e=>setForm({...form, email:e.target.value})} />
        <input className="border p-2 rounded" placeholder="Password" type="password"
          value={form.passwordHash} onChange={e=>setForm({...form, passwordHash:e.target.value})} />
        <select className="border p-2 rounded"
          value={form.role} onChange={e=>setForm({...form, role:e.target.value})}>
          <option>Backoffice</option>
          <option>StationOperator</option>
        </select>
        <button className="bg-black text-white px-3 py-2 rounded">Create</button>
      </form>

      <div className="grid gap-3">
        {users.map(u => (
          <div key={u.id} className="bg-white border rounded p-3 flex items-center gap-3">
            <div className="flex-1">
              <div className="font-medium">{u.username}</div>
              <div className="text-sm text-gray-600">{u.email} • {u.role}</div>
            </div>
            <button className="text-sm border px-3 py-1 rounded" onClick={()=>remove(u.id)}>Delete</button>
          </div>
        ))}
      </div>
    </div>
  );
}
