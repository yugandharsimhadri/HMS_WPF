# Sivayaan HMS — Premium Promotional Video
## Production Bible v1.0

> **Status:** Production-ready creative blueprint.
> **Duration:** 60 seconds (master cut) · **Format:** 16:9 3840×2160 master → 1920×1080 deliverable · **VO:** Indian English · **CTA:** Book a Demo
> **Reference tier:** Apple / Salesforce / Oracle product films.

---

## 0. How To Use This Document

Every UI shot is written as a placeholder tag, e.g. `[SCREEN: Dashboard]`.
When you upload the actual screenshots, drop them into `/marketing/video-promo/screens/`
using the matching filename from the **Shot List** (Section 5). The storyboard,
motion graphics plan, and edit plan all reference these tags, so no rewrite is
needed — you simply composite the real screenshots where the tags appear.

**Golden rule — do not break this:**
> Real UI = real screenshots, composited in After Effects.
> AI video = environments, hands, devices, abstract transitions ONLY.
> Never AI-generate the product UI. For a healthcare product, faked UI destroys trust.

---

## 1. Creative Strategy

### 1.1 Positioning Statement
Sivayaan HMS is not software you install and tolerate. It is the operating system
of a modern hospital — calm, intelligent, and quietly powerful — deployed with
local support that global suites cannot match.

### 1.2 Core Idea — "The Calm Hospital"
A hospital runs on a thousand small decisions per minute. Sivayaan HMS turns that
noise into a single, clear signal. The film opens in the *noise* (paper, queues,
frustration) and resolves into the *signal* — a clean, confident, digitally-native
hospital where every role moves with clarity. This contrast is the entire emotional
engine of the film.

**Three-act spine:**
1. **Friction (0–15s)** — the weight of a hospital running on fragments.
2. **Clarity (15–45s)** — Sivayaan HMS unifying every workflow on one screen.
3. **Trust (45–60s)** — local support, enterprise security, and the invitation.

### 1.3 Brand Voice
- **Tone:** Confident, warm, precise. Never shouty. Never hype-y.
- **Pace of speech:** Slow for an enterprise ad. ~130 wpm. Let images breathe.
- **Words we use:** *Clarity. Unify. Local. Calm. Complete. Ready.*
- **Words we avoid:** *Cheap, easy, revolutionary, game-changing, world-class.*

### 1.4 Target Audience (priority order)
1. Hospital Owners & Administrators (decision-makers)
2. Doctors (influencers, credibility carriers)
3. Clinic Owners & Multi-speciality operators (scalability buyers)

---

## 2. Visual System

### 2.1 Color
A restrained, premium healthcare palette. No neon, no rainbow gradients.

| Token | Hex | Use |
|------|------|------|
| `--ink` | `#0B1220` | Deep background, end cards, text on light |
| `--midnight` | `#0F1B2D` | Mid-tone dark surfaces, device frames |
| `--clinical` | `#F5F7FA` | Light backgrounds, light-mode UI showcase |
| `--mist` | `#E6ECF2` | Dividers, subtle cards |
| `--trust-blue` | `#2E6BE6` | Primary brand accent, CTAs, key highlights |
| `--teal` | `#10B7C4` | Secondary accent, "live" / positive states |
| `--sage` | `#3FB27F` | Health/positive, confirmation |
| `--amber` | `#E8A33D` | Attention (used sparingly) |
| `--coral` | `#E5586B` | Alerts only — never decorative |

**Rule:** a frame uses at most two accents at once. Trust-blue carries 80% of accent duty.

### 2.2 Typography
- **Headline / Display:** *Inter* or *SF Pro Display* — Tight, 600 weight, -2% tracking, sentence case.
- **Body / UI labels:** *Inter* 400/500, generous line-height (1.4).
- **Numerals:** Tabular figures, monospaced, for any stat counters.
- **Avoid:** Decorative serifs, all-caps headlines, exclamation marks.

### 2.3 Layout & Composition
- Generous negative space — Apple-grade breathing room.
- 12-column grid; key elements on thirds or center-axis for hero moments.
- UI screenshots float on `--ink` or `--clinical` backgrounds with a soft 40px radius, 2–4% drop shadow, never a hard box.
- Persistent lower-third "Sivayaan HMS" wordmark from second 15 onward — quiet, bottom-left, 60% opacity.

