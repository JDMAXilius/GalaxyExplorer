# being-relay

The Cosmic Being's mind and voice. Unity holds one WebSocket to this service; the service holds the API keys.

    cp .env.example .env      # fill in ANTHROPIC_API_KEY and OPENAI_API_KEY
    npm install
    npm run dev               # ws://<this machine>:8787/being

Point `Assets/data/being/cosmic_being_settings.asset` at this machine's address, then summon the being from the dock.

Protocol: JSON text frames both ways, binary frames for audio. Unity sends `hello` with its context, `turn` (typed `text`, or `audioBytes` followed by one binary frame of 16 kHz mono PCM16), and `interrupt`. The relay sends `transcript`, `text` per sentence, `action` for a tool call, binary frames of 24 kHz mono PCM16 speech, then `done`, or `error`.

Knowledge is read straight from `Assets/data` at start, so the being and the app never disagree.
