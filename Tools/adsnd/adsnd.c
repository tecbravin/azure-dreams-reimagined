/*
 * adsnd — renders Azure Dreams (PS1, SLUS-006.14) background music from the original disc data.
 *
 * The game's music is not stored as audio. It is a Konami "KDT" sequence (inside a KCET/TSDB/KDT2
 * container) played at run time by the game's sound driver through the PlayStation SPU, using the
 * instruments of a Sony VAB bank. This program re-implements both halves:
 *
 *   Sequencer — follows the decompiled driver (azure-dreams-decomp-master/src/engine/audio/sequence):
 *     initialize_audio_sequence_tracks, audio_tick_sequence_tracks, audio_read_compact_sequence_event,
 *     audio_dispatch_midi_event, audio_apply_channel_control, apply_tone_parameter_command,
 *     start_channel_note, audio_sequence_note_off, audio_compute_voice_stereo_volumes,
 *     audio_compute_pitch_bend_offset, sound_step_bipolar_oscillation, scale_value_with_remainder.
 *     Ticked by the root-counter-2 interrupt at 590.625 Hz (open_timer_2_update_event,
 *     sound_tick_guarded_periodic_update), with channel modulation every 11 ticks. Matching the OST
 *     recordings confirmed this rate (a 600 Hz vsync clock plays 1.6% fast).
 *   SPU — ADPCM decoding with loop flags, pitch counter with 4-point interpolation, hardware ADSR,
 *     per-voice volumes, and a stereo reverb send for voices whose tone enables reverb.
 *
 * Usage: adsnd <file.BIN> <vab_offset> <kdt_offset> <out.wav> [passes=2] [fade_seconds=8] [reverb_mix=0.25]
 * Prints one JSON line with the song timing (seconds): intro, loop, duration.
 */
#include <math.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define RATE 44100
/* Root counter 2 interrupt (open_timer_2_update_event: SetRCnt(RCntCNT2, 0x1C00)): system clock / 8 / 0x1C00 */
#define TIMER_HZ (33868800.0 / 8.0 / 0x1C00)          /* 590.625 Hz: one sequencer tick per interrupt */
#define MODULATION_EVERY 11                            /* sound_tick_guarded_periodic_update */
#define NVOICE 24
#define MAXTRK 32
#define MAX_SECONDS 900

typedef int32_t s32; typedef uint32_t u32; typedef int16_t s16; typedef uint16_t u16; typedef uint8_t u8;

static u8 *disc; static long disc_len;
static u8 vab[0xA20 + 0x200 * 128];   /* header + tones; NRPN edits tones in place, like the driver in RAM */
static const u8 *vab_body; static long vab_body_len;
static int vab_ps, tone_base[128];
static u32 vag_off[256];

/* ------------------------------------------------------------------ voices */
enum { ENV_OFF, ENV_ATTACK, ENV_DECAY, ENV_SUSTAIN, ENV_RELEASE };
typedef struct {
    int on; u32 addr, loop_addr; int stopping; int block_pos; s16 buf[28]; s32 s1, s2; s16 hist[4];
    u32 counter; int pitch; int env_phase; s32 env; int env_wait; u16 adsr1, adsr2;
    int vol_l, vol_r, reverb;
    int channel, note, start_note, velocity, sustained, keyed;
    int prog_vol, tone_vol, prog_pan, tone_pan, center, shift, pbmin, pbmax;
} Voice;
static Voice voices[NVOICE];

/* ------------------------------------------------------------------ driver */
typedef struct {
    int pos, end, delay, acc, step, rem, finished, last_note, chan;
    int s_pos, s_delay, s_rem, s_finished, s_last, loopcount;
} Track;
static Track trk[MAXTRK]; static int ntrk, division; static const u8 *seq; static u32 seq_limit;

