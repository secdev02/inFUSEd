<#
decoy_webdav.ps1  (Minimal Fake WebDAV Server in PowerShell)

Goals:
- Map a drive:   net use Z: http://SERVER/drive
- Enumerate via Explorer / dir / ls
- Present a fake tree (built-in minimal defaults)
- Optionally load/override from JSON (-ConfigPath)
- Log every request attempt to console (method/url/depth/ua)

Run (Admin recommended for port 80):
  powershell.exe -ExecutionPolicy Bypass -File .\decoy_webdav.ps1

With JSON:
  powershell.exe -ExecutionPolicy Bypass -File .\decoy_webdav.ps1 -ConfigPath .\fakefs.json

Notes:
- Windows WebDAV client (WebClient + MiniRedir) is picky; this script handles "/" probes and "/drive" vs "/drive/".
#>

param(
    [string]$ListenAddress = "+",
    [int]$Port = 80,
    [string]$ShareRoot = "/drive",
    [string]$ConfigPath = ""
)

Set-StrictMode -Version 2
$ErrorActionPreference = "Stop"

# ----------------------------
# Fake filesystem storage
# ----------------------------
# Key: full DAV path, e.g. "/drive/Docs/" or "/drive/readme.txt"
# Value: hashtable with:
#   Type = "dir" | "file"
#   Name
#   CreatedUtc (DateTime)
#   ModifiedUtc (DateTime)
#   For files: ContentType, ContentBytes, Size
$FakeFs = @{}

# ----------------------------
# Utility helpers
# ----------------------------
function Normalize-ConfigPath {
    param([string]$p)
    if ([string]::IsNullOrWhiteSpace($p)) { return $p }
    $p = [System.Uri]::UnescapeDataString($p)
    if (-not $p.StartsWith("/")) { $p = "/" + $p }
    return $p
}

function Normalize-Path {
    param([string]$rawPath)
    $p = [System.Uri]::UnescapeDataString($rawPath)
    if (-not $p.StartsWith("/")) { $p = "/" + $p }
    return $p
}

function New-BytesFromText {
    param([string]$s)
    if ($null -eq $s) { $s = "" }
    [System.Text.Encoding]::UTF8.GetBytes($s)
}

function Parse-UtcDateOrDefault {
    param([string]$iso, [DateTime]$defaultUtc)
    if ([string]::IsNullOrWhiteSpace($iso)) { return $defaultUtc }
    try {
        return ([DateTimeOffset]::Parse($iso)).UtcDateTime
    } catch {
        return $defaultUtc
    }
}

function To-Rfc1123 {
    param([DateTime]$utc)
    if ($utc.Kind -ne [DateTimeKind]::Utc) { $utc = $utc.ToUniversalTime() }
    $utc.ToString("R")
}

function Dav-CommonHeaders {
    param($resp)
    # These headers help Windows WebDAV mini-redirector behave.
    $resp.Headers["DAV"] = "1,2"
    $resp.Headers["MS-Author-Via"] = "DAV"
    $resp.Headers["Accept-Ranges"] = "bytes"
}

function Write-Log {
    param($req)
    $depth = $req.Headers["Depth"]
    $ua = $req.UserAgent
    $remote = ""
    try { $remote = $req.RemoteEndPoint.ToString() } catch { $remote = "unknown" }
    Write-Host ("[{0}] {1} {2} Depth={3} From={4} UA={5}" -f (Get-Date), $req.HttpMethod, $req.RawUrl, $depth, $remote, $ua)
}

function Send-Bytes {
    param(
        $resp,
        [int]$code,
        [string]$contentType,
        [byte[]]$bytes
    )
    $resp.StatusCode = $code
    if ($contentType) { $resp.ContentType = $contentType }
    $resp.ContentLength64 = $bytes.Length
    $out = $resp.OutputStream
    $out.Write($bytes, 0, $bytes.Length)
    $out.Close()
}

