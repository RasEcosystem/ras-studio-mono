const {app} = require("electron");

function isLocalApplicationUrl(value) {
    try {
        const url = new URL(value);
        return url.protocol === "http:" &&
            (url.hostname === "127.0.0.1" ||
                url.hostname === "localhost" ||
                url.hostname === "[::1]");
    } catch {
        return false;
    }
}

function secureApplicationWindow(contents) {
    if (contents.getType() !== "window") {
        return;
    }

    contents.session.setPermissionRequestHandler(
        (_webContents, _permission, callback) => callback(false));

    contents.on("will-navigate", (event, url) => {
        if (!isLocalApplicationUrl(url)) {
            event.preventDefault();
        }
    });

    contents.setWindowOpenHandler(() => ({action: "deny"}));
}

exports.onStartup = function onStartup() {
    // ElectronNET's generated lock runs too late to prevent a duplicate backend.
    if (!app.requestSingleInstanceLock()) {
        process.exit(0);
    }

    app.on("web-contents-created", (_event, contents) => {
        secureApplicationWindow(contents);
    });

    return true;
};