typedef struct {
    int program, vol, pan, expr, cc12, sustain, bend, mono;
    int cc1, cc2, cc3, depth, period, step, osc, dir;   /* sound_step_bipolar_oscillation */
    int nrpn_op, nrpn_target;
} Chan;
static Chan ch[16];
static int master_level = 0x7F;
static int reverb_depth = 0;          /* NRPN 16 */
static int reverb_mode = 0;           /* NRPN 15 */

static double now_s;
static double loop_save_s = -1, first_jump_s = -1;

static u16 rd16(const u8 *p) { return p[0] | (p[1] << 8); }
static void wr16(u8 *p, u16 v) { p[0] = v & 0xFF; p[1] = v >> 8; }
static u8 *tone_ptr(int program, int t) { return vab + 0x820 + ((tone_base[program] * 16 + t) << 5); }

static void load_vab(long off)
{
    if (memcmp(disc + off, "pBAV", 4)) { fprintf(stderr, "no VAB at %#lx\n", off); exit(1); }
    vab_ps = rd16(disc + off + 0x12);
    long head = 0xA20 + vab_ps * 0x200;
    memcpy(vab, disc + off, head);
    /* On the disc the sample body (VB) starts at the next 2048-byte sector after the header (VH). */
    vab_body = disc + off + ((head + 2047) & ~2047L);
    vab_body_len = (long)(disc[off + 12] | disc[off + 13] << 8 | disc[off + 14] << 16) - head;
    int n = 0;
    for (int p = 0; p < 128; p++) { tone_base[p] = n; if (vab[0x20 + p * 16]) n++; }
    const u8 *sizes = vab + 0x820 + vab_ps * 0x200;
    u32 acc = 0;
    for (int i = 0; i < 256; i++) { vag_off[i] = acc; acc += (u32)rd16(sizes + i * 2) << 3; }
}

/* --------------------------------------------------------------------- SPU */
static const int F0[5] = { 0, 60, 115, 98, 122 }, F1[5] = { 0, 0, -52, -55, -60 };

static void decode_block(Voice *v)
{
    if ((long)v->addr + 16 > vab_body_len) { v->stopping = 1; memset(v->buf, 0, sizeof v->buf); v->block_pos = 0; return; }
    const u8 *b = vab_body + v->addr;
    int shift = b[0] & 15, filter = (b[0] >> 4) & 7, flags = b[1];
    if (filter > 4) filter = 4;
    if (shift > 12) shift = 9;
    if (flags & 4) v->loop_addr = v->addr;
    for (int i = 0; i < 28; i++) {
        int nib = (b[2 + i / 2] >> ((i & 1) * 4)) & 15;
        s32 s = ((s16)(nib << 12)) >> shift;
        s += (v->s1 * F0[filter] + v->s2 * F1[filter] + 32) >> 6;
        if (s > 32767) s = 32767;
        if (s < -32768) s = -32768;
        v->s2 = v->s1; v->s1 = s; v->buf[i] = (s16)s;
    }
    v->block_pos = 0;
    if (flags & 1) {                       /* loop end: repeat, or voice ends (ENDX, envelope released to 0) */
        if (flags & 2) v->addr = v->loop_addr;
        else v->stopping = 1;
    } else v->addr += 16;
}

static s16 next_sample(Voice *v)
{
    if (v->block_pos >= 28) {
        if (v->stopping) { v->on = 0; v->env_phase = ENV_OFF; v->env = 0; return 0; }
        decode_block(v);
    }
    return v->buf[v->block_pos++];
}

/* Hardware ADSR (psx-spx, "SPU Volume and ADSR Generator"). Rate = shift<<2 | step; decay and release
 * store only the shift (4 and 5 bits), with a fixed step of -8. */
