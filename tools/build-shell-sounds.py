import argparse
import array
import math
import os
import random
import wave

SAMPLE_RATE = 48000
BIT_DEPTH_SCALE = 32767.0
BELL_ATTACK_SECONDS = 0.006

BELL_PARTIALS = (
    (1.00, 1.00, 1.00),
    (2.00, 0.42, 0.62),
    (2.99, 0.26, 0.44),
    (4.21, 0.15, 0.30),
    (5.43, 0.08, 0.21),
    (6.79, 0.04, 0.15),
)


def frame_count(seconds):
    return int(round(seconds * SAMPLE_RATE))


def buffer_of(seconds):
    return [0.0] * frame_count(seconds)


def smooth_step(position):
    clamped = min(1.0, max(0.0, position))
    return clamped * clamped * (3.0 - 2.0 * clamped)


def attack_curve(elapsed, attack_seconds):
    if elapsed >= attack_seconds:
        return 1.0
    return smooth_step(elapsed / attack_seconds)


def exponential_decay(elapsed, decay_seconds):
    return math.exp(-elapsed / decay_seconds)


def glide_frequency(position, start_hertz, end_hertz, curve):
    shaped = position ** curve
    return start_hertz * (end_hertz / start_hertz) ** shaped


def sweep_tone(seconds, start_hertz, end_hertz, curve=1.0, harmonic_gain=0.0):
    total = frame_count(seconds)
    phase = 0.0
    harmonic_phase = 0.0
    rendered = []
    for index in range(total):
        position = index / total
        frequency = glide_frequency(position, start_hertz, end_hertz, curve)
        phase += 2.0 * math.pi * frequency / SAMPLE_RATE
        harmonic_phase += 2.0 * math.pi * frequency * 2.0 / SAMPLE_RATE
        rendered.append(math.sin(phase) + harmonic_gain * math.sin(harmonic_phase))
    return rendered


def white_noise(seconds, seed):
    generator = random.Random(seed)
    return [generator.uniform(-1.0, 1.0) for _ in range(frame_count(seconds))]


def sweeping_lowpass(source, start_hertz, end_hertz, curve=1.0, poles=2):
    filtered = list(source)
    total = len(filtered)
    for _ in range(poles):
        state = 0.0
        for index in range(total):
            cutoff = glide_frequency(index / total, start_hertz, end_hertz, curve)
            coefficient = 1.0 - math.exp(-2.0 * math.pi * cutoff / SAMPLE_RATE)
            state += coefficient * (filtered[index] - state)
            filtered[index] = state
    return filtered


def highpass(source, cutoff_hertz):
    coefficient = 1.0 - math.exp(-2.0 * math.pi * cutoff_hertz / SAMPLE_RATE)
    state = 0.0
    filtered = []
    for sample in source:
        state += coefficient * (sample - state)
        filtered.append(sample - state)
    return filtered


def shape(source, attack_seconds, decay_seconds, hold_seconds=0.0):
    shaped = []
    for index, sample in enumerate(source):
        elapsed = index / SAMPLE_RATE
        envelope = attack_curve(elapsed, attack_seconds)
        if elapsed > attack_seconds + hold_seconds:
            envelope *= exponential_decay(elapsed - attack_seconds - hold_seconds, decay_seconds)
        shaped.append(sample * envelope)
    return shaped


def swell(source, peak_seconds, release_seconds):
    shaped = []
    for index, sample in enumerate(source):
        elapsed = index / SAMPLE_RATE
        if elapsed <= peak_seconds:
            envelope = smooth_step(elapsed / peak_seconds)
        else:
            envelope = 1.0 - smooth_step(min(1.0, (elapsed - peak_seconds) / release_seconds))
        shaped.append(sample * envelope)
    return shaped


