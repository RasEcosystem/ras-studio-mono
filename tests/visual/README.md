# UI screenshots

Captures the RasStudio Home page for all supported themes in desktop and mobile sizes.
It also captures every primary route with the Carbon theme to catch layout and copy regressions across pages.
The test starts Kestrel without Electron on an explicit `127.0.0.1` diagnostic port.

Requires:

- .NET SDK
- curl
- Chromium, Chromium Browser, or Google Chrome

Run:

```bash
tests/visual/run-screenshots.sh
```

Screenshots are saved to `artifacts/visual`.

You can pass another output directory as the first argument or select a
Chromium-compatible executable with the `BROWSER` environment variable.