static void env_tick(Voice *v)
{
    int mode_exp, decrease, shift, step, target = 0;
    switch (v->env_phase) {
    case ENV_ATTACK: mode_exp = v->adsr1 >> 15; decrease = 0; shift = (v->adsr1 >> 10) & 31; step = 7 - ((v->adsr1 >> 8) & 3); break;
    case ENV_DECAY: mode_exp = 1; decrease = 1; shift = (v->adsr1 >> 4) & 15; step = -8;
        target = ((v->adsr1 & 15) + 1) * 0x800; break;
    case ENV_SUSTAIN: mode_exp = v->adsr2 >> 15; decrease = (v->adsr2 >> 14) & 1; shift = (v->adsr2 >> 8) & 31;
        step = decrease ? -8 + ((v->adsr2 >> 6) & 3) : 7 - ((v->adsr2 >> 6) & 3); break;
    case ENV_RELEASE: mode_exp = (v->adsr2 >> 5) & 1; decrease = 1; shift = v->adsr2 & 31; step = -8; break;
    default: return;
    }
    if (v->env_wait > 0) { v->env_wait--; return; }
    int cycles = 1 << (shift > 11 ? shift - 11 : 0);
    s32 add = step << (shift < 11 ? 11 - shift : 0);
    if (mode_exp && !decrease && v->env > 0x6000) cycles *= 4;
    if (mode_exp && decrease) add = (add * v->env) >> 15;
    v->env += add;
    if (v->env > 0x7FFF) v->env = 0x7FFF;
    if (v->env < 0) v->env = 0;
    v->env_wait = cycles - 1;
    if (v->env_phase == ENV_ATTACK && v->env >= 0x7FFF) { v->env_phase = ENV_DECAY; v->env_wait = 0; }
    else if (v->env_phase == ENV_DECAY && v->env <= target) { v->env_phase = ENV_SUSTAIN; v->env_wait = 0; }
    else if (v->env_phase == ENV_RELEASE && v->env == 0) { v->env_phase = ENV_OFF; v->on = 0; }
}

static void spu_key_on(Voice *v, u32 sample_addr)
{
    v->on = 1; v->stopping = 0; v->addr = sample_addr; v->loop_addr = sample_addr; v->block_pos = 28;
    v->s1 = v->s2 = 0; memset(v->hist, 0, sizeof v->hist); v->counter = 0;
    v->env = 0; v->env_phase = ENV_ATTACK; v->env_wait = 0;
}
static void spu_key_off(Voice *v) { if (v->on && v->env_phase != ENV_OFF) { v->env_phase = ENV_RELEASE; v->env_wait = 0; } }
static int key_status(const Voice *v) { if (!v->on || v->env_phase == ENV_OFF) return 0; return v->env_phase == ENV_RELEASE ? 2 : 1; }

/* ------------------------------------------------------- driver: voices */
static int bend_offset(const Voice *v, int level)
{
    if (level < 0x40) return v->pbmin ? -((v->pbmin << 1) * (0x3F - level)) : 0;
    if (level == 0x40) return 0;
    return v->pbmax ? (v->pbmax * 2) * (level - 0x40) : 0;
}

static void update_pitch(Voice *v)
{
    const Chan *c = &ch[v->channel];
    int p = (s16)(c->osc + (v->start_note << 7) + bend_offset(v, c->bend));
    int semitone = p >> 7, fine = p & 0x7F;
    double note = semitone + fine / 128.0;
    double sample_note = v->shift ? (v->center - 1) + (0x7F - v->shift) / 128.0 : v->center;
    double pitch = 4096.0 * pow(2.0, (note - sample_note) / 12.0);
    if (pitch > 0x3FFF) pitch = 0x3FFF;
    v->pitch = (int)pitch;
}

static void update_volume(Voice *v)
{
    const Chan *c = &ch[v->channel];
    int pan = vab[0x19] + v->prog_pan + v->tone_pan + c->pan - 0xC0;
    if (pan < 0) pan = 0;
    if (pan > 0x7F) pan = 0x7F;
    s32 product = (s32)((u32)(vab[0x18] * master_level * c->expr * c->vol) >> 14);
    product *= v->prog_vol;
    s32 vol = (product * v->tone_vol) >> 14, l, r;
    if (pan >= 0x40) { r = vol; l = ((0x40 - (pan & 0x3F)) * (vol * 2)) >> 7; }
    else { l = vol; r = (pan * (vol * 2)) >> 7; }
    l = (l * 0x7F) >> 7; r = (r * 0x7F) >> 7;
    l = (l * (v->velocity & 0x7F)) >> 7; r = (r * (v->velocity & 0x7F)) >> 7;
    if (c->cc12 >= 0x40) { l = (l * l) >> 14; r = (r * r) >> 14; }
    v->vol_l = l; v->vol_r = r;
}

