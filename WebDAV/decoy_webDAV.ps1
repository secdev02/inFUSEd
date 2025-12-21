<#
decoy_webdav.ps1  (Minimal Fake WebDAV Server in PowerShell)

- Map: net use R: http://SERVER/drive
- Browse: dir R:\ , dir R:\docs , explorer R:\
- Fake tree from built-in defaults + optional JSON (-ConfigPath)
- Logs every request to console

Run (Admin recommended for port 80):
  powershell.exe -ExecutionPolicy Bypass -File .\decoy_webdav.ps1

With JSON:
  powershell.exe -ExecutionPolicy Bypass -File .\decoy_webdav.ps1 -ConfigPath .\fakefs.json
#>

param(
    [string]$ListenAddress = "+",
    [int]$Port = 80,
    [string]$ShareRoot = "/drive",
    [string]$ConfigPath = ""
)

Set-StrictMode -Version 2
$ErrorActionPreference = "Stop"

# Case-insensitive filesystem map
$FakeFs = New-Object System.Collections.Hashtable ([System.StringComparer]::OrdinalIgnoreCase)

# ----------------------------
# StrictMode-safe JSON property accessor
# ----------------------------
function Get-JsonPropString {
    param(
        [Parameter(Mandatory=$true)]$obj,
        [Parameter(Mandatory=$true)][string]$name
    )
    $p = $obj.PSObject.Properties[$name]
    if ($null -eq $p) { return $null }
    if ($null -eq $p.Value) { return $null }
    return [string]$p.Value
}

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
    return [System.Text.Encoding]::UTF8.GetBytes($s)
}

function Parse-UtcDateOrDefault {
    param([string]$iso, [DateTime]$defaultUtc)
    if ([string]::IsNullOrWhiteSpace($iso)) { return $defaultUtc }
    try { return ([DateTimeOffset]::Parse($iso)).UtcDateTime } catch { return $defaultUtc }
}

function To-Rfc1123 {
    param([DateTime]$utc)
    if ($utc.Kind -ne [DateTimeKind]::Utc) { $utc = $utc.ToUniversalTime() }
    return $utc.ToString("R")
}

function Dav-CommonHeaders {
    param($resp)
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
    param($resp, [int]$code, [string]$contentType, [byte[]]$bytes)
    $resp.StatusCode = $code
    if ($contentType) { $resp.ContentType = $contentType }
    $resp.ContentLength64 = $bytes.Length
    $out = $resp.OutputStream
    $out.Write($bytes, 0, $bytes.Length)
    $out.Close()
}

function Send-Text {
    param($resp, [int]$code, [string]$contentType, [string]$text)
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($text)
    Send-Bytes $resp $code $contentType $bytes
}

function Get-BaseUrl {
    param($req)
    $hostHeader = $req.Headers["Host"]
    if ([string]::IsNullOrWhiteSpace($hostHeader)) { return "http://localhost" }
    return "http://" + $hostHeader
}

function Parse-DepthHeader {
    param($req)
    $d = $req.Headers["Depth"]
    if ([string]::IsNullOrWhiteSpace($d)) { return 0 }
    if ($d -eq "infinity") { return 1 }  # keep minimal
    $n = 0
    [void][int]::TryParse($d, [ref]$n)
    return $n
}

# ----------------------------
# FakeFS helpers (case + slash tolerant)
# ----------------------------
function CanonDir {
    param([string]$p)
    if (-not $p.EndsWith("/")) { $p += "/" }
    return $p
}

function Resolve-ExistingPath {
    param([string]$p)

    if ($FakeFs.ContainsKey($p)) { return $p }

    if (-not $p.EndsWith("/")) {
        $p2 = $p + "/"
        if ($FakeFs.ContainsKey($p2)) { return $p2 }
    }

    if ($p.EndsWith("/")) {
        $p3 = $p.TrimEnd("/")
        if ($FakeFs.ContainsKey($p3)) { return $p3 }
    }

    return $null
}

function Is-Dir {
    param([string]$path)
    $rp = Resolve-ExistingPath $path
    if ($null -eq $rp) { return $false }
    return ($FakeFs[$rp].Type -eq "dir")
}

function Is-File {
    param([string]$path)
    $rp = Resolve-ExistingPath $path
    if ($null -eq $rp) { return $false }
    return ($FakeFs[$rp].Type -eq "file")
}

