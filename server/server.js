const { WaveLinkController } = require('@darrellvs/node-wave-link-sdk');
const WebSocket = require('ws');

const sdk = new WaveLinkController();
const wss = new WebSocket.Server({ port: 8765 });
let ready = false;
let retryCount = 0;

function sendStatus(status) {
  const msg = JSON.stringify({ type: 'status', status });
  wss.clients.forEach(client => {
    if (client.readyState === WebSocket.OPEN) client.send(msg);
  });
}

function buildState() {
  const channels = sdk.getChannels();
  const mixes = sdk.getMixes();
  const waveDevices = sdk.getWaveInputDevices();

  return {
    inputs: channels.map(ch => ({
      id: ch.id,
      name: ch.name,
      volume: Math.round(ch.level * 100),
      isMuted: ch.isMuted
    })),
    mixes: mixes.map(m => ({ id: m.id, name: m.name, isMuted: m.isMuted })),
    cells: channels.flatMap(ch =>
      mixes.map(m => {
        const send = ch.mixes?.find(s => s.id === m.id);
        const routed = !!send;
        return {
          inputId: ch.id,
          mixId: m.id,
          volume: Math.round((send?.level ?? 0) * 100),
          isMuted: send?.isMuted ?? false,
          routed
        };
      })
    ),
    micDevices: waveDevices.flatMap(d =>
      d.inputs.map(i => ({
        id: i.id,
        deviceName: d.name,
        name: i.name ?? `Input ${i.id}`,
        gain: i.gain?.value ?? 0,
        micPcMix: i.micPcMix?.value ?? 0,
        isMuted: i.isMuted
      }))
    )
  };
}

function broadcast() {
  if (!ready) return;
  const state = buildState();
  const msg = JSON.stringify({ type: 'state', data: state });
  wss.clients.forEach(client => {
    if (client.readyState === WebSocket.OPEN) client.send(msg);
  });
}

function broadcastLevelMeters() {
  const meters = sdk.getLevelMeters();
  const channels = meters.channels.map(m => ({
    id: m.id,
    left: m.levelLeftPercentage,
    right: m.levelRightPercentage
  }));
  if (channels.length === 0) return;
  const msg = JSON.stringify({ type: 'levelMeters', data: { channels } });
  wss.clients.forEach(client => {
    if (client.readyState === WebSocket.OPEN) client.send(msg);
  });
}

function subscribeLevelMetersForAll() {
  const channels = sdk.getChannels();
  channels.forEach(ch => sdk.subscribeLevelMeter('channel', ch.id));
}

sdk.on('ready', () => {
  console.log('✅ Connected to Wave Link 3');
  ready = true;
  retryCount = 0;
  sendStatus('connected');
  broadcast();

  subscribeLevelMetersForAll();
  sdk.subscribeFocusedApp();
});

sdk.on('channelChanged', () => { broadcast(); });
sdk.on('mixChanged', () => { broadcast(); });

sdk.on('channelsChanged', () => {
  console.log('📡 Channels changed (added/removed)');
  subscribeLevelMetersForAll();
  broadcast();
});

sdk.on('mixesChanged', () => {
  console.log('📡 Mixes changed (added/removed)');
  broadcast();
});

sdk.on('levelMeterChanged', () => { broadcastLevelMeters(); });

sdk.on('focusedAppChanged', (app) => {
  const msg = JSON.stringify({ type: 'focusedApp', data: { id: app.id, name: app.name, channelId: app.channel?.id ?? '' } });
  wss.clients.forEach(client => {
    if (client.readyState === WebSocket.OPEN) client.send(msg);
  });
});

sdk.on('disconnected', () => {
  console.log('🔌 Wave Link disconnected');
  ready = false;
  sendStatus('disconnected');
});

sdk.connect();

// إذا لم يتصل Wave Link خلال 15 ثانية، نبلغ العميل
setTimeout(() => {
  if (!ready) {
    console.log('⏳ Wave Link not found – waiting...');
    sendStatus('noWaveLink');
  }
}, 15000);

wss.on('connection', ws => {
  console.log('🖥️ WPF client connected');
  if (ready) ws.send(JSON.stringify({ type: 'state', data: buildState() }));

  ws.on('message', data => {
    try {
      const msg = JSON.parse(data);
      if (msg.type === 'setInputVolume' && msg.inputId) {
        sdk.setChannel({ id: msg.inputId, level: msg.volume / 100 });
      } else if (msg.type === 'setInputMixVolume' && msg.inputId && msg.mixId) {
        sdk.setChannel({
          id: msg.inputId,
          mixes: [{ id: msg.mixId, level: msg.volume / 100 }]
        });
      } else if (msg.type === 'setInputMute' && msg.inputId !== undefined) {
        sdk.setChannel({ id: msg.inputId, isMuted: msg.isMuted });
      } else if (msg.type === 'setInputMixMute' && msg.inputId && msg.mixId) {
        sdk.setChannel({ id: msg.inputId, mixes: [{ id: msg.mixId, isMuted: msg.isMuted }] });
      } else if (msg.type === 'setMixMute' && msg.mixId) {
        sdk.setMix({ id: msg.mixId, isMuted: msg.isMuted });
      } else if (msg.type === 'addChannelToMix' && msg.inputId && msg.mixId) {
        sdk.setChannel({
          id: msg.inputId,
          mixes: [{ id: msg.mixId, level: 0.8 }]
        });
      } else if (msg.type === 'removeChannelFromMix' && msg.inputId && msg.mixId) {
        // SDK doesn't support removing directly; set level to 0 to effectively remove
        sdk.setChannel({
          id: msg.inputId,
          mixes: [{ id: msg.mixId, level: 0 }]
        });
      }
    } catch (err) {
      console.error('Invalid command:', data.toString());
    }
  });

  ws.on('close', () => console.log('WPF client disconnected'));
});

console.log('🎛️ Server running on ws://localhost:8765');

// Heartbeat: ping كل عميل كل 15 ثانية
setInterval(() => {
  wss.clients.forEach(client => {
    if (client.readyState === WebSocket.OPEN) {
      client.ping();
    }
  });
}, 15000);