function Send-Text {
    param(
        $resp,
        [int]$code,
        [string]$contentType,
        [string]$text
    )
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($text)
    Send-Bytes $resp $code $contentType $bytes
}

function Get-BaseUrl {
    param($req)
    # Do NOT use $host/$Host (read-only automatic variable). Use $hostHeader instead.
    $hostHeader = $req.Headers["Host"]
    if ([string]::IsNullOrWhiteSpace($hostHeader)) { return "http://localhost" }
    "http://" + $hostHeader
}

function Parse-DepthHeader {
    param($req)
    $d = $req.Headers["Depth"]
    if ([string]::IsNullOrWhiteSpace($d)) { return 0 }
    if ($d -eq "infinity") { return 1 } # keep minimal
    $n = 0
    [void][int]::TryParse($d, [ref]$n)
    $n
}

function Is-Dir {
    param([string]$path)
    $FakeFs.ContainsKey($path) -and $FakeFs[$path].Type -eq "dir"
}

function Is-File {
    param([string]$path)
    $FakeFs.ContainsKey($path) -and $FakeFs[$path].Type -eq "file"
}

function Get-Children {
    param([string]$dirPath)
    if (-not $dirPath.EndsWith("/")) { $dirPath += "/" }

    $children = New-Object System.Collections.Generic.List[string]
    foreach ($k in $FakeFs.Keys) {
        if ($k -eq $dirPath) { continue }
        if ($k.StartsWith($dirPath)) {
            $rest = $k.Substring($dirPath.Length)
            if ($rest.Length -eq 0) { continue }
            $restTrim = $rest.TrimEnd("/")
            if ($restTrim -notmatch "/") {
                $children.Add($k) | Out-Null
            }
        }
    }
    $children
}

function Add-FakeDirEx {
    param(
        [string]$path,
        [DateTime]$createdUtc,
        [DateTime]$modifiedUtc
    )
    if (-not $path.EndsWith("/")) { $path += "/" }
    $FakeFs[$path] = @{
        Type = "dir"
        Name = ($path.TrimEnd("/") -split "/")[-1]
        CreatedUtc = $createdUtc
        ModifiedUtc = $modifiedUtc
    }
}

function Add-FakeFileEx {
    param(
        [string]$path,
        [string]$contentType,
        [byte[]]$contentBytes,
        [DateTime]$createdUtc,
        [DateTime]$modifiedUtc
    )
    $FakeFs[$path] = @{
        Type = "file"
        Name = ($path -split "/")[-1]
        ContentType = $contentType
        ContentBytes = $contentBytes
        Size = $contentBytes.Length
        CreatedUtc = $createdUtc
        ModifiedUtc = $modifiedUtc
    }
}

function Ensure-ParentDirs {
    param([string]$fileOrDirPath, [DateTime]$createdUtc, [DateTime]$modifiedUtc)

    $p = $fileOrDirPath
    if ($p.EndsWith("/")) { $p = $p.TrimEnd("/") }

    $idx = $p.LastIndexOf("/")
    if ($idx -le 0) { return }

    $parent = $p.Substring(0, $idx + 1) # include trailing slash
    $parts = $parent.TrimEnd("/").Split("/")
    $cur = ""

    foreach ($part in $parts) {
        if ([string]::IsNullOrWhiteSpace($part)) {
            $cur = "/"
            continue
        }
        if ($cur -eq "/") { $cur = "/" + $part + "/" }
        else { $cur = $cur + $part + "/" }

        if (-not $FakeFs.ContainsKey($cur)) {
            Add-FakeDirEx -path $cur -createdUtc $createdUtc -modifiedUtc $modifiedUtc
        }
    }
}