### 2.4 Iconography & Motifs
- **Signal line:** a single horizontal light-streak that travels across frames as a transition motif (the "signal through the noise").
- **Node mesh:** a faint, slowly drifting point-cloud representing the connected hospital — used as ambient background texture, never dominant.
- **Status dots:** teal/green "live" pulses to signify real-time data.

### 2.5 Motion Language
- **Easing:** `cubic-bezier(0.22, 1, 0.36, 1)` (ease-out-quint) for 90% of moves. Never linear.
- **Duration:** UI elements ease over 18–28 frames; camera moves are slow — 2–4s pushes.
- **Screen reveals:** UI screens enter as a gentle dolly + 4° parallax tilt, resolve to flat. Avoid screen "flips" and bouncy springs.
- **Camera on UI:** Ken Burns pushes at 1.02–1.08x over the shot, never more. The product is the star; motion must not upstage it.
- **Particle/ambient drift:** 6–10 second cycles, imperceptible unless you watch.

---

## 3. Sound & Music

### 3.1 Music
- **Style:** Minimal modern classical / ambient electronica hybrid. Think Apple keynote underscore — piano motif + warm pads + subtle sub-bass pulse.
- **Tempo:** ~96 BPM, but felt, not beaten.
- **Structure:**
  - 0–15s: sparse piano, paper/room tone, restrained.
  - 15s hit: a single low "thrum" transition → pads swell, pulse begins.
  - 15–45s: full but calm bed; arpeggio enters at ~30s as features accelerate.
  - 45–60s: resolve — pulse fades, piano returns, sustained chord into logo.
- **License:** Buy a premium license — Artlist, Musicbed, or Epidemic Sound. Do not use free/royalty-free-tier tracks for a film at this tier.

### 3.2 Sound Design (foreground details)
- Paper shuffle, distant pager, keyboard tick-cluster, soft "ping" on each UI reveal, low whoosh on transitions, a single heartbeat-style sub hit at 15s.
- UI interactions get a soft digital "tick" — never cartoonish.

### 3.3 Voice-over
- **Voice:** Indian English, male or female, 30–45, warm baritone/alto, confident, unhurried.
- **Recording:** studio, large-diaphragm condenser, slight de-ess, light compression, -16 LUFS.
- **Mix:** VO sits clearly above music; music ducks -6dB under VO automatically.

### 3.4 Loudness
- **Target:** -14 LUFS integrated (web/YouTube), true-peak -1 dBTP.

---

## 4. Complete Storyboard

Timecodes are for the 60s master. Each panel lists: visual, VO, on-screen copy, audio cue.

### ACT 1 — FRICTION (0–15s)

**Panel 1 — 0:00–0:03 · "The Weight"**
- **Visual:** Slow push across a hospital reception desk at golden hour. Stacks of paper registers, a tired administrator, a queue out of focus behind. Color graded cool/desaturated. No UI yet.
- **VO:** *"Every hospital runs on a thousand decisions a minute."*
- **On-screen:** —
- **Audio:** Room tone, paper shuffle, distant pager.

**Panel 2 — 0:03–0:06 · "The Fragments"**
- **Visual:** Quick, controlled cuts (4 cuts, 0.75s each): a handwritten token slip → a desktop with three legacy windows open → a phone ringing unanswered → a pharmacy rack. Each cut slightly tighter. Still desaturated.
- **VO:** *"Paper. Queues. Separate systems that never speak."*
- **On-screen:** —
- **Audio:** Each cut lands on a soft percussive tick. Tension rising.

**Panel 3 — 0:06–0:10 · "The Cost"**
- **Visual:** A doctor at a desk, looking at a chaotic screen, rubbing their temple. Hold on the face. Shallow depth of field.
- **VO:** *"And somewhere in that noise, care waits."*
- **On-screen:** —
- **Audio:** Music drops to near-silence. One breath.

**Panel 4 — 0:10–0:15 · "The Pivot"**
- **Visual:** A clean horizontal line of light (the **signal line**) draws left-to-right across the frame, washing the cool grade away. As it passes, the desk surface resolves into a sleek tablet running Sivayaan HMS. Color saturates to brand palette.
- **VO:** *"Sivayaan HMS brings it all into one calm signal."*
- **On-screen:** (fades in with the line) **One hospital. One system.**
- **Audio:** Low whoosh + sub hit on the line. Music begins its pulse.

### ACT 2 — CLARITY (15–45s)

