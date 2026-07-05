const { WaveLinkController } = require('@darrellvs/node-wave-link-sdk');
const WebSocket = require('ws');

const sdk = new WaveLinkController();
const wss = new WebSocket.Server({ port: 8765 });
let ready = false;

function buildState() {
  const channels = sdk.getChannels();
  const mixes = sdk.getMixes();

  return {
    inputs: channels.map(ch => ({
      id: ch.id,
      name: ch.name,
      volume: Math.round(ch.level * 100),
      isMuted: ch.isMuted          // ← أضفنا حالة الكتم
    })),
    mixes: mixes.map(m => ({ id: m.id, name: m.name })),
    cells: channels.flatMap(ch =>
      mixes.map(m => {
        const send = ch.mixes?.find(s => s.id === m.id);
        return {
          inputId: ch.id,
          mixId: m.id,
          volume: Math.round((send?.level ?? 0.8) * 100)
        };
      })
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

sdk.on('ready', () => {
  console.log('✅ Connected to Wave Link 3');
  ready = true;
  broadcast();
  sdk.on('channelChanged', broadcast);
  sdk.on('mixChanged', broadcast);
});

sdk.on('disconnected', () => {
  console.log('🔌 Disconnected');
  ready = false;
});

sdk.connect();

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
        // أمر الكتم الجديد
        sdk.setChannel({ id: msg.inputId, isMuted: msg.isMuted });
      }
    } catch (err) {
      console.error('Invalid command:', data.toString());
    }
  });

  ws.on('close', () => console.log('WPF client disconnected'));
});

console.log('🎛️ Server running on ws://localhost:8765');