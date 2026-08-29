# Encryption lifecycle audit

This doc is a full lifecycle audit of the end-to-end encryption system, from a fresh install through every key-loss and re-key scenario, across both this client and the Aethernet backend. Read it when you touch anything under src/Aetherphone/Core/Crypto, when you change a key endpoint on the backend, or when you need to answer "what does each user see when X happens to their keys". It was produced 2026-08-29 against the client dev branch and the backend main and dev branches; where a doc and the code disagree, the code wins. The design description in [networking.md](networking.md#end-to-end-encryption) stays the reference for the wire formats; this doc covers behavior over time and the gaps.

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Core/Crypto/KeyVault.cs | Identity key state machine: provision, refresh, lock, recover, reset, device link |
| src/Aetherphone/Core/Crypto/CryptoBox.cs | Primitives: P-256 ECDH, HKDF-SHA256, AES-256-GCM, EC1 and EL1 wraps |
| src/Aetherphone/Core/Crypto/ConversationKeyStore.cs | Per-conversation content keys: generations, wraps, healing, self repair |
| src/Aetherphone/Core/Crypto/EnvelopeCodec.cs | AE1 message envelopes, AAD binding, franking commitment |
| src/Aetherphone/Core/Crypto/RecoveryKey.cs | Recovery code generation and PBKDF2 escrow wrap |
| src/Aetherphone/Core/Crypto/PeerKeyDirectory.cs | Peer public key cache and rotation notices |
| src/Aetherphone/Core/Crypto/DecryptedHistoryStore.cs | Remembered plaintext of already-decrypted messages |
| src/Aetherphone/Core/Crypto/UnwrapFailureCache.cs | Suppresses re-attempting a wrap that already failed |
| src/Aetherphone/Windows/Components/Chat/EncryptionVaultActions.cs | Every user-facing vault action: recover, reset, regenerate code, device link |
| Aethernet.Api/Endpoints/KeyEndpoints.cs (backend) | Key directory, escrow storage, device link tickets |
| Aethernet.Api/Common/EnvelopeRules.cs, KeyWrapRules.cs (backend) | Server-side envelope and wrap shape validation |

## The design in one page

Every Aethernet account has one P-256 identity keypair, generated client side (CryptoBox.TryGenerateIdentity) and versioned by the server (UserEncryptionKey.KeyVersion, starts at 1, bumps on every rotation). The private key is stored locally inside the Dalamud plugin config, DPAPI-protected per Windows user (LocalKeyProtector), keyed by account id in Configuration.EncryptionKeysByUserId.

Each encrypted conversation scope (chat:, gram:, velvet:, ads:) has a 32-byte content key (CEK) per generation. A generation is minted client side and distributed as one EC1 wrap per member: ephemeral P-256 ECDH against the member's public key, HKDF-SHA256, AES-256-GCM. Membership changes and unreadable keys roll the generation forward; old generations stay valid for old messages and are never re-encrypted.

Messages travel as AE1.generation.base64 envelopes: AES-256-GCM over (content type byte, 32-byte franking key, UTF-8 body), with AAD binding scope, generation, and sender. A cleartext HMAC-SHA256 commitment tag makes abuse reports verifiable (franking). Media uses the same seal with an aep-media-v1 AAD domain.

Recovery is server-side escrow: the private key, AES-GCM wrapped under a PBKDF2-HMAC-SHA256 key (600,000 iterations) derived from a 20-character recovery code (100 bits of entropy), uploaded next to the public key. Rotation archives the previous escrow (UserKeyEscrows), so one code per key version can unlock the whole history chain. Device linking transfers the identity key from an unlocked PC to a new one through an EL1 wrap against an ephemeral key, gated by a human comparing a 6-digit code (backend dev branch, 5-minute tickets, at most 3 pending).

The server never sees plaintext of an encrypted message. It does see and store: every public key (unauthenticated), every passphrase-wrapped private key ever escrowed, all wraps, and all metadata (sender, recipients, timestamps, kinds, read receipts, media dimensions and content types).

## Lifecycle walkthrough

### Fresh install

There is no setup wizard and no opt-in. On the first frames after sign in, any chat surface tick or the Settings encryption page calls KeyVault.RefreshAsync. A fresh account gets HTTP 404 from GET /keys/me; after two consecutive 404 confirmations (guards against a flaky read) ProvisionAsync runs: generate the identity key, write the local blob and read it back to verify, auto-generate a recovery code, escrow it server side with the public key, and hold the plaintext code in Configuration.PendingRecoveryCode until the user confirms they saved it. A 409 means another device provisioned first and the pending key is discarded. If the server has a key but this device has none, the vault goes Locked and never generates a competing key.

Until the user acknowledges the code, three prompts run: a banner on the chat list and above every thread ("Save your recovery code so you never lose these chats"), and a phone notification from EncryptionGuide. Acknowledging requires re-typing the last code group (TryConfirmSavedCode).

### Opening a chat

Opening a thread fetches ConversationKeysDto. If CurrentGeneration is 0 and every member has a published key, the opener generates the CEK and posts generation 1 with a wrap per member; a race loses to a 409 and refetches. If any member has no key, nothing is minted and sends stay plaintext (open lock, "can't receive encrypted messages yet" on the info page). Until the CEK hydrates, encrypted bodies show "Decrypting..." placeholders. The server piggybacks healing hints on every keys fetch: StaleWrapUserIds (their key rotated, re-wrap all generations for them), MissingWrapUserIds (wrap the current generation), NeedsNewGeneration (roll forward). Healing is entirely client driven and lazy: it happens when someone who holds the CEK next opens the thread.

### Sending and receiving

Sends seal to the current generation. Once a thread has been encrypted, DowngradeBlocked stops accidental plaintext sends (the composer is replaced by a lock row if this device cannot encrypt). Receivers unwrap the CEK once per generation; a wrap that fails to unwrap is remembered by UnwrapFailureCache (exact wrap string, per scope and generation, in-memory, no TTL) so the client does not grind on it every frame; a changed wrap string is always retried. Every successfully decrypted body is remembered in DecryptedHistoryStore (DPAPI-protected file per account, 20,000 entries), which is what keeps already-read messages readable after a key change on the same machine.

## Scenario matrix

The columns describe what each side experiences and where the behavior lives. "A" is the user whose keys change; "B" is any chat partner.

| # | Scenario | What happens | A sees | B sees |
| --- | --- | --- | --- | --- |
| 1 | Fresh install, fresh account | Silent auto-provision after two 404s; code escrowed; PendingRecoveryCode held | Save-code banners plus notification until verified | Nothing; B can now mint or heal wraps for A |
| 2 | First chat between two keyed users | Opener mints generation 1, wraps for both | Closed lock, accent tint, "End-to-end encrypted" | Same after hydration; "Decrypting..." until then |
| 3 | Chat where B has no key yet | No generation minted; plaintext mode | Open lock; info page "{B} can't receive encrypted messages yet" | Open lock; provisioned automatically on B's next sign in |
| 4 | A reinstalls plugin, same PC and Windows user | Config and DPAPI blob survive (they live in pluginConfigs); vault Unlocked on refresh | Nothing unusual | Nothing |
| 5 | A wipes the PC, HAS the recovery code | Fresh install goes Locked (server key exists, no local blob); A enters the code; RecoverWithCodeAsync unwraps the escrow, verifies the public key matches, stores and unlocks; archived escrows are silently tried with the same code | "Chats are locked on this device. Tap to unlock", then normal; old messages re-decrypt from server envelopes; DecryptedHistoryStore cache is gone but not needed | Nothing at all: key version did not change |
| 6 | A wipes the PC, LOST the recovery code, has a second linked PC | Device link: new PC posts an ephemeral key, gets a 6-digit code; old PC polls every 6 s, prompts, wraps the identity key as EL1; new PC unwraps and verifies against the server public key | "Unlock from my other PC", verification number, then unlocked | Nothing |
| 7 | A wipes the PC, LOST the recovery code, no other PC | Dead key. Only exit is Reset: ResetAsync provisions a new key, KeyVersion bumps, old escrow is archived (still locked to the lost code forever), fresh code generated. Old CEK wraps target the old key; A cannot unwrap them. When A opens a thread it cannot read, ShouldRekeyUnreadable rolls a new generation (2-minute cooldown) so new messages flow | Locked screen with "Create a new key on this device"; after reset, old messages show "Sent to an earlier key" placeholders permanently; new messages fine | B's history stays readable (B holds the CEKs). B is told "{A}'s security key changed" only if B opens that thread's encryption info page first (see gap 5); B's client heals wraps for the new key on next thread open |
| 8 | A resets while B is offline with queued messages to the old generation | Server accepts sends to any generation not in the future, so B's client may keep sealing to a generation A can no longer read until B next fetches keys | Those messages show "Sent to an earlier key" forever (A's new key never had a wrap) | B sees own messages fine; no signal that A could not read them |
| 9 | A locked (key lost) but has NOT reset yet | Nothing changes for the conversation; the generation is still current | Locked banners; blocked composer on encrypted threads; previously read bodies still show from remembered history | Nothing; B keeps sending into a mailbox A cannot open, with no indicator |
| 10 | A rotates on purpose (reset with the old key still present) | Same as 7 server-side, but the old blob is retired locally (up to 8 kept), so old generations keep opening on that PC; old escrow reachable via "Older chats" with the old code | Everything readable locally; on other machines the old code unlocks archived escrows (memory only, re-entered each session, see gap 9) | Same as 7 |
| 11 | DPAPI cannot open the local blob (Windows profile change, corrupt store) | LocalKeyStatus.Unreadable; vault Locked; blob left in place | "Windows could not open the encryption key saved on this PC", recover or reset | Nothing |
| 12 | Wine or Linux (no DPAPI) | LocalKeyProtector falls back to a raw base64 blob; encryption continues; LocalCacheUnavailable set | Nothing visible in chat (see gap 2) | Nothing |
| 13 | Two fresh devices provision simultaneously | Both generate; server PUT with ExpectedKeyVersion 0; loser gets 409 and discards its pending key, then adopts or locks | At most a transient "Setting up encryption..." | Nothing |
| 14 | B's key changes (B rotated or reset) | A's PeerKeyDirectory compares KeyVersion against Configuration.KnownPeerKeyVersions on the next resolve; greater version raises an in-memory rotation notice | "{B}'s security key changed" banner, 1:1 threads only, only after visiting the thread's encryption info page, lost on restart (gap 5) | n/a |
| 15 | Malicious or compromised server swaps a public key at the same version | Cryptographically invisible: keys are unauthenticated, wraps unsigned, detection is version-increment only. KeyDistributionTrustTests pins this deliberately | Nothing. Manual security code comparison on the info page is the only defense | Nothing |
| 16 | Group chat member added | Server flags MissingWrapUserIds or NeedsNewGeneration; a member holding the CEK wraps for the newcomer or rolls a generation; history before the join stays sealed to older generations | Member rows show "Ready for encryption" or "No encryption key yet" | Newcomer reads from their join generation onward |
| 17 | Group chat with one keyless member | No encryption at all: sends are plaintext for everyone until every member has a key | Open lock | Open lock |
| 18 | A deletes the account | Backend AccountEraser wipes keys, escrows, wraps, chat and velvet and ad messages, media. Gap: Aethergram DM rows survive (backend RETENTION.md admits this) | Account gone | Threads tombstone per surface retention rules |
| 19 | Tampered ciphertext or wrong envelope metadata | AES-GCM plus AAD fail closed: wrong scope, generation, sender, or flipped byte all refuse to decrypt | "This message is damaged" or "Sent to an earlier key" | Same |
| 20 | Franking verification fails (forged commitment tag) | Envelope still decodes; Verified flag cleared; but no renderer reads the flag (gap 6) | Message looks completely normal | Same |