def bell_voice(seconds, frequency, decay_seconds, detune_cents=0.0):
    total = frame_count(seconds)
    tuned = frequency * (2.0 ** (detune_cents / 1200.0))
    rendered = [0.0] * total
    for ratio, amplitude, decay_scale in BELL_PARTIALS:
        partial_frequency = tuned * ratio
        if partial_frequency >= SAMPLE_RATE * 0.45:
            continue
        step = 2.0 * math.pi * partial_frequency / SAMPLE_RATE
        partial_decay = decay_seconds * decay_scale
        for index in range(total):
            elapsed = index / SAMPLE_RATE
            envelope = attack_curve(elapsed, BELL_ATTACK_SECONDS) * exponential_decay(elapsed, partial_decay)
            if elapsed > BELL_ATTACK_SECONDS and envelope < 1e-5:
                break
            rendered[index] += amplitude * envelope * math.sin(step * index)
    return rendered


def mix_into(left, right, source, at_seconds, gain, pan=0.0, delay_seconds=0.0):
    left_gain = gain * math.cos((pan + 1.0) * math.pi / 4.0) * math.sqrt(2.0)
    right_gain = gain * math.sin((pan + 1.0) * math.pi / 4.0) * math.sqrt(2.0)
    start = frame_count(at_seconds)
    right_start = start + frame_count(delay_seconds)
    for index, sample in enumerate(source):
        target = start + index
        if 0 <= target < len(left):
            left[target] += sample * left_gain
        target = right_start + index
        if 0 <= target < len(right):
            right[target] += sample * right_gain


def fade_edges(channel, fade_in_seconds, fade_out_seconds):
    fade_in = frame_count(fade_in_seconds)
    fade_out = frame_count(fade_out_seconds)
    total = len(channel)
    for index in range(min(fade_in, total)):
        channel[index] *= smooth_step(index / fade_in)
    for index in range(min(fade_out, total)):
        channel[total - 1 - index] *= smooth_step(index / fade_out)


def normalize(left, right, target_peak):
    peak = 0.0
    for channel in (left, right):
        for sample in channel:
            peak = max(peak, abs(sample))
    if peak <= 0.0:
        return
    scale = target_peak / peak
    for channel in (left, right):
        for index in range(len(channel)):
            channel[index] *= scale


def write_stereo(path, left, right, target_peak, fade_out_seconds=0.03):
    fade_edges(left, 0.002, fade_out_seconds)
    fade_edges(right, 0.002, fade_out_seconds)
    normalize(left, right, target_peak)
    interleaved = array.array("h")
    for index in range(len(left)):
        interleaved.append(int(round(max(-1.0, min(1.0, left[index])) * BIT_DEPTH_SCALE)))
        interleaved.append(int(round(max(-1.0, min(1.0, right[index])) * BIT_DEPTH_SCALE)))
    with wave.open(path, "wb") as target:
        target.setnchannels(2)
        target.setsampwidth(2)
        target.setframerate(SAMPLE_RATE)
        target.writeframes(interleaved.tobytes())
    print("{0}: {1:.3f}s peak {2:.2f}".format(os.path.basename(path), len(left) / SAMPLE_RATE, target_peak))