static Voice *find_voice(int channel, int note)
{
    for (int i = 0; i < NVOICE; i++) if (key_status(&voices[i]) == 0) return &voices[i];
    for (int i = 0; i < NVOICE; i++) if (key_status(&voices[i]) == 2) return &voices[i];
    for (int i = 0; i < NVOICE; i++) if (voices[i].channel == channel && voices[i].note == note) return &voices[i];
    for (int i = 0; i < NVOICE; i++) if (!voices[i].keyed) return &voices[i];
    return NULL;
}

static void note_on(int channel, int note, int velocity)
{
    Chan *c = &ch[channel];
    const u8 *prog = vab + 0x20 + c->program * 16;
    if (!prog[0]) return;
    for (int t = 0; t < prog[0] && t < 16; t++) {
        u8 *tone = tone_ptr(c->program, t);
        if (note < tone[6] || note > tone[7]) continue;
        Voice *v = NULL;
        int legato = 0;
        if (c->mono) {                                   /* cc126: reuse the channel's voice */
            for (int i = 0; i < NVOICE; i++) if (voices[i].on && voices[i].channel == channel) { v = &voices[i]; break; }
            if (v && v->keyed) legato = 1;
        }
        if (!v) v = find_voice(channel, note);
        if (!v) continue;
        v->channel = channel; v->note = note; v->start_note = note; v->velocity = velocity;
        v->keyed = 1; v->sustained = c->sustain;
        v->prog_vol = prog[1]; v->prog_pan = prog[4];
        v->tone_vol = tone[2]; v->tone_pan = tone[3]; v->center = tone[4]; v->shift = tone[5];
        v->pbmin = tone[12]; v->pbmax = tone[13];
        v->reverb = (tone[1] & 4) != 0;
        update_volume(v);
        update_pitch(v);
        if (!legato) {
            v->adsr1 = rd16(tone + 16); v->adsr2 = rd16(tone + 18);
            spu_key_on(v, vag_off[rd16(tone + 22)]);
        }
    }
}

static void note_off(int channel, int note)
{
    for (int i = 0; i < NVOICE; i++) {
        Voice *v = &voices[i];
        if (!v->on || v->channel != channel || v->note != note) continue;
        if (!v->sustained) { spu_key_off(v); v->keyed = 0; }
        else v->velocity |= 0x80;
    }
}

static void refresh_channel(int channel, int volume, int pitch)
{
    for (int i = 0; i < NVOICE; i++) if (voices[i].on && voices[i].channel == channel) {
        if (volume) update_volume(&voices[i]);
        if (pitch) update_pitch(&voices[i]);
    }
}

/* apply_tone_parameter_command: NRPN (cc98 = operation, cc99 = tone index) + data entry (cc6). */
static void tone_parameter(Chan *c, int value)
{
    if (!vab[0x20 + c->program * 16]) return;
    u8 *cell = tone_ptr(c->program, c->nrpn_target & 15);
    u16 h10 = rd16(cell + 16), h12 = rd16(cell + 18);
    switch (c->nrpn_op) {
    case 1: cell[1] = value; break;
    case 2: cell[6] = value; break;
    case 3: cell[7] = value; break;
    case 4: h10 = (h10 & 0xFF) | ((0x7F - value) << 8); break;
    case 5: h10 = (h10 & 0xFF) | ((0x7F - value) << 8) | 0x8000; break;
    case 6: h10 = (h10 & 0xFF0F) | ((value << 1) & 0xF0); break;
    case 7: h10 = (h10 & 0xFFF0) | (value >> 3); break;
    case 8: h12 = (h12 & 0x603F) | ((0x7F - value) << 6); break;
    case 9: h12 = (h12 & 0x603F) | ((0x7F - value) << 6) | 0x8000; break;
    case 10: h12 = (h12 & 0xFFC0) | ((0x7F - value) >> 3); break;
    case 11: h12 = ((h12 & 0xFFC0) | ((0x7F - value) >> 3)) | 0x20; break;
    case 12: if (value < 0x41) h12 |= 0x4000; else h12 &= 0xBFFF; break;
    case 15: reverb_mode = value; break;
    case 16: reverb_depth = value; break;
    default: break;
    }
    wr16(cell + 16, h10); wr16(cell + 18, h12);
}

