// --- DOM Elements ---
const backendVersion = document.getElementById("backendVersion");
const mlVersion = document.getElementById("mlVersion");
const sttModel = document.getElementById("sttModel");
const vadEnabled = document.getElementById("vadEnabled");
const dedupEnabled = document.getElementById("dedupEnabled");
const chunkDuration = document.getElementById("chunkDuration");
const overlapDuration = document.getElementById("overlapDuration");
const overlayFontSize = document.getElementById("overlayFontSize");
const overlayFontSizeValue = document.getElementById("overlayFontSizeValue");
const overlayDisplayDuration = document.getElementById("overlayDisplayDuration");
const overlayFontColor = document.getElementById("overlayFontColor");
const overlayBgOpacity = document.getElementById("overlayBgOpacity");
const overlayBgOpacityValue = document.getElementById("overlayBgOpacityValue");
const overlayShowTranslation = document.getElementById("overlayShowTranslation");
const overlayShowOriginal = document.getElementById("overlayShowOriginal");
const overlayTranslationFontSize = document.getElementById("overlayTranslationFontSize");
const overlayTranslationFontSizeValue = document.getElementById("overlayTranslationFontSizeValue");
const overlayTranslationBgOpacity = document.getElementById("overlayTranslationBgOpacity");
const overlayTranslationBgOpacityValue = document.getElementById("overlayTranslationBgOpacityValue");
const overlayTranslationFontColor = document.getElementById("overlayTranslationFontColor");
const obsUrl = document.getElementById("obsUrl");
const copyObsUrl = document.getElementById("copyObsUrl");
const sourceType = document.getElementById("sourceType");
const fileSection = document.getElementById("fileSection");
const micSection = document.getElementById("micSection");
const uploadForm = document.getElementById("uploadForm");
const fileInput = document.getElementById("fileInput");
const submitBtn = document.getElementById("submitBtn");
const micDevice = document.getElementById("micDevice");
const startMicBtn = document.getElementById("startMicBtn");
const stopMicBtn = document.getElementById("stopMicBtn");
const statusBlock = document.getElementById("status");
const resultBlock = document.getElementById("result");
const rusText = document.getElementById("rusText");
const engText = document.getElementById("engText");
const subtitlesDetails = document.getElementById("subtitles");
const subtitleLog = document.getElementById("subtitleLog");
const errorBlock = document.getElementById("error");

// --- Settings state ---
let settings = {
    model: "base",
    vad_enabled: true,
    chunk_duration_ms: 3000,
    overlap_ms: 0,
    dedup_enabled: true,
    overlay_font_size: 24,
    overlay_font_color: "#FFFFFF",
    overlay_bg_opacity: 0.7,
    overlay_display_duration_ms: 5000,
    overlay_show_translation: true,
    overlay_show_original: true,
    overlay_translation_font_size: 19,
    overlay_translation_bg_opacity: 0.5,
    overlay_translation_font_color: "#FFFFFF",
};

// --- Source switching ---
sourceType.addEventListener("change", () => {
    const isMic = sourceType.value === "microphone";
    fileSection.hidden = isMic;
    micSection.hidden = !isMic;
    hideElement(errorBlock);
    hideElement(statusBlock);
});

// --- File upload ---
uploadForm.addEventListener("submit", async (e) => {
    e.preventDefault();

    const file = fileInput.files[0];
    if (!file) return;

    setStatus("processing", "Обработка...");
    hideElement(resultBlock);
    hideElement(errorBlock);
    submitBtn.disabled = true;

    try {
        const formData = new FormData();
        formData.append("file", file);

        const response = await fetch("/api/subtitles", {
            method: "POST",
            body: formData,
        });

        if (!response.ok) {
            const text = await response.text();
            throw new Error(text || `Ошибка сервера: ${response.status}`);
        }

        const data = await response.json();
        rusText.textContent = data.rus_text || "(пусто)";
        engText.textContent = data.eng_text || "(empty)";
        showElement(resultBlock);
        setStatus("success", "Готово");
    } catch (err) {
        errorBlock.textContent = err.message;
        showElement(errorBlock);
        setStatus("error", "Ошибка");
    } finally {
        submitBtn.disabled = false;
    }
});

