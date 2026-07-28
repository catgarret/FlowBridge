package io.github.mazemei.dxdisplaycleanup;

import android.content.ContentResolver;
import android.content.Context;
import android.database.Cursor;
import android.net.Uri;
import android.provider.DocumentsContract;
import android.provider.OpenableColumns;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

final class PhoneTransferPlanner {
    static final class Plan {
        final List<PhoneTransferEntry> entries;
        final long totalBytes;

        Plan(List<PhoneTransferEntry> entries, long totalBytes) {
            this.entries = entries;
            this.totalBytes = totalBytes;
        }
    }

    private static final int MAX_ITEMS = 100000;
    private static final int MAX_DEPTH = 128;

    private PhoneTransferPlanner() {
    }

    static Plan create(Context context, List<Uri> roots) throws Exception {
        if (roots == null || roots.isEmpty()) {
            throw new IllegalArgumentException("No files were shared.");
        }
        List<PhoneTransferEntry> entries = new ArrayList<>();
        Set<String> visited = new HashSet<>();
        for (int index = 0; index < roots.size(); index++) {
            Uri uri = roots.get(index);
            if (uri == null) {
                continue;
            }
            DocumentInfo info = inspect(context, uri);
            add(context, uri, info, index + 1,
                    sanitizeSegment(info.name), entries, visited, 0);
        }
        if (entries.isEmpty()) {
            throw new IllegalArgumentException("No readable files were shared.");
        }
        long total = 0;
        boolean unknown = false;
        for (PhoneTransferEntry entry : entries) {
            if (entry.directory) {
                continue;
            }
            if (entry.size < 0) {
                unknown = true;
            } else {
                total += entry.size;
            }
        }
        return new Plan(entries, unknown ? -1 : total);
    }

    private static void add(Context context, Uri uri, DocumentInfo info,
            int rootId, String relativePath,
            List<PhoneTransferEntry> entries, Set<String> visited,
            int depth) throws Exception {
        if (depth > MAX_DEPTH) {
            throw new IllegalArgumentException("Folder nesting is too deep.");
        }
        if (entries.size() >= MAX_ITEMS) {
            throw new IllegalArgumentException("Too many transfer items.");
        }
        String visitKey = uri.toString();
        if (!visited.add(visitKey)) {
            return;
        }

        entries.add(new PhoneTransferEntry(
                rootId, info.directory, uri, relativePath,
                info.size, info.lastModified));
        if (!info.directory) {
            return;
        }

        List<DocumentChild> children = listChildren(context, uri, info.documentId);
        for (DocumentChild child : children) {
            add(context, child.uri, child.info, rootId,
                    relativePath + "/" + sanitizeSegment(child.info.name),
                    entries, visited, depth + 1);
        }
    }

    private static DocumentInfo inspect(Context context, Uri uri)
            throws Exception {
        ContentResolver resolver = context.getContentResolver();
        boolean document = DocumentsContract.isDocumentUri(context, uri)
                || DocumentsContract.isTreeUri(uri);
        String documentId = null;
        if (document) {
            try {
                documentId = DocumentsContract.isTreeUri(uri)
                        ? DocumentsContract.getTreeDocumentId(uri)
                        : DocumentsContract.getDocumentId(uri);
            } catch (IllegalArgumentException ignored) {
                documentId = null;
            }
        }

        Uri queryUri = uri;
        if (DocumentsContract.isTreeUri(uri) && documentId != null) {
            queryUri = DocumentsContract.buildDocumentUriUsingTree(
                    uri, documentId);
        }

        String[] projection = document
                ? new String[]{
                DocumentsContract.Document.COLUMN_DOCUMENT_ID,
                DocumentsContract.Document.COLUMN_DISPLAY_NAME,
                DocumentsContract.Document.COLUMN_MIME_TYPE,
                DocumentsContract.Document.COLUMN_SIZE,
                DocumentsContract.Document.COLUMN_LAST_MODIFIED}
                : new String[]{OpenableColumns.DISPLAY_NAME,
                OpenableColumns.SIZE};
        try (Cursor cursor = resolver.query(queryUri, projection,
                null, null, null)) {
            if (cursor != null && cursor.moveToFirst()) {
                String name = getString(cursor, document
                        ? DocumentsContract.Document.COLUMN_DISPLAY_NAME
                        : OpenableColumns.DISPLAY_NAME);
                long size = getLong(cursor, document
                        ? DocumentsContract.Document.COLUMN_SIZE
                        : OpenableColumns.SIZE, -1);
                String mime = document ? getString(cursor,
                        DocumentsContract.Document.COLUMN_MIME_TYPE) :
                        resolver.getType(uri);
                long modified = document ? getLong(cursor,
                        DocumentsContract.Document.COLUMN_LAST_MODIFIED, 0) : 0;
                String queriedId = document ? getString(cursor,
                        DocumentsContract.Document.COLUMN_DOCUMENT_ID) : null;
                if (queriedId != null && !queriedId.isEmpty()) {
                    documentId = queriedId;
                }
                return new DocumentInfo(
                        fallbackName(uri, name),
                        DocumentsContract.Document.MIME_TYPE_DIR.equals(mime),
                        size, modified, documentId);
            }
        }

        String mime = resolver.getType(uri);
        return new DocumentInfo(
                fallbackName(uri, uri.getLastPathSegment()),
                DocumentsContract.Document.MIME_TYPE_DIR.equals(mime),
                -1, 0, documentId);
    }

