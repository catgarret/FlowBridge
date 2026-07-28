package io.github.mazemei.dxdisplaycleanup;

import android.net.Uri;

final class PhoneTransferEntry {
    final int rootId;
    final boolean directory;
    final Uri uri;
    final String relativePath;
    final long size;
    final long lastModified;

    PhoneTransferEntry(int rootId, boolean directory, Uri uri,
            String relativePath, long size, long lastModified) {
        this.rootId = rootId;
        this.directory = directory;
        this.uri = uri;
        this.relativePath = relativePath;
        this.size = size;
        this.lastModified = lastModified;
    }
}