function Get-Children {
    param([string]$dirPath)

    $dirPath = CanonDir $dirPath
    $dirKey = Resolve-ExistingPath $dirPath
    if ($null -eq $dirKey) { return @() }

    $children = New-Object System.Collections.Generic.List[string]

    foreach ($k in $FakeFs.Keys) {
        $kStr = [string]$k
        $dirStr = [string]$dirKey

        if ($kStr -eq $dirStr) { continue }

        # FIXED: valid if syntax and method call
        if ( $kStr.StartsWith($dirStr, [System.StringComparison]::OrdinalIgnoreCase) ) {
            $rest = $kStr.Substring($dirStr.Length)
            if ($rest.Length -eq 0) { continue }

            $restTrim = $rest.TrimEnd("/")
            if ($restTrim -notmatch "/") {
                $children.Add($kStr) | Out-Null
            }
        }
    }

    return $children
}

function Add-FakeDirEx {
    param([string]$path, [DateTime]$createdUtc, [DateTime]$modifiedUtc)
    $path = CanonDir $path
    $FakeFs[$path] = @{
        Type = "dir"
        Name = ($path.TrimEnd("/") -split "/")[-1]
        CreatedUtc = $createdUtc
        ModifiedUtc = $modifiedUtc
    }
}

function Add-FakeFileEx {
    param([string]$path, [string]$contentType, [byte[]]$contentBytes, [DateTime]$createdUtc, [DateTime]$modifiedUtc)
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
        -contentBytes (New-BytesFromText "Q4 Summary (fake).`r`nNothing to see here.`r`n") `
        -createdUtc $nowUtc -modifiedUtc $nowUtc
}

# ----------------------------
# JSON import (extends/overrides built-in)
# ----------------------------
function Import-FakeFsFromJson {
    param([string]$jsonPath, [string]$defaultRoot)

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
    $sr = Get-JsonPropString $cfg "shareRoot"
    if (-not [string]::IsNullOrWhiteSpace($sr)) { $root = Normalize-ConfigPath $sr }
    $root = $root.TrimEnd("/")

    $defaultsCreated  = [DateTime]::UtcNow
    $defaultsModified = [DateTime]::UtcNow
    $defaultsType = "application/octet-stream"
    $placeholderText = "This is a simulated file.`r`n"

    $defaultsObj = $cfg.PSObject.Properties["defaults"]
    if ($defaultsObj -and $defaultsObj.Value) {
        $d = $defaultsObj.Value
        $defaultsCreated  = Parse-UtcDateOrDefault (Get-JsonPropString $d "createdUtc")  $defaultsCreated
        $defaultsModified = Parse-UtcDateOrDefault (Get-JsonPropString $d "modifiedUtc") $defaultsModified

        $ct = Get-JsonPropString $d "contentType"
        if (-not [string]::IsNullOrWhiteSpace($ct)) { $defaultsType = $ct }

        $pt = Get-JsonPropString $d "placeholderText"
        if (-not [string]::IsNullOrWhiteSpace($pt)) { $placeholderText = $pt }
    }

    $entriesProp = $cfg.PSObject.Properties["entries"]
    if (-not ($entriesProp -and $entriesProp.Value)) {
        Write-Host "[Config] No entries[] in JSON; keeping built-in tree." -ForegroundColor Yellow
        return $root
    }

    foreach ($e in $entriesProp.Value) {
        $etype = Get-JsonPropString $e "type"
        $epath = Get-JsonPropString $e "path"
        if ([string]::IsNullOrWhiteSpace($etype) -or [string]::IsNullOrWhiteSpace($epath)) { continue }

        $p = Normalize-ConfigPath $epath

        $created  = Parse-UtcDateOrDefault (Get-JsonPropString $e "createdUtc")  $defaultsCreated
        $modified = Parse-UtcDateOrDefault (Get-JsonPropString $e "modifiedUtc") $defaultsModified

        if ($etype -eq "dir") {
            Ensure-ParentDirs -fileOrDirPath $p -createdUtc $created -modifiedUtc $modified
            Add-FakeDirEx -path $p -createdUtc $created -modifiedUtc $modified
            continue
        }

        if ($etype -eq "file") {
            Ensure-ParentDirs -fileOrDirPath $p -createdUtc $created -modifiedUtc $modified

            $ctype = $defaultsType
            $ct2 = Get-JsonPropString $e "contentType"
            if (-not [string]::IsNullOrWhiteSpace($ct2)) { $ctype = $ct2 }

            $bytes = $null
            $b64 = Get-JsonPropString $e "contentBase64"
            if (-not [string]::IsNullOrWhiteSpace($b64)) {
                try { $bytes = [Convert]::FromBase64String($b64) } catch { $bytes = $null }
            }
            if ($null -eq $bytes) {
                $txt = Get-JsonPropString $e "contentText"
                if (-not [string]::IsNullOrWhiteSpace($txt)) { $bytes = New-BytesFromText $txt }
                else { $bytes = New-BytesFromText $placeholderText }
            }

            Add-FakeFileEx -path $p -contentType $ctype -contentBytes $bytes -createdUtc $created -modifiedUtc $modified

            $sizeStr = Get-JsonPropString $e "size"
            if (-not [string]::IsNullOrWhiteSpace($sizeStr)) {
                try { $FakeFs[$p].Size = [int64]$sizeStr } catch {}
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
    param([string]$path, [int]$depth)

    $resolved = Resolve-ExistingPath $path
    if ($null -eq $resolved) { return $null }

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append('<?xml version="1.0" encoding="utf-8"?>')
    [void]$sb.Append('<D:multistatus xmlns:D="DAV:">')

    function Append-Response {
        param([string]$p0)

        $p = Resolve-ExistingPath $p0
        if ($null -eq $p) { return }

        $isDir = ($FakeFs[$p].Type -eq "dir")
        $isFile = ($FakeFs[$p].Type -eq "file")
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
            $contentType = [string]$FakeFs[$p].ContentType
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

    Append-Response $resolved

    if ($depth -ge 1 -and (Is-Dir $resolved)) {
        $kids = Get-Children $resolved
        foreach ($k in $kids) { Append-Response $k }
    }

    [void]$sb.Append("</D:multistatus>")
    return $sb.ToString()
}

# ----------------------------
# Build fake FS
# ----------------------------
$ShareRoot = Normalize-ConfigPath $ShareRoot
$ShareRoot = $ShareRoot.TrimEnd("/")

Set-BuiltInDebugTree -root $ShareRoot
$ShareRoot = Import-FakeFsFromJson -jsonPath $ConfigPath -defaultRoot $ShareRoot

# Ensure share root exists
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
Write-Host ("Fake WebDAV root: {0}/ (map: net use R: http://HOST:{1}{0})" -f $ShareRoot, $Port)
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

        # Root probe handling
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
                $resp.StatusCode = 302
                $resp.Headers["Location"] = ($ShareRoot + "/")
                $resp.Close()
                continue
            }

            Send-Text $resp 405 "text/plain; charset=utf-8" ("Method not allowed: " + $req.HttpMethod)
            continue
        }

        if ($req.HttpMethod -eq "OPTIONS") {
            $resp.StatusCode = 200
            $resp.Headers["Allow"] = "OPTIONS, GET, HEAD, PROPFIND, LOCK, UNLOCK"
            $resp.Headers["Public"] = "OPTIONS, GET, HEAD, PROPFIND, LOCK, UNLOCK"
            $resp.Close()
            continue
        }

        if ($req.HttpMethod -eq "PROPFIND") {
            $depth = Parse-DepthHeader $req
            $rp = Resolve-ExistingPath $path
            if ($null -eq $rp) {
                Send-Text $resp 404 "text/plain; charset=utf-8" "Not found"
                continue
            }

            $xml = Build-PropfindResponseXml $rp $depth
            if ($null -eq $xml) {
                Send-Text $resp 404 "text/plain; charset=utf-8" "Not found"
                continue
            }

            Send-Text $resp 207 "text/xml; charset=utf-8" $xml
            continue
        }

        if ($req.HttpMethod -eq "GET") {
            $rp = Resolve-ExistingPath $path
            if ($null -ne $rp -and (Is-File $rp)) {
                $bytes = [byte[]]$FakeFs[$rp].ContentBytes
                $ctype = [string]$FakeFs[$rp].ContentType
                Send-Bytes $resp 200 $ctype $bytes
                continue
            }
            if ($null -ne $rp -and (Is-Dir $rp)) {
                Send-Text $resp 403 "text/plain; charset=utf-8" "Directory listing via PROPFIND only."
                continue
            }
            Send-Text $resp 404 "text/plain; charset=utf-8" "Not found"
            continue
        }

        # Minimal LOCK/UNLOCK support
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

        Send-Text $resp 405 "text/plain; charset=utf-8" ("Method not allowed: " + $req.HttpMethod)
    }
}
finally {
    try { $listener.Stop() } catch {}
    try { $listener.Close() } catch {}
}
