using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;

namespace BalloonParty.Editor.Release
{
    /// <summary>
    ///     Creates the GitHub release and uploads the APKs, talking to the REST API directly.
    /// </summary>
    /// <remarks>
    ///     This was a bash script (<c>Tools/upload_release.sh</c>) invoked as <c>/bin/bash</c>, which
    ///     cannot resolve on Windows, and which needed <c>jq</c> — not shipped with Git Bash — so
    ///     fixing only the interpreter path just moved the failure a few lines down. Doing it in C#
    ///     needs nothing installed beyond what the editor already has.
    ///     <para>
    ///         It also keeps the token out of a process command line. The old call passed it as an
    ///         argument, so any crash dump printed it in full — which is exactly how one leaked.
    ///     </para>
    /// </remarks>
    internal static class ReleasePublisher
    {
        private const string ApiRoot = "https://api.github.com";
        private const string ApkContentType = "application/vnd.android.package-archive";

        // A 230 MB development APK over a slow link outlasts the 100s default by a wide margin.
        private const int UploadTimeoutMs = 30 * 60 * 1000;

        internal static bool Publish(in ReleaseRequest request, Action<string> log)
        {
            if (!Validate(request, log))
            {
                return false;
            }

            var tag = "v" + request.Version;
            var body = BuildReleaseBody(request, tag, log);

            log("Creating GitHub release...");
            if (!TryCreateRelease(request, tag, body, log, out var uploadUrl))
            {
                return false;
            }

            var failed = false;
            foreach (var apk in request.ApkPaths)
            {
                var name = Path.GetFileName(apk);
                log("Uploading " + name + "...");
                if (TryUploadAsset(uploadUrl, name, apk, request.Token, log, out var downloadUrl))
                {
                    log("  -> " + downloadUrl);
                }
                else
                {
                    failed = true;
                }
            }

            if (failed)
            {
                log("\nWARNING: Some uploads failed. Check the release page for details.");
                return false;
            }

            log("\n=== Release published successfully ===");
            log("  Tag:      " + tag);
            log("  Commit:   " + Shorten(request.CommitSha));
            log("  Assets:   " + request.ApkPaths.Count + " APKs uploaded");
            log("  Release:  https://github.com/" + request.Repository + "/releases/tag/" + tag);
            return true;
        }

        private static bool Validate(in ReleaseRequest request, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                log("ERROR: GitHub token required.");
                return false;
            }

            if (request.ApkPaths == null || request.ApkPaths.Count == 0)
            {
                log("ERROR: At least one APK path is required.");
                return false;
            }

            foreach (var apk in request.ApkPaths)
            {
                if (!File.Exists(apk))
                {
                    log("ERROR: APK not found at: " + apk);
                    return false;
                }
            }

            // Checked before anything is uploaded: the API would reject the tag anyway, but only
            // after the release call, leaving a half-made release behind.
            if (!string.IsNullOrEmpty(RunGit(request.ProjectRoot, "rev-parse v" + request.Version)))
            {
                log("ERROR: Tag v" + request.Version + " already exists locally. Choose a different version.");
                return false;
            }

            return true;
        }

        private static string BuildReleaseBody(in ReleaseRequest request, string tag, Action<string> log)
        {
            var lastTag = RunGit(request.ProjectRoot, "describe --tags --abbrev=0").Trim();
            string changelog;
            string rangeLabel;
            if (string.IsNullOrEmpty(lastTag))
            {
                changelog = RunGit(request.ProjectRoot, "--no-pager log --oneline -30 --no-decorate");
                rangeLabel = "Recent changes (last 30 commits)";
            }
            else
            {
                changelog = RunGit(request.ProjectRoot, "--no-pager log --oneline " + lastTag + "..HEAD --no-decorate");
                rangeLabel = "Changes since " + lastTag;
            }

            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(request.Summary))
            {
                builder.Append(request.Summary.Trim()).Append("\n\n");
            }

