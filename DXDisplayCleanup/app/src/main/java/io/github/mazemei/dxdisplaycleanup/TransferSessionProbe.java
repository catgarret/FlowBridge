package io.github.mazemei.dxdisplaycleanup;

import java.io.BufferedInputStream;
import java.io.BufferedOutputStream;
import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.io.IOException;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.nio.charset.StandardCharsets;

final class TransferSessionProbe {
    private static final int MAGIC = 0x44584D52;
    private static final int VERSION = 1;
    private static final String STATUS_PROBE_BATCH =
            "__DXM_STATUS_PROBE__";
    private static final int MAX_RESPONSE_BYTES = 1024 * 1024;

    private TransferSessionProbe() {
    }

    static boolean isReceiverReady(TransferSessionStore.Session session,
            int timeoutMillis) {
        if (session == null || !session.isReady()) {
            return false;
        }

        int timeout = Math.max(250, timeoutMillis);
        try (Socket socket = new Socket()) {
            socket.connect(new InetSocketAddress(
                    InetAddress.getLoopbackAddress(), session.port), timeout);
            socket.setTcpNoDelay(true);
            socket.setSoTimeout(timeout);
            try (DataOutputStream output = new DataOutputStream(
                    new BufferedOutputStream(socket.getOutputStream()));
                 DataInputStream input = new DataInputStream(
                         new BufferedInputStream(socket.getInputStream()))) {
                output.writeInt(MAGIC);
                output.writeInt(VERSION);
                writeString(output, session.token);
                writeString(output, STATUS_PROBE_BATCH);
                output.flush();
                return readResponse(input);
            }
        } catch (IOException | RuntimeException ignored) {
            return false;
        }
    }

    private static void writeString(DataOutputStream output, String value)
            throws IOException {
        byte[] bytes = (value == null ? "" : value)
                .getBytes(StandardCharsets.UTF_8);
        output.writeInt(bytes.length);
        output.write(bytes);
    }

    private static boolean readResponse(DataInputStream input)
            throws IOException {
        int status = input.read();
        if (status < 0) {
            return false;
        }
        int length = input.readInt();
        if (length < 0 || length > MAX_RESPONSE_BYTES) {
            return false;
        }
        byte[] bytes = new byte[length];
        input.readFully(bytes);
        return status == 1;
    }
}
