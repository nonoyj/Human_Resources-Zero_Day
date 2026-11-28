#!/usr/bin/env python3
"""
HR//0D -- does every .ps1 file actually PARSE?

WHY THIS EXISTS (v126)
    For eight rounds the art gate reported green while never running: a stray `if (-not $SkipCompile) {`
    at line ~95 (present since defc45c) left a `try` without its `catch`, and PowerShell -- which parses the
    WHOLE file before it runs a single line -- refused to load it. Every "the gate passed" in those rounds was
    a claim about a file that no interpreter had ever accepted.

    The first version of this tool counted braces and looked for `try`/`catch` with a regex. It caught that
    bug, but a brace count is not a parse: it cannot see a `}` that closes the wrong block, an unterminated
    here-string, or a `function` that swallowed the rest of the file. It now asks PowerShell itself, via
    System.Management.Automation.Language.Parser -- the same parser the player's box uses -- when pwsh is
    available, and falls back to the honest brace check (clearly labelled FALLBACK) when it is not.

    FALLBACK is not a pass. It says so, and exits 2, so nothing downstream can quote it as green.

USAGE
    python3 tools/check_powershell_syntax.py            # all of scripts/*.ps1 + *.ps1
    python3 tools/check_powershell_syntax.py FILE ...   # just these
    PWSH=/tmp/pwsh/pwsh python3 tools/check_powershell_syntax.py

EXIT
    0  PowerShell parsed every file.     1  at least one file does not parse.
    2  no pwsh found and the fallback check ran (NOT a pass, NOT a failure).
"""

import glob
import os
import re
import shutil
import subprocess
import sys
import tempfile

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

PARSER_PS1 = r"""
$errs = $null; $toks = $null
foreach ($p in $args) {
  $errs = $null
  $null = [System.Management.Automation.Language.Parser]::ParseFile($p, [ref]$toks, [ref]$errs)
  if ($errs -and $errs.Count) {
    foreach ($e in $errs) { '{0}|{1}|{2}|{3}' -f $p, $e.Extent.StartLineNumber, $e.ErrorId, $e.Message }
  } else { '{0}|OK' -f $p }
}
"""


def find_pwsh():
    for cand in (os.environ.get("PWSH"), "pwsh", "powershell", "/tmp/pwsh/pwsh"):
        if not cand:
            continue
        path = shutil.which(cand) or (cand if os.path.isfile(cand) else None)
        if path:
            return path
    return None


def parse_with_pwsh(pwsh, files):
    """Returns [(path, line, id, message)]; an OK file yields (path, 0, 'OK', '')."""
    script = os.path.join(tempfile.mkdtemp(), "parse.ps1")
    with open(script, "w", encoding="utf-8-sig") as fh:
        fh.write(PARSER_PS1)
    cmd = [pwsh, "-NoLogo", "-NoProfile", "-NonInteractive", "-File", script] + files
    out = subprocess.run(cmd, capture_output=True, text=True, timeout=600).stdout
    rows = []
    for line in out.splitlines():
        line = line.rstrip()
        if not line or "|" not in line:
            continue
        parts = line.rsplit("|", 3)
        if len(parts) == 2 and parts[1] == "OK":
            rows.append((parts[0], 0, "OK", ""))
        elif len(parts) == 4:
            rows.append((parts[0], int(parts[1]), parts[2], parts[3]))
        # anything else is pwsh chatter, not a result
    return rows