**Panel 5 — 0:15–0:19 · "The Dashboard"**
- **Visual:** `[SCREEN: Dashboard]` — full-bleed on `--ink` background, gentle dolly-in (1.05x), 4° parallax tilt resolving flat. Teal "live" pulses animate on the realtime stats. Numerals count up smoothly.
- **VO:** *"See your entire hospital in a single glance — live, and in real time."*
- **On-screen (lower third):** **Real-time command center**
- **Audio:** Soft UI ping. Pulse bed sustains.

**Panel 6 — 0:19–0:23 · "OPD Flow"**
- **Visual:** `[SCREEN: OPD / Appointments]` — screen enters from right with parallax; cursor (a soft glowing dot, not an OS cursor) moves to a patient row and clicks; a clean appointment card blooms open. Cut to a doctor tapping a tablet — same UI reflected.
- **VO:** *"From appointment to consultation, in seconds."*
- **On-screen:** **OPD · Smart scheduling**
- **Audio:** Two soft ticks on the click events.

**Panel 7 — 0:23–0:27 · "IPD & Bed Management"**
- **Visual:** `[SCREEN: IPD / Bed Status]` — bed-grid view; beds color-code (occupied/available) with a soft bloom as statuses update. Camera slow push to a ward bay.
- **VO:** *"Beds, wards, discharges — always clear, always current."*
- **On-screen:** **IPD · Live bed status**
- **Audio:** Soft chime as a bed flips to "available" (sage).

**Panel 8 — 0:27–0:31 · "Pharmacy"**
- **Visual:** `[SCREEN: Pharmacy / Billing]` — bill line-items populate as a pharmacist scans a strip; stock counter ticks down; a "low stock" tag gently pulses amber, then resolves.
- **VO:** *"Pharmacy and billing that think as fast as you do."*
- **On-screen:** **Pharmacy · Billing · Stock**
- **Audio:** Rapid soft ticks matching item entry.

**Panel 9 — 0:31–0:35 · "Lab & Radiology"**
- **Visual:** `[SCREEN: Lab / Reports]` — a sample row moves from "Collected" → "Processing" → "Ready" with a smooth state transition. Result PDF slides in, then the same report appears on a doctor's tablet.
- **VO:** *"Results that travel from lab to doctor, the moment they're ready."*
- **On-screen:** **Lab · Radiology · Instant reports**
- **Audio:** Single "ready" ping.

