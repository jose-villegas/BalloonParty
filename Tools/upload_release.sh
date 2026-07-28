#!/usr/bin/env bash
# upload_release.sh — Creates a GitHub release with an APK asset.
# Called by the Unity editor tool; not intended for direct use.
#
# Usage: upload_release.sh <version> <apk_path> <token> <repo> [commit_sha]
#
# Steps:
#   1. Validates inputs
#   2. Generates changelog from commits since last tag
#   3. Creates an annotated git tag vX.Y.Z
#   4. Pushes the tag to origin
#   5. Creates a GitHub release via the REST API
#   6. Uploads the APK as a release asset

set -euo pipefail

VERSION="${1:?Usage: upload_release.sh <version> <apk_path> <token> <repo> [commit_sha]}"
APK_PATH="${2:?APK path required}"
TOKEN="${3:?GitHub token required}"
REPO="${4:?Repository (owner/name) required}"
COMMIT_SHA="${5:-$(git rev-parse HEAD)}"

TAG="v${VERSION}"

# --- Validate ---
if [ ! -f "$APK_PATH" ]; then
    echo "ERROR: APK not found at: $APK_PATH" >&2
    exit 1
fi

if git rev-parse "$TAG" >/dev/null 2>&1; then
    echo "ERROR: Tag $TAG already exists. Choose a different version." >&2
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

RELEASE_BODY="## ${RANGE_LABEL}

${CHANGELOG}

---
**Build info**
- Version: \`${VERSION}\`
- Commit: \`${COMMIT_SHA}\`
- Built: $(date -u '+%Y-%m-%d %H:%M UTC')"

echo "--- Release Notes ---"
echo "$RELEASE_BODY"
echo "---------------------"

# --- Git tag ---
echo "Creating tag $TAG..."
git tag -a "$TAG" -m "Release ${VERSION} (${COMMIT_SHA})"

echo "Pushing tag to origin..."
git push origin "$TAG"

# --- Create GitHub release ---
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
    # Clean up: delete the tag we just pushed
    git push origin --delete "$TAG" 2>/dev/null || true
    git tag -d "$TAG" 2>/dev/null || true
    exit 1
fi

# --- Upload APK ---
APK_NAME="BalloonParty-${VERSION}.apk"
echo "Uploading ${APK_NAME}..."
UPLOAD_RESPONSE=$(curl -s -X POST \
    -H "Authorization: token ${TOKEN}" \
    -H "Accept: application/vnd.github+json" \
    -H "Content-Type: application/vnd.android.package-archive" \
    "${UPLOAD_URL}?name=${APK_NAME}" \
    --data-binary "@${APK_PATH}")

DOWNLOAD_URL=$(echo "$UPLOAD_RESPONSE" | jq -r '.browser_download_url')

if [ -z "$DOWNLOAD_URL" ] || [ "$DOWNLOAD_URL" = "null" ]; then
    echo "ERROR: Failed to upload APK. Response:" >&2
    echo "$UPLOAD_RESPONSE" >&2
    exit 1
fi

echo ""
echo "=== Release published successfully ==="
echo "  Tag:      $TAG"
echo "  Commit:   $COMMIT_SHA"
echo "  APK:      $DOWNLOAD_URL"
echo "  Release:  https://github.com/${REPO}/releases/tag/${TAG}"