# ----------------------------
# Built-in minimal debug tree
# ----------------------------
function Set-BuiltInDebugTree {
    param([string]$root)

    $root = Normalize-ConfigPath $root
    $root = $root.TrimEnd("/")

    $nowUtc = [DateTime]::UtcNow
    Add-FakeDirEx  -path ($root + "/")              -createdUtc $nowUtc -modifiedUtc $nowUtc
    Add-FakeDirEx  -path ($root + "/Docs/")         -createdUtc $nowUtc -modifiedUtc $nowUtc
    Add-FakeDirEx  -path ($root + "/Tools/")        -createdUtc $nowUtc -modifiedUtc $nowUtc
    Add-FakeDirEx  -path ($root + "/Docs/Reports/") -createdUtc $nowUtc -modifiedUtc $nowUtc

    Add-FakeFileEx -path ($root + "/readme.txt") `
        -contentType "text/plain" `
        -contentBytes (New-BytesFromText "Fake WebDAV share.`r`nBuilt-in debug tree.`r`n") `
        -createdUtc $nowUtc -modifiedUtc $nowUtc

    Add-FakeFileEx -path ($root + "/Docs/Reports/Q4-summary.txt") `
        -contentType "text/plain" `
        -contentBytes (New-BytesFromText "Q4 Summary (fake).`r`n") `
        -createdUtc $nowUtc -modifiedUtc $nowUtc

    Add-FakeFileEx -path ($root + "/Tools/setup.ps1") `
        -contentType "text/plain" `
        -contentBytes (New-BytesFromText "Write-Host 'Hello from a fake file.'`r`n") `
        -createdUtc $nowUtc -modifiedUtc $nowUtc
}

# ----------------------------
# JSON import (extends/overrides built-in)
# ----------------------------
function Import-FakeFsFromJson {
    param(
        [string]$jsonPath,
        [string]$defaultRoot
    )

    $defaultRoot = Normalize-ConfigPath $defaultRoot
    $defaultRoot = $defaultRoot.TrimEnd("/")

    if ([string]::IsNullOrWhiteSpace($jsonPath)) { return $defaultRoot }
    if (-not (Test-Path -LiteralPath $jsonPath)) {
        Write-Host ("[Config] JSON not found: {0} (using built-in only)" -f $jsonPath) -ForegroundColor Yellow
        return $defaultRoot
    }

    $raw = Get-Content -LiteralPath $jsonPath -Raw
    $cfg = $raw | ConvertFrom-Json

    $root = $defaultRoot
    if ($cfg.shareRoot) { $root = Normalize-ConfigPath ([string]$cfg.shareRoot) }
    $root = $root.TrimEnd("/")

    $defaultsCreated = [DateTime]::UtcNow
    $defaultsModified = [DateTime]::UtcNow
    $defaultsType = "application/octet-stream"
    $placeholderText = "This is a simulated file.`r`n"

    if ($cfg.defaults) {
        $defaultsCreated = Parse-UtcDateOrDefault ([string]$cfg.defaults.createdUtc) $defaultsCreated
        $defaultsModified = Parse-UtcDateOrDefault ([string]$cfg.defaults.modifiedUtc) $defaultsModified
        if ($cfg.defaults.contentType) { $defaultsType = [string]$cfg.defaults.contentType }
        if ($cfg.defaults.placeholderText) { $placeholderText = [string]$cfg.defaults.placeholderText }
    }

    if (-not $cfg.entries) {
        Write-Host "[Config] No entries[] in JSON; keeping built-in tree." -ForegroundColor Yellow
        return $root
    }

    foreach ($e in $cfg.entries) {
        if (-not $e.path -or -not $e.type) { continue }

        $p = Normalize-ConfigPath ([string]$e.path)

        # If caller forgot /drive prefix but provided a relative path, attach to root
        if (-not $p.StartsWith("/")) { $p = "/" + $p }
        if (-not $p.StartsWith($root + "/") -and $p -notmatch "^/") {
            $p = $root + "/" + $p.TrimStart("/")
        }

        $created = Parse-UtcDateOrDefault ([string]$e.createdUtc) $defaultsCreated
        $modified = Parse-UtcDateOrDefault ([string]$e.modifiedUtc) $defaultsModified

        if ([string]$e.type -eq "dir") {
            Ensure-ParentDirs -fileOrDirPath $p -createdUtc $created -modifiedUtc $modified
            Add-FakeDirEx -path $p -createdUtc $created -modifiedUtc $modified
            continue
        }

        if ([string]$e.type -eq "file") {
            Ensure-ParentDirs -fileOrDirPath $p -createdUtc $created -modifiedUtc $modified

            $ctype = $defaultsType
            if ($e.contentType) { $ctype = [string]$e.contentType }

            $bytes = $null
            if ($e.contentBase64) {
                try { $bytes = [Convert]::FromBase64String([string]$e.contentBase64) } catch { $bytes = $null }
            }
            if ($null -eq $bytes) {
                if ($e.contentText) { $bytes = New-BytesFromText ([string]$e.contentText) }
                else { $bytes = New-BytesFromText $placeholderText }
            }

            Add-FakeFileEx -path $p -contentType $ctype -contentBytes $bytes -createdUtc $created -modifiedUtc $modified

            # If JSON provided a "size", treat it as reported size (optional)
            if ($e.size) {
                try { $FakeFs[$p].Size = [int64]$e.size } catch {}
            }
            continue
        }
    }

    return $root
}

# ----------------------------
# PROPFIND response builder
# ----------------------------
function Build-PropfindResponseXml {
    param(
        [string]$baseUrl,
        [string]$path,
        [int]$depth
    )

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append('<?xml version="1.0" encoding="utf-8"?>')
    [void]$sb.Append('<D:multistatus xmlns:D="DAV:">')

    function Append-Response {
        param([string]$p)

        $isDir = Is-Dir $p
        $isFile = Is-File $p
        if (-not ($isDir -or $isFile)) { return }

        $href = $p
        if ($isDir -and (-not $href.EndsWith("/"))) { $href += "/" }

        $displayName = $FakeFs[$p].Name
        if ([string]::IsNullOrWhiteSpace($displayName)) { $displayName = "/" }

        $modUtc = [DateTime]::UtcNow
        if ($FakeFs[$p].ContainsKey("ModifiedUtc")) { $modUtc = [DateTime]$FakeFs[$p].ModifiedUtc }
        $lastMod = To-Rfc1123 $modUtc

        $contentType = ""
        $contentLength = "0"

        if ($isDir) {
            $contentType = "httpd/unix-directory"
            $contentLength = "0"
        } else {
            $contentType = $FakeFs[$p].ContentType
            $contentLength = [string]$FakeFs[$p].Size
        }

        [void]$sb.Append("<D:response>")
        [void]$sb.Append("<D:href>" + $href + "</D:href>")
        [void]$sb.Append("<D:propstat>")
        [void]$sb.Append("<D:status>HTTP/1.1 200 OK</D:status>")
        [void]$sb.Append("<D:prop>")
        [void]$sb.Append("<D:displayname>" + $displayName + "</D:displayname>")
        [void]$sb.Append("<D:getlastmodified>" + $lastMod + "</D:getlastmodified>")
        [void]$sb.Append("<D:getcontenttype>" + $contentType + "</D:getcontenttype>")
        [void]$sb.Append("<D:getcontentlength>" + $contentLength + "</D:getcontentlength>")
        if ($isDir) {
            [void]$sb.Append("<D:resourcetype><D:collection/></D:resourcetype>")
        } else {
            [void]$sb.Append("<D:resourcetype/>")
        }
        [void]$sb.Append("</D:prop>")
        [void]$sb.Append("</D:propstat>")
        [void]$sb.Append("</D:response>")
    }

    Append-Response $path

    if ($depth -ge 1 -and (Is-Dir $path)) {
        $kids = Get-Children $path
        foreach ($k in $kids) { Append-Response $k }
    }

    [void]$sb.Append("</D:multistatus>")
    $sb.ToString()
}

# ----------------------------
# Build fake FS:
# 1) built-in debug tree
# 2) optional JSON import extends/overrides
# ----------------------------
$ShareRoot = Normalize-ConfigPath $ShareRoot
$ShareRoot = $ShareRoot.TrimEnd("/")

Set-BuiltInDebugTree -root $ShareRoot
$ShareRoot = Import-FakeFsFromJson -jsonPath $ConfigPath -defaultRoot $ShareRoot

# Ensure share root exists even if JSON omitted it
$ShareRootSlash = $ShareRoot + "/"
if (-not $FakeFs.ContainsKey($ShareRootSlash)) {
    $nowUtc = [DateTime]::UtcNow
    Add-FakeDirEx -path $ShareRootSlash -createdUtc $nowUtc -modifiedUtc $nowUtc
}

Write-Host ("[FakeFS] Loaded {0} entries under {1}/" -f $FakeFs.Count, $ShareRoot)

# ----------------------------
# Listener setup
# ----------------------------
$prefix = "http://{0}:{1}/" -f $ListenAddress, $Port
$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add($prefix)

Write-Host ("Listening on {0}" -f $prefix)
Write-Host ("Fake WebDAV root: {0}/ (map: net use Z: http://HOST:{1}{0})" -f $ShareRoot, $Port)
Write-Host ("Press Ctrl+C to stop.")
Write-Host ""

$listener.Start()

try {
    while ($true) {
        $context = $listener.GetContext()
        $req = $context.Request
        $resp = $context.Response

        Write-Log $req
        Dav-CommonHeaders $resp

        $path = Normalize-Path $req.Url.AbsolutePath

        # Normalize /drive vs /drive/
        if ($path -eq $ShareRoot) { $path = $ShareRoot + "/" }

        # ---- Root probe handling: Windows WebDAV often probes "/" first ----
        if ($path -eq "/") {
            if ($req.HttpMethod -eq "OPTIONS") {
                $resp.StatusCode = 200
                $resp.Headers["Allow"] = "OPTIONS, GET, HEAD, PROPFIND, LOCK, UNLOCK"
                $resp.Headers["Public"] = "OPTIONS, GET, HEAD, PROPFIND, LOCK, UNLOCK"
                $resp.Close()
                continue
            }

            if ($req.HttpMethod -eq "PROPFIND") {
                $depth = Parse-DepthHeader $req
                $now = To-Rfc1123 ([DateTime]::UtcNow)

                $xml = '<?xml version="1.0" encoding="utf-8"?>' +
                       '<D:multistatus xmlns:D="DAV:">' +
                       '<D:response>' +
                       '<D:href>/</D:href>' +
                       '<D:propstat><D:status>HTTP/1.1 200 OK</D:status><D:prop>' +
                       '<D:displayname>/</D:displayname>' +
                       '<D:getlastmodified>' + $now + '</D:getlastmodified>' +
                       '<D:getcontenttype>httpd/unix-directory</D:getcontenttype>' +
                       '<D:getcontentlength>0</D:getcontentlength>' +
                       '<D:resourcetype><D:collection/></D:resourcetype>' +
                       '</D:prop></D:propstat>' +
                       '</D:response>'

                if ($depth -ge 1) {
                    $shareHref = $ShareRoot + "/"
                    $xml += '<D:response>' +
                            '<D:href>' + $shareHref + '</D:href>' +
                            '<D:propstat><D:status>HTTP/1.1 200 OK</D:status><D:prop>' +
                            '<D:displayname>' + $ShareRoot.Trim("/") + '</D:displayname>' +
                            '<D:getlastmodified>' + $now + '</D:getlastmodified>' +
                            '<D:getcontenttype>httpd/unix-directory</D:getcontenttype>' +
                            '<D:getcontentlength>0</D:getcontentlength>' +
                            '<D:resourcetype><D:collection/></D:resourcetype>' +
                            '</D:prop></D:propstat>' +
                            '</D:response>'
                }

                $xml += '</D:multistatus>'
                Send-Text $resp 207 "text/xml; charset=utf-8" $xml
                continue
            }

            if ($req.HttpMethod -eq "GET") {
                # Redirect browsers / explorers to the actual share root
                $loc = $ShareRoot + "/"
                $resp.StatusCode = 302
                $resp.Headers["Location"] = $loc
                $resp.Close()
                continue
            }

            Send-Text $resp 405 "text/plain; charset=utf-8" ("Method not allowed: " + $req.HttpMethod)
            continue
        }
        # ---- End root probe handling ----

        # OPTIONS responder for DAV negotiation
        if ($req.HttpMethod -eq "OPTIONS") {
            $resp.StatusCode = 200
            $resp.Headers["Allow"] = "OPTIONS, GET, HEAD, PROPFIND, LOCK, UNLOCK"
            $resp.Headers["Public"] = "OPTIONS, GET, HEAD, PROPFIND, LOCK, UNLOCK"
            $resp.Close()
            continue
        }

        # HEAD is used sometimes
        if ($req.HttpMethod -eq "HEAD") {
            if (Is-File $path) {
                $resp.StatusCode = 200
                $resp.ContentType = $FakeFs[$path].ContentType
                $resp.ContentLength64 = [int64]$FakeFs[$path].Size
            } elseif (Is-Dir $path) {
                $resp.StatusCode = 200
                $resp.ContentType = "httpd/unix-directory"
                $resp.ContentLength64 = 0
            } else {
                $resp.StatusCode = 404
                $resp.ContentLength64 = 0
            }
            $resp.Close()
            continue
        }

        # Directory listing / properties
        if ($req.HttpMethod -eq "PROPFIND") {
            $depth = Parse-DepthHeader $req
            if (Is-Dir $path -or Is-File $path) {
                $baseUrl = Get-BaseUrl $req
                $xml = Build-PropfindResponseXml $baseUrl $path $depth
                Send-Text $resp 207 "text/xml; charset=utf-8" $xml
                continue
            }
            Send-Text $resp 404 "text/plain; charset=utf-8" "Not found"
            continue
        }

        # LOCK/UNLOCK: Windows often does this while mapping
        if ($req.HttpMethod -eq "LOCK") {
            $token = "opaquelocktoken:" + ([Guid]::NewGuid().ToString())
            $resp.StatusCode = 200
            $resp.Headers["Lock-Token"] = "<" + $token + ">"

            $body = '<?xml version="1.0" encoding="utf-8"?>' +
                    '<D:prop xmlns:D="DAV:"><D:lockdiscovery><D:activelock>' +
                    '<D:locktype><D:write/></D:locktype>' +
                    '<D:lockscope><D:exclusive/></D:lockscope>' +
                    '<D:depth>Infinity</D:depth>' +
                    '<D:timeout>Second-600</D:timeout>' +
                    '<D:locktoken><D:href>' + $token + '</D:href></D:locktoken>' +
                    '</D:activelock></D:lockdiscovery></D:prop>'

            Send-Text $resp 200 "text/xml; charset=utf-8" $body
            continue
        }

        if ($req.HttpMethod -eq "UNLOCK") {
            $resp.StatusCode = 204
            $resp.Close()
            continue
        }

        # GET returns dummy content for files; for dirs, tell client to use PROPFIND
        if ($req.HttpMethod -eq "GET") {
            if (Is-File $path) {
                $bytes = [byte[]]$FakeFs[$path].ContentBytes
                $ctype = [string]$FakeFs[$path].ContentType
                Send-Bytes $resp 200 $ctype $bytes
                continue
            }
            if (Is-Dir $path) {
                Send-Text $resp 403 "text/plain; charset=utf-8" "Directory listing via PROPFIND only."
                continue
            }
            Send-Text $resp 404 "text/plain; charset=utf-8" "Not found"
            continue
        }

        # Anything else
        Send-Text $resp 405 "text/plain; charset=utf-8" ("Method not allowed: " + $req.HttpMethod)
    }
}
finally {
    try { $listener.Stop() } catch {}
    try { $listener.Close() } catch {}
}
