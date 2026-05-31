const overlay = document.getElementById("subtitleOverlay");

let settings = {
    overlay_font_size: 24,
    overlay_font_color: "#FFFFFF",
    overlay_bg_opacity: 0.7,
    overlay_display_duration_ms: 5000,
    overlay_show_translation: true,
    overlay_show_original: true,
    overlay_translation_font_size: 19,
    overlay_translation_bg_opacity: 0.5,
};

let ws = null;
let hideTimer = null;

async function loadSettings() {
    try {
        const response = await fetch("/api/settings");
        if (!response.ok) return;
        Object.assign(settings, await response.json());
        applyStyles();
    } catch (err) {
        console.warn("Could not load settings:", err);
    }
}

function applyStyles() {
    const root = document.documentElement;
    root.style.setProperty("--overlay-font-size", settings.overlay_font_size + "px");
    root.style.setProperty("--overlay-font-color", settings.overlay_font_color);
    root.style.setProperty(
        "--overlay-bg",
        `rgba(0, 0, 0, ${settings.overlay_bg_opacity})`
    );
}

function showSubtitle(rusText, engText) {
    if (!rusText) return;

    if (hideTimer) {
        clearTimeout(hideTimer);
        hideTimer = null;
    }

    overlay.innerHTML = "";

    // Show original text only if enabled
    if (settings.overlay_show_original) {
        const rusLine = document.createElement("div");
        rusLine.className = "subtitle-line";
        rusLine.textContent = rusText;
        overlay.appendChild(rusLine);
    }

    if (settings.overlay_show_translation && engText) {
        const engLine = document.createElement("div");
        engLine.className = "subtitle-line subtitle-translation";
        engLine.textContent = engText;
        // Apply translation-specific styles
        engLine.style.fontSize = settings.overlay_translation_font_size + "px";
        engLine.style.backgroundColor = `rgba(0, 0, 0, ${settings.overlay_translation_bg_opacity})`;
        overlay.appendChild(engLine);
    }

    hideTimer = setTimeout(hideSubtitle, settings.overlay_display_duration_ms);
}

function hideSubtitle() {
    const lines = overlay.querySelectorAll(".subtitle-line");
    lines.forEach((line) => line.classList.add("fading"));
    setTimeout(() => {
        overlay.innerHTML = "";
    }, 600);
}

function connect() {
    const wsProtocol = location.protocol === "https:" ? "wss:" : "ws:";
    ws = new WebSocket(`${wsProtocol}//${location.host}/ws/subtitles`);

    ws.onopen = () => {
        console.log("[Overlay] Connected");
    };

    ws.onmessage = (event) => {
        const msg = JSON.parse(event.data);
        if (msg.type === "subtitle") {
            showSubtitle(msg.rus_text, msg.eng_text);
        } else if (msg.type === "settings_update") {
            Object.assign(settings, msg);
            applyStyles();
        }
    };

    ws.onclose = () => {
        console.log("[Overlay] Disconnected, reconnecting in 3s...");
        setTimeout(connect, 3000);
    };

    ws.onerror = () => {
        console.error("[Overlay] WebSocket error");
    };
}

loadSettings();
connect();
