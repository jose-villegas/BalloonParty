#!/usr/bin/env bash
# upload_release.sh — Creates a GitHub release and uploads APK assets.
# Called by the Unity editor tool; not intended for direct use.
#
# Usage: upload_release.sh <version> <token> <repo> <summary> <checksums> <apk_path> [apk_path ...]
#
# Steps:
#   1. Validates inputs
#   2. Generates changelog from commits since last tag
#   3. Creates an annotated git tag vX.Y.Z
#   4. Pushes the tag to origin
#   5. Creates a GitHub release via the REST API (with checksums + commit info)
#   6. Uploads each APK as a release asset

set -euo pipefail

VERSION="${1:?Usage: upload_release.sh <version> <token> <repo> <summary> <checksums> <apk ...>}"
TOKEN="${2:?GitHub token required}"
REPO="${3:?Repository (owner/name) required}"
SUMMARY="${4:-}"
CHECKSUMS="${5:?Checksums string required}"
shift 5

if [ $# -eq 0 ]; then
    echo "ERROR: At least one APK path is required." >&2
    exit 1
fi

APK_PATHS=("$@")
TAG="v${VERSION}"
COMMIT_SHA=$(git rev-parse HEAD)
TREE_HASH=$(git rev-parse HEAD^{tree})

# --- Validate ---
for apk in "${APK_PATHS[@]}"; do
    if [ ! -f "$apk" ]; then
        echo "ERROR: APK not found at: $apk" >&2
        exit 1
    fi
done

if git rev-parse "$TAG" >/dev/null 2>&1; then
    echo "ERROR: Tag $TAG already exists locally. Choose a different version." >&2
    exit 1
fi

# --- Changelog ---
LAST_TAG=$(git describe --tags --abbrev=0 2>/dev/null || echo "")
if [ -n "$LAST_TAG" ]; then
    CHANGELOG=$(git --no-pager log --oneline "$LAST_TAG..HEAD" --no-decorate)
    RANGE_LABEL="Changes since $LAST_TAG"
else
    CHANGELOG=$(git --no-pager log --oneline -30 --no-decorate)
    RANGE_LABEL="Recent changes (last 30 commits)"
fi

# --- Build summary section ---
SUMMARY_SECTION=""
if [ -n "$SUMMARY" ]; then
    SUMMARY_SECTION="${SUMMARY}

"
fi

RELEASE_BODY="${SUMMARY_SECTION}## ${RANGE_LABEL}

${CHANGELOG}

---
**Build provenance**
- Version: \`${VERSION}\`
- Commit: [\`${COMMIT_SHA:0:10}\`](https://github.com/${REPO}/commit/${COMMIT_SHA})
- Tree hash: \`${TREE_HASH}\`
- Built: $(date -u '+%Y-%m-%d %H:%M UTC')

To verify a build came from this exact source: check out \`${TAG}\`, confirm
\`git rev-parse HEAD^{tree}\` matches the tree hash above. Each APK also contains
\`Resources/BuildInfo.json\` with the commit and tree hash baked in.

**SHA-256 checksums**
\`\`\`
$(echo -e "$CHECKSUMS")
\`\`\`

**Assets**
| File | Variant |
|------|---------|
| \`BalloonParty-${VERSION}-release.apk\` | Release (store-ready) |
| \`BalloonParty-${VERSION}-release-cheats.apk\` | Release + cheat console |
| \`BalloonParty-${VERSION}-dev.apk\` | Development (profiler, debug logs) |"

echo "--- Release Notes ---"
echo "$RELEASE_BODY"
echo "---------------------"

# --- Create GitHub release (also creates the tag via the API — no git push needed) ---
echo "Creating GitHub release..."
RELEASE_RESPONSE=$(curl -s -X POST \
    -H "Authorization: token ${TOKEN}" \
    -H "Accept: application/vnd.github+json" \
    -H "Content-Type: application/json" \
    "https://api.github.com/repos/${REPO}/releases" \
    -d "$(jq -n \
        --arg tag "$TAG" \
        --arg name "Release ${VERSION}" \
        --arg body "$RELEASE_BODY" \
        --arg sha "$COMMIT_SHA" \
        '{tag_name: $tag, name: $name, body: $body, target_commitish: $sha, draft: false, prerelease: false}'
    )")

UPLOAD_URL=$(echo "$RELEASE_RESPONSE" | jq -r '.upload_url' | sed 's/{?name,label}//')

if [ -z "$UPLOAD_URL" ] || [ "$UPLOAD_URL" = "null" ]; then
    echo "ERROR: Failed to create release. Response:" >&2
    echo "$RELEASE_RESPONSE" >&2
    exit 1
fi

# --- Upload APKs ---
FAILED=0
for apk in "${APK_PATHS[@]}"; do
    APK_NAME=$(basename "$apk")
    echo "Uploading ${APK_NAME}..."
    UPLOAD_RESPONSE=$(curl -s -X POST \
        -H "Authorization: token ${TOKEN}" \
        -H "Accept: application/vnd.github+json" \
        -H "Content-Type: application/vnd.android.package-archive" \
        "${UPLOAD_URL}?name=${APK_NAME}" \
        --data-binary "@${apk}")

    DOWNLOAD_URL=$(echo "$UPLOAD_RESPONSE" | jq -r '.browser_download_url')

    if [ -z "$DOWNLOAD_URL" ] || [ "$DOWNLOAD_URL" = "null" ]; then
        echo "WARNING: Failed to upload ${APK_NAME}:" >&2
        echo "$UPLOAD_RESPONSE" >&2
        FAILED=1
    else
        echo "  -> $DOWNLOAD_URL"
    fi
done

if [ "$FAILED" -eq 1 ]; then
    echo ""
    echo "WARNING: Some uploads failed. Check the release page for details." >&2
    exit 1
fi

echo ""
echo "=== Release published successfully ==="
echo "  Tag:      $TAG"
echo "  Commit:   ${COMMIT_SHA:0:10}"
echo "  Assets:   ${#APK_PATHS[@]} APKs uploaded"
echo "  Release:  https://github.com/${REPO}/releases/tag/${TAG}"
