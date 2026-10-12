"""Generates Lantern Keeper's original sound effects, ambience loops and music.

Everything this script writes is original work synthesised from sine waves and noise
(no samples), so it carries no third-party license. docs/CREDITS.md lists each file as
"Original, generated for this project".

Run from the Unity project folder ("My project"):
    python Tools/generate_audio.py
It overwrites the .wav files under Assets/_Project/Audio/{SFX,Ambience,Music}. Unity keeps
each file's .meta (and so its GUID), so nothing needs re-wiring afterwards.
Needs numpy and scipy.
"""
import os
import sys

import numpy as np
from scipy import signal
from scipy.io import wavfile

SR = 44100
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "_Project", "Audio")
rng = np.random.default_rng(1031)  # fixed seed: the same files every run


# ----------------------------------------------------------------------------- helpers

def t_axis(seconds, sr=SR):
    return np.arange(int(round(seconds * sr))) / sr


def env_adsr(n, a, d, s, r, sr=SR):
    """Attack/decay/sustain/release envelope of n samples (times in seconds, s = level)."""
    a_n, d_n, r_n = int(round(a * sr)), int(round(d * sr)), int(round(r * sr))
    s_n = max(0, n - a_n - d_n - r_n)
    parts = [np.linspace(0, 1, a_n, endpoint=False), np.linspace(1, s, d_n, endpoint=False),
             np.full(s_n, s), np.linspace(s, 0, r_n)]
    out = np.concatenate(parts)
    return np.pad(out, (0, max(0, n - len(out))))[:n]


def decay(n, half_life, sr=SR):
    return 0.5 ** (np.arange(n) / (half_life * sr))


def fade(x, a=0.005, r=0.02, sr=SR):
    x = x.copy()
    a_n, r_n = min(len(x), int(round(a * sr))), min(len(x), int(round(r * sr)))
    if a_n: x[:a_n] *= np.linspace(0, 1, a_n)
    if r_n: x[-r_n:] *= np.linspace(1, 0, r_n)
    return x


def tone(freq, seconds, sr=SR):
    """Sine with a constant or per-sample frequency."""
    n = int(round(seconds * sr))
    f = np.full(n, freq, dtype=float) if np.isscalar(freq) else np.asarray(freq, dtype=float)[:n]
    return np.sin(2 * np.pi * np.cumsum(f) / sr)


def glide(f0, f1, seconds, curve=1.0, sr=SR):
    n = int(round(seconds * sr))
    return f0 + (f1 - f0) * np.linspace(0, 1, n) ** curve


def noise(seconds, sr=SR):
    return rng.standard_normal(int(round(seconds * sr)))


def bandpass(x, lo, hi, sr=SR, order=2):
    sos = signal.butter(order, [lo, hi], btype="band", fs=sr, output="sos")
    return signal.sosfilt(sos, x)


def lowpass(x, cutoff, sr=SR, order=2):
    return signal.sosfilt(signal.butter(order, cutoff, btype="low", fs=sr, output="sos"), x)


def highpass(x, cutoff, sr=SR, order=2):
    return signal.sosfilt(signal.butter(order, cutoff, btype="high", fs=sr, output="sos"), x)


def shaped_noise(n, shape, sr=SR):
    """Periodic noise: random phases with a magnitude curve shape(freq_hz). Loops seamlessly."""
    freqs = np.fft.rfftfreq(n, 1 / sr)
    spectrum = shape(freqs) * np.exp(1j * rng.uniform(0, 2 * np.pi, len(freqs)))
    spectrum[0] = 0
    x = np.fft.irfft(spectrum, n)
    return x / (np.max(np.abs(x)) + 1e-9)


def band(freqs, lo, hi, slope=2.0):
    """Smooth band-pass magnitude curve for shaped_noise."""
    f = np.maximum(freqs, 1e-3)
    return 1 / (1 + (lo / f) ** (2 * slope)) / (1 + (f / hi) ** (2 * slope))


def circular_reverb(x, seconds=1.6, mix=0.3, sr=SR):
    """Reverb whose tail wraps round the end of the buffer, so a loop stays seamless."""
    n = len(x)
    ir_n = min(n, int(round(seconds * sr)))
    ir = rng.standard_normal(ir_n) * decay(ir_n, seconds / 6, sr)
    ir = lowpass(ir, 4500, sr)
    ir /= np.sqrt(np.sum(ir ** 2))
    wet = np.fft.irfft(np.fft.rfft(x) * np.fft.rfft(np.pad(ir, (0, n - ir_n))), n)
    return x * (1 - mix) + wet * mix


def reverb(x, seconds=1.2, mix=0.25, sr=SR):
    """Ordinary reverb for one-shots: the buffer grows by the tail."""
    ir_n = int(round(seconds * sr))
    ir = rng.standard_normal(ir_n) * decay(ir_n, seconds / 6, sr)
    ir = lowpass(ir, 5000, sr)
    ir /= np.sqrt(np.sum(ir ** 2))
    wet = signal.fftconvolve(x, ir)
    dry = np.pad(x, (0, len(wet) - len(x)))
    return dry * (1 - mix) + wet * mix


