# Desktop lifecycle smoke test

Starts RasStudio through ElectronNET.Core in unpackaged, .NET-first mode. Electron
runs headlessly, creates the Blazor window, closes it after startup, and must stop
the local Kestrel backend with it.

Run on Linux with:

```bash
tests/desktop/run-smoke.sh
```

The `--no-sandbox` Electron argument is used only by this isolated headless test.
Packaged applications keep Chromium sandboxing enabled.
