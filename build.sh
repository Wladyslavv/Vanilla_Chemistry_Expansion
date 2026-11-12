#!/usr/bin/env bash
# Builds VanillaChemExt.dll with the .NET Framework csc that ships with Windows (C# 5),
# then assembles the mod folder in dist/vanilla_chem_ext.
# Usage: bash build.sh [--install]   (--install copies the mod into the game's Mods folder)
set -euo pipefail

PROJECT="$(cd "$(dirname "$0")" && pwd)"
GAME="C:/Program Files (x86)/Steam/steamapps/common/Probably Stolen"
MANAGED="$GAME/Probably Stolen_Data/Managed"
CSC="/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe"

# csc.exe refuses to start from a working directory longer than MAX_PATH, so build in a short temp dir.
BUILD="$TEMP/vcebuild"
rm -rf "$BUILD"
mkdir -p "$BUILD"
cp "$PROJECT"/src/*.cs "$BUILD/"

refs=()
# UnityEngine.UI: the electrolyzer's window connector subclasses a vanilla UI element (SpritePixelElement), and the
# handbook styles the paper UI's images (Canvas is in UnityEngine.UIModule). Unity.TextMeshPro: the handbook styles its
# page number text.
for dll in mscorlib netstandard System System.Core Assembly-CSharp UnityEngine.CoreModule UnityEngine.UI UnityEngine.UIModule Unity.TextMeshPro 0Harmony; do
    refs+=("-r:$MANAGED/$dll.dll")
done

(cd "$BUILD" && "$CSC" -nologo -noconfig -nostdlib -target:library -optimize+ -out:VanillaChemExt.dll "${refs[@]}" *.cs)

DIST="$PROJECT/dist/vanilla_chem_ext"
rm -rf "$DIST"
mkdir -p "$DIST/Localization" "$DIST/Textures/Items" "$DIST/Textures/Paper"
cp "$BUILD/VanillaChemExt.dll" "$PROJECT/manifest.xml" "$PROJECT/preview.png" "$DIST/"
# The mod folder keeps the full player guide as its README (the repository README is about the cartel system).
cp "$PROJECT/docs/MOD_GUIDE.md" "$DIST/README.md"
cp "$PROJECT"/Localization/*.csv "$DIST/Localization/"
# ModLoader turns every Textures/Items/*.png into a pixel-art item sprite keyed by file name.
cp "$PROJECT"/Textures/Items/*.png "$DIST/Textures/Items/"
# The game build lacks its paper asset catalog, so vanilla notebook sprites (background, buttons) do not
# resolve for mod books; ModLoader loads Textures/Paper/*.png as this mod's paper sprites instead.
cp "$PROJECT"/Textures/Paper/*.png "$DIST/Textures/Paper/"
echo "Built $DIST"

if [[ "${1:-}" == "--install" ]]; then
    TARGET="$GAME/Mods/vanilla_chem_ext"
    # Before 1.0.0 this mod was installed as Mods/dream_toxin; remove it so it doesn't load twice.
    rm -rf "$GAME/Mods/dream_toxin"
    # 1.2.0 merged the Starch Genes and Industrial Alcohol test mods; remove them so nothing registers twice.
    rm -rf "$GAME/Mods/starch_genes_test" "$GAME/Mods/industrial_alcohol_test"
    # 1.3.0 merged the Nuclear Chemistry test mod.
    rm -rf "$GAME/Mods/nuclear_chem_test" "$GAME/Mods/nuclear_waste_reprocessing"
    rm -rf "$TARGET"
    cp -r "$DIST" "$TARGET"
    echo "Installed to $TARGET"
fi
