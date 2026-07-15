const MixerClient = require('./mixer-client.js');
const icons = require('./icons.js');

let mixer = null;
let pluginPort = null;
let pluginUUID = null;
let registerEvent = null;
let ws = null;
let contexts = {};
let imagesCache = {};
let lastState = null;
const KNOB_STEP = 2;

// ═══════ Entry Point (called by StreamDock Creator) ═══════
function connectElgatoStreamDeckSocket(inPort, inPluginUUID, inRegisterEvent, inInfo) {
  pluginPort = inPort;
  pluginUUID = inPluginUUID;
  registerEvent = inRegisterEvent;

  _connectToSDK();
}

// ═══════ SDK Connection ═══════
function _connectToSDK() {
  ws = new (require('ws'))(`ws://127.0.0.1:${pluginPort}`);

  ws.on('open', () => {
    console.log('[plugin] Connected to StreamDock SDK');
    ws.send(JSON.stringify({ event: registerEvent, uuid: pluginUUID }));
  });

  ws.on('message', (data) => _handleSDKMessage(data));

  ws.on('close', () => {
    console.log('[plugin] SDK disconnected, retrying in 2s...');
    setTimeout(_connectToSDK, 2000);
  });
}

// ═══════ Message Router ═══════
function _handleSDKMessage(data) {
  try {
    const msg = JSON.parse(data);
    const { event, context, payload } = msg;

    switch (event) {
      case 'deviceDidConnect':
        _initMixer();
        break;

      case 'willAppear':
        _onWillAppear(context, payload);
        break;

      case 'willDisappear':
        delete contexts[context];
        delete imagesCache[context];
        break;

      case 'keyDown':
        _onKeyDown(context, payload);
        break;

      case 'dialRotate':
        _onDialRotate(context, payload);
        break;

      case 'didReceiveSettings':
        _onSettingsReceived(context, payload);
        break;

      case 'propertyInspectorDidAppear':
        _sendStateToPropertyInspector(context);
        break;

      case 'sendToPlugin':
        // Property Inspector → Plugin bridge (unused currently)
        break;
    }
  } catch (err) {
    console.error('[plugin] Error handling SDK message:', err);
  }
}

// ═══════ Mixer Init ═══════
function _initMixer() {
  if (mixer) return;
  mixer = new MixerClient();
  mixer.on('state', (state) => {
    lastState = state;
    _refreshAllKeys();
  });
  mixer.connect();
}

// ═══════ willAppear ═══════
function _onWillAppear(context, payload) {
  const settings = payload?.settings || {};
  const action = payload?.action || '';

  contexts[context] = { action, settings, controller: payload?.controller };
  _refreshKey(context);
}

// ═══════ Key Down (Mute Toggle) ═══════
function _onKeyDown(context, payload) {
  if (!mixer || !mixer.connected) return;

  const ctx = contexts[context];
  if (!ctx) return;
  const s = ctx.settings;

  if (ctx.action === 'com.mixer.channel-level') {
    if (!s.channelId) return;
    if (s.mixId) {
      const muted = mixer.getChannelMuted(s.channelId, s.mixId);
      mixer.setChannelMixMute(s.channelId, s.mixId, !muted);
    } else {
      const muted = mixer.getChannelMuted(s.channelId, null);
      mixer.setChannelMute(s.channelId, !muted);
    }
  } else if (ctx.action === 'com.mixer.mix-level') {
    if (!s.mixId) return;
    const mix = mixer.getMix(s.mixId);
    if (mix) {
      mixer.setMixMute(s.mixId, !mix.isMuted);
    }
  }
}

