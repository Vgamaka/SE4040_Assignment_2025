import { useEffect, useState } from "react";
import { getBackOfficeStations } from "../../services/api";

export default function StationList() {
  const [data, setData] = useState({ total: 0, items: [] });
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState("");

  useEffect(() => {
    let mounted = true;
    (async () => {
      setLoading(true);
      setErr("");
      try {
        const res = await getBackOfficeStations({ page, pageSize });
        if (mounted) setData(res);
      } catch (e) {
        console.error(e);
        setErr(
          e?.response?.data?.message ||
            e?.response?.data?.error ||
            "Failed to load stations."
        );
      } finally {
        setLoading(false);
      }
    })();
    return () => { mounted = false; };
  }, [page, pageSize]);

  const totalPages = Math.max(1, Math.ceil((data?.total || 0) / pageSize));

  return (
    <div className="bg-white border rounded-xl shadow">
      <div className="px-4 py-3 border-b">
        <h3 className="font-semibold">My Stations</h3>
        <p className="text-xs text-gray-500">BackOffice-owned stations</p>
      </div>

      {err && (
        <div className="m-4 text-sm bg-red-50 border border-red-200 text-red-700 rounded p-2">
          {err}
        </div>
      )}

      {loading ? (
        <div className="p-4 text-sm text-gray-600">Loading…</div>
      ) : data.items?.length ? (
        <>
          <div className="overflow-x-auto">
            <table className="min-w-full text-sm">
              <thead className="bg-gray-50">
                <tr>
                  <th className="text-left px-4 py-2">Name</th>
                  <th className="text-left px-4 py-2">Type</th>
                  <th className="text-left px-4 py-2">Connectors</th>
                  <th className="text-left px-4 py-2">Status</th>
                  <th className="text-left px-4 py-2">Auto-Approve</th>
                  <th className="text-left px-4 py-2">Actions</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((s) => (
                  <tr key={s.id} className="border-t">
                    <td className="px-4 py-2">{s.name}</td>
                    <td className="px-4 py-2">{s.type}</td>
                    <td className="px-4 py-2">{s.connectors}</td>
                    <td className="px-4 py-2">{s.status}</td>
                    <td className="px-4 py-2">
                      {s.autoApproveEnabled ? "Yes" : "No"}
                    </td>
                    <td className="px-4 py-2">
                      <a
                        href={`/backoffice/stations/${s.id}`}
                        className="text-blue-700 hover:underline"
                      >
                        View
                      </a>
                      <span className="mx-2 text-gray-300">|</span>
                      <a
                        href={`/backoffice/stations/${s.id}/schedule`}
                        className="text-blue-700 hover:underline"
                      >
                        Schedule
                      </a>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          <div className="flex items-center justify-between p-3 border-t text-sm">
            <div>
              Page {page} of {totalPages} • {data.total} total
            </div>
            <div className="space-x-2">
              <button
                className="px-3 py-1 border rounded disabled:opacity-50"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page <= 1}
              >
                Prev
              </button>
              <button
                className="px-3 py-1 border rounded disabled:opacity-50"
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages}
              >
                Next
              </button>
            </div>
          </div>
        </>
      ) : (
        <div className="p-4 text-sm text-gray-600">No stations found.</div>
      )}
    </div>
  );
}
