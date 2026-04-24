"""
Cloud Theory — RVC Voice Conversion Service
============================================
A local FastAPI server that converts text to a singing character voice:
  text  →  edge-tts (base speech)  →  RVC (voice conversion)  →  WAV

Unity sends a POST /speak request; the WAV response is played through
RVCVoiceConverter.cs, which has AutotuneFilter on the same GameObject
for per-word pitch correction against the current cloud chord.

Run:
    python rvc_service.py

The server binds to 127.0.0.1:8765 by default.
"""

import asyncio
import io
import logging
import os
import tempfile

import edge_tts
import numpy as np
import soundfile as sf
import uvicorn
from fastapi import FastAPI, HTTPException
from fastapi.responses import Response
from pydub import AudioSegment
from pydantic import BaseModel

# ── Logging ────────────────────────────────────────────────────────────────
logging.basicConfig(level=logging.INFO, format="%(levelname)s  %(message)s")
log = logging.getLogger("rvc_service")

# ── RVC model paths ────────────────────────────────────────────────────────
_DIR   = os.path.dirname(os.path.abspath(__file__))
MODEL  = os.path.join(_DIR, "models", "voice.pth")
INDEX  = os.path.join(_DIR, "models", "voice.index")

# ── App and model state ────────────────────────────────────────────────────
app = FastAPI(title="Cloud Theory RVC Service")
_rvc = None   # set during startup if a model file is present


# ── Startup ────────────────────────────────────────────────────────────────
@app.on_event("startup")
async def _startup():
    global _rvc
    if not os.path.exists(MODEL):
        log.warning(
            "No RVC model found at %s.\n"
            "  Running in TTS-only mode — place voice.pth in Python/models/ "
            "to enable voice conversion.",
            MODEL,
        )
        return

    try:
        from rvc_python.infer import RVCInference
        _rvc = RVCInference(device="cpu")   # swap to "cuda:0" for GPU
        index_path = INDEX if os.path.exists(INDEX) else None
        _rvc.load_model(MODEL, index_path)
        log.info("RVC model loaded: %s", MODEL)
        if index_path:
            log.info("RVC index  loaded: %s", index_path)
    except Exception as exc:
        log.error("Failed to load RVC model: %s", exc)
        _rvc = None


# ── Request schema ─────────────────────────────────────────────────────────
class SpeakRequest(BaseModel):
    text:         str
    voice:        str = "en-US-AriaNeural"   # edge-tts voice name
    rate:         str = "+0%"                # speech rate  e.g. "+10%"
    pitch:        str = "+0Hz"               # base pitch   e.g. "+5Hz"
    f0_up_key:    int = 0                    # RVC semitone shift (0 = no shift)
    f0_method:    str = "rmvpe"              # RVC pitch algorithm


# ── /speak ─────────────────────────────────────────────────────────────────
@app.post("/speak", response_class=Response)
async def speak(req: SpeakRequest):
    """Convert text to a (optionally RVC-converted) WAV and return it."""

    # 1. Generate base speech via edge-tts (produces MP3 chunks).
    mp3_chunks: list[bytes] = []
    try:
        communicate = edge_tts.Communicate(req.text, req.voice,
                                           rate=req.rate, pitch=req.pitch)
        async for chunk in communicate.stream():
            if chunk["type"] == "audio":
                mp3_chunks.append(chunk["data"])
    except Exception as exc:
        raise HTTPException(status_code=502, detail=f"edge-tts error: {exc}")

    if not mp3_chunks:
        raise HTTPException(status_code=500, detail="edge-tts produced no audio.")

    # 2. Decode MP3 → mono float32 numpy array via pydub.
    mp3_bytes = b"".join(mp3_chunks)
    try:
        seg = AudioSegment.from_mp3(io.BytesIO(mp3_bytes))
        seg = seg.set_channels(1)          # force mono
        samples = np.array(seg.get_array_of_samples(), dtype=np.float32)
        samples /= 2 ** (seg.sample_width * 8 - 1)   # normalise to [-1, 1]
        sample_rate = seg.frame_rate
    except Exception as exc:
        raise HTTPException(status_code=500,
                            detail=f"MP3 decode failed (is ffmpeg on PATH?): {exc}")

    # 3. Run RVC if a model is loaded, otherwise pass through raw TTS audio.
    if _rvc is not None:
        samples, sample_rate = _run_rvc(samples, sample_rate, req)

    # 4. Encode float32 numpy array → WAV bytes.
    wav_buf = io.BytesIO()
    sf.write(wav_buf, samples, sample_rate, format="WAV", subtype="FLOAT")
    wav_buf.seek(0)

    return Response(content=wav_buf.read(), media_type="audio/wav")


def _run_rvc(samples: np.ndarray, sr: int, req: SpeakRequest):
    """
    Run RVC inference.  Works with rvc-python's file-based API by writing
    a temp WAV, calling infer_file, then reading the result back.
    Falls back to a numpy-based call if the package supports it.
    """
    try:
        # Try numpy API first (some rvc-python builds expose this).
        out = _rvc.infer_audio(samples, sr,
                               f0_up_key=req.f0_up_key,
                               f0_method=req.f0_method)
        return out, sr
    except (AttributeError, TypeError):
        pass  # fall through to file-based approach

    # File-based fallback — universally supported.
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp_in, \
         tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp_out:
        in_path  = tmp_in.name
        out_path = tmp_out.name

    try:
        sf.write(in_path, samples, sr, subtype="FLOAT")
        _rvc.infer_file(
            in_path, out_path,
            f0_up_key=req.f0_up_key,
            f0_method=req.f0_method,
        )
        out_samples, out_sr = sf.read(out_path, dtype="float32")
        if out_samples.ndim > 1:
            out_samples = out_samples.mean(axis=1)
        return out_samples, out_sr
    except Exception as exc:
        log.error("RVC inference failed, returning raw TTS audio: %s", exc)
        return samples, sr
    finally:
        for p in (in_path, out_path):
            try:
                os.unlink(p)
            except OSError:
                pass


# ── /status ────────────────────────────────────────────────────────────────
@app.get("/status")
async def status():
    return {
        "rvc_loaded":   _rvc is not None,
        "model_path":   MODEL,
        "index_path":   INDEX,
        "model_exists": os.path.exists(MODEL),
        "index_exists": os.path.exists(INDEX),
    }


# ── Entry point ────────────────────────────────────────────────────────────
if __name__ == "__main__":
    uvicorn.run("rvc_service:app", host="127.0.0.1", port=8765, reload=False)