// --- Microphone streaming ---
const SAMPLE_RATE = 16000;
let audioContext = null;
let scriptProcessor = null;
let pcmBuffer = [];
let overlapBuffer = null;
let ws = null;
let mediaStream = null;
let chunkTimer = null;

startMicBtn.addEventListener("click", startMicrophone);
stopMicBtn.addEventListener("click", stopMicrophone);

async function startMicrophone() {
    hideElement(errorBlock);

    try {
        const deviceId = micDevice.value || undefined;
        mediaStream = await navigator.mediaDevices.getUserMedia({
            audio: {
                deviceId: deviceId ? { exact: deviceId } : undefined,
                echoCancellation: true,
                noiseSuppression: true,
            },
        });

        const wsProtocol = location.protocol === "https:" ? "wss:" : "ws:";
        ws = new WebSocket(`${wsProtocol}//${location.host}/ws/subtitles`);

        ws.onopen = () => {
            console.log("[WS] Connected");
            setStatus("success", "Запись активна");
        };

        ws.onmessage = (event) => {
            const msg = JSON.parse(event.data);
            if (msg.type === "subtitle") {
                appendSubtitle(msg.rus_text, msg.eng_text);
            } else if (msg.type === "error") {
                console.error("[WS] Error:", msg.error);
            }
        };

        ws.onerror = () => {
            showError("WebSocket ошибка соединения");
        };

        ws.onclose = () => {
            console.log("[WS] Disconnected");
            stopMicrophone();
        };

        audioContext = new AudioContext({ sampleRate: SAMPLE_RATE });
        const source = audioContext.createMediaStreamSource(mediaStream);
        scriptProcessor = audioContext.createScriptProcessor(4096, 1, 1);

        scriptProcessor.onaudioprocess = (event) => {
            const samples = event.inputBuffer.getChannelData(0);
            pcmBuffer.push(new Float32Array(samples));
        };

        source.connect(scriptProcessor);
        scriptProcessor.connect(audioContext.destination);

        overlapBuffer = null;
        chunkTimer = setInterval(sendCurrentBuffer, settings.chunk_duration_ms);

        startMicBtn.disabled = true;
        stopMicBtn.disabled = false;
        subtitleLog.innerHTML = "";
        showElement(subtitlesDetails);
        setStatus("success", "Подключение...");
    } catch (err) {
        showError(err.message || "Не удалось получить доступ к микрофону");
    }
}

function stopMicrophone() {
    if (chunkTimer) {
        clearInterval(chunkTimer);
        chunkTimer = null;
    }
    if (scriptProcessor) {
        scriptProcessor.disconnect();
        scriptProcessor = null;
    }
    if (audioContext) {
        audioContext.close();
        audioContext = null;
    }
    if (mediaStream) {
        mediaStream.getTracks().forEach((t) => t.stop());
        mediaStream = null;
    }
    if (ws) {
        ws.close();
        ws = null;
    }
    pcmBuffer = [];
    overlapBuffer = null;

    startMicBtn.disabled = false;
    stopMicBtn.disabled = true;
    setStatus("success", "Запись остановлена");
}

