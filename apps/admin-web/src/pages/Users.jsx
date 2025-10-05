import { useEffect, useState } from "react";
import api from "../services/api";

export default function EvOwners() {
  const [owners, setOwners] = useState([]);
  const [loading, setLoading] = useState(true);
  const [form, setForm] = useState({
    nic: "",
    name: "",
    email: "",
    phone: "",
    vehicleNumber: "",
    passwordHash: ""
  });
  const [editMode, setEditMode] = useState(null);
  const [editForm, setEditForm] = useState({});
  const [isSubmitting, setIsSubmitting] = useState(false);

  const loadOwners = async () => {
    setLoading(true);
    try {
      const { data } = await api.get("/api/evowner");
      setOwners(data);
    } catch (error) {
      console.error("Failed to fetch EV owners:", error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadOwners();
  }, []);

  const createOwner = async (e) => {
    e.preventDefault();
    if (!form.nic || !form.email || !form.passwordHash) return;
    
    setIsSubmitting(true);
    try {
      await api.post("/api/evowner", form);
      setForm({
        nic: "",
        name: "",
        email: "",
        phone: "",
        vehicleNumber: "",
        passwordHash: ""
      });
      await loadOwners();
    } catch (error) {
      console.error("Failed to create EV owner:", error);
      alert(error.response?.data?.message || "Failed to create EV owner");
    } finally {
      setIsSubmitting(false);
    }
  };

  const startEdit = (owner) => {
    setEditMode(owner.nic);
    setEditForm({
      name: owner.name || "",
      email: owner.email || "",
      phone: owner.phone || "",
      vehicleNumber: owner.vehicleNumber || "",
      passwordHash: ""
    });
  };

  const cancelEdit = () => {
    setEditMode(null);
    setEditForm({});
  };

  const saveEdit = async (nic) => {
    try {
      await api.put(`/api/evowner/${nic}`, editForm);
      setEditMode(null);
      setEditForm({});
      await loadOwners();
    } catch (error) {
      console.error("Failed to update EV owner:", error);
      alert(error.response?.data?.message || "Failed to update EV owner");
    }
  };

  const toggleStatus = async (nic, isActive) => {
    try {
      if (isActive) {
        await api.put(`/api/evowner/${nic}/deactivate`);
      } else {
        await api.put(`/api/evowner/${nic}/reactivate`);
      }
      await loadOwners();
    } catch (error) {
      console.error("Failed to toggle status:", error);
      alert(error.response?.data?.message || "Failed to toggle status");
    }
  };

  const deleteOwner = async (nic) => {
    if (!confirm("Are you sure you want to delete this EV owner?")) return;
    try {
      await api.delete(`/api/evowner/${nic}`);
      await loadOwners();
    } catch (error) {
      console.error("Failed to delete EV owner:", error);
      alert(error.response?.data?.message || "Failed to delete EV owner");
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-gray-50 to-gray-100 p-6">
      <div className="max-w-7xl mx-auto space-y-8">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-4xl font-bold text-gray-900">EV Owners</h1>
            <p className="text-gray-600 mt-2">Manage electric vehicle owners and their accounts</p>
          </div>
        </div>

        <div className="bg-white rounded-2xl shadow-xl border border-gray-200 overflow-hidden">
          <div className="bg-gradient-to-r from-blue-900 to-blue-800 px-8 py-6">
            <h2 className="text-2xl font-bold text-white flex items-center gap-3">
              <span className="text-3xl">🚗</span>
              Create New EV Owner
            </h2>
            <p className="text-blue-100 mt-1">Register a new electric vehicle owner</p>
          </div>

          <div className="p-8 bg-gradient-to-br from-gray-50 to-white">
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              <input
                className="border border-gray-300 px-4 py-3 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                placeholder="NIC Number *"
                value={form.nic}
                onChange={e => setForm({...form, nic: e.target.value})}
              />
              <input
                className="border border-gray-300 px-4 py-3 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                placeholder="Full Name"
                value={form.name}
                onChange={e => setForm({...form, name: e.target.value})}
              />
              <input
                className="border border-gray-300 px-4 py-3 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                placeholder="Email *"
                type="email"
                value={form.email}
                onChange={e => setForm({...form, email: e.target.value})}
              />
              <input
                className="border border-gray-300 px-4 py-3 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                placeholder="Phone Number"
                value={form.phone}
                onChange={e => setForm({...form, phone: e.target.value})}
              />
              <input
                className="border border-gray-300 px-4 py-3 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                placeholder="Vehicle Number"
                value={form.vehicleNumber}
                onChange={e => setForm({...form, vehicleNumber: e.target.value})}
              />
              <input
                className="border border-gray-300 px-4 py-3 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all"
                placeholder="Password *"
                type="password"
                value={form.passwordHash}
                onChange={e => setForm({...form, passwordHash: e.target.value})}
              />
            </div>
            <button
              onClick={createOwner}
              disabled={isSubmitting}
              className="mt-4 bg-gradient-to-r from-blue-600 to-blue-700 hover:from-blue-700 hover:to-blue-800 text-white px-8 py-3 rounded-lg font-semibold shadow-lg hover:shadow-xl transition-all duration-200 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {isSubmitting ? "Creating..." : "Create EV Owner"}
            </button>
          </div>
        </div>

        <div className="bg-white rounded-2xl shadow-xl border border-gray-200 overflow-hidden">
          <div className="bg-gradient-to-r from-gray-900 to-gray-800 px-8 py-6">
            <h2 className="text-2xl font-bold text-white flex items-center gap-3">
              <span className="text-3xl">📋</span>
              Registered EV Owners ({owners.length})
            </h2>
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
            ) : owners.length === 0 ? (
              <div className="text-center py-12 bg-gray-50 rounded-xl border-2 border-dashed border-gray-300">
                <div className="text-6xl mb-4">🚗</div>
                <p className="text-gray-600 font-medium">No EV owners found</p>
                <p className="text-gray-500 text-sm mt-1">Create your first EV owner using the form above</p>
              </div>
            ) : (
              <div className="grid gap-4">
                {owners.map(owner => (
                  <div
                    key={owner.nic}
                    className={`group bg-gradient-to-br ${
                      owner.isActive ? "from-white to-gray-50" : "from-gray-100 to-gray-200"
                    } border ${
                      owner.isActive ? "border-gray-200" : "border-gray-300"
                    } rounded-xl p-6 hover:shadow-lg transition-all duration-200`}
                  >
                    {editMode === owner.nic ? (
                      <div className="space-y-4">
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                          <input
                            className="border border-gray-300 px-4 py-2 rounded-lg focus:ring-2 focus:ring-blue-500 outline-none"
                            placeholder="Full Name"
                            value={editForm.name}
                            onChange={e => setEditForm({...editForm, name: e.target.value})}
                          />
                          <input
                            className="border border-gray-300 px-4 py-2 rounded-lg focus:ring-2 focus:ring-blue-500 outline-none"
                            placeholder="Email"
                            type="email"
                            value={editForm.email}
                            onChange={e => setEditForm({...editForm, email: e.target.value})}
                          />
                          <input
                            className="border border-gray-300 px-4 py-2 rounded-lg focus:ring-2 focus:ring-blue-500 outline-none"
                            placeholder="Phone"
                            value={editForm.phone}
                            onChange={e => setEditForm({...editForm, phone: e.target.value})}
                          />
                          <input
                            className="border border-gray-300 px-4 py-2 rounded-lg focus:ring-2 focus:ring-blue-500 outline-none"
                            placeholder="Vehicle Number"
                            value={editForm.vehicleNumber}
                            onChange={e => setEditForm({...editForm, vehicleNumber: e.target.value})}
                          />
                          <input
                            className="border border-gray-300 px-4 py-2 rounded-lg focus:ring-2 focus:ring-blue-500 outline-none md:col-span-2"
                            placeholder="New Password (leave blank to keep current)"
                            type="password"
                            value={editForm.passwordHash}
                            onChange={e => setEditForm({...editForm, passwordHash: e.target.value})}
                          />
                        </div>
                        <div className="flex gap-2">
                          <button
                            onClick={() => saveEdit(owner.nic)}
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
                        <div className="flex-shrink-0 w-14 h-14 bg-gradient-to-br from-blue-500 to-indigo-600 rounded-full flex items-center justify-center text-white font-bold text-xl shadow-md">
                          {owner.name ? owner.name.charAt(0).toUpperCase() : owner.nic.charAt(0)}
                        </div>
                        
                        <div className="flex-1 min-w-0">
                          <div className="flex items-center gap-3 mb-2">
                            <h4 className="font-bold text-gray-900 text-lg">
                              {owner.name || "No Name"}
                            </h4>
                            <span className={`px-3 py-1 rounded-full text-xs font-semibold ${
                              owner.isActive
                                ? "bg-green-100 text-green-700"
                                : "bg-red-100 text-red-700"
                            }`}>
                              {owner.isActive ? "Active" : "Inactive"}
                            </span>
                          </div>
                          
                          <div className="grid grid-cols-1 md:grid-cols-2 gap-2 text-sm text-gray-600">
                            <div className="flex items-center gap-2">
                              <span className="font-semibold">🆔 NIC:</span>
                              <span>{owner.nic}</span>
                            </div>
                            <div className="flex items-center gap-2">
                              <span className="font-semibold">✉️ Email:</span>
                              <span className="truncate">{owner.email}</span>
                            </div>
                            {owner.phone && (
                              <div className="flex items-center gap-2">
                                <span className="font-semibold">📞 Phone:</span>
                                <span>{owner.phone}</span>
                              </div>
                            )}
                            {owner.vehicleNumber && (
                              <div className="flex items-center gap-2">
                                <span className="font-semibold">🚙 Vehicle:</span>
                                <span>{owner.vehicleNumber}</span>
                              </div>
                            )}
                          </div>
                        </div>
                        
                        <div className="flex-shrink-0 flex gap-2 opacity-0 group-hover:opacity-100 transition-opacity">
                          <button
                            onClick={() => startEdit(owner)}
                            className="px-4 py-2 bg-blue-50 border border-blue-200 text-blue-700 rounded-lg hover:bg-blue-100 transition-colors font-medium text-sm"
                          >
                            Edit
                          </button>
                          <button
                            onClick={() => toggleStatus(owner.nic, owner.isActive)}
                            className={`px-4 py-2 border rounded-lg transition-colors font-medium text-sm ${
                              owner.isActive
                                ? "bg-orange-50 border-orange-200 text-orange-700 hover:bg-orange-100"
                                : "bg-green-50 border-green-200 text-green-700 hover:bg-green-100"
                            }`}
                          >
                            {owner.isActive ? "Deactivate" : "Activate"}
                          </button>
                          <button
                            onClick={() => deleteOwner(owner.nic)}
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
    </div>
  );
}