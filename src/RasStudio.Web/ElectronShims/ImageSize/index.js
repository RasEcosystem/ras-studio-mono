"use strict";

const {nativeImage} = require("electron");

function readImageSize(imagePath) {
    if (typeof imagePath !== "string" || imagePath.length === 0) {
        throw new TypeError("An image path is required.");
    }

    const image = nativeImage.createFromPath(imagePath);
    if (image.isEmpty()) {
        throw new Error(`Unable to read image dimensions from '${imagePath}'.`);
    }

    return image.getSize();
}

function imageSize(imagePath, callback) {
    if (typeof callback !== "function") {
        return readImageSize(imagePath);
    }

    try {
        callback(null, readImageSize(imagePath));
    } catch (error) {
        callback(error);
    }
}

module.exports = {imageSize};