def place(buffer, x, at_seconds, gain=1.0, sr=SR, wrap=False):
    """Adds x into buffer at a time. With wrap, anything past the end comes back in at the start."""
    start = int(round(at_seconds * sr))
    if wrap:
        idx = (start + np.arange(len(x))) % len(buffer)
        np.add.at(buffer, idx, x * gain)
    else:
        end = min(len(buffer), start + len(x))
        if end > start: buffer[start:end] += x[:end - start] * gain


def normalise(x, peak=0.89):
    return x * (peak / (np.max(np.abs(x)) + 1e-9))


def trim_tail(x, threshold=0.004, sr=SR):
    """Cuts the inaudible end of a one-shot's reverb tail and fades the last 30 ms."""
    level = np.abs(x) / (np.max(np.abs(x)) + 1e-9)
    loud = np.nonzero(level > threshold)[0]
    if len(loud) == 0: return x
    return fade(x[:min(len(x), loud[-1] + int(round(0.03 * sr)))], 0.0, 0.03, sr)


def save(folder, name, x, sr=SR, peak=0.89):
    path = os.path.join(ROOT, folder)
    os.makedirs(path, exist_ok=True)
    data = np.clip(normalise(x, peak), -1, 1)
    wavfile.write(os.path.join(path, name + ".wav"), sr, (data * 32767).astype(np.int16))
    seconds = len(x) / sr
    print(f"  {folder}/{name}.wav  {seconds:5.2f} s  {'stereo' if x.ndim == 2 else 'mono'}")


def midi(note):
    return 440.0 * 2 ** ((note - 69) / 12)


def bell(freq, seconds, bright=1.0):
    """Music-box / celesta note: a few inharmonic partials with fast-decaying highs."""
    n = int(round(seconds * SR))
    out = np.zeros(n)
    for ratio, gain, life in ((1, 1.0, 0.45), (2.0, 0.45, 0.25), (3.01, 0.28 * bright, 0.12), (5.4, 0.16 * bright, 0.05)):
        out += gain * tone(freq * ratio, seconds) * decay(n, life)
    return fade(out, 0.002, 0.03)


def voice(f0, seconds, formants, vibrato=5.5, depth=0.012, breath=0.05):
    """A sung-ish tone: a buzzy source pushed through vowel formants. f0 may be an array."""
    n = int(round(seconds * SR))
    t = np.arange(n) / SR
    f = (np.full(n, f0, dtype=float) if np.isscalar(f0) else np.asarray(f0, dtype=float)[:n])
    f = f * (1 + depth * np.sin(2 * np.pi * vibrato * t))
    phase = 2 * np.pi * np.cumsum(f) / SR
    source = sum(np.sin(k * phase) / k for k in range(1, 14))
    source += breath * rng.standard_normal(n)
    out = np.zeros(n)
    for centre, width, gain in formants:
        out += gain * bandpass(source, max(60, centre - width), centre + width)
    return out


OOH = ((320, 90, 1.0), (800, 120, 0.35), (2300, 250, 0.05))
AAH = ((700, 130, 1.0), (1150, 150, 0.5), (2600, 300, 0.1))
EEE = ((300, 80, 1.0), (2200, 250, 0.3), (3000, 300, 0.1))


# ----------------------------------------------------------------------------- sound effects

def sfx_candle_low():
    # Two falling soft tones and a flutter: "your flame is guttering".
    out = np.zeros(int(round(0.95 * SR)))
    for i, (note, at) in enumerate(((74, 0.0), (69, 0.26))):
        n = int(round(0.5 * SR))
        x = (tone(midi(note), 0.5) + 0.3 * tone(midi(note) * 2, 0.5)) * decay(n, 0.12)
        x *= 1 + 0.5 * np.sin(2 * np.pi * 17 * np.arange(n) / SR)
        place(out, fade(x), at, 0.8)
    return reverb(out, 0.8, 0.2)


def sfx_extinguish():
    # A puff, then the hiss of a wick going out.
    n = int(round(1.1 * SR))
    hiss = highpass(noise(1.1), 2500) * decay(n, 0.16)
    puff = lowpass(noise(1.1), 500) * env_adsr(n, 0.01, 0.12, 0.0, 0.01) * 2.5
    down = tone(glide(520, 120, 1.1, 0.5), 1.1) * decay(n, 0.1) * 0.5
    return fade(hiss * 0.7 + puff + down, 0.002, 0.15)