function sendCurrentBuffer() {
    if (pcmBuffer.length === 0) return;
    if (!ws || ws.readyState !== WebSocket.OPEN) return;

    const chunks = pcmBuffer.splice(0);
    const totalSamples = chunks.reduce((sum, c) => sum + c.length, 0);
    if (totalSamples === 0) return;

    let merged = new Float32Array(totalSamples);
    let offset = 0;
    for (const chunk of chunks) {
        merged.set(chunk, offset);
        offset += chunk.length;
    }

    const overlapSamples = Math.round((settings.overlap_ms / 1000) * SAMPLE_RATE);

    let sendSamples;
    if (overlapBuffer && overlapBuffer.length > 0) {
        sendSamples = new Float32Array(overlapBuffer.length + merged.length);
        sendSamples.set(overlapBuffer, 0);
        sendSamples.set(merged, overlapBuffer.length);
    } else {
        sendSamples = merged;
    }

    // Save tail for next chunk overlap
    if (overlapSamples > 0 && merged.length >= overlapSamples) {
        overlapBuffer = new Float32Array(merged.subarray(merged.length - overlapSamples));
    } else {
        overlapBuffer = null;
    }

    const wavBuffer = encodeWav(sendSamples, SAMPLE_RATE);
    const base64 = arrayBufferToBase64(wavBuffer);

    ws.send(
        JSON.stringify({
            type: "audio_chunk",
            audio_data: base64,
            timestamp: Date.now(),
        })
    );
    console.log(`[Audio] Sent chunk: ${sendSamples.length} samples (${(sendSamples.length / SAMPLE_RATE).toFixed(1)}s)`);
}

function encodeWav(samples, sampleRate) {
    const numChannels = 1;
    const bytesPerSample = 2;
    const blockAlign = numChannels * bytesPerSample;
    const dataSize = samples.length * blockAlign;
    const headerSize = 44;

    const buffer = new ArrayBuffer(headerSize + dataSize);
    const view = new DataView(buffer);

    writeString(view, 0, "RIFF");
    view.setUint32(4, 36 + dataSize, true);
    writeString(view, 8, "WAVE");
    writeString(view, 12, "fmt ");
    view.setUint32(16, 16, true);
    view.setUint16(20, 1, true);
    view.setUint16(22, numChannels, true);
    view.setUint32(24, sampleRate, true);
    view.setUint32(28, sampleRate * blockAlign, true);
    view.setUint16(32, blockAlign, true);
    view.setUint16(34, bytesPerSample * 8, true);
    writeString(view, 36, "data");
    view.setUint32(40, dataSize, true);

    let offset = 44;
    for (let i = 0; i < samples.length; i++) {
        const s = Math.max(-1, Math.min(1, samples[i]));
        view.setInt16(offset, s < 0 ? s * 0x8000 : s * 0x7fff, true);
        offset += 2;
    }

    return buffer;
}

function writeString(view, offset, string) {
    for (let i = 0; i < string.length; i++) {
        view.setUint8(offset + i, string.charCodeAt(i));
    }
}

function arrayBufferToBase64(buffer) {
    const bytes = new Uint8Array(buffer);
    let binary = "";
    for (let i = 0; i < bytes.byteLength; i++) {
        binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary);
}

function appendSubtitle(rus, eng) {
    const entry = document.createElement("div");
    entry.className = "subtitle-entry";

    const rusLine = document.createElement("div");
    rusLine.className = "subtitle-rus";
    rusLine.textContent = rus || "(тишина)";

    const engLine = document.createElement("div");
    engLine.className = "subtitle-eng";
    engLine.textContent = eng || "(silence)";

    entry.appendChild(rusLine);
    entry.appendChild(engLine);
    subtitleLog.appendChild(entry);
    subtitleLog.scrollTop = subtitleLog.scrollHeight;
}

// --- Microphone device enumeration ---
async function loadMicrophones() {
    try {
        const tempStream = await navigator.mediaDevices.getUserMedia({ audio: true });
        tempStream.getTracks().forEach((t) => t.stop());

        const devices = await navigator.mediaDevices.enumerateDevices();
        const audioInputs = devices.filter((d) => d.kind === "audioinput");

        micDevice.innerHTML = "";
        audioInputs.forEach((device, i) => {
            const option = document.createElement("option");
            option.value = device.deviceId;
            option.text = device.label || `Микрофон ${i + 1}`;
            micDevice.appendChild(option);
        });
    } catch (err) {
        console.warn("Could not enumerate devices:", err);
    }
}