def build_startup(path):
    duration = 1.70
    bloom_at = 0.45
    left = buffer_of(duration)
    right = buffer_of(duration)

    sub = shape(sweep_tone(1.3, 42.0, 96.0, curve=0.55), 0.28, 0.75, hold_seconds=0.22)
    mix_into(left, right, sub, 0.0, 0.55)

    riser = swell(sweeping_lowpass(white_noise(bloom_at + 0.14, 20260827), 320.0, 7200.0, curve=1.7), bloom_at, 0.14)
    mix_into(left, right, riser, 0.0, 0.16, pan=-0.25)
    mix_into(left, right, riser, 0.008, 0.16, pan=0.25)

    impact = shape(sweep_tone(0.45, 150.0, 78.0, curve=0.4), 0.004, 0.16)
    mix_into(left, right, impact, bloom_at, 0.42)

    strike = shape(highpass(white_noise(0.09, 4711), 2400.0), 0.001, 0.022)
    mix_into(left, right, strike, bloom_at, 0.20)

    chord = (
        (523.25, 0.000, 1.00, 0.80),
        (659.26, 0.055, 0.72, 0.68),
        (783.99, 0.115, 0.62, 0.58),
        (1174.66, 0.205, 0.40, 0.40),
    )
    for frequency, offset, gain, decay in chord:
        span = min(decay * 6.0, duration - bloom_at - offset)
        mix_into(left, right, bell_voice(span, frequency, decay, detune_cents=-3.0), bloom_at + offset, gain * 0.5, pan=-0.35)
        mix_into(left, right, bell_voice(span, frequency, decay, detune_cents=3.0), bloom_at + offset, gain * 0.5, pan=0.35, delay_seconds=0.006)

    air = shape(sweeping_lowpass(white_noise(0.9, 9001), 9000.0, 1800.0), 0.01, 0.30)
    mix_into(left, right, air, bloom_at, 0.05, pan=0.4)
    mix_into(left, right, air, bloom_at + 0.01, 0.05, pan=-0.4)

    write_stereo(path, left, right, 0.70, fade_out_seconds=0.35)


def build_minimize(path):
    duration = 0.38
    left = buffer_of(duration)
    right = buffer_of(duration)

    body = shape(sweep_tone(0.30, 640.0, 296.0, curve=0.6, harmonic_gain=0.18), 0.006, 0.11)
    mix_into(left, right, body, 0.0, 0.44)

    fifth = shape(sweep_tone(0.26, 960.0, 444.0, curve=0.6), 0.008, 0.08)
    mix_into(left, right, fifth, 0.006, 0.16, pan=-0.2)
    mix_into(left, right, fifth, 0.010, 0.16, pan=0.2)

    air = shape(sweeping_lowpass(white_noise(0.30, 5150), 6400.0, 620.0, curve=1.4), 0.012, 0.10)
    mix_into(left, right, air, 0.0, 0.20, pan=0.3, delay_seconds=0.004)
    mix_into(left, right, air, 0.0, 0.20, pan=-0.3)

    seat = shape(sweep_tone(0.20, 180.0, 116.0, curve=0.5), 0.004, 0.07)
    mix_into(left, right, seat, 0.17, 0.30)

    write_stereo(path, left, right, 0.60, fade_out_seconds=0.06)


def build_maximize(path):
    duration = 0.40
    left = buffer_of(duration)
    right = buffer_of(duration)

    body = shape(sweep_tone(0.28, 300.0, 648.0, curve=0.55, harmonic_gain=0.20), 0.006, 0.10)
    mix_into(left, right, body, 0.0, 0.42)

    fifth = shape(sweep_tone(0.24, 450.0, 972.0, curve=0.55), 0.010, 0.08)
    mix_into(left, right, fifth, 0.008, 0.16, pan=-0.2)
    mix_into(left, right, fifth, 0.012, 0.16, pan=0.2)

    air = shape(sweeping_lowpass(white_noise(0.32, 7331), 700.0, 7600.0, curve=0.8), 0.014, 0.09)
    mix_into(left, right, air, 0.0, 0.18, pan=-0.3, delay_seconds=0.004)
    mix_into(left, right, air, 0.0, 0.18, pan=0.3)

    ping = bell_voice(0.22, 1046.5, 0.10)
    mix_into(left, right, ping, 0.19, 0.17, pan=-0.25)
    mix_into(left, right, ping, 0.19, 0.17, pan=0.25, delay_seconds=0.005)

    write_stereo(path, left, right, 0.60, fade_out_seconds=0.08)


def main():
    default_out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "Aetherphone", "Sounds", "Ui")
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", default=default_out)
    options = parser.parse_args()
    destination = os.path.abspath(options.out)
    os.makedirs(destination, exist_ok=True)
    build_startup(os.path.join(destination, "startup.wav"))
    build_minimize(os.path.join(destination, "minimize.wav"))
    build_maximize(os.path.join(destination, "maximize.wav"))


main()
