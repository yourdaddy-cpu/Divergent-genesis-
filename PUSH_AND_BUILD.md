# Push & build — one command

```bash
./Tools/publish.sh
```

That's the whole thing. It pushes to
`github.com/yourdaddy-cpu/Divergent-genesis-`, waits for CI, downloads the APK
next to the script, and prints the direct download URL. First run takes about
**40 minutes** (most of it IL2CPP compiling C++).

**Before the first run, add the Unity licence secret** — step 2 below. Without it
the build fails at activation.

<details>
<summary>Or do it by hand, step by step</summary>

---
## 1. Push the code (2 commands)

The repository is fully committed locally at `/home/user/divergent-genesis`
(commit `a9d9195`, 119 files) and pointed at your remote. **I could not push it
myself** — this sandbox has no GitHub credentials (no `gh`, no token, no stored
credentials), and I will not ask you to paste a personal access token into a chat.

Pick whichever is easiest:

### Option A — GitHub CLI (recommended)

```bash
gh auth login          # once; browser sign-in, no token copy-pasting
cd divergent-genesis
git push -u origin main
```

### Option B — HTTPS + a personal access token

Create a token at <https://github.com/settings/tokens/new> with the `repo` scope,
then:

```bash
cd divergent-genesis
git remote set-url origin https://<your-username>:<token>@github.com/yourdaddy-cpu/Divergent-genesis-
git push -u origin main
```

### Option C — GitHub Desktop / VS Code

Open the folder, publish branch `main` to your GitHub account, and tick
*push to `https://github.com/yourdaddy-cpu/Divergent-genesis-`*.

> The remote repo is **public and empty** (zero commits), so the first push is a
> normal, non-forced push.

---

## 2. Add the Unity licence secret (required)

Without this the workflow will start, install Unity, and then **fail at licence
activation** — you will get a red build and no APK.

**Get a free Personal licence**

1. Sign in at <https://license.unity3d.com/manual>
2. Select **Personal** → download the `.ulf` file

**Add it to the repo**

1. Repo → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**
2. Name: `UNITY_LICENSE`
3. Value: the **entire contents** of the `.ulf` file (it starts with
   `<DeveloperData ...` and ends with `</DeveloperData>`) — paste all of it

For a Professional licence, also add `UNITY_SERIAL` (`XXXX-XXXX-XXXX-XXXX`).
GameCI can also read secrets named `UNITY_LICENSE_FILE`.

---

## 3. Watch the build

<https://github.com/yourdaddy-cpu/Divergent-genesis-/actions>

Two jobs run in order:

| Job | Time | What it does |
| --- | --- | --- |
| **Type-check scripts** | ~10 s | `./Tools/verify.sh` — type-checks all 40 scripts and validates the Unity YAML. No licence needed. |
| **Build Android APK** | ~30–45 min | GameCI installs Unity 6000.0.23f1, activates the licence, calls `DGBuildScript.BuildAndroid`. Only runs if type-check passes. |

You can also start it manually: **Actions → Divergent Genesis - Android → Run workflow**.

---

## 4. Download the APK

In the run summary, at the bottom:

> **Artifacts → divergent-genesis-apk**

The file inside is `divergent-genesis.apk`.

Or with the CLI:

```bash
gh run list
gh run download --repo yourdaddy-cpu/Divergent-genesis- -n divergent-genesis-apk
```

Artifacts are retained **30 days**.

### Install on the phone

```bash
adb install -r divergent-genesis.apk
```

Or copy it to the phone and open it (allow *install from unknown sources*).

---

## If the build fails

Open the failed run → the red job → **Annotations** / step logs. The two most
likely failures:

**"No valid Unity Editor license found"** → `UNITY_LICENSE` is missing,
mis-pasted, or was created for a different machine. Re-download it from the
manual page and replace the secret.

**Compilation errors in the Unity editor** → these would mean the API stub in
`Tools/CompileCheck/UnityStub.cs` disagrees with real Unity 6. That is a genuine
bug in the stub, and the fix is to correct the stub signature — the game code was
already validated against it. Send me the log and I'll patch it.

---

## After you push

Tell me and I'll pick it up from there. I cannot push or read your Actions
artifacts from this sandbox — both need credentials I don't have — so anything
after the push has to be you running the command and sending me the output.

</details>

---

## Windows 10

Everything runs from **Git Bash**, not PowerShell. If you don't have it, install
Git for Windows (<https://git-scm.com/download/win>) and open "Git Bash" from the
Start menu.

```bash
# 1. install dependencies (once)
winget install Git.Git
winget install Microsoft.DotNet.SDK.8
winget install Python.Python.3

# 2. restart Git Bash so it picks up the new PATH, then:
cd /c/path/to/Divergent-genesis

# 3. authenticate (opens a browser)
gh auth login
gh auth setup-git

# 4. check the project is sound (optional but fast - ~15s)
./Tools/verify.sh

# 5. publish
./Tools/publish.sh
```

**If the scripts say `Permission denied`**, Git needs the execute bit:

```bash
git update-index --chmod=+x Tools/publish.sh Tools/verify.sh
```

**If `./Tools/verify.sh` can't find `python`**, Python isn't on PATH in Git Bash.
It is not required for the build — only for the optional structure check:

```bash
export PATH="$PATH:/c/Users/$USERNAME/AppData/Local/Programs/Python/Python313"
```

### Opening it in Unity

1. Unity Hub → **Installs** → **Install Editor** → pick **6000.0.23f1**
   (the exact version in `ProjectSettings/ProjectVersion.txt`)
2. **Add** → select the `Divergent-genesis` folder
3. Wait for the first import (resolves packages, imports the four shaders)
4. Open `Assets/Scenes/Main.unity` → **Play**

> **One manual step is unavoidable:** Unity needs an interactive licence
> activation on a fresh machine. If Unity Hub says *no licence*, click
> **Manage → Add → Get a free personal licence**. The CI build does not need
> this — it uses the `UNITY_LICENSE` secret instead.

### Line endings

`.gitattributes` pins every text file to LF, so a Windows checkout won't rewrite
the whole tree as CRLF and make the Linux CI diff appear to change every file.
Don't delete it.
