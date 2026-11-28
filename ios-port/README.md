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

Not on iOS: VLC video playback (Unity's VideoPlayer is used instead), Discord status, USB lighting.
Songs: copy them into Files > On My iPhone > YARG Mobile.

Playing without a controller: on-screen pads (`Assets/Script/Input/Touch/TouchGuitarOverlay.cs`)
appear during gameplay as a virtual gamepad — five frets bottom-left, strum up/down
bottom-right, star power above the strum, pause at the top. "PADS" (top-left) shows or
hides them anywhere. Make a five-fret guitar profile once, bind it to the on-screen
gamepad ("Default Gamepad as 5F Guitar" works as-is), then tap a pad to connect it.
Controllers: Bluetooth gamepads iOS recognises (Xbox, PlayStation, Switch Pro, MFi) work,
including a CRKD neck (binding mode "CRKD Guitar (Mode 1)"). Guitars that present as a
USB keyboard (e.g. Santroller in keyboard mode, through a USB-C/Lightning adapter) work
as a keyboard profile: defaults are frets 1-5, strum Up/Down arrows (or Enter/Right Shift),
star power Backspace. Wired XInput (Xbox 360-style) devices are generally not recognised by iOS; test before relying on one.

Build: Actions > "YARG iOS build" > Run workflow. The IPA is the run's `YARG-Mobile-ipa-runN` artifact.
