# ii Reborn 1.2.4 Changelog

## New Mods
- **Bug Physics** — grab and throw the bug with real physics via your grips
- **Bouncy Bug** — makes the physics bug bouncy
- **Become Bug** — see through the bug's eyes
- **Freeze Bug** — freezes the bug mid-air
- **Bug Follow Me** — bug follows you around
- **Lucy Physics** — grab and fling Lucy with real physics
- **Bouncy Lucy** — makes Lucy bouncy
- **Lucy Follow Me** — Lucy chases you everywhere
- **Spin Cosmetics** — spins all enabled cosmetics
- **Voice Hijack + Keep Voice Hijack Target** — hijacks the newest joiner's voice slot
- **Map Force Unload** (Master) — forces every client to unload their current map

## Fixes for the new GT update (mods that were silently broken)
- **Unlock All Cosmetics** — reflection lookup fixed (game method went public)
- **Break Network Triggers** — `allowedMapsToJoin` is now a property; map-prefix logic restored
- **Beta Play Tag** — authority moved from master to lowest actor number; old gate no-op'd and got the master reported
- **Kill Target** — stale RPC shape rewritten for the new 6-arg handler + proper authority gate
- **Close Room** — removed (event has no consumer since the update)
- **Platform Spam / Platform Gun** — dead remote-share event removed
- **Freeze Server** — dead event-250 leg removed (working 186 leg kept)
- **Elevator Kick** — payload fixed for the new 2-int handler
- **CreateItems** — now gates on true Ghost Reactor zone authority

## Other
- **Tag All** — hidden 0.1s report throttle removed; tags everyone instantly
- **MOTD board** — now shows "Current menu status: Online" in green instead of "Loading..."
- **Configs rework** — Community Configs no longer auto-fetches; all 6 premade configs removed; Import Config File, Save Config, and the Downloaded Config Files list remain
- **Bug Physics performance** — cached rigidbody, transform writes only on movement, bug AI motion zeroed on grab (no more fps drop while enabled)
- Removed stale duplicate Freeze Bug / Become Bug buttons
- Fixed false tooltips (Guardian Spaz, Become Lurker, Make Room Public, Guardian Bring Away Gun/All)