static void loop_save(void)
{
    for (int i = 0; i < ntrk; i++) {
        Track *t = &trk[i];
        t->s_pos = t->pos; t->loopcount = 0x14; t->s_delay = t->delay; t->s_rem = t->rem;
        t->s_finished = t->finished; t->s_last = t->last_note;
    }
    if (loop_save_s < 0) loop_save_s = now_s;
}

static void control_change(Track *t, int channel, int cc, int value)
{
    Chan *c = &ch[channel];
    if (cc == 0x63) {                                    /* loop markers (multitrack mode) */
        if (value == 0x14) loop_save();
        else if (value == 0x1E && t->loopcount != 0) {
            if (t->loopcount < 0x7F) t->loopcount--;
            for (int i = 0; i < ntrk; i++) {
                Track *u = &trk[i];
                u->pos = u->s_pos; u->delay = u->s_delay; u->rem = u->s_rem; u->finished = u->s_finished; u->last_note = u->s_last;
            }
            if (first_jump_s < 0) first_jump_s = now_s;
        }
    }
    if (cc == 6 && c->nrpn_target == 0x14) {             /* loop count */
        for (int i = 0; i < ntrk; i++) trk[i].loopcount = value;
        return;
    }
    switch (cc) {
    case 1:                                              /* vibrato depth (cc3 < 0x40) */
        if (c->cc3 < 0x40) { c->cc1 = value; c->depth = value << 1; if (c->period && c->depth) c->step = (c->depth << 2) / c->period; }
        else { c->cc1 = value; c->period = value; if (value && c->depth) c->step = (c->depth / value) << 1; }
        if (!c->cc1 || !c->step) { c->osc = 0; refresh_channel(channel, 0, 1); }
        break;
    case 2: if (c->cc3 < 0x40) c->period = value; else c->depth = value << 1; break;
    case 3: c->cc3 = value; break;
    case 7: c->vol = value; refresh_channel(channel, 1, 0); break;
    case 10: c->pan = value ? value : 1; refresh_channel(channel, 1, 0); break;
    case 11: c->expr = value; refresh_channel(channel, 1, 0); break;
    case 12: c->cc12 = value; refresh_channel(channel, 1, 0); break;
    case 30: master_level = value; for (int i = 0; i < 16; i++) refresh_channel(i, 1, 0); break;
    case 64:
        c->sustain = value >= 0x40;
        for (int i = 0; i < NVOICE; i++) {
            Voice *v = &voices[i];
            if (!v->on || v->channel != channel) continue;
            if (value < 0x40) { if (v->sustained && (v->velocity & 0x80)) { spu_key_off(v); v->keyed = 0; } v->sustained = 0; }
            else v->sustained = 1;
        }
        break;
    case 6: if (c->nrpn_target != 0x1E) tone_parameter(c, value); break;
    case 98: c->nrpn_op = value; break;
    case 99: c->nrpn_target = value; break;
    case 126: c->mono = value; break;
    default: break;
    }
}