            builder.Append("## ").Append(rangeLabel).Append("\n\n");
            builder.Append(changelog.Trim()).Append("\n\n---\n");
            builder.Append("**Build provenance**\n");
            builder.Append("- Version: `").Append(request.Version).Append("`\n");
            builder.Append("- Commit: [`").Append(Shorten(request.CommitSha)).Append("`](https://github.com/")
                .Append(request.Repository).Append("/commit/").Append(request.CommitSha).Append(")\n");
            builder.Append("- Tree hash: `").Append(request.TreeHash).Append("`\n");
            builder.Append("- Built: ").Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm")).Append(" UTC\n\n");
            builder.Append("To verify a build came from this exact source: check out `").Append(tag)
                .Append("`, confirm\n`git rev-parse HEAD^{tree}` matches the tree hash above. Each APK also contains\n")
                .Append("`Resources/BuildInfo.json` with the commit and tree hash baked in.\n\n");
            builder.Append("**SHA-256 checksums**\n```\n");
            foreach (var line in request.ChecksumLines)
            {
                builder.Append(line).Append('\n');
            }

            builder.Append("```\n\n**Assets**\n| File | Variant |\n|------|---------|\n");
            builder.Append("| `BalloonParty-").Append(request.Version).Append("-release.apk` | Release (store-ready) |\n");
            builder.Append("| `BalloonParty-").Append(request.Version)
                .Append("-release-cheats.apk` | Release + cheat console |\n");
            builder.Append("| `BalloonParty-").Append(request.Version)
                .Append("-dev.apk` | Development (profiler, debug logs) |");

