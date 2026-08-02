package io.github.mazemei.dxdisplaycleanup;

import org.junit.Test;

import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.net.InetAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.atomic.AtomicReference;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

public class TransferSessionProbeTest {
    @Test
    public void authenticatedReceiverRespondsReady() throws Exception {
        AtomicReference<Throwable> serverFailure = new AtomicReference<>();
        try (ServerSocket server = new ServerSocket(
                0, 1, InetAddress.getLoopbackAddress())) {
            Thread responder = new Thread(() -> {
                try (Socket socket = server.accept();
                     DataInputStream input = new DataInputStream(
                             socket.getInputStream());
                     DataOutputStream output = new DataOutputStream(
                             socket.getOutputStream())) {
                    assertEquals(0x44584D52, input.readInt());
                    assertEquals(1, input.readInt());
                    assertEquals("secret", readString(input));
                    assertEquals("__DXM_STATUS_PROBE__", readString(input));
                    output.writeByte(1);
                    writeString(output, "Ready");
                    output.flush();
                } catch (Throwable throwable) {
                    serverFailure.set(throwable);
                }
            });
            responder.start();

            TransferSessionStore.Session session =
                    new TransferSessionStore.Session(
                            true, server.getLocalPort(), "secret");
            assertTrue(TransferSessionProbe.isReceiverReady(session, 1500));
            responder.join(2000);
            assertNull(serverFailure.get());
        }
    }

    private static String readString(DataInputStream input) throws Exception {
        int length = input.readInt();
        byte[] bytes = new byte[length];
        input.readFully(bytes);
        return new String(bytes, StandardCharsets.UTF_8);
    }

    private static void writeString(DataOutputStream output, String value)
            throws Exception {
        byte[] bytes = value.getBytes(StandardCharsets.UTF_8);
        output.writeInt(bytes.length);
        output.write(bytes);
    }
}
