const state = {
  token: null,
  email: null,
  nzToday: new Date().toISOString().slice(0, 10),
  lastToLocation: "FARM-NZ-002"
};

const $ = (id) => document.getElementById(id);

function setHttpBadge(el, status) {
  el.textContent = status ? `HTTP ${status}` : "";
  el.className = "http-badge";
  if (!status) return;
  if (status >= 200 && status < 300) el.classList.add("status-ok");
  else if (status >= 400 && status < 500) el.classList.add("status-warn");
  else el.classList.add("status-err");
}

function authHeaders() {
  const headers = { "Content-Type": "application/json" };
  if (state.token) {
    headers["Authorization"] = `Bearer ${state.token}`;
  }
  return headers;
}

async function loadDiagnosticsInfo() {
  try {
    const res = await fetch("/api/diagnostics/info");
    if (!res.ok) return;
    const data = await res.json();
    $("badge-db").textContent = `DB: ${data.databaseProvider}`;
    $("badge-auth").textContent = `Auth: ${data.authMode}`;
    $("badge-nzdate").textContent = `NZ Today: ${data.nzToday}`;
    state.nzToday = data.nzToday;
    if (!$("f-movementDate").value) {
      $("f-movementDate").value = data.nzToday;
    }
  } catch (err) {
    console.error("Failed to load diagnostics info", err);
  }
}

async function signIn() {
  const email = $("auth-email").value.trim();
  const password = $("auth-password").value;
  const res = await fetch("/api/auth/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password })
  });

  if (!res.ok) {
    $("auth-status").textContent = `Sign-in failed (${res.status})`;
    $("auth-status").className = "status-pill status-err";
    return;
  }

  const data = await res.json();
  state.token = data.accessToken;
  state.email = data.email;
  $("auth-status").textContent = `Signed in as ${data.displayName} (${data.email})`;
  $("auth-status").className = "status-pill status-ok";
  $("btn-copy-token").disabled = false;
  $("btn-logout").disabled = false;
  await refreshLogs();
}

function signOut() {
  state.token = null;
  state.email = null;
  $("auth-status").textContent = "Not signed in";
  $("auth-status").className = "status-pill status-warn";
  $("btn-copy-token").disabled = true;
  $("btn-logout").disabled = true;
}

async function submitMovement(e) {
  e.preventDefault();
  const payload = {
    animalId: $("f-animalId").value || null,
    species: $("f-species").value || null,
    fromLocationId: $("f-fromLocationId").value || null,
    toLocationId: $("f-toLocationId").value || null,
    movementDate: $("f-movementDate").value || null,
    recordedBy: $("f-recordedBy").value || null,
    notes: $("f-notes").value || null
  };

  const res = await fetch("/api/movements", {
    method: "POST",
    headers: authHeaders(),
    body: JSON.stringify(payload)
  });

  setHttpBadge($("post-status"), res.status);
  const text = await res.text();
  try {
    const json = JSON.parse(text);
    $("post-response").textContent = JSON.stringify(json, null, 2);
    if (res.ok && json.toLocationId) {
      state.lastToLocation = json.toLocationId;
      $("q-animalId").value = json.animalId;
      await fetchHistory();
    }
  } catch {
    $("post-response").textContent = text || "(empty response)";
  }

  await Promise.all([refreshOutbox(), refreshLogs()]);
}

async function fetchHistory() {
  const animalId = $("q-animalId").value.trim();
  const res = await fetch(`/api/animals/${encodeURIComponent(animalId)}/movements`, {
    headers: authHeaders()
  });

  setHttpBadge($("get-status"), res.status);
  const text = await res.text();
  let json = null;
  try {
    json = JSON.parse(text);
    $("get-response").textContent = JSON.stringify(json, null, 2);
  } catch {
    $("get-response").textContent = text;
  }

  const tbody = $("history-tbody");
  if (!res.ok || !json) {
    $("history-summary").textContent = `Request returned HTTP ${res.status}.`;
    tbody.innerHTML = `<tr><td colspan="5" class="muted">Unable to load history (${res.status}).</td></tr>`;
    await refreshLogs();
    return;
  }

  $("history-summary").textContent =
    `Animal ${json.animalId} · Species: ${json.species ?? "None yet"} · Total movements: ${json.totalMovements}`;

  if (!json.movements || json.movements.length === 0) {
    tbody.innerHTML = `<tr><td colspan="5" class="muted">200 OK — Empty movement history for this animal.</td></tr>`;
  } else {
    tbody.innerHTML = json.movements
      .map(
        (m) => `<tr>
          <td>${escapeHtml(m.movementDate)}</td>
          <td><code>${escapeHtml(m.fromLocationId)}</code> → <code>${escapeHtml(m.toLocationId)}</code></td>
          <td>${escapeHtml(m.species)}</td>
          <td>${escapeHtml(m.recordedBy)}</td>
          <td>${escapeHtml(new Date(m.createdAtUtc).toISOString().replace("T", " ").slice(0, 19))}</td>
        </tr>`
      )
      .join("");
  }

  await refreshLogs();
}

async function refreshOutbox() {
  try {
    const res = await fetch("/api/diagnostics/outbox?take=30");
    if (!res.ok) return;
    const data = await res.json();
    const tbody = $("outbox-tbody");

    if (!data.messages || data.messages.length === 0) {
      tbody.innerHTML = `<tr><td colspan="5" class="muted">No outbox messages yet. Record a movement to see one appear.</td></tr>`;
      return;
    }

    tbody.innerHTML = data.messages
      .map((m) => {
        const cls =
          m.status === "Processed" ? "status-ok" : m.status === "Pending" ? "status-warn" : "status-err";
        const occ = new Date(m.occurredAtUtc).toISOString().slice(11, 19);
        const proc = m.processedAtUtc ? new Date(m.processedAtUtc).toISOString().slice(11, 19) : "—";
        return `<tr>
          <td><span class="status-pill ${cls}">${escapeHtml(m.status)}</span></td>
          <td title="${escapeHtml(m.payloadJson)}">${escapeHtml(m.eventType)}</td>
          <td>${occ}</td>
          <td>${proc}</td>
          <td>${m.retryCount}</td>
        </tr>`;
      })
      .join("");
  } catch (err) {
    console.error("Failed to refresh outbox", err);
  }
}

