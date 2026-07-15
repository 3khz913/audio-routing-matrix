const WebSocket = require('ws');

class MixerClient {
  constructor() {
    this.ws = null;
    this.state = { inputs: [], mixes: [], cells: [] };
    this.listeners = [];
    this.connected = false;
  }

  connect() {
    this._tryConnect();
  }

  _tryConnect() {
    if (this.ws) {
      try { this.ws.close(); } catch (_) {}
    }

    this.ws = new WebSocket('ws://localhost:8765');

    this.ws.on('open', () => {
      console.log('[mixer-client] Connected to mixer server');
    });

    this.ws.on('message', (data) => {
      try {
        const msg = JSON.parse(data.toString());
        if (msg.type === 'state' && msg.data) {
          this.state = msg.data;
          this.connected = true;
          this._emit('state', this.state);
        } else if (msg.type === 'status') {
          console.log('[mixer-client] Status:', msg.status);
        }
      } catch (err) {
        // ignore malformed messages
      }
    });

    this.ws.on('close', () => {
      console.log('[mixer-client] Disconnected, retrying in 3s...');
      this.connected = false;
      this._emit('disconnected');
      setTimeout(() => this._tryConnect(), 3000);
    });

    this.ws.on('error', () => {
      // handled by close
    });
  }

  send(payload) {
    if (this.ws && this.ws.readyState === WebSocket.OPEN) {
      this.ws.send(JSON.stringify(payload));
    }
  }

  on(event, fn) {
    this.listeners.push({ event, fn });
  }

  _emit(event, data) {
    for (const l of this.listeners) {
      if (l.event === event) l.fn(data);
    }
  }

  getChannel(id) {
    return this.state.inputs?.find(c => c.id === id);
  }

  getMix(id) {
    return this.state.mixes?.find(m => m.id === id);
  }

  getCell(inputId, mixId) {
    return this.state.cells?.find(c => c.inputId === inputId && c.mixId === mixId);
  }

  getChannelVolume(inputId, mixId) {
    if (mixId) {
      const cell = this.getCell(inputId, mixId);
      return cell ? cell.volume : 0;
    }
    const ch = this.getChannel(inputId);
    return ch ? ch.volume : 0;
  }

  getChannelMuted(inputId, mixId) {
    if (mixId) {
      const cell = this.getCell(inputId, mixId);
      return cell ? cell.isMuted : false;
    }
    const ch = this.getChannel(inputId);
    return ch ? ch.isMuted : false;
  }

  setChannelVolume(inputId, volume) {
    this.send({ type: 'setInputVolume', inputId, volume });
  }

  setChannelMixVolume(inputId, mixId, volume) {
    this.send({ type: 'setInputMixVolume', inputId, mixId, volume });
  }

  setChannelMute(inputId, isMuted) {
    this.send({ type: 'setInputMute', inputId, isMuted });
  }

  setChannelMixMute(inputId, mixId, isMuted) {
    this.send({ type: 'setInputMixMute', inputId, mixId, isMuted });
  }

  setMixMute(mixId, isMuted) {
    this.send({ type: 'setMixMute', mixId, isMuted });
  }
}

module.exports = MixerClient;