    private static List<DocumentChild> listChildren(
            Context context, Uri parent, String parentDocumentId)
            throws Exception {
        if (parentDocumentId == null || parentDocumentId.isEmpty()) {
            throw new IllegalArgumentException(
                    "This folder provider does not allow its contents to be read.");
        }
        ContentResolver resolver = context.getContentResolver();
        Uri childrenUri;
        try {
            childrenUri = DocumentsContract.buildChildDocumentsUriUsingTree(
                    parent, parentDocumentId);
        } catch (IllegalArgumentException exception) {
            childrenUri = DocumentsContract.buildChildDocumentsUri(
                    parent.getAuthority(), parentDocumentId);
        }
        String[] projection = new String[]{
                DocumentsContract.Document.COLUMN_DOCUMENT_ID,
                DocumentsContract.Document.COLUMN_DISPLAY_NAME,
                DocumentsContract.Document.COLUMN_MIME_TYPE,
                DocumentsContract.Document.COLUMN_SIZE,
                DocumentsContract.Document.COLUMN_LAST_MODIFIED};
        List<DocumentChild> result = new ArrayList<>();
        try (Cursor cursor = resolver.query(childrenUri, projection,
                null, null, null)) {
            if (cursor == null) {
                throw new IllegalArgumentException(
                        "This folder provider returned no child list.");
            }
            while (cursor.moveToNext()) {
                String childId = getString(cursor,
                        DocumentsContract.Document.COLUMN_DOCUMENT_ID);
                if (childId == null || childId.isEmpty()) {
                    continue;
                }
                Uri childUri;
                try {
                    childUri = DocumentsContract.buildDocumentUriUsingTree(
                            parent, childId);
                } catch (IllegalArgumentException exception) {
                    childUri = DocumentsContract.buildDocumentUri(
                            parent.getAuthority(), childId);
                }
                String name = getString(cursor,
                        DocumentsContract.Document.COLUMN_DISPLAY_NAME);
                String mime = getString(cursor,
                        DocumentsContract.Document.COLUMN_MIME_TYPE);
                result.add(new DocumentChild(childUri, new DocumentInfo(
                        fallbackName(childUri, name),
                        DocumentsContract.Document.MIME_TYPE_DIR.equals(mime),
                        getLong(cursor,
                                DocumentsContract.Document.COLUMN_SIZE, -1),
                        getLong(cursor,
                                DocumentsContract.Document.COLUMN_LAST_MODIFIED, 0),
                        childId)));
            }
        }
        return result;
    }

    private static String sanitizeSegment(String value) {
        String result = value == null ? "unnamed" : value.trim()
                .replace('/', '_').replace('\\', '_');
        if (result.isEmpty() || ".".equals(result) || "..".equals(result)) {
            return "unnamed";
        }
        return result;
    }

    private static String fallbackName(Uri uri, String value) {
        if (value != null && !value.trim().isEmpty()) {
            return value;
        }
        String segment = uri.getLastPathSegment();
        return segment == null || segment.trim().isEmpty()
                ? "unnamed" : segment;
    }

    private static String getString(Cursor cursor, String column) {
        int index = cursor.getColumnIndex(column);
        return index < 0 || cursor.isNull(index)
                ? null : cursor.getString(index);
    }

    private static long getLong(Cursor cursor, String column, long fallback) {
        int index = cursor.getColumnIndex(column);
        return index < 0 || cursor.isNull(index)
                ? fallback : cursor.getLong(index);
    }

    private static final class DocumentInfo {
        final String name;
        final boolean directory;
        final long size;
        final long lastModified;
        final String documentId;

        DocumentInfo(String name, boolean directory, long size,
                long lastModified, String documentId) {
            this.name = name;
            this.directory = directory;
            this.size = size;
            this.lastModified = lastModified;
            this.documentId = documentId;
        }
    }

    private static final class DocumentChild {
        final Uri uri;
        final DocumentInfo info;

        DocumentChild(Uri uri, DocumentInfo info) {
            this.uri = uri;
            this.info = info;
        }
    }
}