def sfx_respawn():
    # A match strike, then a warm rising shimmer as the lantern relights.
    out = np.zeros(int(round(1.5 * SR)))
    strike_n = int(round(0.22 * SR))
    strike = bandpass(noise(0.22), 2500, 9000) * env_adsr(strike_n, 0.01, 0.1, 0.2, 0.08)
    place(out, strike, 0.0, 0.6)
    for i, note in enumerate((62, 66, 69, 74)):
        place(out, bell(midi(note), 0.9), 0.2 + i * 0.11, 0.55)
    whoosh_n = int(round(0.8 * SR))
    whoosh = bandpass(noise(0.8), 300, 1800) * env_adsr(whoosh_n, 0.35, 0.2, 0.2, 0.2)
    place(out, whoosh, 0.12, 0.25)
    return reverb(out, 1.0, 0.25)


def sfx_flare_start():
    n = int(round(0.5 * SR))
    whoosh = bandpass(noise(0.5), 400, 5000) * env_adsr(n, 0.12, 0.2, 0.3, 0.15)
    rise = tone(glide(180, 720, 0.5, 0.6), 0.5) * env_adsr(n, 0.05, 0.25, 0.2, 0.15) * 0.5
    sparkle = tone(glide(1400, 2600, 0.5), 0.5) * decay(n, 0.08) * 0.15
    return fade(whoosh + rise + sparkle, 0.004, 0.08)


def sfx_flare_stop():
    n = int(round(0.45 * SR))
    whoosh = bandpass(noise(0.45), 300, 3000) * env_adsr(n, 0.02, 0.2, 0.15, 0.2)
    fall = tone(glide(640, 150, 0.45, 0.7), 0.45) * decay(n, 0.12) * 0.5
    return fade(whoosh * 0.8 + fall, 0.004, 0.1)


def sfx_flare_loop():
    # A roaring flame that loops: shaped noise with slow, periodic crackle and swell.
    seconds = 2.0
    n = int(round(seconds * SR))
    t = np.arange(n) / SR
    roar = shaped_noise(n, lambda f: band(f, 120, 1400) * (1 + 0.0 * f))
    air = shaped_noise(n, lambda f: band(f, 1800, 7000)) * 0.35
    swell = 1 + 0.18 * np.sin(2 * np.pi * 1.0 * t) + 0.1 * np.sin(2 * np.pi * 3.5 * t + 1.0)
    hum = 0.2 * np.sin(2 * np.pi * 110 * t) * (1 + 0.3 * np.sin(2 * np.pi * 7 * t))
    return (roar + air) * swell + hum


def sfx_platform_appear():
    # A glassy two-note chime going up.
    out = np.zeros(int(round(0.5 * SR)))
    place(out, bell(midi(86), 0.4, 1.4), 0.0, 0.6)
    place(out, bell(midi(93), 0.42, 1.4), 0.06, 0.6)
    shimmer = highpass(noise(0.3), 6000) * decay(int(round(0.3 * SR)), 0.04)
    place(out, shimmer, 0.0, 0.08)
    return reverb(out, 0.6, 0.3)


def sfx_platform_vanish():
    out = np.zeros(int(round(0.45 * SR)))
    place(out, bell(midi(88), 0.35, 1.2), 0.0, 0.5)
    place(out, bell(midi(81), 0.4, 1.2), 0.07, 0.55)
    n = int(round(0.4 * SR))
    fall = tone(glide(1900, 700, 0.4), 0.4) * decay(n, 0.06) * 0.2
    place(out, fall, 0.0)
    return reverb(out, 0.5, 0.3)


def sfx_ghost_notice():
    # "Ooh?" - a short rising question.
    seconds = 0.55
    n = int(round(seconds * SR))
    x = voice(glide(300, 560, seconds, 0.6), seconds, OOH, vibrato=7, depth=0.02)
    return reverb(fade(x * env_adsr(n, 0.04, 0.1, 0.8, 0.2), 0.01, 0.1), 0.9, 0.3)


def sfx_ghost_chase():
    # A longer wail that swoops up and wobbles.
    seconds = 1.25
    n = int(round(seconds * SR))
    pitch = np.concatenate([glide(260, 620, 0.35, 0.7), glide(620, 520, seconds - 0.35, 1.0)])
    x = voice(pitch, seconds, AAH, vibrato=8.5, depth=0.035, breath=0.08)
    return reverb(fade(x * env_adsr(n, 0.05, 0.2, 0.75, 0.4), 0.01, 0.2), 1.3, 0.35)


def sfx_ghost_repel():
    # A startled yelp falling away, with a rush of air.
    seconds = 0.8
    n = int(round(seconds * SR))
    x = voice(glide(760, 240, seconds, 0.5), seconds, EEE, vibrato=11, depth=0.04, breath=0.1)
    x *= env_adsr(n, 0.01, 0.15, 0.6, 0.35)
    whoosh = bandpass(noise(seconds), 500, 4500) * env_adsr(n, 0.02, 0.25, 0.1, 0.3) * 0.5
    return reverb(fade(x + whoosh, 0.005, 0.2), 1.0, 0.3)


