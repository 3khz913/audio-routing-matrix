# mixer

Audio routing matrix for Elgato Wave Link 3, controlled from a WPF app with global
MIDI Learn support for every slider/mute control.

## Architecture

```
mixer-project/
├── server/              Node.js bridge (Wave Link 3 SDK <-> WebSocket :8765)
│   ├── package.json
│   └── server.js
└── mixer/                WPF client (.NET 10, MVVM)
    ├── mixer.csproj
    ├── App.xaml / App.xaml.cs
    ├── MainWindow.xaml / .cs
    ├── Models/            DTOs matching the server's JSON contract + MidiMapping
    ├── Services/          Logger, WaveLinkService, MidiService, MidiMappingStorage
    ├── ViewModels/        MainViewModel, SourceViewModel, MixViewModel, CellViewModel, ...
    └── Views/             Theme.xaml (dark UI), EditCellWindow (MIDI Learn dialog)
```

## Running it

1. **Start Wave Link 3** and make sure it's fully loaded.
2. **Start the bridge server**:
   ```bash
   cd server
   npm install
   npm start
   ```
   You should see `Connected to Elgato Wave Link 3.` in the console. If not, check
   that Wave Link is running — the server retries every 5 seconds.
3. **Run the WPF app** (from Visual Studio, or `dotnet run` inside `mixer/`).
   The status dot in the top-right turns green once it's connected to the server.

## Key design points (carried over from prior iterations)

- The server always sends/receives **lowercase JSON keys**; the WPF `JsonSerializerOptions`
  use `PropertyNameCaseInsensitive = true` to match.
- `sdk.setChannel(...)` always takes a **single object**: `{ id, ...props }`.
- Slider drags are **debounced** (80ms) client-side before hitting the WebSocket, so
  dragging fast doesn't flood the server or freeze the UI.
- Incoming state updates **mutate existing ViewModel instances** instead of rebuilding
  the collections, so the UI doesn't flicker or lose focus while you're interacting with it.
- MIDI uses **Melanchall.DryWetMidi** and supports any class-compliant device — never
  Raw Input API (that was tried before and caused Access Violation crashes).
- All MIDI device calls are wrapped in try/catch; a failing device shows a status
  message instead of taking down the app.
- `EditCellWindow` unsubscribes from MIDI events in `Cleanup()` on close, to avoid
  leaking a stray "Learn" capture into a later session.
- Every unhandled exception is caught at the `Application` level and logged to
  `%LocalAppData%\mixer\app_log.txt` instead of crashing silently.

## Extending mappings

Each control (a send-level cell, or a source's master volume/mute) has a
`MappingKey`:
- Cells: `"{inputId}|{mixId}"` (e.g. `"sys|pm"`)
- Master controls: `"{inputId}|master"` (e.g. `"sys|master"`)

`MidiMappingStorage` persists these to `%LocalAppData%\mixer\midi_mappings.json`,
so you can hand-edit or back up mappings if needed.