static int rd(Track *t)
{
    if ((u32)t->pos + 1 > seq_limit) { t->finished = 1; t->pos++; return -1; }
    return seq[t->pos++];
}
static int vlq(Track *t)
{
    int b = rd(t);
    if (b <= 0) return 0;
    if (!(b & 0x80)) return b;
    int v = b & 0x7F;
    do { b = rd(t); v = (v << 7) + (b & 0x7F); } while (b & 0x80 && !t->finished);
    return v;
}
static void scale_delay(Track *t)
{
    switch (division) {
    case 0x30: t->delay = t->delay * 10 + t->rem; t->rem = t->delay & 3; t->delay /= 4; break;
    case 0x60: t->delay = t->delay * 5 + t->rem; t->rem = t->delay & 3; t->delay /= 4; break;
    case 0xC0: case 0xF0: t->delay += t->rem; t->rem = t->delay & 1; t->delay /= 2; break;
    case 0x120: case 0x168: t->delay /= 3; break;
    case 0x1E0: case 0x180: t->delay += t->rem; t->rem = t->delay & 3; t->delay /= 4; break;
    }
}

/* audio_read_compact_sequence_event: returns 1 when no delta time follows */
static int compact_event(Track *t)
{
    int cmd = rd(t), op;
    if (cmd < 0) return 0;
    if (!(cmd & 0x80)) {
        int b = rd(t); op = b;
        if (b & 0x7F) note_on(t->chan, cmd & 0x7F, b & 0x7F); else note_off(t->chan, cmd & 0x7F);
        t->last_note = cmd;
        return (op & 0x80) != 0;
    }
    int idx = (cmd & 0x7F) - 0x46;
    switch (idx) {
    case 4: note_off(t->chan, t->last_note & 0x7F); return 0;
    case 5: note_off(t->chan, t->last_note & 0x7F); return 1;
    case 1: op = rd(t); for (int i = 0; i < ntrk; i++) trk[i].step = ((op & 0x7F) << 1) + 2; return (op & 0x80) != 0;
    case 2: op = rd(t); ch[t->chan].bend = op & 0x7F; refresh_channel(t->chan, 0, 1); break;
    case 3: op = rd(t); ch[t->chan].program = op & 0x7F; break;
    case 0: op = rd(t); t->chan = op & 0xF; break;
    case 57: op = rd(t); t->finished = 1; break;
    default: op = rd(t); control_change(t, t->chan, cmd & 0x7F, op & 0x7F); break;
    }
    return (op & 0x80) != 0;
}

static void sequencer_tick(void)
{
    for (int i = 0; i < ntrk; i++) {
        Track *t = &trk[i];
        if (t->finished) continue;
        t->acc += t->step;
        if (t->acc < 0x100) continue;
        t->acc &= 0xFF;
        if (t->delay == 0) {
            do {
                if (compact_event(t) == 0) { if (t->finished) break; t->delay = vlq(t); }
                else if (t->finished) break;
                if (t->delay) scale_delay(t);
            } while (t->delay == 0);
            if (t->delay) t->delay--;
        } else t->delay--;
    }
}

static void modulation_frame(void)
{
    for (int c = 0; c < 16; c++) {
        Chan *k = &ch[c];
        if (!k->cc1) { if (k->osc) { k->osc = 0; refresh_channel(c, 0, 1); } continue; }
        if (k->dir) { if (k->osc + k->step < k->depth) k->osc += k->step; else { k->dir = 0; k->osc = k->depth; } }
        else { if (k->osc - k->step > -k->depth) k->osc -= k->step; else { k->dir = 1; k->osc = -k->depth; } }
        refresh_channel(c, 0, 1);
    }
}

