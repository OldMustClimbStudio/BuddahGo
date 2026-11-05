BuddahGo Solo CJK

A renamed subset of Noto Sans CJK SC Regular, distributed under SIL OFL 1.1.
Original copyright/name/license metadata is retained in the font. See OFL.txt.
Source: https://github.com/notofonts/noto-cjk/blob/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf
Downloaded 2026-10-01. Original SHA256: 2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b
Subset using fonttools 4.60.1, retaining Basic Latin and characters in project C# and serialized Unity/UI source.
Family/PostScript names changed to BuddahGo Solo CJK / BuddahGoSoloCJK.
2026-11-03: regenerated with 539 codepoints (the previous 499 plus the graphics-settings labels, UI/Text settings.*
rows, and the settings panel's own strings) from the Noto Sans SC variable font installed on the build machine
(NotoSansSC-VF.ttf, instanced at wght 400 with fontTools 4.68 varLib.instancer, then subset). Same family, same OFL
licence; the renamed family/PostScript names are unchanged.
The TMP asset is now a Dynamic font asset whose source font file is this subset OTF (serialized 2048px atlas with all
539 glyphs added in the editor, so a player needs no OS font). To add characters: regenerate the OTF with the new
codepoints (the 2026-11-03 session script did: instance the VF, subset, rename), then in the editor assign the OTF as
the asset's Source Font File, call ClearFontAssetData(true) and TryAddCharacters with every codepoint, and save. Do not
reuse glyph-index caches from an older font file. When adding translated UI strings, verify coverage first.