def sfx_ghost_eat():
    # Happy munching: five little "nom"s.
    out = np.zeros(int(round(1.5 * SR)))
    for i in range(5):
        seconds = 0.16
        n = int(round(seconds * SR))
        f0 = 210 + 25 * (i % 2)
        x = voice(glide(f0, f0 * 0.8, seconds), seconds, AAH if i % 2 == 0 else OOH, vibrato=0, depth=0)
        crunch = bandpass(noise(seconds), 1500, 6000) * decay(n, 0.02) * 0.25
        place(out, fade((x * env_adsr(n, 0.01, 0.05, 0.6, 0.06) + crunch), 0.004, 0.03), 0.05 + i * 0.26, 0.9)
    return reverb(out, 0.5, 0.15)


def sfx_ghost_hit():
    # A cold "boo" with a thump under it.
    seconds = 0.5
    n = int(round(seconds * SR))
    x = voice(glide(330, 150, seconds, 0.6), seconds, OOH, vibrato=14, depth=0.05, breath=0.12)
    thump = tone(glide(140, 45, 0.25), 0.25) * decay(int(round(0.25 * SR)), 0.05) * 1.3
    out = x * env_adsr(n, 0.005, 0.12, 0.5, 0.25)
    place(out, thump, 0.0)
    return reverb(fade(out, 0.002, 0.15), 0.8, 0.25)


def sfx_ghost_return():
    # A quiet, falling sigh: the ghost loses interest.
    seconds = 0.7
    n = int(round(seconds * SR))
    x = voice(glide(360, 230, seconds, 1.2), seconds, OOH, vibrato=4, depth=0.01, breath=0.2)
    return reverb(fade(x * env_adsr(n, 0.12, 0.2, 0.5, 0.3), 0.02, 0.25), 1.0, 0.35) * 0.7


def sfx_candy_throw():
    n = int(round(0.28 * SR))
    whoosh = bandpass(noise(0.28), 700, 5000) * env_adsr(n, 0.05, 0.1, 0.15, 0.1)
    pitch = tone(glide(500, 1100, 0.28, 0.7), 0.28) * env_adsr(n, 0.03, 0.12, 0.0, 0.05) * 0.2
    return fade(whoosh + pitch, 0.003, 0.05)


def sfx_candy_eaten():
    # A gulp and a pop.
    out = np.zeros(int(round(0.4 * SR)))
    gulp = tone(glide(180, 420, 0.14, 2.0), 0.14) * env_adsr(int(round(0.14 * SR)), 0.01, 0.06, 0.4, 0.05)
    pop = tone(glide(900, 1500, 0.05), 0.05) * decay(int(round(0.05 * SR)), 0.012)
    place(out, gulp, 0.0, 0.9)
    place(out, pop, 0.17, 0.7)
    return reverb(out, 0.3, 0.12)


def sfx_candle_pickup():
    # A warm rising arpeggio with a soft "fwoomp" of flame.
    out = np.zeros(int(round(0.9 * SR)))
    for i, note in enumerate((69, 74, 78, 81)):
        place(out, bell(midi(note), 0.6), i * 0.07, 0.5)
    fw_n = int(round(0.3 * SR))
    place(out, lowpass(noise(0.3), 900) * env_adsr(fw_n, 0.05, 0.15, 0.0, 0.05), 0.0, 0.5)
    return reverb(out, 0.7, 0.22)


def sfx_checkpoint_lit():
    # Strike, fwoomp, and a slow homely chord: a porch lantern catching.
    out = np.zeros(int(round(2.0 * SR)))
    strike_n = int(round(0.18 * SR))
    place(out, bandpass(noise(0.18), 3000, 9000) * env_adsr(strike_n, 0.01, 0.08, 0.2, 0.06), 0.0, 0.45)
    fw_n = int(round(0.5 * SR))
    place(out, lowpass(noise(0.5), 700) * env_adsr(fw_n, 0.08, 0.25, 0.0, 0.1), 0.12, 0.9)
    for i, note in enumerate((50, 62, 66, 69, 74)):
        place(out, bell(midi(note), 1.5, 0.7), 0.2 + i * 0.09, 0.45)
    return reverb(out, 1.4, 0.3)


def sfx_gate_open():
    # Iron: a latch clank, a rattle of bars, and a low grind as it sinks.
    seconds = 1.3
    out = np.zeros(int(round(seconds * SR)))
    for at, gain in ((0.0, 1.0), (0.09, 0.5), (0.2, 0.35), (0.34, 0.25)):
        n = int(round(0.3 * SR))
        clank = sum(tone(f, 0.3) * decay(n, life) for f, life in ((412, 0.05), (957, 0.035), (1630, 0.025), (2741, 0.015)))
        place(out, clank + bandpass(noise(0.3), 2000, 8000) * decay(n, 0.01) * 0.6, at, gain * 0.5)
    grind_n = int(round(1.0 * SR))
    grind = bandpass(noise(1.0), 60, 420) * env_adsr(grind_n, 0.15, 0.3, 0.6, 0.3)
    grind *= 1 + 0.6 * np.sign(np.sin(2 * np.pi * 23 * np.arange(grind_n) / SR))
    place(out, grind, 0.2, 0.9)
    return reverb(fade(out, 0.002, 0.2), 0.9, 0.2)


