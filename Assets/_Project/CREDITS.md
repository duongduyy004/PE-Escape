# Asset credits

## Vietnamese UI font source

The requested `Baloo2-ExtraBold` and `Nunito-Bold` TMP assets use the installed
Noto Sans ExtraBold and Noto Sans Bold font files as project-compatible local
fallback sources because Baloo 2 and Nunito were not installed in the build
environment. Noto Sans is distributed under the SIL Open Font License 1.1.
The asset filenames describe their intended UI roles; they do not claim that
the embedded font face is Baloo 2 or Nunito. The role assets link to a
project-owned `VietnameseFallback` TMP asset carrying the required UI glyph
set because some broad-range codepoints are not covered directly by the role
assets.

- Source: `/usr/share/fonts/truetype/noto/NotoSans-ExtraBold.ttf` (available locally)
- Source: `/usr/share/fonts/truetype/noto/NotoSans-Bold.ttf` (available locally)
- License: SIL Open Font License 1.1

## Character sprites

Every character in Sprint, Football and Volleyball is a project-generated flat cartoon
sprite in PE uniform (white T-shirt, navy track pants, sneakers), made with image
generation from the "Chạy trốn thể chất" art brief and cut to 192 × 256 RGBA PNGs on
2026-10-05. Folder and pose names are
fixed identifiers used by `CharacterArt`. `MaleAdventurer` is the player in
every minigame and is never tinted.

| Project folder | Character | Roles |
| --- | --- | --- |
| `Characters/MaleAdventurer/` | Anh Khoá Trên (main hero) | Player in Sprint, Football and Volleyball |
| `Characters/MalePerson/` | Tân Thủ | Sprint rival (lane 1), Football goalkeeper |
| `Characters/FemalePerson/` | Mai Toang | Sprint rival (lane 3) |
| `Characters/FemaleAdventurer/` | Cô Thể Chất (PE teacher) | Sprint rival (lane 4), Volleyball opponent |

Each folder holds the same poses: `idle run0 run1 run2 hit cheer0 cheer1 fallDown back
climb0 climb1 hurt duck hold jump attack1 slide fall`.

### Sports supplement (2026-10-10)

`chay-tron-the-chat-sports-supplement.zip`, project-generated with image generation from the same
art brief, cut to 192 × 256 RGBA PNGs: 36 extra hero poses (sprint, falls, frog jump, volleyball,
back-view football, expressions) in `Characters/MaleAdventurer/`, 8 extra teacher gestures in
`Characters/BossPE/`, the football goalkeeper `Characters/StudentKeeper/` (12 poses, a different
student from the hero), and 12 256 × 256 portraits in `UI/Portraits/` (not used yet).

## Project-generated Volleyball art

The court, net, court lines, ball and ball/marker shadow in `MG_Volleyball` are flat shapes
authored by this project: the scene configurator draws the court from coloured quads and
generates `Art/Environments/Volleyball/{Ball,Shadow,Pixel}.png` procedurally. No third-party
art is used there; the athletes are the project character sprites (see above).

## Project-generated report demo art

The game logo mark, Android icon, home illustration, and Sprint environment
layers were generated specifically for this project with OpenAI image
generation on 2026-09-05. The logo intentionally contains no baked text;
Unity renders the exact Vietnamese product title with TextMeshPro.


## Audio (2026-09-26)

KMA - Audio credits

Music by Kevin MacLeod (incompetech.com), licensed under CC BY 4.0:
https://creativecommons.org/licenses/by/4.0/
Move Forward (menu/map): https://incompetech.com/music/royalty-free/index.html?isrc=USUAN1300018
Cipher (sprint): https://incompetech.com/music/royalty-free/index.html?isrc=USUAN1100844
Beachfront Celebration (volleyball): https://incompetech.com/music/royalty-free/index.html?isrc=USUAN1200022
Winner Winner! (football): https://incompetech.com/music/royalty-free/index.html?isrc=USUAN1400036
Original music recordings unchanged; encoded by Unity for playback and repeated in-game.

Volleyball Hit by Nicholas Judy / designerschoice, licensed under CC BY 4.0:
https://freesound.org/people/designerschoice/sounds/845535/
https://creativecommons.org/licenses/by/4.0/
Edits: converted to mono PCM, peak-normalized, edge fades added.

Kenney Interface Sounds, Music Jingles, Impact Sounds (CC0):
https://kenney.nl/assets/interface-sounds
https://kenney.nl/assets/music-jingles
https://kenney.nl/assets/impact-sounds
https://creativecommons.org/publicdomain/zero/1.0/

Additional CC0 sound recordings (trimmed, converted to mono, normalized and faded):
Referee whistle - Rosa-Orenes256: https://freesound.org/people/Rosa-Orenes256/sounds/538422/
Crowd Cheer - FoolBoyMedia: https://freesound.org/people/FoolBoyMedia/sounds/397434/
Running Footsteps - ralph.whitehead: https://freesound.org/people/ralph.whitehead/sounds/565708/
Footsteps on sand and gravel - fthgurdy: https://freesound.org/people/fthgurdy/sounds/528948/
SoccerBallKick - purchasing102: https://freesound.org/people/purchasing102/sounds/521825/

Source files and licenses are retained in Assets/_Project/Audio/ThirdParty.
Prepared cue offsets and hashes: Assets/_Project/Audio/Prepared/provenance.json.

Journey dialogue emoji (15 glyphs) are drawn for this project in the shared flat cartoon
style by `tools/render-journey-emoji.js` and packed into a 360x216 atlas
(Assets/_Project/Art/Emoji/JourneyEmojiAtlas.png).

## Project-generated campus backdrop

`Art/Environments/Campus/` (sky, track, volleyball net and a
white pixel for tinted quads) is drawn by `tools/render-campus-art.js` as SVG and rasterised with
`@resvg/resvg-js` on 2026-10-06. Same palette and ink outline as the Football GoalView art. `CampusSkyline.png` is not drawn: it is
cropped, pixels unchanged, from the Sprint campus illustration `Art/Environments/Sprint/Campus.png`.

## Menu branding composition and procedural controls (2026-10-12)

The menu's two-line `CHẠY TRỐN / THỂ CHẤT` wordmark is rendered with TextMeshPro
by `Scripts/UI/HomePresentationBuilder.cs`; it is text composition, not a newly
exported logo image or font. The school badge is assembled from project-authored
UI shapes in the same builder. `Scripts/UI/HomeMenuIcons.cs` draws the menu's
functional icons at runtime, and `Scripts/Gameplay/Sprint/SprintFootIcons.cs`
draws the left/right shoe icons at runtime. These are project-authored code
assets with no third-party artwork or icon-font dependency.

The existing `Art/Brand/GameLogo.png` and `Art/Brand/AppIcon.png` remain the
separately credited project-generated art dated 2026-09-05 above; this update
does not create or reattribute those PNGs.
