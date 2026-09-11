# Desktop lifecycle smoke test

Starts RasStudio through ElectronNET.Core in unpackaged, .NET-first mode. Electron
creates the Blazor window on the current display, closes it after startup, and
must stop the local Kestrel backend with it. CI runners need an X11 display or a
virtual display such as Xvfb.

Run on Linux with:

```bash
tests/SmokeTests/Desktop/run-smoke.sh
```

The test disables GPU acceleration and Chromium sandboxing for the test process.
The application's normal browser-window configuration keeps sandboxing enabled.
It uses a temporary generated manifest with `singleInstance=false`, keeping the
test isolated from a production RasStudio instance that may already be open.
The production manifest is still checked for `singleInstance=true`.

To verify the built Linux AppImage instead of the unpackaged host, run:

```bash
tests/SmokeTests/Desktop/run-packaged-linux-smoke.sh
```