def sfx_gate_close():
    seconds = 1.1
    out = np.zeros(int(round(seconds * SR)))
    grind_n = int(round(0.7 * SR))
    grind = bandpass(noise(0.7), 60, 420) * env_adsr(grind_n, 0.1, 0.2, 0.6, 0.15)
    grind *= 1 + 0.6 * np.sign(np.sin(2 * np.pi * 19 * np.arange(grind_n) / SR))
    place(out, grind, 0.0, 0.8)
    n = int(round(0.4 * SR))
    clang = sum(tone(f, 0.4) * decay(n, life) for f, life in ((196, 0.09), (530, 0.06), (1211, 0.04), (2380, 0.02)))
    place(out, clang + lowpass(noise(0.4), 300) * decay(n, 0.03) * 1.5, 0.62, 0.9)
    return reverb(fade(out, 0.002, 0.1), 0.9, 0.22)


def sfx_win_jingle():
    # Home safe: a rising D major run that lands on a warm chord.
    out = np.zeros(int(round(3.6 * SR)))
    run = ((62, 0.0), (66, 0.16), (69, 0.32), (74, 0.48), (78, 0.64), (81, 0.8), (86, 1.04))
    for note, at in run:
        place(out, bell(midi(note), 1.4), at, 0.55)
    for note in (50, 57, 62, 66, 69, 74):
        place(out, bell(midi(note), 2.4, 0.6), 1.3, 0.4)
    pad_n = int(round(2.2 * SR))
    pad = sum(tone(midi(n), 2.2) for n in (50, 57, 62, 66)) * env_adsr(pad_n, 0.3, 0.4, 0.7, 1.0) * 0.12
    place(out, pad, 1.25)
    return reverb(out, 1.8, 0.3)


def sfx_prompt():
    # A soft single chime for tutorial prompts.
    out = np.zeros(int(round(0.6 * SR)))
    place(out, bell(midi(81), 0.5, 0.8), 0.0, 0.5)
    place(out, bell(midi(88), 0.5, 0.8), 0.05, 0.3)
    return reverb(out, 0.6, 0.25)


def sfx_sparkle():
    out = np.zeros(int(round(0.5 * SR)))
    for i, note in enumerate((93, 98, 105)):
        place(out, bell(midi(note), 0.3, 1.5), i * 0.045, 0.4)
    return reverb(out, 0.4, 0.25)


# ----------------------------------------------------------------------------- ambience (loops)

def amb_night(seconds=24.0, sr=22050):
    """Wind in the dark with crickets. Every modulation is periodic over the loop."""
    n = int(round(seconds * sr))
    t = np.arange(n) / sr
    low = shaped_noise(n, lambda f: band(f, 60, 500), sr)
    mid = shaped_noise(n, lambda f: band(f, 350, 1500), sr)
    gust = 0.55 + 0.3 * np.sin(2 * np.pi * t * 2 / seconds) + 0.15 * np.sin(2 * np.pi * t * 5 / seconds + 1.3)
    whistle = 0.5 + 0.5 * np.sin(2 * np.pi * t * 3 / seconds + 2.1)
    wind = low * gust * 0.9 + mid * gust * whistle * 0.35

    crickets = np.zeros(n)
    # (carrier Hz, chirps in the loop, pulses per chirp, gain, phase offset)
    for carrier, chirps, pulses, gain, offset in ((4300, 40, 3, 0.10, 0.0), (4750, 30, 4, 0.07, 0.31), (3900, 24, 2, 0.05, 0.57)):
        period = seconds / chirps
        for c in range(chirps):
            if rng.random() < 0.18: continue  # a cricket pauses now and then
            for p in range(pulses):
                pulse_n = int(round(0.018 * sr))
                pulse = np.sin(2 * np.pi * carrier * np.arange(pulse_n) / sr) * np.hanning(pulse_n)
                place(crickets, pulse, c * period + offset + p * 0.028, gain, sr, wrap=True)
    return wind + crickets


