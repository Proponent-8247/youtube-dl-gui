# Authenticode and catalog signature revocation policy

This policy covers **PAR-05-001 / issue #26**, the security check performed by
`youtube-dl-gui/Updater/Updater.cs` (`ExecutableTrust`) when downloaded provider
or updater executables carry an Authenticode signature, or when they are members
of a Windows catalog signed by a certificate chain.

## Chain validation and revocation

Windows `WinVerifyTrust` is configured for no interactive UI, with
`WTD_REVOKE_WHOLECHAIN` and `WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT`.
This requires signer and intermediate certificate revocation checking; the
locally trusted root is not individually queried for revocation. The same
policy is used to validate a discovered signed catalog file.

* **Trusted, unrevoked chain whose revocation status is established:** the
  native trust operation may return success, subject to all other applicable
  Windows Authenticode/policy requirements.
* **Revoked signer/intermediate, invalid signature, untrusted chain:** not
  `Valid`. The update must not use such a signature as authenticity proof.
* **No network, unavailable CRL/OCSP, unknown revocation status:** fail
  closed for the signed authenticity path. Do not silently turn revocation
  checks off or classify a known signature as authenticated.
* **Unsigned file:** a separate, explicitly weaker compatibility policy.
  The required SHA-256 remains mandatory; a present but invalid/revoked signed
  chain must *not* be reinterpreted as unsigned to bypass verification.

No fallback to `WTD_REVOCATION_CHECK_NONE` or local-only authorization is
permitted when online revocation cannot be established. Offline installations
may require users to update their Windows revocation cache or regain access
to the certificate authority endpoints. A signed update may be refused when
Windows is unable to establish its trust status; this is preferable to
accepting a revoked signer.

## Known limitations and required acceptance

The cryptographic trust decision relies on Windows certificate policy and
its time-of-signing/timestamp interpretation. SHA-256 verifies bytes against
release metadata but is not independent signer authentication. Publisher
identity restrictions and standalone application replacement are separately
tracked under issue #27. Changes to this policy must be reviewed separately.

Before closing #26, run a controlled signed binary and signed catalog
matrix that covers trusted/nonrevoked, revoked, unsigned, tampered, and
revocation-endpoint-unavailable fixtures; verify revoked or indeterminate
signed objects cannot return `Valid`. Normal CI also exercises exact
WinVerifyTrust configuration through reflection, signed native system
executables, unsigned fixtures and all application regressions. An ordinary
green CI run by itself does not prove the revoked-certificate negative case.
