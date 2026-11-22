# Publishing Flutter Deck notices
This build opens Home, shows bundled or cached notices and tracks explicit Mark all read locally. Editing a notice changes its fingerprint and makes it unread again. Nothing is posted automatically.

## Rollout (not performed)
1. Approve a public repository/site and exact public endpoint for developer-authored notices. Current publisher-feed.txt is intentionally blank.
2. Use news-example.xml as schema example: news version=1; up to 50 item elements with unique stable id, kind announcement/changelog/update, UTC date yyyy-MM-ddTHH:mm:ssZ; title <=140 characters and body <=4000 characters. Plain text only; XML-escape special characters. Use real posting timestamps. Remove the bundled example claims before publishing actual notices.
3. Host at HTTPS on raiinman.github.io or raw.githubusercontent.com/raiinman/... with a direct 200 response; redirects, auth, query tokens and other hosts are rejected. Have the receiving HTTPS/TLS path verified on supported Windows versions before rollout. No transport test has been performed.
4. Put the approved exact URL in publisher-feed.txt beside FlutterDeck.exe and distribute that upgrade. Existing older installed copies have no reader and cannot receive until upgraded. Copying this file later is also local configuration, not something the client auto-discovers.
5. Publish subsequent reviewed text changes to that same endpoint. Updated clients fetch on startup, every 15 minutes while open, or Refresh. There is no push, background service, notification while closed, updater install or account connection.

## Safety and offline behavior
128 KiB UTF-8 limit, 8-second abort timer, no redirects/cookies/credentials/proxy fallback or remote images/scripts/actions. Host sees ordinary request network metadata; no game paths, saves, BIOS or user settings are attached. XML DTD/entities prohibited. The developer must protect the publisher account: HTTPS is transport authentication, not independently signed notice authenticity.
Source-keyed local cache retains last validated feed; failure preserves it with stale warning. Read markers are separate and bounded. Cache/read failures are disclosed, never repair game files. Mark all read applies only to current content. No live delivery or GUI verification claimed.

## Verification for this change
Native compilation and all existing pure/structural checks passed. News fixtures cover allowed/disallowed URLs, schema, DTD, duplicate ID, dates, invalid UTF-8, 128 KiB limit, 50/51 item boundary, title/body boundaries, deterministic order, changed-item unread state, source separation, offline roundtrip, invalid-feed retention, corrupt read markers and failed-write paths. Network timeout and interaction coverage are structural only.