## Findings

Ordered by severity. Client file references are relative to src/Aetherphone; backend references are to the FFXIV-Aethernet repo.

### Critical: trust model

1. **The server is a fully trusted, unauthenticated key directory.** Public keys carry no signature, no proof-of-possession on upload (backend KeyEndpoints validates shape only), and wraps are validated by prefix and length alone (KeyWrapRules.IsValidWrappedKey). A server that substitutes a key while keeping the version number is undetectable; the version-only TOFU in PeerKeyDirectory catches only honest rotations. KeyDistributionTrustTests documents this on purpose, but the two mitigations (rotation notice, manual security code comparison) have zero test coverage and the notice is nearly unreachable (finding 5). The docs never state this trust model. Mitigation options, in increasing cost: sign key bundles with a long-term key anchored in the recovery code, add security-code verification prompts on rotation, key transparency log.
2. **Silent plaintext fallback at rest.** LocalKeyProtector.Protect catches every exception and writes "raw." plus base64 of the private key into the config JSON (KeyVault.cs:1226-1235); DecryptedHistoryStore.Write does the same for message plaintext. On Wine or Linux the whole system runs with an effectively plaintext private key and history on disk, with only an internal flag set and nothing shown in chat. networking.md claims the key "is simply not persisted" in this case, which is wrong. At minimum: surface a visible warning state, and correct the doc.
3. **The auto-generated recovery code sits in plaintext config until acknowledged.** Configuration.PendingRecoveryCode holds the code that unwraps the server escrow; a user who never clicks through the save flow keeps it there forever, next to the (possibly raw) private key.

