let deviceId = null;
let player = null;

async function getAccessToken() {
    const r = await fetch('/auth/token');
    const j = await r.json();
    return j.access_token;
}

window.onSpotifyWebPlaybackSDKReady = async () => {
    const token = await getAccessToken();

    player = new Spotify.Player({
        name: 'Player do Gustavo',
        getOAuthToken: cb => cb(token),
        volume: 0.8
    });

    player.addListener('ready', ({ device_id }) => {
        deviceId = device_id;
        console.log('Device pronto:', deviceId);
    });

    player.addListener('initialization_error', e => console.error(e.message));
    player.addListener('authentication_error', e => console.error(e.message));
    player.addListener('account_error', e => console.error(e.message));
    player.addListener('playback_error', e => console.error(e.message));

    player.addListener('player_state_changed', state => {
        if (!state) return;
        const t = state.track_window.current_track;
        const label = t ? `${t.name} — ${t.artists.map(a => a.name).join(', ')}` : '–';
        const el = document.getElementById('nowPlaying');
        if (el) el.textContent = label;
    });

    await player.connect();

    // Botões globais (se existirem no DOM)
    const playBtn = document.getElementById('btnPlay');
    const pauseBtn = document.getElementById('btnPause');

    if (playBtn) playBtn.onclick = async () => {
        const form = new FormData();
        form.append('deviceId', deviceId);

        form.append('contextUri', 'spotify:playlist:37i9dQZF1DXcBWIGoYBM5M'); 
        await fetch('/player/play', { method: 'POST', body: form });
    };

    if (pauseBtn) pauseBtn.onclick = async () => {
        const form = new FormData();
        form.append('deviceId', deviceId);
        await fetch('/player/pause', { method: 'POST', body: form });
    };
};
