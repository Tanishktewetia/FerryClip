# FerryClip â€” Standing Rules (read this before doing anything)

These rules apply to **every session, every phase, no exceptions.** They come from `plan.md`. If you ever feel unsure, re-read this file before acting â€” do not guess, do not skip a rule to save time.

---

## 1. Phase discipline
- Work on **one phase at a time**, in the order given in `plan.md`.
- **Never start the next phase** until the human explicitly says "start Phase N."
- Complete the whole phase yourself (code, build, unit tests) without pausing for trivial questions. Only stop early if truly blocked (see rule 2).

## 2. No attempt limit on fixing errors
- Keep trying different approaches until the phase is genuinely working. Do not stop mid-fix just because a few attempts failed.
- The agent stops only when the phase is complete and ready for the human's manual test â€” or when it hits a real architectural blocker (something `plan.md`/`architecture.md` doesn't actually support, not just a bug to fix). In that case, write the blocker into `LOG.md` and ask the human clearly.

## 3. Git â€” the most important rule
- **Never run `git add`, `git commit`, or `git push` unless the human has explicitly said something like "approved, push it" for this specific phase.**
- Until that green light, all work stays as uncommitted local changes.
- Before every commit, show the human the exact list of files about to be staged (`git status`) and wait for confirmation â€” do not just commit.
- Confirm at the start of every session that the working directory is still the correct git repo (`git remote -v` should show https://github.com/Tanishktewetia/FerryClip.git). If it doesn't, STOP and tell the human before writing any code.

## 4. No vague files to GitHub â€” ever
- Build output (`bin/`, `obj/`, `build/`, `app/build/`, `.gradle/`, `dist/`), IDE files, local keystores/certs, `local.properties`, and any scratch/spike/temp file (e.g. "Pasted text.txt", "notes.txt") must be `.gitignore`d â€” never committed.
- Keep such files locally if useful; they just never go to GitHub.
- Before staging, be able to explain every single file in one sentence. If you can't, ask the human instead of guessing.

## 5. LOG.md â€” mandatory every phase
- Append one entry per phase, in this exact format:
```md
## Phase N â€” <short title> â€” <date>
**Status:** Done / Partial / Blocked
**Built:** <bullets>
**Files added/changed:** <list>
**Design note (if UI):** <palette, layout choice, why>
**Known issues:** <bullets, or "none">
**Manual test required:** <yes/no>
```
- Never rewrite or delete old entries â€” always append.
- Keep `LOG.md` accurate: if something changes (e.g. the repo gets properly git-initialized), update the relevant known-issue line rather than leaving stale information in place.
- Never write clipboard content into `LOG.md`, logs, or commit messages â€” not even a snippet.

## 6. UI is a top priority
- Every UI-touching phase must meet the bar in `plan.md` section 2: one clear status-first screen, real color palette, dark mode, no default-gray placeholder look, Windows tray-first popover style, Android Material 3.
- Include a one-paragraph design note in the phase report whenever UI is touched.

## 7. Product goal: no setup friction
- The product promise is: install the app, connect to the same Wi-Fi/hotspot, and it works. No Developer options, no Wireless debugging, no companion/helper apps (e.g. no Shizuku), no root, no keyboard-switching.
- PC -> phone clipboard sync must be fully automatic, zero taps.
- Phone -> PC is the one exception: a single tap (e.g. on a notification) is acceptable, because Android blocks silent background clipboard reads by design. Anything beyond one tap, or any setup step beyond installing the app and granting normal permissions, is out of scope â€” flag it and ask before implementing.

## 8. No scope creep
- Do not add features, screens, or refactors the current phase doesn't list.
- Do not add new dependencies without listing them in the report first.
- Do not silently change anything in `architecture.md` â€” flag it and ask.

## 9. Testing boundaries
- You cannot test on the phone or verify real Wi-Fi/hotspot/notification behavior. Never claim something "works on the phone."
- Say "built, ready for you to test" and give exact numbered manual test steps with expected results.
- The human tests manually and reports back before anything is approved.

## 10. End-of-phase report (every phase, in chat)
1. What was built
2. Design note (if UI)
3. Exact build/run commands
4. Numbered manual test steps + expected results
5. Known issues / not done
6. What logs to send if something fails
7. End with: "Waiting for your test results before I commit."

---

**If any instruction from the human seems to conflict with these rules, point out the conflict and ask before proceeding.**