sourceType.addEventListener("change", () => {
    if (sourceType.value === "microphone") {
        loadMicrophones();
    }
});

// --- Settings: load / save ---
async function loadSettings() {
    try {
        const response = await fetch("/api/settings");
        if (!response.ok) throw new Error("Failed to load settings");
        settings = await response.json();

        // Populate model dropdown
        sttModel.innerHTML = "";
        ["tiny", "base", "small", "medium", "large"].forEach((name) => {
            const option = document.createElement("option");
            option.value = name;
            option.textContent = name;
            if (name === settings.model) option.selected = true;
            sttModel.appendChild(option);
        });

        vadEnabled.checked = settings.vad_enabled;
        dedupEnabled.checked = settings.dedup_enabled;
        chunkDuration.value = settings.chunk_duration_ms / 1000;
        overlapDuration.value = settings.overlap_ms / 1000;

        overlayFontSize.value = settings.overlay_font_size;
        overlayFontSizeValue.textContent = settings.overlay_font_size;
        overlayFontColor.value = settings.overlay_font_color;
        overlayBgOpacity.value = Math.round(settings.overlay_bg_opacity * 100);
        overlayBgOpacityValue.textContent = Math.round(settings.overlay_bg_opacity * 100);
        overlayDisplayDuration.value = settings.overlay_display_duration_ms / 1000;
        overlayShowTranslation.checked = settings.overlay_show_translation;
        overlayShowOriginal.checked = settings.overlay_show_original;
        overlayTranslationFontSize.value = settings.overlay_translation_font_size;
        overlayTranslationFontSizeValue.textContent = settings.overlay_translation_font_size;
        overlayTranslationBgOpacity.value = Math.round(settings.overlay_translation_bg_opacity * 100);
        overlayTranslationBgOpacityValue.textContent = Math.round(settings.overlay_translation_bg_opacity * 100);
        overlayTranslationFontColor.value = settings.overlay_translation_font_color;
        obsUrl.textContent = `${location.origin}/overlay.html`;

        enableSettingsInputs(true);
    } catch (err) {
        console.warn("Could not load settings:", err);
        sttModel.innerHTML = '<option value="">Ошибка загрузки</option>';
    }
}

async function saveSettings(patch) {
    try {
        const response = await fetch("/api/settings", {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(patch),
        });

        if (!response.ok) throw new Error(`Server error: ${response.status}`);
        settings = await response.json();
    } catch (err) {
        showError(`Ошибка сохранения настроек: ${err.message}`);
    }
}

function enableSettingsInputs(enabled) {
    sttModel.disabled = !enabled;
    vadEnabled.disabled = !enabled;
    dedupEnabled.disabled = !enabled;
    chunkDuration.disabled = !enabled;
    overlapDuration.disabled = !enabled;
    overlayFontSize.disabled = !enabled;
    overlayFontColor.disabled = !enabled;
    overlayBgOpacity.disabled = !enabled;
    overlayDisplayDuration.disabled = !enabled;
    overlayShowTranslation.disabled = !enabled;
    overlayShowOriginal.disabled = !enabled;
    overlayTranslationFontSize.disabled = !enabled;
    overlayTranslationBgOpacity.disabled = !enabled;
    overlayTranslationFontColor.disabled = !enabled;
}

sttModel.addEventListener("change", async () => {
    const model = sttModel.value;
    if (!model) return;
    enableSettingsInputs(false);
    setStatus("processing", `Загрузка модели ${model}...`);
    await saveSettings({ model });
    setStatus("success", `Модель: ${model}`);
    enableSettingsInputs(true);
});

vadEnabled.addEventListener("change", async () => {
    await saveSettings({ vad_enabled: vadEnabled.checked });
});

dedupEnabled.addEventListener("change", async () => {
    await saveSettings({ dedup_enabled: dedupEnabled.checked });
});

chunkDuration.addEventListener("change", async () => {
    const val = parseFloat(chunkDuration.value);
    if (val >= 1 && val <= 10) {
        await saveSettings({ chunk_duration_ms: Math.round(val * 1000) });
    }
});