def amb_water(seconds=12.0, sr=22050):
    """A creek: steady rush plus little bubbling blips."""
    n = int(round(seconds * sr))
    t = np.arange(n) / sr
    rush = shaped_noise(n, lambda f: band(f, 500, 3200), sr)
    rush *= 0.75 + 0.15 * np.sin(2 * np.pi * t * 4 / seconds) + 0.1 * np.sin(2 * np.pi * t * 9 / seconds + 0.7)
    low = shaped_noise(n, lambda f: band(f, 120, 500), sr) * 0.35
    out = rush * 0.6 + low
    for _ in range(140):
        length = rng.uniform(0.03, 0.09)
        blip_n = int(round(length * sr))
        f0 = rng.uniform(500, 1500)
        f = f0 * (1 + rng.uniform(0.2, 0.8) * np.linspace(0, 1, blip_n))
        blip = np.sin(2 * np.pi * np.cumsum(f) / sr) * np.hanning(blip_n)
        place(out, blip, rng.uniform(0, seconds), rng.uniform(0.08, 0.25), sr, wrap=True)
    return out


def amb_choir(seconds=20.0, sr=22050):
    """Far-off voices holding a D minor chord, swelling in and out, with a breath of wind."""
    n = int(round(seconds * sr))
    t = np.arange(n) / sr
    out = np.zeros(n)
    # Each voice: whole number of cycles of vibrato and swell over the loop, so it wraps cleanly.
    for i, (note, swell_cycles) in enumerate(((50, 2), (57, 3), (62, 2), (65, 3), (69, 1))):
        f0 = round(midi(note) * seconds) / seconds  # whole number of cycles
        vib = 1 + 0.006 * np.sin(2 * np.pi * t * round(5.0 * seconds + i) / seconds)
        phase = 2 * np.pi * np.cumsum(f0 * vib) / sr
        phase *= (2 * np.pi * round(f0 * seconds)) / phase[-1] if phase[-1] > 0 else 1
        source = sum(np.sin(k * phase) / k for k in range(1, 9))
        spectrum = np.fft.rfft(source)
        freqs = np.fft.rfftfreq(n, 1 / sr)
        shape = np.exp(-((freqs - 330) / 160) ** 2) + 0.35 * np.exp(-((freqs - 820) / 200) ** 2)
        sung = np.fft.irfft(spectrum * shape, n)
        swell = 0.5 + 0.5 * np.sin(2 * np.pi * t * swell_cycles / seconds + i * 1.7)
        out += sung / (np.max(np.abs(sung)) + 1e-9) * swell ** 2 * (0.9 if i < 3 else 0.6)
    wind = shaped_noise(n, lambda f: band(f, 200, 900), sr) * (0.5 + 0.5 * np.sin(2 * np.pi * t * 2 / seconds))
    return circular_reverb(out + wind * 0.5, 2.5, 0.45, sr)


# ----------------------------------------------------------------------------- music (loops)

BPM = 72.0
BEAT = 60.0 / BPM
BARS = 8
LOOP_SECONDS = BARS * 4 * BEAT  # 26.67 s
MUSIC_SR = 32000

# D minor, one chord per bar. Each entry: bass note, chord tones (MIDI).
PROGRESSION = [
    (38, (62, 65, 69)),  # Dm
    (34, (58, 62, 65)),  # Bb
    (41, (60, 65, 69)),  # F
    (36, (60, 64, 67)),  # C
    (38, (62, 65, 69)),  # Dm
    (34, (58, 62, 65)),  # Bb
    (43, (58, 62, 67)),  # Gm
    (45, (61, 64, 69)),  # A
]


def music_bell(freq, seconds, bright=1.0):
    n = int(round(seconds * MUSIC_SR))
    t = np.arange(n) / MUSIC_SR
    out = np.zeros(n)
    for ratio, gain, life in ((1, 1.0, 0.5), (2.0, 0.4, 0.28), (3.01, 0.22 * bright, 0.12), (5.4, 0.1 * bright, 0.05)):
        out += gain * np.sin(2 * np.pi * freq * ratio * t) * 0.5 ** (t / life)
    return fade(out, 0.002, 0.03, MUSIC_SR)


def music_pad(notes, seconds, attack=0.9, release=1.2):
    n = int(round(seconds * MUSIC_SR))
    t = np.arange(n) / MUSIC_SR
    out = np.zeros(n)
    for i, note in enumerate(notes):
        f = midi(note)
        for detune in (-0.004, 0.0, 0.004):
            out += np.sin(2 * np.pi * f * (1 + detune) * t + i) + 0.3 * np.sin(2 * np.pi * 2 * f * (1 + detune) * t)
    out *= env_adsr(n, attack, 0.3, 0.85, release, MUSIC_SR)
    return out / (3 * len(notes))


def stereo(left, right):
    return np.stack([left, right], axis=1)


def finish_music(left, right, reverb_mix=0.3):
    left = circular_reverb(left, 2.2, reverb_mix, MUSIC_SR)
    right = circular_reverb(right, 2.2, reverb_mix, MUSIC_SR)
    return stereo(left, right)


