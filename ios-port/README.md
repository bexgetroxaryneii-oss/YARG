# YARG Mobile (iOS port)

An unofficial iPhone build of YARG for sideloading. Made by Claude Strife - X.

- `.github/workflows/ios-port.yml` builds an unsigned IPA (Actions > "YARG iOS build").
  It needs the repository secrets `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD`.
- `build-natives.sh` fetches the BASS iOS frameworks (free for non-commercial
  use, from un4seen.com; never stored here) and builds `yarg_audio.framework`.
- `PatchPInvoke` points the managed BASS and SQLite bindings at the linked
  libraries (`__Internal`), as IL2CPP on iOS requires.
- `Assets/Editor/IosPort` exports the Xcode project, embeds the frameworks and
  turns on the Files-app song folder.

Not on iOS: video venues (VLC), Discord status, USB lighting.
Songs: copy them into Files > On My iPhone > YARG Mobile.

Build: Actions > "YARG iOS build" > Run workflow. The IPA is the run's `YARG-Mobile-ipa-runN` artifact.