overlapDuration.addEventListener("change", async () => {
    const val = parseFloat(overlapDuration.value);
    if (val >= 0 && val <= 3) {
        await saveSettings({ overlap_ms: Math.round(val * 1000) });
    }
});

// --- Overlay settings ---
overlayFontSize.addEventListener("input", () => {
    overlayFontSizeValue.textContent = overlayFontSize.value;
});

overlayFontSize.addEventListener("change", async () => {
    const val = parseInt(overlayFontSize.value);
    if (val >= 12 && val <= 48) {
        await saveSettings({ overlay_font_size: val });
    }
});

overlayFontColor.addEventListener("change", async () => {
    await saveSettings({ overlay_font_color: overlayFontColor.value });
});

overlayBgOpacity.addEventListener("input", () => {
    overlayBgOpacityValue.textContent = overlayBgOpacity.value;
});

overlayBgOpacity.addEventListener("change", async () => {
    const val = parseInt(overlayBgOpacity.value) / 100;
    if (val >= 0 && val <= 1) {
        await saveSettings({ overlay_bg_opacity: val });
    }
});

overlayDisplayDuration.addEventListener("change", async () => {
    const val = parseFloat(overlayDisplayDuration.value);
    if (val >= 1 && val <= 15) {
        await saveSettings({ overlay_display_duration_ms: Math.round(val * 1000) });
    }
});

overlayShowTranslation.addEventListener("change", async () => {
    await saveSettings({ overlay_show_translation: overlayShowTranslation.checked });
});

overlayShowOriginal.addEventListener("change", async () => {
    await saveSettings({ overlay_show_original: overlayShowOriginal.checked });
});

overlayTranslationFontSize.addEventListener("input", () => {
    overlayTranslationFontSizeValue.textContent = overlayTranslationFontSize.value;
});

overlayTranslationFontSize.addEventListener("change", async () => {
    const val = parseInt(overlayTranslationFontSize.value);
    if (val >= 10 && val <= 40) {
        await saveSettings({ overlay_translation_font_size: val });
    }
});

overlayTranslationBgOpacity.addEventListener("input", () => {
    overlayTranslationBgOpacityValue.textContent = overlayTranslationBgOpacity.value;
});

overlayTranslationBgOpacity.addEventListener("change", async () => {
    const val = parseInt(overlayTranslationBgOpacity.value) / 100;
    if (val >= 0 && val <= 1) {
        await saveSettings({ overlay_translation_bg_opacity: val });
    }
});

overlayTranslationFontColor.addEventListener("change", async () => {
    await saveSettings({ overlay_translation_font_color: overlayTranslationFontColor.value });
});

copyObsUrl.addEventListener("click", () => {
    const url = obsUrl.textContent;
    navigator.clipboard.writeText(url).then(() => {
        copyObsUrl.textContent = "Скопировано!";
        setTimeout(() => { copyObsUrl.textContent = "Копировать"; }, 2000);
    });
});

loadVersions();
loadSettings();

// --- Load versions ---
async function loadVersions() {
    try {
        const response = await fetch("/api/version");
        if (!response.ok) throw new Error("Failed to load versions");
        const data = await response.json();
        backendVersion.textContent = data.backend || "unknown";
        mlVersion.textContent = data.ml || "unknown";
    } catch (err) {
        console.warn("Could not load versions:", err);
        backendVersion.textContent = "error";
        mlVersion.textContent = "error";
    }
}

// --- Helpers ---
function setStatus(type, text) {
    statusBlock.textContent = text;
    statusBlock.className = `status status-${type}`;
    showElement(statusBlock);
}

function showError(message) {
    errorBlock.textContent = message;
    showElement(errorBlock);
    setStatus("error", "Ошибка");
}

function showElement(el) {
    el.hidden = false;
}

function hideElement(el) {
    el.hidden = true;
}