**Panel 10 — 0:35–0:39 · "Reports & Analytics"**
- **Visual:** `[SCREEN: Reports / Analytics]` — a bar chart builds bar-by-bar, a line draws across, a revenue KPI counts up to a target number (use a believable dummy figure, never a real hospital's number).
- **VO:** *"And the insight to run it like a enterprise."*
- **On-screen:** **Analytics · Revenue · Operations**
- **Audio:** Chart-build ticks; arpeggio enters.

**Panel 11 — 0:39–0:45 · "The Connected Hospital"**
- **Visual:** Split-screen montage, 3 vertical panels: Reception · Doctor · Pharmacy — each running the same Sivayaan UI, all updating in sync. Pull back to reveal a faint **node mesh** connecting them. End on a wide shot of a calm, bright hospital corridor.
- **VO:** *"One system. Every role. Fully connected."*
- **On-screen:** **The complete digital hospital**
- **Audio:** Music peaks gently. Pulse strongest here.

### ACT 3 — TRUST (45–60s)

**Panel 12 — 0:45–0:49 · "Built for India, Ready for the Cloud"**
- **Visual:** A map-of-India silhouette with soft light nodes lighting up across cities (local support), then the camera pulls up into a stylized cloud layer; a tablet, a phone, and a desktop all show Sivayaan HMS responsively.
- **VO:** *"Built with local support. Cloud-ready. Mobile-friendly. And ready for what's next."*
- **On-screen:** **Local support · Cloud-ready · Mobile-friendly · AI-ready**
- **Audio:** Soft ascending pad.

**Panel 13 — 0:49–0:54 · "Enterprise-grade Trust"**
- **Visual:** `[SCREEN: Security / Roles]` or a clean abstract security visual — role-based access cards, a padlock-to-shield morph, encrypted-data particle stream. End on a small lock icon settling into the UI.
- **VO:** *"Secure by design. Trusted by hospitals that can't afford downtime."*
- **On-screen:** **Role-based access · Encrypted · Always on**
- **Audio:** Confident low pad.

**Panel 14 — 0:54–0:58 · "The Invitation"**
- **Visual:** A hospital owner (real footage, warm, mid-50s, trustworthy) walking a bright corridor, then turning to camera with a slight nod. Cut to the product on a wall display in a boardroom.
- **VO:** *"This is the calm hospital. This is Sivayaan HMS."*
- **On-screen:** (centered, large) **Sivayaan HMS**
- **Audio:** Music resolves to sustained chord.

**Panel 15 — 0:58–1:00 · "End Card / CTA"**
- **Visual:** `--ink` background. Sivayaan logo + wordmark center. Below it: **Book a Demo** button (trust-blue, 48px radius pill) with a soft glow. Below: website URL + phone. A faint signal line pulses once beneath.
- **VO:** — (music tails out)
- **On-screen:** **Book a Demo** · `sivayaan.com` · `+91 XXXXX XXXXX`
- **Audio:** Final soft ping on the CTA appearance; music tail to silence.

---

## 5. Shot List

Filename convention: `SH##_ACT##_TIMECODE_DESC.<ext>`.
Drop matching screenshots into `screens/` with the `[SCREEN: …]` name.

| # | Time | Tag / File | Type | Notes |
|---|------|-----------|------|-------|
| 1 | 0:00–0:03 | `SH01_B-roll_reception_golden.mp4` | B-roll | Hospital reception, paper, queue. Cool grade. |
| 2 | 0:03–0:04 | `SH02_B-roll_token_slip.mp4` | B-roll | Handwritten token, macro. |
| 3 | 0:04–0:05 | `SH03_B-roll_legacy_desktop.mp4` | B-roll | Cluttered legacy windows. |
| 4 | 0:05–0:06 | `SH04_B-roll_phone_ringing.mp4` | B-roll | Unanswered desk phone. |
| 5 | 0:06–0:07 | `SH05_B-roll_pharmacy_rack.mp4` | B-roll | Pharmacy shelves, slight motion. |
| 6 | 0:06–0:10 | `SH06_B-roll_doctor_frustrated.mp4` | B-roll | Doctor at desk, temple rub, shallow DOF. |
| 7 | 0:10–0:15 | `SH07_TRANSITION_signal_line.mp4` | FX / AE | Signal line wipes; tablet resolves with UI. |
| 8 | 0:15–0:19 | `[SCREEN: Dashboard]` → `SH08_UI_dashboard.png` | Screenshot comp | Full-bleed, dolly-in, live pulses, count-up. |
| 9 | 0:19–0:23 | `[SCREEN: OPD / Appointments]` → `SH09_UI_opd.png` | Screenshot comp | Cursor click, card bloom. Cross-cut tablet. |
| 10 | 0:23–0:27 | `[SCREEN: IPD / Bed Status]` → `SH10_UI_ipd_beds.png` | Screenshot comp | Bed-grid color states. |
| 11 | 0:27–0:31 | `[SCREEN: Pharmacy / Billing]` → `SH11_UI_pharmacy_billing.png` | Screenshot comp | Line-item populate, stock tick. |
| 12 | 0:31–0:35 | `[SCREEN: Lab / Reports]` → `SH12_UI_lab_reports.png` | Screenshot comp | State flow Collected→Ready. |
| 13 | 0:35–0:39 | `[SCREEN: Reports / Analytics]` → `SH13_UI_reports_analytics.png` | Screenshot comp | Chart build, KPI count-up. |
| 14 | 0:39–0:45 | `SH14_MONTAGE_connected_hospital.mp4` | Comp / B-roll | 3-panel split + node mesh + corridor. Needs `[SCREEN: Dashboard]`, `[SCREEN: OPD]`, `[SCREEN: Pharmacy]` reused. |
| 15 | 0:45–0:49 | `SH15_FX_india_cloud.mp4` | FX / AE | India nodes → cloud → multi-device. |
| 16 | 0:49–0:54 | `[SCREEN: Security / Roles]` → `SH16_UI_security.png` | Screenshot comp | Role cards + lock morph. (If no security screen, use abstract AE security visual.) |
| 17 | 0:54–0:58 | `SH17_B-roll_owner_corridor.mp4` | B-roll | Hospital owner, warm, nods to camera. Boardroom product wall. |
| 18 | 0:58–1:00 | `SH18_ENDCARD_cta.png` | FX / AE | Logo, wordmark, CTA pill, URL, phone. |

> Need these extra screenshots if available (nice-to-have, not blockers):
> `[SCREEN: Mobile Dashboard]`, `[SCREEN: Patient Registration]`, `[SCREEN: Inventory]`, `[SCREEN: EMR]`.

---

## 6. Subtitle Script (SRT)

Burned-in captions, 1080p safe area, white `Inter` 32px on a 60% opacity `--ink` bar, bottom-center. Sentence case. Max 32 chars/line.

```
1
00:00:01,000 --> 00:00:04,000
Every hospital runs on a
thousand decisions a minute.

2
00:00:04,000 --> 00:00:07,000
Paper. Queues.
Separate systems that never speak.

3
00:00:07,000 --> 00:00:11,000
And somewhere in that noise,
care waits.

4
00:00:11,000 --> 00:00:16,000
Sivayaan HMS brings it all
into one calm signal.

5
00:00:16,000 --> 00:00:21,000
See your entire hospital in a
single glance — live, in real time.

6
00:00:21,000 --> 00:00:25,000
From appointment to consultation,
in seconds.

7
00:00:25,000 --> 00:00:29,000
Beds, wards, discharges —
always clear, always current.

8
00:00:29,000 --> 00:00:33,000
Pharmacy and billing that think
as fast as you do.

9
00:00:33,000 --> 00:00:37,000
Results that travel from lab
to doctor, the moment they're ready.

10
00:00:37,000 --> 00:00:41,000
And the insight to run it
like an enterprise.

11
00:00:41,000 --> 00:00:47,000
One system. Every role.
Fully connected.

12
00:00:47,000 --> 00:00:52,000
Built with local support.
Cloud-ready. Mobile-friendly.
And ready for what's next.

13
00:00:52,000 --> 00:00:57,000
Secure by design.
Trusted by hospitals that can't
afford downtime.

14
00:00:57,000 --> 00:01:00,000
This is the calm hospital.
This is Sivayaan HMS.
```

---

## 7. Voice-over Script

**Tone notes:** Warm, steady, unhurried. ~130 wpm. Pauses marked with `/`.
Deliver lines with downward inflection at periods — confident, not salesy.

```
[0:00] Every hospital runs on a thousand decisions a minute.
[0:04] Paper. / Queues. / Separate systems that never speak.
[0:07] And somewhere in that noise, / care waits.
[0:11] Sivayaan HMS brings it all into one calm signal.

[0:15] See your entire hospital in a single glance —
       live, / and in real time.
[0:19] From appointment to consultation, / in seconds.
[0:23] Beds, wards, discharges — / always clear, / always current.
[0:27] Pharmacy and billing / that think as fast as you do.
[0:31] Results that travel from lab to doctor,
       the moment they're ready.
[0:35] And the insight to run it like an enterprise.
[0:39] One system. / Every role. / Fully connected.

[0:45] Built with local support. / Cloud-ready. / Mobile-friendly.
       And ready for what's next.
[0:49] Secure by design. / Trusted by hospitals that can't afford downtime.

[0:54] This is the calm hospital. / This is Sivayaan HMS.
```

**Word count:** ~95 words → ~95s at 60wpm, ~44s at 130wpm budgeted for VO; the rest is music/air. Confirms pacing is unhurried.

**Delivery directions by line:**
- Lines 1–3 (friction): slightly lower, heavier, slower.
- Line 4 (pivot): lift, brighten — the turn.
- Lines 5–11 (clarity): even, crisp, confident. Smile on "in seconds."
- Lines 12–13 (trust): grounded, warm, reassuring.
- Line 14 (invitation): slow, final, definitive. Land "Sivayaan HMS."

---

## 8. Motion Graphics Plan (After Effects)

### 8.1 Project setup
- **Comp:** 3840×2160, 24fps, 60s. Working color space: Rec.709.
- **Deliverable render:** 1920×1080 ProRes 422 HQ master + H.264 1080p web + captioned version.
- **Folder structure:**
  - `01_Screens/` — imported PNG screenshots, pre-comped per shot.
  - `02_B-roll/` — live footage plates.
  - `03_FX/` — signal line, node mesh, transitions, particles.
  - `04_Text/` — titles, lower-thirds, end card.
  - `05_Audio/` — music, SFX, VO stems.
  - `06_Renders/` — output.

### 8.2 UI screenshot compositing recipe (per screen)
1. Place screenshot in its own pre-comp at native resolution.
2. Parent pre-comp to a 3D-null; add a **4° X-rotation** + slight Z-push for parallax; animate rotation → 0° and Z → 0 over the first 12 frames of the shot (`ease-out-quint`).
3. Add **Camera** (35mm, slow dolly-in 1.05x over shot duration).
4. Background: `--ink` or `--clinical` solid + a 6% opacity **node mesh** looping behind.
5. Drop shadow: soft, 40px blur, 0px distance, 35% opacity, 90° (down).
6. Corner radius: 40px mask (or RoundedRectangle effect).
7. Animate highlights: mask teal "live" pulses (1s loop, opacity 40→100→40); numerals via **Slider Control** + `easeOut` count-up over 20 frames.
8. Cursor: 24px soft glowing dot (trust-blue, 60% glow) with 8px blur — never an OS arrow. Motion path eased.

### 8.3 Signature FX builds
- **Signal Line (Panel 4, 14):** a 2px trust-blue horizontal stroke with a 60px-wide glow trail, drawn via `Trim Paths` 0→100% over 1.5s; a `Displacement Map` ripple follows the tip; a `CC Blobbylize` softens the leading edge. On contact it desaturates→saturates the underlying grade using a `Luma Inverted Matte`.
- **Node Mesh:** `Plexus` or a hand-built shape layer grid (24×14 nodes), 1px lines at 12% opacity, slow 8s drift via `Wiggle` on a null. Used as ambient texture only.
- **State Transitions (Lab, Bed, Pharmacy):** pre-comp each state as a layer; crossfade 8 frames with `ease-out-quint`; add a 1px teal line that sweeps the row as it changes.
- **Chart Build (Reports):** bars via `Trim Paths` on rectangles, staggered 3 frames each; line via `Trim Paths`; KPI via Slider count-up. Keep on-brand colors only.
- **India + Cloud (Panel 12):** silhouette path with 12 node lights scaling 0→100% staggered; camera pulls up; a `CC Sphere`-derived soft cloud layer fades in; three device mockups (desktop/tablet/phone) fan in with parallax, each showing `[SCREEN: Dashboard]` reframed.
- **End Card:** logo scales 92→100% ease-out-quint over 18 frames; CTA pill fades 0→100% at +6 frames with a 1.5s looping 4% glow pulse; signal line draws once beneath.

### 8.4 Persistent elements
- Lower-third wordmark from 0:15: bottom-left, 120×40px, 60% opacity, no animation.
- Optional top-right "live" teal dot pulsing 1s loop at 50% opacity — only during Act 2.

### 8.5 Color & grade
- **Act 1 grade:** cool, desaturated — lift shadows toward blue, reduce saturation 20%.
- **Transition:** signal line wipes the grade to neutral.
- **Act 2–3 grade:** warm-neutral, full saturation, slight contrast lift, subtle vignette (8% darkening at corners).
- **LUT:** custom, subtle. Avoid stock film looks.

### 8.6 Caption render
- Burn captions from the SRT (Section 6) in a separate render pass so one master can carry toggled captions.

---

## 9. Video Editing Plan

### 9.1 Tool
Premiere Pro (offline) or DaVinci Resolve (free-tier is enough). After Effects for all graphics; linked via DynamicLink or exported ProRes proxies.

### 9.2 Edit structure (timeline)
- **V1:** B-roll + UI comps (the spine).
- **V2:** FX/transition layer (signal line, node mesh, particles).
- **V3:** Titles, lower-thirds, end card.
- **V4 (optional):** caption burn pass.
- **A1:** VO. **A2:** Music. **A3:** SFX. **A4:** Room tone / ambience bed.

### 9.3 Cut rhythm
- Act 1: cuts on the beat every ~0.75s — four tight cuts in Panel 2, then a long 4s hold on the doctor.
- Transition: one continuous 5s move — no cuts.
- Act 2: cuts every 4s, locked to UI shot duration. Cuts land on VO line starts.
- Act 3: cuts slow to 4–5s; end card holds 2s.
- **J-cuts:** VO for the next shot begins 6–10 frames before the cut. Music carries across every cut in Act 2.

### 9.4 Color pipeline
1. Normalize all B-roll to Rec.709.
2. Apply Act 1 cool grade (Section 8.5).
3. Transition to neutral at 0:13.
4. Apply Act 2–3 warm-neutral grade.
5. Final vignette + film grain (3%, very fine) for cohesion.
6. Render ProRes 422 HQ master.

### 9.5 Audio mix pass
1. Lay VO on A1; normalize to -6 dBFS peak.
2. Music on A2; sidechain duck -6dB under VO.
3. SFX on A3; place to picture; -12 dBFS.
4. Room tone on A4 at -30 dBFS under everything.
5. Master to -14 LUFS integrated, -1 dBTP.

### 9.6 Export specs
| Deliverable | Codec | Resolution | Bitrate | Audio |
|------|------|------|------|------|
| Master | ProRes 422 HQ | 3840×2160 | ~HQ | 48k PCM |
| Web primary | H.264 | 1920×1080 | 16 Mbps | AAC 320 |
| Web captions | H.264 | 1920×1080 | 16 Mbps | AAC 320 + burned SRT |
| Social square/vertical | H.264 | 1080×1080 / 1080×1920 | 12 Mbps | AAC 256 |

> You selected 16:9 only; the square/vertical rows are included for a future reframe, not required now.

---

## 10. AI Video Prompts

Use these in **Veo 3 / Sora / Runway Gen-3 / Kling** for B-roll and environmental shots ONLY.
Never use them to generate product UI. Settings: 16:9, 24fps, 5s clips, cinematic.

### 10.1 Reception / Friction plates
```
Cinematic 5s shot, slow dolly-in across a hospital reception desk at golden hour,
stacks of paper patient registers, a tired South Asian administrator in her 40s
writing by hand, a soft-focus queue of waiting patients behind. Desaturated cool
color grade, shallow depth of field, ambient warm desk lamp, 35mm lens, no text,
no UI, photorealistic, Apple keynote aesthetic.
```

### 10.2 Doctor frustration
```
Cinematic 5s portrait, South Asian doctor in a white coat mid-40s sitting at a
cluttered desk lit by a monitor, rubbing his temple, eyes tired, slight slow
head turn toward camera. Cool desaturated grade, shallow depth of field, 50mm
lens, soft window light from the side, no text, no UI, photorealistic,
documentary realism.
```

### 10.3 Signal-line transition environment
```
Cinematic 5s shot, a clean modern hospital corridor in soft blue hour light,
camera at desk height slowly pushing forward, the lighting subtly shifts from
cool desaturated to warm neutral as a soft horizontal light streak passes
through the frame left to right, leaving clarity and warmth behind it.
Photorealistic, no people, no text, no UI, 24mm lens, premium enterprise
aesthetic.
```

### 10.4 Connected corridor (Act 2 end)
```
Cinematic 5s wide shot, a bright calm modern hospital corridor, soft daylight,
a few staff moving quietly, camera slowly pulling back, warm neutral color
grade, subtle lens flare from a window, 16mm lens, photorealistic, no text, no
UI, reassuring and premium, Apple healthcare aesthetic.
```

### 10.5 Hospital owner / trust
```
Cinematic 5s shot, a distinguished South Asian hospital owner mid-50s in
business attire walking down a bright modern hospital corridor toward camera,
confident warm expression, slight nod at the end, soft natural window light,
warm neutral grade, 35mm lens, shallow depth of field, photorealistic,
trustworthy, no text, no UI.
```

### 10.6 India + cloud (abstract, AE-friendly base)
```
Cinematic 5s abstract shot, dark navy background, a faint glowing map of India
made of soft light points, dots gently pulsing and connecting with thin lines,
camera slowly rising and pulling back to reveal a soft stylized cloud layer
above, deep blue and teal palette, premium enterprise motion graphics aesthetic,
no text, no UI, photorealistic lighting, 24fps.
```

### 10.7 Device B-roll (for cloud-ready section)
```
Cinematic 5s shot, a clean white desk in soft daylight, a desktop monitor, a
tablet, and a smartphone arranged in a gentle fan, each showing a blank neutral
app interface (abstract only, no readable text), camera slow orbit, slight
parallax, warm neutral grade, 35mm lens, photorealistic, premium product
photography aesthetic.
```

### 10.8 Ambient particle / data texture (for node mesh base)
```
Cinematic 5s abstract shot, dark navy background, a slow drifting field of soft
teal and blue light points connected by very thin faint lines, subtle parallax,
ambient data-network texture, premium enterprise aesthetic, no text, no UI,
24fps, very low contrast, looping-friendly.
```

### 10.9 Optional: hands on tablet (OPD cross-cut)
```
Cinematic 5s over-the-shoulder shot, a South Asian doctor in a white coat
holding a tablet, tapping the screen gently with one finger, soft daylight from
a window, warm neutral grade, 35mm lens, shallow depth of field, screen is
blank/overexposed (no UI), photorealistic, calm and premium.
```

### 10.10 Prompting rules (paste at the top of every session)
```
RULES:
- 16:9, 24fps, 5s, cinematic.
- Photorealistic, premium enterprise aesthetic (Apple/Salesforce tier).
- No on-screen text. No fake UI. No cartoonish motion.
- South Asian talent where people appear.
- Warm neutral or cool desaturated grade as specified per shot.
- Camera moves are slow: dolly, push, pull, orbit, parallax. Never handheld shake.
```

---

## 11. Production Checklist

### 11.1 Pre-production
- [ ] Collect Sivayaan logo (SVG/PNG transparent) + brand color confirmation.
- [ ] Collect website URL, demo phone, and final CTA copy.
- [ ] Upload screenshots to `/marketing/video-promo/screens/` per Shot List filenames.
- [ ] Verify screenshots are anonymized — no real patient names/numbers.
- [ ] License premium music track (Artlist / Musicbed / Epidemic).
- [ ] Book VO artist (Indian English) + studio time.
- [ ] Cast: administrator (40s), doctor (mid-40s), hospital owner (mid-50s) — or confirm AI B-roll route.
- [ ] Lock the creative with stakeholders against this document.

### 11.2 Asset preparation
- [ ] Clean each screenshot: hide any test data, align to 16:9 safe area, export PNG at 2x.
- [ ] Pre-comp each `[SCREEN: …]` in After Effects per Section 8.2.
- [ ] Generate AI B-roll clips (Section 10) at 4K where possible; review for artifacts.
- [ ] Build FX library: signal line, node mesh, state-transition sweep, chart build.
- [ ] Build end-card comp (logo, wordmark, CTA, URL, phone).

### 11.3 Production / shoot (if shooting live B-roll instead of AI)
- [ ] Scout a bright, modern hospital location; secure permissions.
- [ ] Shoot 4K/6K, 24fps, log profile; slate each shot to the Shot List.
- [ ] Capture room tone per location for the ambience bed.

### 11.4 Post-production
- [ ] Assemble V1 spine in Premiere/Resolve per Section 9.2.
- [ ] Integrate AE comps via DynamicLink or ProRes proxies.
- [ ] First rough cut → review pacing against the storyboard timecodes.
- [ ] Record VO; edit to picture with J-cuts.
- [ ] Sound design pass (SFX to picture).
- [ ] Music license + edit; sidechain duck under VO.
- [ ] Color grade pass (Act 1 cool → Act 2/3 warm-neutral).
- [ ] Caption pass from SRT.
- [ ] Final mix to -14 LUFS / -1 dBTP.

### 11.5 QA & delivery
- [ ] Review on a calibrated monitor and a laptop screen (both grades must read).
- [ ] Check captions for accuracy, timing, safe-area placement.
- [ ] Verify no real patient data is visible in any frame.
- [ ] Confirm CTA, URL, and phone render correctly on the end card.
- [ ] Export master + web + captioned versions per Section 9.6.
- [ ] Stakeholder sign-off.
- [ ] Upload to YouTube (unlisted for review → public on launch), embed on site.

### 11.6 Distribution readiness (future)
- [ ] Reframe 9:16 and 1:1 versions when ready.
- [ ] Prepare 6s and 15s cutdowns for paid campaigns.
- [ ] Generate thumbnail (hospital corridor + "Sivayaan HMS" + CTA).

---

## 12. Deliverables Summary

| Deliverable | Section | Status |
|------|------|------|
| Creative strategy & concept | 1 | ✅ Ready |
| Visual system (color/type/motion) | 2 | ✅ Ready |
| Sound & music direction | 3 | ✅ Ready |
| Complete storyboard (15 panels) | 4 | ✅ Ready |
| Shot list (18 shots) | 5 | ✅ Ready |
| Subtitle script (SRT) | 6 | ✅ Ready |
| Voice-over script | 7 | ✅ Ready |
| Motion graphics plan (AE) | 8 | ✅ Ready |
| Video editing plan | 9 | ✅ Ready |
| AI video prompts (10) | 10 | ✅ Ready |
| Production checklist | 11 | ✅ Ready |

**Next action for you:** upload the screenshots (and logo/brand colors if you have them) to `/marketing/video-promo/screens/`, and I'll map each `[SCREEN: …]` tag to its file and produce the After Effects pre-comp specs per shot.

*Prepared for Sivayaan Technologies · Sivayaan HMS premium promotional film · v1.0*