### High: lifecycle correctness and dead ends

4. **A correct code against a stale escrow reports "wrong code".** Fixed since the audit: RecoverWithCodeAsync now returns a RecoveryAttemptOutcome, tries archived escrows when the current escrow does not open, keeps any older key the code does open (persisted locally), and the UI explains that the code is from before the key changed instead of calling it wrong.
5. **Peer rotation notices are nearly unreachable and not persisted.** The only ResolveAsync trigger is opening a 1:1 thread's encryption info page (MessageApp.Encryption.cs:34-38); group threads never show the banner (Thread.cs early-returns for groups); the new version is persisted before the user sees the notice, and the notice itself is memory only, so a restart destroys it. In practice almost nobody will ever see "security key changed". Resolve peer keys when a thread opens, persist an unacknowledged-rotation flag, and show the banner in groups.
6. **Franking verification failures are invisible.** TranscriptFlags.Encrypted and Unverified are set (MessageApp.Thread.cs:368-375) but no renderer reads them; a message whose commitment tag failed verification renders identically to a verified one. Silent integrity failure; also weakens the report flow.
7. **Encryption guide notifications open the wrong page.** Fixed since the audit: NotificationRouter now routes notifications carrying the encryption guide group key through the EncryptionSetupLauncher, so they open the Encryption page.
8. **Same-conversation replay is unprevented.** The AAD is scope, generation, sender: no message id or counter. The server does not deduplicate envelopes. A relayed identical envelope appears as a legitimate new message from the same sender.
9. **Restored archived keys are memory only.** Fixed since the audit: every key restored from an archived escrow is now persisted into the local retired-key store (RetireStoredBlob), so it survives restarts, and a Locked device also loads its retired keys so already-restored chats stay readable while locked.
10. **Backend main lags dev on encryption.** The deployed contract (device link endpoints, DeviceLinkRequests table) exists only on the backend dev branch; main has neither the endpoints nor tests. Anyone auditing or deploying from main ships a client whose device-link flow 404s.