# ------------------------------------------------------------------ fallback: balance only
def strip_code(text):
    """Blank comments and string CONTENTS, keeping newlines, so braces inside them cannot vote."""
    out = []
    i, n = 0, len(text)
    in_block = in_line = in_single = in_double = in_here = False
    here_tag = None
    while i < n:
        ch = text[i]
        two = text[i:i + 2]
        if in_here:
            m = re.match(r"\n\s*" + re.escape(here_tag or "") + r"\s*'@", text[i - 1:])
            if m:
                out.append("\n" * text[i - 1:i - 1 + len(m.group(0))].count("\n"))
                i = i - 1 + len(m.group(0))
                in_here = False
                continue
            out.append("\n" if ch == "\n" else " ")
            i += 1
            continue
        if in_block:
            if two == "#>":
                in_block = False
                i += 2
                continue
            out.append("\n" if ch == "\n" else " ")
            i += 1
            continue
        if in_line:
            if ch == "\n":
                in_line = False
                out.append("\n")
            else:
                out.append(" ")
            i += 1
            continue
        if in_single:
            if ch == "'":
                in_single = False
                out.append("'")
            else:
                out.append("\n" if ch == "\n" else " ")
            i += 1
            continue
        if in_double:
            if ch == '"':
                in_double = False
                out.append('"')
            elif ch == "`" and i + 1 < n:
                out.append("  ")
                i += 2
                continue
            else:
                out.append("\n" if ch == "\n" else " ")
            i += 1
            continue
        # not inside anything
        if two == "<#":
            in_block = True
            out.append("  ")
            i += 2
            continue
        if ch == "#":
            in_line = True
            out.append(" ")
            i += 1
            continue
        if two == "@'":
            in_here, here_tag = True, "@'"
            out.append("  ")
            i += 2
            continue
        if ch == "'":
            in_single = True
            out.append("'")
            i += 1
            continue
        if ch == '"':
            in_double = True
            out.append('"')
            i += 1
            continue
        out.append(ch)
        i += 1
    return "".join(out)


OPEN = {"{": "}", "(": ")", "[": "]"}
CLOSE = {v: k for k, v in OPEN.items()}


def fallback_check(path):
    with open(path, encoding="utf-8-sig") as fh:
        code = strip_code(fh.read())
    rows = []
    stack = []
    line = 1
    for ch in code:
        if ch == "\n":
            line += 1
        elif ch in OPEN:
            stack.append((ch, line))
        elif ch in CLOSE:
            if not stack:
                rows.append((path, line, "StrayClose", "stray '%s' with nothing open" % ch))
            elif stack[-1][0] != CLOSE[ch]:
                o, ol = stack.pop()
                rows.append((path, line, "Mismatch",
                             "'%s' closes a '%s' opened on line %d" % (ch, o, ol)))
            else:
                stack.pop()
    for o, ol in stack:
        rows.append((path, ol, "Unclosed", "'%s' opened here is never closed" % o))
    # every `try` needs a catch or a finally somewhere inside its own block
    for m in re.finditer(r"\btry\b\s*\{", code):
        depth, i, found = 0, m.end() - 1, False
        while i < len(code):
            if code[i] == "{":
                depth += 1
            elif code[i] == "}":
                depth -= 1
                if depth == 0:
                    break
            elif depth == 1 and re.match(r"(catch|finally)\b", code[i:]):
                found = True
                break
            i += 1
        if not found:
            rows.append((path, code[:m.start()].count("\n") + 1, "MissingCatchOrFinally",
                         "this try has no catch or finally"))
    return rows


def main(argv):
    if argv:
        files = [os.path.abspath(a) for a in argv]
    else:
        files = sorted(glob.glob(os.path.join(REPO, "scripts", "*.ps1"))) + \
                sorted(glob.glob(os.path.join(REPO, "*.ps1")))
    files = [f for f in files if os.path.isfile(f)]
    if not files:
        print("no .ps1 files found")
        return 1

    pwsh = find_pwsh()
    if pwsh:
        rows = parse_with_pwsh(pwsh, files)
        bad = [r for r in rows if r[2] != "OK"]
        for path, line, err_id, msg in bad:
            print("%s:%d  %s  %s" % (os.path.relpath(path, REPO), line, err_id, msg))
        if bad:
            print("\nFAIL: %d PowerShell error(s) in %d file(s) -- no script with a parse error can run"
                  % (len(bad), len(files)))
            return 1
        print("PASS: %d PowerShell file(s) parsed by %s"
              % (len(files), os.path.basename(pwsh)))
        return 0

    bad = []
    for f in files:
        bad += fallback_check(f)
    for path, line, err_id, msg in bad:
        print("%s:%d  %s  %s" % (os.path.relpath(path, REPO), line, err_id, msg))
    if bad:
        print("\nFAIL: %d structural error(s) in %d file(s) (fallback check -- no pwsh on this box)"
              % (len(bad), len(files)))
        return 1
    print("FALLBACK: %d file(s) balance and every try has a catch/finally -- NOT a parse. "
          "Install pwsh for the real check; do not quote this as green." % len(files))
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
