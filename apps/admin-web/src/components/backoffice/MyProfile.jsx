import { useEffect, useState } from "react";
import { getBackOfficeMe } from "../../services/api";

function Field({ label, children }) {
  return (
    <div className="grid grid-cols-3 gap-3 py-2">
      <div className="text-gray-500 text-sm">{label}</div>
      <div className="col-span-2">{children}</div>
    </div>
  );
}

export default function MyProfile() {
  const [me, setMe] = useState(null);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState("");

  const load = async () => {
    setLoading(true);
    setErr("");
    try {
      const res = await getBackOfficeMe();
      setMe(res);
    } catch (e) {
      console.error(e);
      setErr(
        e?.response?.data?.message ||
          e?.response?.data?.error ||
          "Failed to load profile."
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const fmt = (d) =>
    d ? new Date(d).toLocaleString() : <span className="text-gray-400">—</span>;

  return (
    <div className="max-w-3xl mx-auto p-4 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">My Profile</h1>
          <p className="text-sm text-gray-600">
            View your BackOffice account details
          </p>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={load}
            className="text-sm px-3 py-1.5 border rounded hover:bg-gray-50"
          >
            Refresh
          </button>
          <a
            href="/logout"
            className="text-sm text-gray-700 hover:text-black"
            onClick={(e) => {
              e.preventDefault();
              localStorage.removeItem("token");
              localStorage.removeItem("user");
              window.location.href = "/login";
            }}
          >
            Logout
          </a>
        </div>
      </div>

      {err && (
        <div className="text-sm bg-yellow-50 border border-yellow-200 text-yellow-700 rounded p-2">
          {err}
        </div>
      )}

      <div className="bg-white border rounded-xl shadow">
        <div className="px-4 py-3 border-b flex items-center justify-between">
          <div>
            <div className="font-semibold">{me?.fullName || "—"}</div>
            <div className="text-xs text-gray-500">NIC: {me?.nic || "—"}</div>
          </div>
          <div className="flex flex-wrap gap-2">
            {(me?.roles || []).map((r) => (
              <span
                key={r}
                className="text-xs bg-blue-50 text-blue-700 border border-blue-200 px-2 py-0.5 rounded"
              >
                {r}
              </span>
            ))}
            {me?.isActive === false ? (
              <span className="text-xs bg-red-50 text-red-700 border border-red-200 px-2 py-0.5 rounded">
                Inactive
              </span>
            ) : (
              <span className="text-xs bg-emerald-50 text-emerald-700 border border-emerald-200 px-2 py-0.5 rounded">
                Active
              </span>
            )}
          </div>
        </div>

        {loading ? (
          <div className="p-4 text-sm text-gray-600">Loading…</div>
        ) : (
          <div className="p-4">
            <Field label="Email">{me?.email || "—"}</Field>
            <Field label="Phone">{me?.phone || "—"}</Field>
            <Field label="Address">
              {me?.address ? (
                <div className="text-sm">
                  {(me.address.line1 || "") + (me.address.line2 ? `, ${me.address.line2}` : "")}
                  {me.address.city ? `, ${me.address.city}` : ""}
                </div>
              ) : (
                "—"
              )}
            </Field>
            <div className="border-t my-2" />
            <Field label="Created At">{fmt(me?.createdAtUtc)}</Field>
            <Field label="Last Updated">{fmt(me?.updatedAtUtc)}</Field>
          </div>
        )}
      </div>
    </div>
  );
}
