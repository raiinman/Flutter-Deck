# Scaling source receipt (private developer artifact)
- Imported original DSI Scaling 0.4.1 Native.cs as scaling/DsiNative.cs unchanged.
- SHA256: 1E97A9F017484A89FB3A9CBC41D44FE76A7BBF13CD3C26F0F76895616744795B.
- Profile DTO extracted from App.cs; mixed application not imported.
- Developer explicitly requested only independent scaling; integration is experimental, local, user-started.
- No source license for public redistribution established here. No publication performed or approved by this package.
- Original lifecycle/probe methods remain in source but were not invoked. PureTest executes only fit/pixel functions on disposable synthetic bitmaps.
- No vendor/backend/Node/DLSS payload, network or native-game bytes are included.

## Identity hardening revision
Current engine is a reviewed derivative, no longer byte-identical to the initial import. Added source PID + UTC process start-time pin, fail-closed enumeration, Start/OnShown/DWM-update and before/after GDI capture revalidation. Derivative SHA256: 4BDC5BCD59E9899BB14176BA4A4B041747BFE16AF0D2B372A43C059C01649ADF. Original import digest above remains the provenance base. Synthetic identity tests do not prove real HWND lifecycle behavior. Existing GUI probes were not invoked; fixture constructors relying on zero handles are no longer accepted by the public pinning constructor.

## Developer licence statement (2026-11-16)
The developer stated that DSI Scaling is their own work. The scaler source in this folder is licensed under the MIT licence in the repository's LICENSE file, copyright Dead Signal Interactive.