static void init_sequence(long off)
{
    seq = disc + off;
    if (memcmp(seq, "KDT ", 4)) { fprintf(stderr, "no KDT at %#lx\n", off); exit(1); }
    seq_limit = seq[4] | seq[5] << 8 | seq[6] << 16;
    division = seq[8] | seq[9] << 8;
    ntrk = seq[12];
    int offset = 0x50;
    for (int i = 0; i < ntrk; i++) {
        Track *t = &trk[i];
        memset(t, 0, sizeof *t);
        int len = rd16(seq + 0x10 + i * 2);
        t->pos = offset; t->end = offset + len; offset += len;
        t->step = 0x68;
        t->delay = vlq(t);
        if (t->delay) scale_delay(t);
    }
    for (int c = 0; c < 16; c++) {
        memset(&ch[c], 0, sizeof ch[c]);
        ch[c].vol = 0x7F; ch[c].pan = 0x40; ch[c].expr = 0x7F; ch[c].bend = 0x40;
    }
}

/* -------------------------------------------------------------- reverb */
/* Small stereo Schroeder reverb standing in for the SPU work-area reverb. */
#define NCOMB 4
static float *comb_buf[2][NCOMB]; static int comb_len[2][NCOMB], comb_pos[2][NCOMB]; static float comb_lp[2][NCOMB];
static float *ap_buf[2][2]; static int ap_len[2][2], ap_pos[2][2];
static void reverb_init(void)
{
    static const int cl[NCOMB] = { 1557, 1617, 1491, 1422 }, al[2] = { 556, 441 };
    for (int s = 0; s < 2; s++) {
        for (int i = 0; i < NCOMB; i++) { comb_len[s][i] = (cl[i] + s * 23) * 2; comb_buf[s][i] = calloc(comb_len[s][i], sizeof(float)); }
        for (int i = 0; i < 2; i++) { ap_len[s][i] = al[i] + s * 23; ap_buf[s][i] = calloc(ap_len[s][i], sizeof(float)); }
    }
}
static float reverb_process(int s, float in)
{
    float out = 0;
    for (int i = 0; i < NCOMB; i++) {
        float *b = comb_buf[s][i]; int p = comb_pos[s][i];
        float y = b[p];
        comb_lp[s][i] = y * 0.6f + comb_lp[s][i] * 0.4f;
        b[p] = in + comb_lp[s][i] * 0.80f;
        comb_pos[s][i] = (p + 1) % comb_len[s][i];
        out += y;
    }
    for (int i = 0; i < 2; i++) {
        float *b = ap_buf[s][i]; int p = ap_pos[s][i];
        float y = b[p]; float x = out + y * 0.5f;
        b[p] = x; out = y - x * 0.5f;
        ap_pos[s][i] = (p + 1) % ap_len[s][i];
    }
    return out * 0.25f;
}

/* ------------------------------------------------------------------ main */
/* 32-bit float WAV: levels are normalized later, so nothing clips here. */
static void write_wav(const char *path, const float *data, long frames)
{
    FILE *f = fopen(path, "wb");
    if (!f) { perror(path); exit(1); }
    u32 bytes = (u32)frames * 8;
    u32 hdr[11] = { 0x46464952, 36 + bytes, 0x45564157, 0x20746d66, 16, 0x00020003, RATE, RATE * 8, 0x00200008, 0x61746164, bytes };
    fwrite(hdr, 4, 11, f);
    fwrite(data, sizeof(float), (size_t)frames * 2, f);
    fclose(f);
}