def music_explore():
    """Calm: slow pad, a sparse music-box figure, a soft bass note each bar."""
    n = int(round(LOOP_SECONDS * MUSIC_SR))
    left, right = np.zeros(n), np.zeros(n)
    # Sparse arpeggio: (beat within the bar, chord tone index, octave shift)
    figure = ((0.0, 0, 12), (1.5, 1, 12), (2.0, 2, 12), (3.5, 1, 24))
    for bar, (bass, chord) in enumerate(PROGRESSION):
        start = bar * 4 * BEAT
        pad = music_pad(chord, 4 * BEAT + 1.0)
        place(left, pad, start, 0.5, MUSIC_SR, wrap=True)
        place(right, pad, start + 0.012, 0.5, MUSIC_SR, wrap=True)
        bass_n = int(round(3.2 * BEAT * MUSIC_SR))
        bass_t = np.arange(bass_n) / MUSIC_SR
        bass_x = (np.sin(2 * np.pi * midi(bass) * bass_t) + 0.25 * np.sin(2 * np.pi * 2 * midi(bass) * bass_t)) \
                 * env_adsr(bass_n, 0.04, 0.5, 0.5, 0.8, MUSIC_SR)
        place(left, bass_x, start, 0.3, MUSIC_SR, wrap=True)
        place(right, bass_x, start, 0.3, MUSIC_SR, wrap=True)
        for i, (beat, tone_index, octave) in enumerate(figure):
            if bar % 2 == 1 and i == 3: continue  # leave air in every other bar
            note = music_bell(midi(chord[tone_index] + octave), 2.2)
            pan = 0.35 + 0.3 * ((i + bar) % 2)
            place(left, note, start + beat * BEAT, 0.26 * (1 - pan), MUSIC_SR, wrap=True)
            place(right, note, start + beat * BEAT, 0.26 * pan, MUSIC_SR, wrap=True)
    return finish_music(left, right, 0.32)


def music_chase():
    """Tense layer over the same chords: pulsing low eighths, a heartbeat, ticking, string stabs."""
    n = int(round(LOOP_SECONDS * MUSIC_SR))
    left, right = np.zeros(n), np.zeros(n)
    eighth = BEAT / 2
    for bar, (bass, chord) in enumerate(PROGRESSION):
        start = bar * 4 * BEAT
        for step in range(8):
            at = start + step * eighth
            # Pulsing bass: root on the beat, an octave up off it.
            note = bass + (12 if step % 2 == 1 else 0)
            pulse_n = int(round(eighth * 0.9 * MUSIC_SR))
            pt = np.arange(pulse_n) / MUSIC_SR
            f = midi(note)
            pulse = (np.sin(2 * np.pi * f * pt) + 0.5 * np.sign(np.sin(2 * np.pi * f * pt)) * 0.4
                     + 0.3 * np.sin(2 * np.pi * 2 * f * pt)) * env_adsr(pulse_n, 0.005, 0.12, 0.35, 0.08, MUSIC_SR)
            pulse = lowpass(pulse, 900, MUSIC_SR)
            gain = 0.42 if step % 2 == 0 else 0.26
            place(left, pulse, at, gain, MUSIC_SR, wrap=True)
            place(right, pulse, at, gain, MUSIC_SR, wrap=True)
            # Ticking sixteenths, quiet.
            for sub in range(2):
                tick_n = int(round(0.03 * MUSIC_SR))
                tick = highpass(rng.standard_normal(tick_n), 6000, MUSIC_SR) * 0.5 ** (np.arange(tick_n) / (0.006 * MUSIC_SR))
                tick_gain = 0.09 if sub == 0 else 0.05
                place(left if (step + sub) % 2 else right, tick, at + sub * eighth / 2, tick_gain, MUSIC_SR, wrap=True)
        # Heartbeat kick on beats 1 and 3 (a double thump).
        for beat in (0, 2):
            for offset, gain in ((0.0, 0.6), (0.22, 0.4)):
                kick_n = int(round(0.22 * MUSIC_SR))
                kt = np.arange(kick_n) / MUSIC_SR
                kick = np.sin(2 * np.pi * np.cumsum(55 + 70 * 0.5 ** (kt / 0.03)) / MUSIC_SR) * 0.5 ** (kt / 0.06)
                place(left, kick, start + beat * BEAT + offset, gain, MUSIC_SR, wrap=True)
                place(right, kick, start + beat * BEAT + offset, gain, MUSIC_SR, wrap=True)
        # Tremolo string stab holding the chord, a semitone of dread on top in the last bar.
        tones = chord + ((chord[0] + 13,) if bar == BARS - 1 else ())
        stab_n = int(round(4 * BEAT * MUSIC_SR))
        st = np.arange(stab_n) / MUSIC_SR
        stab = np.zeros(stab_n)
        for note in tones:
            f = midi(note + 12)
            phase = 2 * np.pi * f * st
            stab += sum(np.sin(k * phase) / k for k in range(1, 7))
        stab = lowpass(stab, 2600, MUSIC_SR) * (0.6 + 0.4 * np.sin(2 * np.pi * 12 * st)) \
               * env_adsr(stab_n, 0.08, 0.4, 0.6, 0.5, MUSIC_SR) / len(tones)
        place(left, stab, start, 0.16, MUSIC_SR, wrap=True)
        place(right, stab, start + 0.009, 0.16, MUSIC_SR, wrap=True)
    return finish_music(left, right, 0.2)