### Medium

11. **Rotation healing is lazy and unnotified.** Peers learn a key is stale only by polling GET keys on thread open; FixWrapsAsync is unreachable from bulk hydration. Messages can be sealed to generations the recipient can never read (scenario 8) with no signal to the sender.
12. **A single broken device can churn generations.** ShouldRekeyUnreadable lets any member that cannot read the current generation mint a new one every 2 minutes; a device stuck with a stale cached key rotates conversations forever, silently.
13. **Regenerating a recovery code has no confirmation** and silently invalidates the previously saved code for the current escrow (BeginCreateRecoveryCode).
14. **Escrow-based recovery is an offline-attack surface.** The server holds ciphertext, salt, and iteration count for every private key version a user ever had, forever (no cap on escrow rows). The 100-bit random code makes brute force impractical today; document the assumption, cap or prune archived escrows, and fix ARCHITECTURE.md's "the server holds no private key" claim (backend doc, misleading as stated).
15. **Account erasure misses Aethergram DM rows** (backend AccountEraser has no GramMessages delete; RETENTION.md acknowledges the gap while SECURITY-AUDIT.md M12 claims it fixed).
16. **Media URLs never expire** (backend chat and gram media URL endpoints), so a leaked URL serves the encrypted blob forever; combined with server-visible content types and dimensions this is more metadata than the docs admit.
17. **DecryptedHistoryStore account-switch race**: the owner check happens once before the async load loop, so a fast account switch can write one account's plaintext into another account's file (DecryptedHistoryStore.cs:202-210). Small window, real cross-account leak on shared machines.
18. **Device link code is not cryptographically bound to the ephemeral key.** The human-compared 6-digit code rides next to the ephemeral key; the server can substitute its own key under a legitimate code (consistent with finding 1, worth stating).
19. **Provisioning failures are invisible**: ProvisionAsync retries silently and chat just shows "Not encrypted" with no explanation.

