# DX Display Cleaner signing identity

The official DX Display Cleaner 1.0.0 APK is signed with this certificate:

- Subject: `CN=maze-mei, OU=DX Manager, O=maze-mei, C=KR`
- SHA-256: `AD:61:58:03:C6:37:60:43:97:50:C3:68:01:E8:15:2A:B8:66:4C:60:EE:48:1E:F1:47:3F:1D:F5:E8:07:33:BE`
- Key: RSA 4096-bit

DX Manager compares both the exact Android package ID and this signing
certificate before enabling its permission-grant action.

The private keystore and `signing.properties` are intentionally excluded from
Git. Back them up securely. Losing the keystore prevents publishing a compatible
update under the same package identity.
