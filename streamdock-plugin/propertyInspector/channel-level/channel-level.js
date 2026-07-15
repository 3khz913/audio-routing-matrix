var ws = null;
var mixerWs = null;
var channels = [];
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
      } else if (msg.event === 'sendToPropertyInspector' && msg.payload) {
        if (msg.payload.channels) {
          channels = msg.payload.channels;
          mixes = msg.payload.mixes;
          populate();
          applySettings();
        }
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
        channels = msg.data.inputs || [];
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

// ═══════ Populate dropdowns ═══════
function populate() {
  var chSelect = document.getElementById('channel');
  var mxSelect = document.getElementById('mix');

  chSelect.innerHTML = '<option value="">-- Select channel --</option>';
  for (var i = 0; i < channels.length; i++) {
    chSelect.innerHTML += '<option value="' + channels[i].id + '">' + esc(channels[i].name) + '</option>';
  }

  mxSelect.innerHTML = '<option value="">All mixes</option>';
  for (var j = 0; j < mixes.length; j++) {
    mxSelect.innerHTML += '<option value="' + mixes[j].id + '">' + esc(mixes[j].name) + '</option>';
  }
}

function applySettings() {
  var chSel = document.getElementById('channel');
  var mxSel = document.getElementById('mix');
  if (channels.length > 0) chSel.value = settings.channelId || '';
  if (mixes.length > 0) mxSel.value = settings.mixId || '';
}

function save() {
  settings.channelId = document.getElementById('channel').value;
  settings.mixId = document.getElementById('mix').value || null;
  if (ws && ws.readyState === 1) {
    ws.send(JSON.stringify({ event: 'setSettings', context: uuid, payload: settings }));
  }
}

function esc(s) { return (s || '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }

document.addEventListener('DOMContentLoaded', function() {
  document.getElementById('channel').addEventListener('change', save);
  document.getElementById('mix').addEventListener('change', save);
});