### Low

20. UnwrapFailureCache holds one entry per scope and generation, so two distinct failing wraps for one generation alternate retries forever (wasted work only).
21. MessageCipher caches and ConversationKeyStore bookkeeping maps are unbounded for the session.
22. Recovery copy buttons put the code on the OS clipboard with no warning or clearing.
23. Silent archived-escrow restore after recovery swallows all errors, so "Older chats" may silently stay locked. Fixed since the audit: the restore now reports how many keys it recovered and points the user at Older chats when the check fails.
24. WrapCek writes a single-byte ephemeral key length with no overflow guard (fine for P-256, a trap for any future curve).
25. KeyVault.OnSessionChanged refreshes only on sign-out; sign-in relies on chat surfaces ticking, so a user with the messages gate closed never provisions and never gets device-link prompts. Deserves at least a comment; it reads like an inverted guard.
26. Encryption.LockedBody is an orphaned LocString; testing-and-release.md's crypto test inventory omits three of the nine test files; messaging-and-chat.md still says "safety-number-changed banner" for what is now the security code plus header lock.

## Test coverage map

Strong: primitives (CryptoBox, EnvelopeCodec, MediaEnvelope round trips, fail-closed, AAD binding), recovery code canonicalization and escrow chain (RecoveryKeyTests plus backend KeyEscrowChainTests), unwrap failure cache semantics, key store migrations and DPAPI taxonomy, backend generation and wrap lifecycle (ChatKeyLifecycleTests, ThreadKeyWrapTests, VelvetKeyHealingTests), franking verdicts (ChatReportRevealTests).

Untested, in priority order:

1. Peer key change detection: PeerKeyDirectory version pinning, rotation notices, security code computation. The load-bearing defense for finding 1, zero tests.
2. The KeyVault state machine: RefreshAsync transitions, ResetAsync, RecoverWithCodeAsync (including the stale-escrow rejection), CreateRecoveryCodeAsync races, pending-blob verify.
3. Client ConversationKeyStore logic: first-open generation mint, CacheWraps, healing, self repair, unreadable rekey.
4. Device link end to end (client KeyVault flows, DeviceLinkWatcher, and any backend tests at all).
5. MessageCipher entirely (placeholder swap, media seal and open, forwards).
6. Tampered AE1 text envelope (byte flip, generation relabel); same-conversation replay.
7. Fresh-install provisioning and the full wipe-then-reinstall journey as integration tests.
8. DecryptedHistoryStore persistence mechanics (eviction, flush, per-account files, the switch race in finding 17).

## Gotchas

- The backend main branch is not the contract the client targets: encryption features land on backend dev first (device linking exists only there today). Audit and deploy from dev.
- "Locked" and "no key" are different states with different exits: Locked means the server has a key this device cannot open (recover, link, or reset); a 404 twice means no key anywhere and triggers silent provisioning.
- Recovery codes are per key version. Resetting generates a fresh code; the old code still opens the archived escrow of the old key (the "Older chats" flow), but nothing else.
- DecryptedHistoryStore is why "old messages still readable here" does not imply "the key still works": remembered plaintext masks live decryption failures on the machine where messages were first read.
- The UnwrapFailureCache never expires within a session; a wrap healed server side is retried only because the wrap string changes, not because time passed.
- EncVersion 0 plaintext is a fully valid mode on every surface. Encryption is per message, enforced client side by DowngradeBlocked only after a thread has been encrypted once.

## Related docs

- [Networking and the Aethernet backend](networking.md), the wire formats and the client protocol reference
- [Messaging and chat](messaging-and-chat.md), the chat stack that consumes all of this
- [State and persistence](state-and-persistence.md), where the config and key blobs live
- [Testing, CI, and releases](testing-and-release.md), the test project layout