int main(int argc, char **argv)
{
    if (argc < 5) { fprintf(stderr, "usage: %s file.BIN vab_offset kdt_offset out.wav [passes] [fade] [reverb_mix]\n", argv[0]); return 2; }
    int passes = argc > 5 ? atoi(argv[5]) : 2;
    double fade = argc > 6 ? atof(argv[6]) : 8.0;
    float reverb_mix = argc > 7 ? (float)atof(argv[7]) : 0.25f;
    FILE *f = fopen(argv[1], "rb");
    if (!f) { perror(argv[1]); return 1; }
    fseek(f, 0, SEEK_END); disc_len = ftell(f); fseek(f, 0, SEEK_SET);
    disc = malloc(disc_len); if (fread(disc, 1, disc_len, f) != (size_t)disc_len) return 1; fclose(f);
    load_vab(strtol(argv[2], NULL, 0));
    init_sequence(strtol(argv[3], NULL, 0));
    reverb_init();

    long max_frames = (long)MAX_SECONDS * RATE;
    float *out = calloc(max_frames * 2, sizeof(float));
    long n = 0; double stop_at = -1, tail_until = -1, next_tick = 0; int ticks = 0;
    while (n < max_frames - 128) {
        now_s = (double)n / RATE;
        sequencer_tick();
        if (++ticks >= MODULATION_EVERY) { modulation_frame(); ticks = 0; }
        next_tick += RATE / TIMER_HZ;                    /* ~74.67 samples per interrupt */
        int spf = (int)(next_tick - n);
        if (stop_at < 0 && first_jump_s >= 0 && loop_save_s >= 0) {
            double loop = first_jump_s - loop_save_s;
            stop_at = first_jump_s + loop * (passes - 1) + fade;
        }
        int all_done = 1;
        for (int i = 0; i < ntrk; i++) if (!trk[i].finished) all_done = 0;
        if (all_done && stop_at < 0 && tail_until < 0) tail_until = now_s + 3.0;
        for (int s = 0; s < spf; s++, n++) {
            float l = 0, r = 0, rl = 0, rr = 0;
            for (int i = 0; i < NVOICE; i++) {
                Voice *v = &voices[i];
                if (!v->on) continue;
                /* 4-point interpolation over the pitch counter (stand-in for the SPU gaussian table) */
                while (v->counter >= 0x1000) {
                    v->counter -= 0x1000;
                    v->hist[0] = v->hist[1]; v->hist[1] = v->hist[2]; v->hist[2] = v->hist[3];
                    v->hist[3] = next_sample(v);
                }
                if (!v->on) continue;
                float t = (v->counter & 0xFFF) / 4096.0f;
                float y0 = v->hist[0], y1 = v->hist[1], y2 = v->hist[2], y3 = v->hist[3];
                float c1 = 0.5f * (y2 - y0), c2 = y0 - 2.5f * y1 + 2.f * y2 - 0.5f * y3, c3 = 0.5f * (y3 - y0) + 1.5f * (y1 - y2);
                float smp = ((c3 * t + c2) * t + c1) * t + y1;
                v->counter += v->pitch;
                env_tick(v);
                float a = smp * (v->env / 32768.0f) / 32768.0f;
                float vl = a * v->vol_l / 16384.0f, vr = a * v->vol_r / 16384.0f;
                l += vl; r += vr;
                if (v->reverb) { rl += vl; rr += vr; }
            }
            float depth = reverb_mode ? (reverb_depth ? reverb_depth / 127.0f : 0.5f) : 0.35f;
            l += reverb_process(0, rl) * reverb_mix * depth * 4.0f;
            r += reverb_process(1, rr) * reverb_mix * depth * 4.0f;
            out[n * 2] = l * 0.5f; out[n * 2 + 1] = r * 0.5f;
        }
        if (stop_at >= 0 && now_s >= stop_at) break;
        if (tail_until >= 0 && now_s >= tail_until) break;
    }
    if (stop_at >= 0 && fade > 0) {                    /* fade out over the last `fade` seconds */
        long fade_frames = (long)(fade * RATE);
        for (long i = 0; i < fade_frames && i < n; i++) {
            float g = (float)i / fade_frames;
            long k = n - 1 - i;
            out[k * 2] *= g; out[k * 2 + 1] *= g;
        }
    }
    float peak = 0;
    for (long i = 0; i < n * 2; i++) if (fabsf(out[i]) > peak) peak = fabsf(out[i]);
    write_wav(argv[4], out, n);
    printf("{\"duration\": %.3f, \"intro\": %.3f, \"loop\": %.3f, \"peak\": %.4f, \"tracks\": %d, \"reverb_mode\": %d, \"reverb_depth\": %d}\n",
        (double)n / RATE, loop_save_s, first_jump_s >= 0 ? first_jump_s - loop_save_s : -1.0, peak, ntrk, reverb_mode, reverb_depth);
    return 0;
}
