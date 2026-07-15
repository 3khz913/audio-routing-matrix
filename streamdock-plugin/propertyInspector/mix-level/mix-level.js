var ws = null;
var mixerWs = null;
var mixes = [];
var settings = {};
var uuid = '';
var sdkPort = 0;
var registerEvent = '';

// ═══════ Called by StreamDock Creator ═══════
function connectElgatoStreamDeckSocket(inPort, inPropertyInspectorUUID, inRegisterEvent, inInfo, inActionInfo) {
  sdkPort = inPort;
  uuid = inPropertyInspectorUUID;
  registerEvent = inRegisterEvent;

  try {
    var actionData = JSON.parse(inActionInfo);
    settings = actionData.payload?.settings || {};
  } catch(e) {}

  connectSDK();
  connectMixer();
}

// ═══════ Connect to StreamDock SDK ═══════
function connectSDK() {
  ws = new WebSocket('ws://127.0.0.1:' + sdkPort);

  ws.onopen = function() {
    ws.send(JSON.stringify({ event: registerEvent, uuid: uuid }));
  };

  ws.onmessage = function(e) {
    try {
      var msg = JSON.parse(e.data);
      if (msg.event === 'didReceiveSettings') {
        settings = msg.payload?.settings || {};
        applySettings();
      }
    } catch(err) {}
  };
}

// ═══════ Connect to mixer server ═══════
function connectMixer() {
  var statusEl = document.getElementById('status');

  mixerWs = new WebSocket('ws://localhost:8765');

  mixerWs.onopen = function() {
    statusEl.textContent = 'Connected';
    statusEl.className = 'status ok';
  };

  mixerWs.onmessage = function(e) {
    try {
      var msg = JSON.parse(e.data);
      if (msg.type === 'state' && msg.data) {
        mixes = msg.data.mixes || [];
        populate();
        applySettings();
      }
    } catch(err) {}
  };

  mixerWs.onclose = function() {
    statusEl.textContent = 'Disconnected - retrying...';
    statusEl.className = 'status err';
    setTimeout(connectMixer, 3000);
  };

  mixerWs.onerror = function() {
    statusEl.textContent = 'Cannot connect to mixer';
    statusEl.className = 'status err';
  };
}

// ═══════ Populate ═══════
function populate() {
  var mxSelect = document.getElementById('mix');
  mxSelect.innerHTML = '<option value="">-- Select mix --</option>';
  for (var j = 0; j < mixes.length; j++) {
    mxSelect.innerHTML += '<option value="' + mixes[j].id + '">' + esc(mixes[j].name) + '</option>';
  }
}

function applySettings() {
  var mxSel = document.getElementById('mix');
  if (mixes.length > 0) mxSel.value = settings.mixId || '';
}

function save() {
  settings.mixId = document.getElementById('mix').value || null;
  if (ws && ws.readyState === 1) {
    ws.send(JSON.stringify({ event: 'setSettings', context: uuid, payload: settings }));
  }
}

function esc(s) { return (s || '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }

document.addEventListener('DOMContentLoaded', function() {
  document.getElementById('mix').addEventListener('change', save);
});