// ═══════ Dial Rotate (Volume Adjust) ═══════
function _onDialRotate(context, payload) {
  if (!mixer || !mixer.connected) return;

  const ctx = contexts[context];
  if (!ctx) return;
  const s = ctx.settings;
  const ticks = payload?.ticks || 0;
  if (ticks === 0) return;

  const step = KNOB_STEP * Math.sign(ticks);

  if (ctx.action === 'com.mixer.channel-level') {
    if (!s.channelId) return;
    if (s.mixId) {
      const vol = mixer.getChannelVolume(s.channelId, s.mixId);
      const newVol = Math.max(0, Math.min(100, vol + step));
      mixer.setChannelMixVolume(s.channelId, s.mixId, newVol);
    } else {
      const vol = mixer.getChannelVolume(s.channelId, null);
      const newVol = Math.max(0, Math.min(100, vol + step));
      mixer.setChannelVolume(s.channelId, newVol);
    }
  } else if (ctx.action === 'com.mixer.mix-level') {
    if (!s.mixId) return;
    const mix = mixer.getMix(s.mixId);
    if (mix) {
      // Mix level from SDK is 0-1; our server uses 0-100
      const vol = Math.round(mix.level * 100);
      const newVol = Math.max(0, Math.min(100, vol + step));
      // There's no direct setMixVolume in our server yet — set via proxy
      console.log('[plugin] Mix volume change requested:', s.mixId, newVol);
    }
  }
}

// ═══════ Settings Changed ═══════
function _onSettingsReceived(context, payload) {
  const settings = payload?.settings || {};
  if (contexts[context]) {
    contexts[context].settings = settings;
    _refreshKey(context);
  }
}

// ═══════ Refresh Key Icon ═══════
function _refreshKey(context) {
  const ctx = contexts[context];
  if (!ctx || !mixer || !mixer.connected) return;
  const s = ctx.settings;

  let imageData;
  let title = '';

  if (ctx.action === 'com.mixer.channel-level') {
    if (!s.channelId) {
      imageData = _blankIcon();
      title = 'No channel';
    } else {
      const channel = mixer.getChannel(s.channelId);
      const name = channel?.name || s.channelId;

      if (s.mixId) {
        const cell = mixer.getCell(s.channelId, s.mixId);
        if (!cell || !cell.routed) {
          imageData = icons.drawUnrouted(name);
          title = name;
        } else {
          const vol = cell.volume;
          const muted = cell.isMuted;
          imageData = icons.drawChannelKey(name, vol, muted);
          title = muted ? 'MUTED' : vol + '%';
        }
      } else {
        const vol = channel ? channel.volume : 0;
        const muted = channel ? channel.isMuted : false;
        imageData = icons.drawChannelKey(name, vol, muted);
        title = muted ? 'MUTED' : vol + '%';
      }
    }
  } else if (ctx.action === 'com.mixer.mix-level') {
    if (!s.mixId) {
      imageData = _blankIcon();
      title = 'No mix';
    } else {
      const mix = mixer.getMix(s.mixId);
      const name = mix?.name || s.mixId;
      const vol = mix ? Math.round(mix.level * 100) : 0;
      const muted = mix ? mix.isMuted : false;
      imageData = icons.drawMixKey(name, vol, muted);
      title = muted ? 'MUTED' : vol + '%';
    }
  }

  if (!imageData) return;

  if (imagesCache[context] === imageData) return;
  imagesCache[context] = imageData;

  _sendToSDK('setImage', context, { image: imageData, target: 1 });
  if (title) {
    _sendToSDK('setTitle', context, { title, target: 1 });
  }
}

// ═══════ Refresh All Keys ═══════
function _refreshAllKeys() {
  for (const context of Object.keys(contexts)) {
    _refreshKey(context);
  }
}

function _sendStateToPropertyInspector(context) {
  if (!mixer || !mixer.connected) return;
  _sendToSDK('sendToPropertyInspector', context, {
    channels: mixer.state.inputs || [],
    mixes: mixer.state.mixes || []
  });
}

// ═══════ Helpers ═══════
function _sendToSDK(event, context, payload) {
  if (!ws || ws.readyState !== 1) return;
  ws.send(JSON.stringify({ event, context, payload }));
}

function _blankIcon() {
  return 'data:image/svg+xml;charset=utf8,' + encodeURIComponent(
    '<svg xmlns="http://www.w3.org/2000/svg" width="112" height="112">' +
    '<rect width="112" height="112" fill="#1C1C1C" rx="8"/>' +
    '<text x="56" y="62" text-anchor="middle" font-family="sans-serif" font-size="14" fill="#555">mixer</text>' +
    '</svg>'
  );
}

module.exports = connectElgatoStreamDeckSocket;
