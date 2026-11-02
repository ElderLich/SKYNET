const RESTART_HINT = 'Restart Arena after updating, then reload this page. If using a separate frontend server, check its /api proxy.';

export async function requestWorkshop(url, body, fetchRequest = fetch) {
    const response = await fetchRequest(url, body ? {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
    } : undefined);
    const type = (response.headers.get('content-type') || '').toLowerCase();
    if (!type.includes('application/json')) {
        throw new Error(`The Workshop API returned a non-JSON response (HTTP ${response.status}). ${RESTART_HINT}`);
    }
    let value;
    try {
        value = await response.json();
    } catch {
        throw new Error(`The Workshop API returned invalid JSON (HTTP ${response.status}). ${RESTART_HINT}`);
    }
    if (!response.ok) throw new Error(value?.error || `Workshop request failed (HTTP ${response.status}).`);
    return value;
}