async function refreshLogs() {
  try {
    const level = $("log-level").value;
    const search = $("log-search").value.trim();
    const params = new URLSearchParams({ level, take: "150" });
    if (search) params.set("search", search);

    const res = await fetch(`/api/diagnostics/logs?${params}`);
    if (!res.ok) return;
    const data = await res.json();
    const consoleEl = $("log-console");

    if (!data.entries || data.entries.length === 0) {
      consoleEl.innerHTML = `<div class="muted">No log entries matching filter.</div>`;
      return;
    }

    consoleEl.innerHTML = data.entries
      .map((e) => {
        const ts = new Date(e.timestampUtc).toISOString().slice(11, 23);
        const shortLvl = e.level.slice(0, 3).toUpperCase();
        const corr = e.correlationId ? `[${e.correlationId.slice(0, 8)}]` : "";
        return `<div class="log-row">
          <span class="log-ts">${ts}</span>
          <span class="log-lvl lvl-${escapeHtml(e.level)}">${shortLvl}</span>
          <span class="log-corr">${escapeHtml(corr)}</span>
          <span class="log-msg">${escapeHtml(e.message)}</span>
        </div>`;
      })
      .join("");
  } catch (err) {
    console.error("Failed to refresh logs", err);
  }
}

async function runTests() {
  $("btn-run-tests").disabled = true;
  $("test-summary").textContent = "Running automated business rule tests...";
  try {
    const res = await fetch("/api/diagnostics/run-tests", { method: "POST" });
    const data = await res.json();
    $("test-summary").innerHTML =
      `<span class="status-pill ${data.failed === 0 ? "status-ok" : "status-err"}">${data.passed}/${data.total} Passed</span> ` +
      `<span class="muted">(${data.totalDurationMs} ms · ${escapeHtml(data.runnerMode)})</span>`;

    $("tests-tbody").innerHTML = data.results
      .map(
        (r) => `<tr>
          <td><span class="status-pill ${r.passed ? "status-ok" : "status-err"}">${r.passed ? "PASS" : "FAIL"}</span></td>
          <td>${escapeHtml(r.category)}</td>
          <td title="${escapeHtml(r.detail)}">${escapeHtml(r.name)}</td>
          <td>${r.durationMs}</td>
        </tr>`
      )
      .join("");
  } finally {
    $("btn-run-tests").disabled = false;
    await refreshLogs();
  }
}

function escapeHtml(str) {
  return String(str ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function addDays(isoDate, days) {
  const d = new Date(isoDate + "T00:00:00Z");
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
}

// Wire events
$("btn-login").addEventListener("click", signIn);
$("btn-logout").addEventListener("click", signOut);
$("btn-copy-token").addEventListener("click", async () => {
  if (state.token) {
    await navigator.clipboard.writeText(state.token);
    $("btn-copy-token").textContent = "Copied!";
    setTimeout(() => ($("btn-copy-token").textContent = "Copy JWT"), 1500);
  }
});

$("movement-form").addEventListener("submit", submitMovement);
$("btn-fetch-history").addEventListener("click", fetchHistory);
$("btn-run-tests").addEventListener("click", runTests);
$("btn-refresh-outbox").addEventListener("click", refreshOutbox);
$("btn-refresh-logs").addEventListener("click", refreshLogs);
$("btn-clear-logs").addEventListener("click", async () => {
  await fetch("/api/diagnostics/logs", { method: "DELETE" });
  await refreshLogs();
});
$("log-level").addEventListener("change", refreshLogs);
$("log-search").addEventListener("input", refreshLogs);

// Presets
$("preset-valid").addEventListener("click", () => {
  $("f-animalId").value = "982000123456789";
  $("f-species").value = "Cattle";
  $("f-fromLocationId").value = "FARM-NZ-001";
  $("f-toLocationId").value = "FARM-NZ-002";
  $("f-movementDate").value = state.nzToday;
  $("f-recordedBy").value = "Jane Doe";
  $("f-notes").value = "Standard farm transfer";
});

$("preset-chain").addEventListener("click", () => {
  $("f-fromLocationId").value = state.lastToLocation;
  $("f-toLocationId").value = "SALEYARD-" + Math.floor(100 + Math.random() * 899);
  $("f-movementDate").value = state.nzToday;
  $("f-notes").value = "Chained follow-up movement";
});

$("preset-same-loc").addEventListener("click", () => {
  $("f-fromLocationId").value = "FARM-NZ-001";
  $("f-toLocationId").value = "FARM-NZ-001";
  $("f-movementDate").value = state.nzToday;
});

$("preset-future").addEventListener("click", () => {
  $("f-fromLocationId").value = "FARM-NZ-001";
  $("f-toLocationId").value = "FARM-NZ-002";
  $("f-movementDate").value = addDays(state.nzToday, 2);
});

$("preset-bad-input").addEventListener("click", () => {
  $("f-recordedBy").value = "";
});

// Boot
(async () => {
  await loadDiagnosticsInfo();
  await signIn();
  await Promise.all([refreshOutbox(), refreshLogs()]);

  setInterval(() => {
    if ($("chk-auto-refresh").checked) {
      refreshOutbox();
      refreshLogs();
    }
  }, 3000);
})();
