import { useEffect, useState } from "react";
import { getBackOfficeMe } from "../../services/api";
import StationList from "../../components/backoffice/StationList";

export default function BackOfficeHome() {
  const [me, setMe] = useState(null);
  const [err, setErr] = useState("");

  useEffect(() => {
    let mounted = true;
    (async () => {
      try {
        const m = await getBackOfficeMe();
        if (mounted) setMe(m);
      } catch (e) {
        console.error(e);
        setErr(
          e?.response?.data?.message ||
            e?.response?.data?.error ||
            "Failed to load profile."
        );
      }
    })();
    return () => { mounted = false; };
  }, []);

  return (
    <div className="max-w-6xl mx-auto p-4 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">BackOffice</h1>
          <p className="text-sm text-gray-600">
            {me ? `Welcome, ${me.fullName}` : "Manage stations, operators and reports"}
          </p>
        </div>
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

      {/* Errors */}
      {err && (
        <div className="text-sm bg-yellow-50 border border-yellow-200 text-yellow-700 rounded p-2">
          {err}
        </div>
      )}

      {/* Quick Links */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
        <a
          href="/backoffice/stations/new"
          className="block bg-white border rounded-xl shadow p-4 hover:shadow-md transition"
        >
          <div className="font-semibold">Create Station</div>
          <div className="text-xs text-gray-500 mt-1">
            Add a new charging location
          </div>
        </a>

        <a
          href="/backoffice/operators"
          className="block bg-white border rounded-xl shadow p-4 hover:shadow-md transition"
        >
          <div className="font-semibold">Manage Operators</div>
          <div className="text-xs text-gray-500 mt-1">
            Add/assign operators to stations
          </div>
        </a>

        <a
          href="/backoffice/reports"
          className="block bg-white border rounded-xl shadow p-4 hover:shadow-md transition"
        >
          <div className="font-semibold">Reports & Summary</div>
          <div className="text-xs text-gray-500 mt-1">
            KPIs, time-series, utilization
          </div>
        </a>

        <a
          href="/backoffice/me"
          className="block bg-white border rounded-xl shadow p-4 hover:shadow-md transition"
        >
          <div className="font-semibold">My Profile</div>
          <div className="text-xs text-gray-500 mt-1">
            BackOffice account details
          </div>
        </a>
      </div>

      {/* Stations table */}
      <StationList />
    </div>
  );
}