def music_title():
    """Title: a music-box melody over the pad. Same key and tempo as the level music."""
    n = int(round(LOOP_SECONDS * MUSIC_SR))
    left, right = np.zeros(n), np.zeros(n)
    # (bar, beat, MIDI note, length in beats)
    melody = (
        (0, 0, 74, 1.5), (0, 1.5, 77, 0.5), (0, 2, 81, 2),
        (1, 0, 82, 1.5), (1, 1.5, 81, 0.5), (1, 2, 77, 2),
        (2, 0, 76, 1), (2, 1, 77, 1), (2, 2, 81, 1), (2, 3, 84, 1),
        (3, 0, 79, 3), (3, 3, 76, 1),
        (4, 0, 74, 1.5), (4, 1.5, 77, 0.5), (4, 2, 81, 2),
        (5, 0, 82, 1), (5, 1, 86, 1), (5, 2, 82, 2),
        (6, 0, 79, 1), (6, 1, 77, 1), (6, 2, 74, 1), (6, 3, 70, 1),
        (7, 0, 73, 2), (7, 2, 69, 2),
    )
    for bar, (bass, chord) in enumerate(PROGRESSION):
        start = bar * 4 * BEAT
        pad = music_pad(chord, 4 * BEAT + 1.0)
        place(left, pad, start, 0.42, MUSIC_SR, wrap=True)
        place(right, pad, start + 0.012, 0.42, MUSIC_SR, wrap=True)
        for beat in (0, 2):
            low = music_bell(midi(bass + 12), 1.8, 0.4)
            place(left, low, start + beat * BEAT, 0.22, MUSIC_SR, wrap=True)
            place(right, low, start + beat * BEAT, 0.22, MUSIC_SR, wrap=True)
        for beat, tone_index in ((1, 0), (3, 2)):
            mid = music_bell(midi(chord[tone_index]), 1.4, 0.6)
            place(left, mid, start + beat * BEAT, 0.12, MUSIC_SR, wrap=True)
            place(right, mid, start + beat * BEAT, 0.16, MUSIC_SR, wrap=True)
    for bar, beat, note, length in melody:
        x = music_bell(midi(note), max(1.2, length * BEAT + 0.8), 1.2)
        at = (bar * 4 + beat) * BEAT
        place(left, x, at, 0.34, MUSIC_SR, wrap=True)
        place(right, x, at + 0.004, 0.3, MUSIC_SR, wrap=True)
    return finish_music(left, right, 0.35)


# ----------------------------------------------------------------------------- main

def main():
    print("Sound effects")
    for name, make in (
        ("candle_low", sfx_candle_low), ("extinguish", sfx_extinguish), ("respawn", sfx_respawn),
        ("flare_start", sfx_flare_start), ("flare_loop", sfx_flare_loop), ("flare_stop", sfx_flare_stop),
        ("platform_appear", sfx_platform_appear), ("platform_vanish", sfx_platform_vanish),
        ("ghost_notice", sfx_ghost_notice), ("ghost_chase", sfx_ghost_chase), ("ghost_repel", sfx_ghost_repel),
        ("ghost_eat", sfx_ghost_eat), ("ghost_hit", sfx_ghost_hit), ("ghost_return", sfx_ghost_return),
        ("candy_throw", sfx_candy_throw), ("candy_eaten", sfx_candy_eaten), ("candle_pickup", sfx_candle_pickup),
        ("checkpoint_lit", sfx_checkpoint_lit), ("gate_open", sfx_gate_open), ("gate_close", sfx_gate_close),
        ("win_jingle", sfx_win_jingle), ("prompt_chime", sfx_prompt), ("pickup_sparkle", sfx_sparkle),
    ):
        x = make()
        save("SFX", name, x if name == "flare_loop" else trim_tail(x))

    print("Ambience")
    save("Ambience", "night_wind_crickets", amb_night(), 22050, 0.7)
    save("Ambience", "creek_water", amb_water(), 22050, 0.7)
    save("Ambience", "graveyard_choir", amb_choir(), 22050, 0.7)

    print("Music")
    save("Music", "music_explore", music_explore(), MUSIC_SR, 0.8)
    save("Music", "music_chase", music_chase(), MUSIC_SR, 0.8)
    save("Music", "music_title", music_title(), MUSIC_SR, 0.8)
    return 0


if __name__ == "__main__":
    sys.exit(main())