            var body = builder.ToString();
            log("--- Release Notes ---");
            log(body);
            log("---------------------");
            return body;
        }

        private static bool TryCreateRelease(
            in ReleaseRequest request, string tag, string body, Action<string> log, out string uploadUrl)
        {
            uploadUrl = null;

            // JsonUtility rather than hand-built JSON: the body carries newlines, backticks and
            // quotes straight from commit subjects, and escaping those by hand is how the shell
            // version needed jq in the first place.
            var payload = JsonUtility.ToJson(new ReleasePayload
            {
                tag_name = tag,
                name = "Release " + request.Version,
                body = body,
                target_commitish = request.CommitSha,
                draft = false,
                prerelease = false,
            });

            var url = ApiRoot + "/repos/" + request.Repository + "/releases";
            if (!TrySend(url, "POST", request.Token, "application/json",
                    Encoding.UTF8.GetBytes(payload), log, out var response))
            {
                log("ERROR: Failed to create release.");
                return false;
            }

            var created = JsonUtility.FromJson<ReleaseResponse>(response);
            if (created == null || string.IsNullOrEmpty(created.upload_url))
            {
                log("ERROR: Release created but no upload URL came back. Response:\n" + response);
                return false;
            }

            // GitHub returns an RFC 6570 template: ".../assets{?name,label}".
            var brace = created.upload_url.IndexOf('{');
            uploadUrl = brace >= 0 ? created.upload_url.Substring(0, brace) : created.upload_url;
            return true;
        }

        private static bool TryUploadAsset(
            string uploadUrl, string assetName, string filePath, string token, Action<string> log,
            out string downloadUrl)
        {
            downloadUrl = null;
            var url = uploadUrl + "?name=" + Uri.EscapeDataString(assetName);
            if (!TrySendFile(url, token, filePath, log, out var response))
            {
                log("WARNING: Failed to upload " + assetName + ".");
                return false;
            }

            var asset = JsonUtility.FromJson<AssetResponse>(response);
            if (asset == null || string.IsNullOrEmpty(asset.browser_download_url))
            {
                log("WARNING: Uploaded " + assetName + " but no download URL came back:\n" + response);
                return false;
            }

            downloadUrl = asset.browser_download_url;
            return true;
        }

        private static bool TrySend(
            string url, string method, string token, string contentType, byte[] payload,
            Action<string> log, out string response)
        {
            var request = CreateRequest(url, method, token, contentType);
            request.ContentLength = payload.Length;
            using (var stream = request.GetRequestStream())
            {
                stream.Write(payload, 0, payload.Length);
            }

            return TryReadResponse(request, log, out response);
        }

        // Streamed rather than read into a byte[]: the development APK is over 200 MB, and buffering
        // it whole costs that much managed heap inside the editor for no reason.
        private static bool TrySendFile(
            string url, string token, string filePath, Action<string> log, out string response)
        {
            var request = CreateRequest(url, "POST", token, ApkContentType);
            var info = new FileInfo(filePath);
            request.ContentLength = info.Length;
            request.AllowWriteStreamBuffering = false;

            using var source = File.OpenRead(filePath);
            using var destination = request.GetRequestStream();
            source.CopyTo(destination, 1 << 20);

            return TryReadResponse(request, log, out response);
        }

        private static HttpWebRequest CreateRequest(
            string url, string method, string token, string contentType)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            request.ContentType = contentType;
            request.Accept = "application/vnd.github+json";

            // GitHub rejects requests without one.
            request.UserAgent = "BalloonParty-ReleaseTool";
            request.Headers["Authorization"] = "token " + token;
            request.Timeout = UploadTimeoutMs;
            request.ReadWriteTimeout = UploadTimeoutMs;
            return request;
        }

        private static bool TryReadResponse(HttpWebRequest request, Action<string> log, out string response)
        {
            response = null;
            try
            {
                using var webResponse = (HttpWebResponse)request.GetResponse();
                using var reader = new StreamReader(webResponse.GetResponseStream() ?? Stream.Null);
                response = reader.ReadToEnd();
                return true;
            }
            catch (WebException exception)
            {
                // The API puts the reason (bad token, tag exists, asset name taken) in the error
                // body, so surfacing only the status code would hide the one useful sentence.
                var detail = "";
                if (exception.Response is HttpWebResponse errorResponse)
                {
                    using var reader = new StreamReader(errorResponse.GetResponseStream() ?? Stream.Null);
                    detail = " " + (int)errorResponse.StatusCode + " " + reader.ReadToEnd();
                }

                log("  HTTP error:" + (string.IsNullOrEmpty(detail) ? " " + exception.Message : detail));
                return false;
            }
        }

        private static string RunGit(string workingDir, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return "";
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : "";
        }

        private static string Shorten(string sha)
        {
            return string.IsNullOrEmpty(sha) || sha.Length <= 10 ? sha : sha.Substring(0, 10);
        }

        [Serializable]
        private class ReleasePayload
        {
            public string tag_name;
            public string name;
            public string body;
            public string target_commitish;
            public bool draft;
            public bool prerelease;
        }

        [Serializable]
        private class ReleaseResponse
        {
            public string upload_url;
        }

        [Serializable]
        private class AssetResponse
        {
            public string browser_download_url;
        }
    }

    internal readonly struct ReleaseRequest
    {
        public readonly string Version;
        public readonly string Token;
        public readonly string Repository;
        public readonly string Summary;
        public readonly string CommitSha;
        public readonly string TreeHash;
        public readonly string ProjectRoot;
        public readonly IReadOnlyList<string> ChecksumLines;
        public readonly IReadOnlyList<string> ApkPaths;

        public ReleaseRequest(
            string version, string token, string repository, string summary, string commitSha,
            string treeHash, string projectRoot, IReadOnlyList<string> checksumLines,
            IReadOnlyList<string> apkPaths)
        {
            Version = version;
            Token = token;
            Repository = repository;
            Summary = summary;
            CommitSha = commitSha;
            TreeHash = treeHash;
            ProjectRoot = projectRoot;
            ChecksumLines = checksumLines;
            ApkPaths = apkPaths;
        }
    }
}
